# ==============================================================================
# Mayli End-to-End Test Suite: Single-File Installer, ARP Registration & Uninstaller
# File: tests/e2e/Test-Installer-Uninstaller-E2E.ps1
# ==============================================================================

$ErrorActionPreference = "Stop"

$repoRoot = (Get-Item "$PSScriptRoot\..\..").FullName
$distDir = Join-Path $repoRoot "dist"
$latestSetup = Get-ChildItem -Path $distDir -Filter "Mayli_Modern_Setup_v*.exe" -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if ($null -eq $latestSetup) {
    throw "No Mayli_Modern_Setup_v*.exe installer executable found in $distDir"
}

$setupExe = $latestSetup.FullName
$versionMatch = [regex]::Match($latestSetup.Name, 'v(\d+\.\d+\.\d+)')
$expectedVersion = if ($versionMatch.Success) { $versionMatch.Groups[1].Value } else { "1.1.67" }

$desktopLink = Join-Path ([Environment]::GetFolderPath("DesktopDirectory")) "Mayli.lnk"
$programsDir = [Environment]::GetFolderPath("Programs")
$startMenuFolder = Join-Path $programsDir "Mayli"
$legacyStartLink = Join-Path $programsDir "Mayli.lnk"
$runRegPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
$arpRegPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Mayli"

# Snapshot existing host state so dev machine shortcuts & Run key are cleanly restored afterward
$backupDesktop = $null
if (Test-Path $desktopLink) { $backupDesktop = [System.IO.File]::ReadAllBytes($desktopLink) }

$backupLegacyStart = $null
if (Test-Path $legacyStartLink) { $backupLegacyStart = [System.IO.File]::ReadAllBytes($legacyStartLink) }

$backupRunValue = $null
try { $backupRunValue = (Get-ItemProperty -Path $runRegPath -Name "Mayli" -ErrorAction Stop).Mayli } catch { }

$tempRoot = Join-Path $env:TEMP ("Mayli_Setup_E2E_" + [Guid]::NewGuid().ToString("N"))
$testInstallA = Join-Path $tempRoot "ScenarioA\Mayli"
$testInstallB = Join-Path $tempRoot "ScenarioB_SharedFolder"
$isolatedDataDir = Join-Path $tempRoot "AppData_Mayli"

$passCount = 0
$failCount = 0

function Assert-Check {
    param([string]$Name, [bool]$Condition, [string]$Detail = "")
    if ($Condition) {
        $script:passCount++
        Write-Host "[PASS] $Name" -ForegroundColor Green
    } else {
        $script:failCount++
        Write-Host "[FAIL] $Name :: $Detail" -ForegroundColor Red
    }
}

function Wait-UntilMissing {
    param([string]$PathToWait, [int]$TimeoutMs = 8000)
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    while ($sw.ElapsedMilliseconds -lt $TimeoutMs) {
        if (-not (Test-Path $PathToWait)) { return $true }
        Start-Sleep -Milliseconds 150
    }
    return (-not (Test-Path $PathToWait))
}

