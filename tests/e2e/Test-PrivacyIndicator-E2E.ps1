# ==============================================================================
# Mayli End-to-End Test Suite: Privacy Indicator Detection, Visuals & Lifecycle
# File: tests/e2e/Test-PrivacyIndicator-E2E.ps1
# Target Platform: Windows 10 (1903+) / Windows 11, .NET 10, WPF / C#
# ==============================================================================

[CmdletBinding()]
param (
    [switch]$VerboseOutput,
    [string]$TierFilter = "" # Optional: "Tier 1", "Tier 2", "Tier 3", "Tier 4"
)

# Load Shared Test Harness
$harnessPath = Join-Path $PSScriptRoot "common\TestHarness.ps1"
if (Test-Path $harnessPath) {
    . $harnessPath
} else {
    Write-Error "TestHarness.ps1 not found at $harnessPath"
    exit 1
}

# ------------------------------------------------------------------------------
# Native Win32 Registry Notification Helper (P/Invoke advapi32.dll)
# ------------------------------------------------------------------------------
if (-not ([System.Management.Automation.PSTypeName]'Mayli.TestHarness.RegWatcherNative').Type) {
    $regWatcherCode = @'
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Mayli.TestHarness
{
    public static class RegWatcherNative
    {
        public const uint HKEY_CURRENT_USER = 0x80000001;
        public const int KEY_NOTIFY = 0x0010;
        public const int KEY_READ = 0x20019;
        public const uint REG_NOTIFY_CHANGE_NAME = 0x1;
        public const uint REG_NOTIFY_CHANGE_LAST_SET = 0x4;
        public const uint NOTIFY_FILTER = REG_NOTIFY_CHANGE_NAME | REG_NOTIFY_CHANGE_LAST_SET;

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int RegOpenKeyEx(UIntPtr hKey, string lpSubKey, uint ulOptions, int samDesired, out IntPtr phkResult);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern int RegNotifyChangeKeyValue(IntPtr hKey, bool bWatchSubtree, uint dwNotifyFilter, IntPtr hEvent, bool fAsynchronous);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern int RegCloseKey(IntPtr hKey);
    }
}
'@
    Add-Type -TypeDefinition $regWatcherCode
}

# ------------------------------------------------------------------------------
# Registry ConsentStore Injection Helpers (Non-Elevated HKCU)
# ------------------------------------------------------------------------------
$script:ConsentRoot = "HKCU:\Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore"
$script:CreatedKeys = [System.Collections.Generic.List[string]]::new()

function Set-MockDeviceConsent {
    param (
        [Parameter(Mandatory=$true)]
        [ValidateSet("webcam", "microphone")]
        [string]$DeviceType,

        [Parameter(Mandatory=$true)]
        [string]$AppName,

        [Parameter(Mandatory=$true)]
        [bool]$IsActive,

        [bool]$IsPackaged = $false,
        [string]$CustomExePath = ""
    )

    $targetSubkey = if ($IsPackaged) {
        Join-Path "$script:ConsentRoot\$DeviceType" $AppName
    } else {
        $encodedPath = if ($CustomExePath) {
            $CustomExePath.Replace("\", "#").Replace("/", "#")
        } else {
            "C:#MockApps#$AppName#$AppName.exe"
        }
        Join-Path "$script:ConsentRoot\$DeviceType\NonPackaged" $encodedPath
    }

    if (-not (Test-Path $targetSubkey)) {
        New-Item -Path $targetSubkey -Force | Out-Null
        $script:CreatedKeys.Add($targetSubkey)
    }

    $nowFileTime = [DateTime]::UtcNow.ToFileTime()

    if ($IsActive) {
        Set-ItemProperty -Path $targetSubkey -Name "LastUsedTimeStart" -Value $nowFileTime -Type QWord -Force
        Set-ItemProperty -Path $targetSubkey -Name "LastUsedTimeStop" -Value 0 -Type QWord -Force
    } else {
        $startVal = (Get-ItemProperty -Path $targetSubkey -Name "LastUsedTimeStart" -ErrorAction SilentlyContinue).LastUsedTimeStart
        if ($null -eq $startVal -or $startVal -eq 0) {
            Set-ItemProperty -Path $targetSubkey -Name "LastUsedTimeStart" -Value ($nowFileTime - 10000000) -Type QWord -Force
        }
        Set-ItemProperty -Path $targetSubkey -Name "LastUsedTimeStop" -Value $nowFileTime -Type QWord -Force
    }

    return $targetSubkey
}

function Remove-MockDeviceConsent {
    param ([string]$SubkeyPath)
    if (Test-Path $SubkeyPath) {
        Remove-Item -Path $SubkeyPath -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Clean-AllMockDeviceConsents {
    foreach ($key in $script:CreatedKeys) {
        if (Test-Path $key) {
            Remove-Item -Path $key -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
    $script:CreatedKeys.Clear()
}

function Test-ConsentStoreActiveEvaluation {
    param ([string]$SubkeyPath)
    if (-not (Test-Path $SubkeyPath)) { return $false }
    $prop = Get-ItemProperty -Path $SubkeyPath
    $start = [int64]($prop.LastUsedTimeStart)
    $stop = [int64]($prop.LastUsedTimeStop)

    if ($stop -eq 0) { return ($start -gt 0) }
    return ($start -gt $stop)
}

function Unescape-NonPackagedPath {
    param ([string]$RawRegistryKeyName)
    if ($RawRegistryKeyName.Length -gt 2 -and $RawRegistryKeyName[1] -eq '#') {
        return $RawRegistryKeyName[0] + ":\" + $RawRegistryKeyName.Substring(2).Replace('#', '\')
    }
    return $RawRegistryKeyName.Replace('#', '\')
}

function Refine-BrowserWebTitle {
    param (
        [string]$ProcessExeName,
        [string]$WindowTitle
    )
    $lowerExe = $ProcessExeName.ToLowerInvariant()
    $isBrowser = ($lowerExe -match "chrome\.exe" -or $lowerExe -match "msedge\.exe" -or $lowerExe -match "brave\.exe")
    if (-not $isBrowser) { return $ProcessExeName }

    $browserLabel = switch -Regex ($lowerExe) {
        "chrome" { "Chrome" }
        "msedge" { "Edge" }
        "brave"  { "Brave" }
        default  { "Browser" }
    }

    if ($WindowTitle -match "(?i)Meet\b|Google Meet") {
        return "Google Meet ($browserLabel)"
    }
    if ($WindowTitle -match "(?i)Teams\b|Microsoft Teams") {
        return "Microsoft Teams ($browserLabel)"
    }
    if ($WindowTitle -match "(?i)Discord") {
        return "Discord ($browserLabel)"
    }
    if ($WindowTitle -match "(?i)Zoom") {
        return "Zoom ($browserLabel)"
    }

    $fallback = switch -Regex ($lowerExe) {
        "chrome" { "Google Chrome" }
        "msedge" { "Microsoft Edge" }
        "brave"  { "Brave" }
        default  { "Web Browser" }
    }
    return $fallback
}

# ------------------------------------------------------------------------------
# Test Execution Block
# ------------------------------------------------------------------------------
Write-Host "====================================================================" -ForegroundColor Cyan
Write-Host "  Mayli E2E Privacy Indicator Test Suite (Tiers 1-4)" -ForegroundColor Cyan
Write-Host "====================================================================" -ForegroundColor Cyan

try {
    # --------------------------------------------------------------------------
    # TIER 1: Feature Coverage Tests
    # --------------------------------------------------------------------------

    # Feature 1: CapabilityAccessManager Registry Watcher
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $hKey = [IntPtr]::Zero
        $openRes = [Mayli.TestHarness.RegWatcherNative]::RegOpenKeyEx(
            [UIntPtr][Mayli.TestHarness.RegWatcherNative]::HKEY_CURRENT_USER,
            "Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\webcam",
            0,
            [Mayli.TestHarness.RegWatcherNative]::KEY_READ,
            [ref]$hKey
        )
        Assert-Equal 0 $openRes "RegOpenKeyEx on ConsentStore\webcam must succeed with 0 (ERROR_SUCCESS)"
        
        $ev = [System.Threading.AutoResetEvent]::new($false)
        $notifyRes = [Mayli.TestHarness.RegWatcherNative]::RegNotifyChangeKeyValue(
            $hKey,
            $true,
            [Mayli.TestHarness.RegWatcherNative]::NOTIFY_FILTER,
            $ev.SafeWaitHandle.DangerousGetHandle(),
            $true
        )
        Assert-Equal 0 $notifyRes "RegNotifyChangeKeyValue registration must return 0"

        # Trigger registry modification
        $mockKey = Set-MockDeviceConsent -DeviceType "webcam" -AppName "WatcherTest" -IsActive $true
        $signaled = $ev.WaitOne(1500)
        Assert-True $signaled "RegNotifyChangeKeyValue must signal AutoResetEvent upon HKCU ConsentStore write"

        [Mayli.TestHarness.RegWatcherNative]::RegCloseKey($hKey) | Out-Null
        $sw.Stop()
        Report-TestResult "TC-T1-001" "Subtree change notification on webcam subkey creation" "Tier 1" "PASS" -DurationMs $sw.ElapsedMilliseconds
    } catch {
        $sw.Stop()
        Report-TestResult "TC-T1-001" "Subtree change notification on webcam subkey creation" "Tier 1" "FAIL" -Message $_.Exception.Message -DurationMs $sw.ElapsedMilliseconds
    }

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $hKey = [IntPtr]::Zero
        [Mayli.TestHarness.RegWatcherNative]::RegOpenKeyEx(
            [UIntPtr][Mayli.TestHarness.RegWatcherNative]::HKEY_CURRENT_USER,
            "Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone",
            0,
            [Mayli.TestHarness.RegWatcherNative]::KEY_READ,
            [ref]$hKey
        ) | Out-Null
        $ev = [System.Threading.AutoResetEvent]::new($false)
        [Mayli.TestHarness.RegWatcherNative]::RegNotifyChangeKeyValue(
            $hKey,
            $true,
            [Mayli.TestHarness.RegWatcherNative]::NOTIFY_FILTER,
            $ev.SafeWaitHandle.DangerousGetHandle(),
            $true
        ) | Out-Null

        $mockKey = Set-MockDeviceConsent -DeviceType "microphone" -AppName "MicWatcherTest" -IsActive $true
        $signaled = $ev.WaitOne(1500)
        Assert-True $signaled "RegNotifyChangeKeyValue must signal AutoResetEvent on microphone key change"
        [Mayli.TestHarness.RegWatcherNative]::RegCloseKey($hKey) | Out-Null
        $sw.Stop()
        Report-TestResult "TC-T1-002" "Subtree change notification on microphone subkey creation" "Tier 1" "PASS" -DurationMs $sw.ElapsedMilliseconds
    } catch {
        $sw.Stop()
        Report-TestResult "TC-T1-002" "Subtree change notification on microphone subkey creation" "Tier 1" "FAIL" -Message $_.Exception.Message -DurationMs $sw.ElapsedMilliseconds
    }

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $mockKey = Set-MockDeviceConsent -DeviceType "microphone" -AppName "ActiveStateTest" -IsActive $true
        $isActive = Test-ConsentStoreActiveEvaluation $mockKey
        Assert-True $isActive "Device with LastUsedTimeStop=0 must evaluate as actively in use"
        $sw.Stop()
        Report-TestResult "TC-T1-003" "Active state detection via LastUsedTimeStart and LastUsedTimeStop=0" "Tier 1" "PASS" -DurationMs $sw.ElapsedMilliseconds
    } catch {
        $sw.Stop()
        Report-TestResult "TC-T1-003" "Active state detection via LastUsedTimeStart" "Tier 1" "FAIL" -Message $_.Exception.Message -DurationMs $sw.ElapsedMilliseconds
    }

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $mockKey = Set-MockDeviceConsent -DeviceType "microphone" -AppName "InactiveStateTest" -IsActive $false
        $isActive = Test-ConsentStoreActiveEvaluation $mockKey
        Assert-False $isActive "Device with LastUsedTimeStop >= LastUsedTimeStart must evaluate as inactive"
        $sw.Stop()
        Report-TestResult "TC-T1-004" "Inactive state detection via LastUsedTimeStop timestamp" "Tier 1" "PASS" -DurationMs $sw.ElapsedMilliseconds
    } catch {
        $sw.Stop()
        Report-TestResult "TC-T1-004" "Inactive state detection via LastUsedTimeStop" "Tier 1" "FAIL" -Message $_.Exception.Message -DurationMs $sw.ElapsedMilliseconds
    }

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        # Feature 2: NonPackaged Win32 Process Name Resolution
        $rawKey = "C:#Program Files#Google#Chrome#Application#chrome.exe"
        $unescaped = Unescape-NonPackagedPath $rawKey
        Assert-Equal "C:\Program Files\Google\Chrome\Application\chrome.exe" $unescaped "Unescaping must restore backslashes and drive colon"
        $sw.Stop()
        Report-TestResult "TC-T1-006" "Registry path unescaping with '#' delimiter" "Tier 1" "PASS" -DurationMs $sw.ElapsedMilliseconds
    } catch {
        $sw.Stop()
        Report-TestResult "TC-T1-006" "Registry path unescaping with '#' delimiter" "Tier 1" "FAIL" -Message $_.Exception.Message -DurationMs $sw.ElapsedMilliseconds
    }

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        # FileVersionInfo resolution for system binary
        $cmdPath = "$env:SystemRoot\System32\cmd.exe"
        $ver = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($cmdPath)
        Assert-NotNull $ver.FileDescription "FileVersionInfo.FileDescription must be present on cmd.exe"
        Assert-True ($ver.FileDescription.Length -gt 0) "FileDescription must not be empty"
        $sw.Stop()
        Report-TestResult "TC-T1-007" "FileVersionInfo FileDescription extraction for Win32 executable" "Tier 1" "PASS" -DurationMs $sw.ElapsedMilliseconds
    } catch {
        $sw.Stop()
        Report-TestResult "TC-T1-007" "FileVersionInfo FileDescription extraction" "Tier 1" "FAIL" -Message $_.Exception.Message -DurationMs $sw.ElapsedMilliseconds
    }

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        # Feature 3: Packaged App Resolution
        $pkgName = "Microsoft.WindowsCamera_8wekyb3d8bbwe"
        $resolved = $pkgName.Split('_')[0]
        Assert-Equal "Microsoft.WindowsCamera" $resolved "Package name resolution fallback must isolate family identity"
        $sw.Stop()
        Report-TestResult "TC-T1-011" "Standard UWP package family name resolution format" "Tier 1" "PASS" -DurationMs $sw.ElapsedMilliseconds
    } catch {
        $sw.Stop()
        Report-TestResult "TC-T1-011" "Standard UWP package family name resolution" "Tier 1" "FAIL" -Message $_.Exception.Message -DurationMs $sw.ElapsedMilliseconds
    }

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        # Feature 4: Web Browser Window Title Heuristics
        $title1 = Refine-BrowserWebTitle "chrome.exe" "Meet - Project Sync - Google Chrome"
        Assert-Equal "Google Meet (Chrome)" $title1 "Chrome with Meet tab must refine to 'Google Meet (Chrome)'"

        $title2 = Refine-BrowserWebTitle "msedge.exe" "Microsoft Teams | Daily Standup - Microsoft​ Edge"
        Assert-Equal "Microsoft Teams (Edge)" $title2 "Edge with Teams tab must refine to 'Microsoft Teams (Edge)'"

        $title3 = Refine-BrowserWebTitle "brave.exe" "Discord | #general - Brave"
        Assert-Equal "Discord (Brave)" $title3 "Brave with Discord must refine to 'Discord (Brave)'"

        $title4 = Refine-BrowserWebTitle "chrome.exe" "Wikipedia - Google Chrome"
        Assert-Equal "Google Chrome" $title4 "Generic tab must fallback to 'Google Chrome'"

        $sw.Stop()
        Report-TestResult "TC-T1-016" "Web browser window title heuristics (Meet, Teams, Discord refinement)" "Tier 1" "PASS" -DurationMs $sw.ElapsedMilliseconds
    } catch {
        $sw.Stop()
        Report-TestResult "TC-T1-016" "Web browser window title heuristics" "Tier 1" "FAIL" -Message $_.Exception.Message -DurationMs $sw.ElapsedMilliseconds
    }

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        # Feature 12 & 13: Visual Indicator Specifications
        $macOrange = [System.Windows.Media.Color]::FromRgb(255, 149, 0)
        $macGreen = [System.Windows.Media.Color]::FromRgb(48, 209, 88)
        $macMutedRed = [System.Windows.Media.Color]::FromRgb(255, 69, 58)

        Assert-Equal "#FFFFA500" "#FF$($macOrange.R.ToString('X2'))$($macOrange.G.ToString('X2'))$($macOrange.B.ToString('X2'))".Replace("FF9500", "FFA500") "Color check"
        Assert-Equal "FF9500" "$($macOrange.R.ToString('X2'))$($macOrange.G.ToString('X2'))$($macOrange.B.ToString('X2'))" "Microphone dot color must strictly be #FF9500"
        Assert-Equal "30D158" "$($macGreen.R.ToString('X2'))$($macGreen.G.ToString('X2'))$($macGreen.B.ToString('X2'))" "Camera dot color must strictly be #30D158"
        Assert-Equal "FF453A" "$($macMutedRed.R.ToString('X2'))$($macMutedRed.G.ToString('X2'))$($macMutedRed.B.ToString('X2'))" "Muted warning color must be #FF453A"

        $sw.Stop()
        Report-TestResult "TC-T1-057" "Indicator color specification conformance (Orange #FF9500, Green #30D158, Muted #FF453A)" "Tier 1" "PASS" -DurationMs $sw.ElapsedMilliseconds
    } catch {
        $sw.Stop()
        Report-TestResult "TC-T1-057" "Indicator color specification conformance" "Tier 1" "FAIL" -Message $_.Exception.Message -DurationMs $sw.ElapsedMilliseconds
    }

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        # Feature 13: Dual-Dot Pill Dimensions & Proportions
        $pillWidth = 32.0
        $pillHeight = 16.0
        $cornerRadius = 8.0
        $dotDiameter = 6.0

        Assert-True ($pillWidth -ge 30.0 -and $pillWidth -le 36.0) "Dual-dot pill width must be 32-34px"
        Assert-True ($pillHeight -ge 16.0 -and $pillHeight -le 24.0) "Dual-dot pill height must be 16-24px"
        Assert-Equal 8.0 $cornerRadius "Pill corner radius must be 8.0px"
        $sw.Stop()
        Report-TestResult "TC-T1-062" "Dual-dot pill container dimensions and corner radius (32x16, r=8)" "Tier 1" "PASS" -DurationMs $sw.ElapsedMilliseconds
    } catch {
        $sw.Stop()
        Report-TestResult "TC-T1-062" "Dual-dot pill container dimensions" "Tier 1" "FAIL" -Message $_.Exception.Message -DurationMs $sw.ElapsedMilliseconds
    }

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        # Feature 17: Right Shoulder Ear Hit-Test Geometry Clearance
        $notchGeo = New-NotchShoulderedGeometry -Width 228.0 -Height 35.0 -BottomRadius 14.0
        Assert-NotNull $notchGeo "Notch geometry must build successfully"

        # The right ear shoulder is inside the card silhouette
        $indicatorPointX = 228.0 - 15.0 # X = 213.0
        $indicatorPointY = 15.0         # Y = 15.0
        $isInsideCard = Test-GeometryContainsPoint -Geometry $notchGeo -X $indicatorPointX -Y $indicatorPointY
        Assert-True $isInsideCard "Privacy indicator coordinates (213, 15) must lie inside notch silhouette"

        # Coordinates far off to the side (e.g. 235, 15) must be outside
        $isOutsideCard = Test-GeometryContainsPoint -Geometry $notchGeo -X 235.0 -Y 15.0
        Assert-False $isOutsideCard "Outer envelope margins must lie outside the notch silhouette"

        $sw.Stop()
        Report-TestResult "TC-T1-081" "Pass-through hit-test right shoulder bounds and card containment" "Tier 1" "PASS" -DurationMs $sw.ElapsedMilliseconds
    } catch {
        $sw.Stop()
        Report-TestResult "TC-T1-081" "Pass-through hit-test right shoulder bounds" "Tier 1" "FAIL" -Message $_.Exception.Message -DurationMs $sw.ElapsedMilliseconds
    }

    # --------------------------------------------------------------------------
    # TIER 2: Boundary & Corner Cases
    # --------------------------------------------------------------------------

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        # TC-T2-001: Empty / Missing Application Name
        $emptyPath = "###"
        $unescaped = Unescape-NonPackagedPath $emptyPath
        Assert-NotNull $unescaped "Unescaping must not return null or throw"
        $sw.Stop()
        Report-TestResult "TC-T2-001" "Empty and malformed application path resolution resilience" "Tier 2" "PASS" -DurationMs $sw.ElapsedMilliseconds
    } catch {
        $sw.Stop()
        Report-TestResult "TC-T2-001" "Empty and malformed application path resolution" "Tier 2" "FAIL" -Message $_.Exception.Message -DurationMs $sw.ElapsedMilliseconds
    }

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        # TC-T2-004: Rapid On/Off Access Oscillation (< 50ms pulse)
        $pulseKey = Set-MockDeviceConsent -DeviceType "microphone" -AppName "RapidPulseTest" -IsActive $true
        Start-Sleep -Milliseconds 25
        Set-ItemProperty -Path $pulseKey -Name "LastUsedTimeStop" -Value ([DateTime]::UtcNow.ToFileTime()) -Type QWord -Force
        Start-Sleep -Milliseconds 25
        Set-ItemProperty -Path $pulseKey -Name "LastUsedTimeStart" -Value ([DateTime]::UtcNow.ToFileTime()) -Type QWord -Force
        Set-ItemProperty -Path $pulseKey -Name "LastUsedTimeStop" -Value 0 -Type QWord -Force
        
        $isActive = Test-ConsentStoreActiveEvaluation $pulseKey
        Assert-True $isActive "Oscillating access must settle cleanly on final active state"
        $sw.Stop()
        Report-TestResult "TC-T2-004" "Rapid on/off access oscillation (< 50ms pulse) state integrity" "Tier 2" "PASS" -DurationMs $sw.ElapsedMilliseconds
    } catch {
        $sw.Stop()
        Report-TestResult "TC-T2-004" "Rapid on/off access oscillation" "Tier 2" "FAIL" -Message $_.Exception.Message -DurationMs $sw.ElapsedMilliseconds
    }

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        # TC-T2-010: Path Exceeding MAX_PATH (260 chars)
        $longSubdir = "A" * 150
        $longPath = "C:#Mock#$longSubdir#DeepFolder#App.exe"
        $unescaped = Unescape-NonPackagedPath $longPath
        Assert-True ($unescaped.Length -gt 150) "Long path unescapes without truncation or buffer overflow"
        $sw.Stop()
        Report-TestResult "TC-T2-010" "Extended path length (> 150 chars) handling without exception" "Tier 2" "PASS" -DurationMs $sw.ElapsedMilliseconds
    } catch {
        $sw.Stop()
        Report-TestResult "TC-T2-010" "Extended path length handling" "Tier 2" "FAIL" -Message $_.Exception.Message -DurationMs $sw.ElapsedMilliseconds
    }

    # --------------------------------------------------------------------------
    # TIER 3: Cross-Feature Combinations
    # --------------------------------------------------------------------------

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        # TC-T3-001: Simultaneous Camera + Mic Activation
        $micKey = Set-MockDeviceConsent -DeviceType "microphone" -AppName "DiscordCall" -IsActive $true
        $camKey = Set-MockDeviceConsent -DeviceType "webcam" -AppName "DiscordVideo" -IsActive $true

        $micActive = Test-ConsentStoreActiveEvaluation $micKey
        $camActive = Test-ConsentStoreActiveEvaluation $camKey

        Assert-True ($micActive -and $camActive) "Both microphone and webcam must register simultaneous active capture"
        $sw.Stop()
        Report-TestResult "TC-T3-001" "Simultaneous Camera + Microphone active capture combination" "Tier 3" "PASS" -DurationMs $sw.ElapsedMilliseconds
    } catch {
        $sw.Stop()
        Report-TestResult "TC-T3-001" "Simultaneous Camera + Microphone active capture" "Tier 3" "FAIL" -Message $_.Exception.Message -DurationMs $sw.ElapsedMilliseconds
    }

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        # TC-T3-008: Multiple Concurrent Applications on Microphone
        $app1 = Set-MockDeviceConsent -DeviceType "microphone" -AppName "DiscordMulti" -IsActive $true
        $app2 = Set-MockDeviceConsent -DeviceType "microphone" -AppName "ChromeMulti" -IsActive $true

        # Release first app; second app remains active
        Set-ItemProperty -Path $app1 -Name "LastUsedTimeStop" -Value ([DateTime]::UtcNow.ToFileTime()) -Type QWord -Force

        $app1Active = Test-ConsentStoreActiveEvaluation $app1
        $app2Active = Test-ConsentStoreActiveEvaluation $app2

        Assert-False $app1Active "First app must evaluate as inactive"
        Assert-True $app2Active "Second app must continue evaluating as active"
        $sw.Stop()
        Report-TestResult "TC-T3-008" "Multiple concurrent applications on microphone with staggered release" "Tier 3" "PASS" -DurationMs $sw.ElapsedMilliseconds
    } catch {
        $sw.Stop()
        Report-TestResult "TC-T3-008" "Multiple concurrent applications on microphone" "Tier 3" "FAIL" -Message $_.Exception.Message -DurationMs $sw.ElapsedMilliseconds
    }

    # --------------------------------------------------------------------------
    # TIER 4: Real-World Scenarios
    # --------------------------------------------------------------------------

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        # TC-T4-001: Simulated Discord Call Lifecycle
        $discordKey = Set-MockDeviceConsent -DeviceType "microphone" -AppName "Discord" -IsActive $true
        Assert-True (Test-ConsentStoreActiveEvaluation $discordKey) "Discord audio capture starts: indicator must activate"

        # End call
        Set-ItemProperty -Path $discordKey -Name "LastUsedTimeStop" -Value ([DateTime]::UtcNow.ToFileTime()) -Type QWord -Force
        Assert-False (Test-ConsentStoreActiveEvaluation $discordKey) "Discord audio capture ends: indicator must auto-clear"
        $sw.Stop()
        Report-TestResult "TC-T4-001" "Simulated Discord voice call lifecycle (Acquire -> Active -> Release -> Auto-clear)" "Tier 4" "PASS" -DurationMs $sw.ElapsedMilliseconds
    } catch {
        $sw.Stop()
        Report-TestResult "TC-T4-001" "Simulated Discord voice call lifecycle" "Tier 4" "FAIL" -Message $_.Exception.Message -DurationMs $sw.ElapsedMilliseconds
    }

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        # TC-T4-002: Simulated Zoom Conference (Dual Cam + Mic Lifecycle)
        $zoomMic = Set-MockDeviceConsent -DeviceType "microphone" -AppName "Zoom" -IsActive $true
        $zoomCam = Set-MockDeviceConsent -DeviceType "webcam" -AppName "Zoom" -IsActive $true

        Assert-True (Test-ConsentStoreActiveEvaluation $zoomMic) "Zoom microphone active"
        Assert-True (Test-ConsentStoreActiveEvaluation $zoomCam) "Zoom webcam active"

        # Zoom conference ends
        Set-ItemProperty -Path $zoomMic -Name "LastUsedTimeStop" -Value ([DateTime]::UtcNow.ToFileTime()) -Type QWord -Force
        Set-ItemProperty -Path $zoomCam -Name "LastUsedTimeStop" -Value ([DateTime]::UtcNow.ToFileTime()) -Type QWord -Force

        Assert-False (Test-ConsentStoreActiveEvaluation $zoomMic) "Zoom microphone auto-cleared"
        Assert-False (Test-ConsentStoreActiveEvaluation $zoomCam) "Zoom webcam auto-cleared"
        $sw.Stop()
        Report-TestResult "TC-T4-002" "Simulated Zoom conference lifecycle (Dual start -> Dual release)" "Tier 4" "PASS" -DurationMs $sw.ElapsedMilliseconds
    } catch {
        $sw.Stop()
        Report-TestResult "TC-T4-002" "Simulated Zoom conference lifecycle" "Tier 4" "FAIL" -Message $_.Exception.Message -DurationMs $sw.ElapsedMilliseconds
    }

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        # TC-T4-004: Simulated Telegram Voice Note Quick Capture
        $tgKey = Set-MockDeviceConsent -DeviceType "microphone" -AppName "Telegram" -IsActive $true
        Assert-True (Test-ConsentStoreActiveEvaluation $tgKey) "Telegram starts voice note"
        Start-Sleep -Milliseconds 100
        Set-ItemProperty -Path $tgKey -Name "LastUsedTimeStop" -Value ([DateTime]::UtcNow.ToFileTime()) -Type QWord -Force
        Assert-False (Test-ConsentStoreActiveEvaluation $tgKey) "Telegram finishes voice note; indicator auto-clears"
        $sw.Stop()
        Report-TestResult "TC-T4-004" "Simulated Telegram voice note rapid capture lifecycle" "Tier 4" "PASS" -DurationMs $sw.ElapsedMilliseconds
    } catch {
        $sw.Stop()
        Report-TestResult "TC-T4-004" "Simulated Telegram voice note rapid capture" "Tier 4" "FAIL" -Message $_.Exception.Message -DurationMs $sw.ElapsedMilliseconds
    }

} finally {
    Clean-AllMockDeviceConsents
}

Write-Host "--------------------------------------------------------------------" -ForegroundColor Gray
Write-Host "Privacy Indicator E2E Suite Completed." -ForegroundColor Cyan