try {
    New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null
    $env:MAYLI_DATA_DIR = $isolatedDataDir

    Write-Host "`n=== Scenario A: Fresh Silent Install -> ARP & Shortcut Verification -> Registry QuietUninstallString ===" -ForegroundColor Cyan
    $procA = Start-Process -FilePath $setupExe -ArgumentList "--silent --install-dir `"$testInstallA`" --skip-process-kill" -Wait -PassThru
    Assert-Check "Setup Exit Code == 0" ($procA.ExitCode -eq 0) "ExitCode=$($procA.ExitCode)"

    Assert-Check "Mayli.exe extracted" (Test-Path (Join-Path $testInstallA "Mayli.exe"))
    Assert-Check "Mayli_Uninstall.exe deployed" (Test-Path (Join-Path $testInstallA "Mayli_Uninstall.exe"))
    Assert-Check "Install manifest written" (Test-Path (Join-Path $testInstallA ".mayli-install-manifest.json"))
    Assert-Check "Desktop shortcut created" (Test-Path $desktopLink)
    Assert-Check "Start Menu app shortcut created" (Test-Path (Join-Path $startMenuFolder "Mayli.lnk"))
    Assert-Check "Start Menu uninstall shortcut created" (Test-Path (Join-Path $startMenuFolder "Uninstall Mayli.lnk"))
    Assert-Check "Weather prefs created in MAYLI_DATA_DIR" (Test-Path (Join-Path $isolatedDataDir "weather_prefs.json"))

    $runVal = $null
    try { $runVal = (Get-ItemProperty -Path $runRegPath -Name "Mayli" -ErrorAction Stop).Mayli } catch { }
    Assert-Check "Startup Run registry matches installed Mayli.exe" ($runVal -eq "`"$testInstallA\Mayli.exe`"") "Actual=$runVal"

    $arp = Get-ItemProperty -Path $arpRegPath -ErrorAction SilentlyContinue
    Assert-Check "ARP Uninstall registry key exists" ($null -ne $arp)
    if ($arp) {
        Assert-Check "ARP DisplayName == Mayli" ($arp.DisplayName -eq "Mayli")
        Assert-Check "ARP DisplayVersion == $expectedVersion" ($arp.DisplayVersion -eq $expectedVersion) "Actual=$($arp.DisplayVersion)"
        Assert-Check "ARP Publisher == Modi Studio" ($arp.Publisher -eq "Modi Studio")
        Assert-Check "ARP InstallLocation matches" ($arp.InstallLocation -eq $testInstallA)
        Assert-Check "ARP EstimatedSize > 10000 KB" ($arp.EstimatedSize -gt 10000) "Actual=$($arp.EstimatedSize)"
        Assert-Check "ARP NoModify == 1 & NoRepair == 1" ($arp.NoModify -eq 1 -and $arp.NoRepair -eq 1)
        Assert-Check "ARP UninstallString points to Mayli_Uninstall.exe" ($arp.UninstallString -like "*Mayli_Uninstall.exe*--uninstall*")
        Assert-Check "ARP QuietUninstallString valid" ($arp.QuietUninstallString -like "*Mayli_Uninstall.exe*--uninstall*--silent*")
    }

    # Execute actual uninstaller directly via ARP QuietUninstallString equivalent
    $uninstExe = Join-Path $testInstallA "Mayli_Uninstall.exe"
    $uProcA = Start-Process -FilePath $uninstExe -ArgumentList "--uninstall --silent --remove-data --skip-process-kill --install-dir `"$testInstallA`"" -Wait -PassThru
    Assert-Check "Uninstaller Exit Code == 0" ($uProcA.ExitCode -eq 0) "ExitCode=$($uProcA.ExitCode)"

    $dirGone = Wait-UntilMissing -PathToWait $testInstallA -TimeoutMs 10000
    Assert-Check "Zero leftover install directory (including self-deleted Mayli_Uninstall.exe)" $dirGone "Leftover files: $((Get-ChildItem $testInstallA -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Name) -join ', ')"
    Assert-Check "Desktop shortcut deleted" (-not (Test-Path $desktopLink))
    Assert-Check "Start Menu Mayli folder deleted" (-not (Test-Path $startMenuFolder))

    $runValAfter = $null
    try { $runValAfter = (Get-ItemProperty -Path $runRegPath -Name "Mayli" -ErrorAction Stop).Mayli } catch { }
    Assert-Check "Startup Run registry deleted" ($null -eq $runValAfter)
    Assert-Check "ARP Uninstall registry deleted" (-not (Test-Path $arpRegPath))
    Assert-Check "Isolated MAYLI_DATA_DIR removed with --remove-data" (-not (Test-Path $isolatedDataDir))

    Write-Host "`n=== Scenario B: Custom Shared Directory Preserves Non-Mayli User Files ===" -ForegroundColor Cyan
    New-Item -ItemType Directory -Path $testInstallB -Force | Out-Null
    $unrelatedFile = Join-Path $testInstallB "user_important_note.txt"
    "keep me safe" | Out-File $unrelatedFile -Encoding UTF8

    $procB = Start-Process -FilePath $setupExe -ArgumentList "--silent --install-dir `"$testInstallB`" --no-shortcut --no-startup --skip-process-kill" -Wait -PassThru
    Assert-Check "Shared folder setup Exit Code == 0" ($procB.ExitCode -eq 0)

    $uninstExeB = Join-Path $testInstallB "Mayli_Uninstall.exe"
    $uProcB = Start-Process -FilePath $uninstExeB -ArgumentList "--uninstall --silent --remove-data --skip-process-kill --install-dir `"$testInstallB`"" -Wait -PassThru
    Assert-Check "Shared folder uninstall Exit Code == 0" ($uProcB.ExitCode -eq 0)

    $uninstGoneB = Wait-UntilMissing -PathToWait $uninstExeB -TimeoutMs 10000
    Assert-Check "Mayli_Uninstall.exe self-deleted from shared folder" $uninstGoneB
    Assert-Check "Mayli.exe deleted from shared folder" (-not (Test-Path (Join-Path $testInstallB "Mayli.exe")))
    Assert-Check "Unrelated user file preserved intact" ((Test-Path $unrelatedFile) -and ((Get-Content $unrelatedFile -Raw).Trim() -eq "keep me safe"))
}
finally {
    [Environment]::SetEnvironmentVariable("MAYLI_DATA_DIR", $null)

    # Restore host pre-test state cleanly
    if (Test-Path $startMenuFolder) { Remove-Item $startMenuFolder -Recurse -Force -ErrorAction SilentlyContinue }
    if (Test-Path $arpRegPath) { Remove-Item $arpRegPath -Recurse -Force -ErrorAction SilentlyContinue }

    if ($null -ne $backupDesktop) {
        [System.IO.File]::WriteAllBytes($desktopLink, $backupDesktop)
    } elseif (Test-Path $desktopLink) {
        Remove-Item $desktopLink -Force -ErrorAction SilentlyContinue
    }

    if ($null -ne $backupLegacyStart) {
        [System.IO.File]::WriteAllBytes($legacyStartLink, $backupLegacyStart)
    }

    if ($null -ne $backupRunValue) {
        Set-ItemProperty -Path $runRegPath -Name "Mayli" -Value $backupRunValue
    } else {
        Remove-ItemProperty -Path $runRegPath -Name "Mayli" -ErrorAction SilentlyContinue
    }

    if (Test-Path $tempRoot) { Remove-Item $tempRoot -Recurse -Force -ErrorAction SilentlyContinue }
}

Write-Host "`n=========================================" -ForegroundColor White
Write-Host "Installer/Uninstaller E2E: $passCount passed, $failCount failed" -ForegroundColor $(if ($failCount -eq 0) { "Green" } else { "Red" })
Write-Host "=========================================" -ForegroundColor White
if ($failCount -gt 0) { exit 1 }
exit 0
