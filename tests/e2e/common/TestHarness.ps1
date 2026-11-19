# ==============================================================================
# Mayli End-to-End Test Suite: Shared Test Harness & Win32 Interop
# File: tests/e2e/common/TestHarness.ps1
# ==============================================================================

# Ensure STA mode and WPF assemblies are loaded
[System.Reflection.Assembly]::LoadWithPartialName("PresentationCore") | Out-Null
[System.Reflection.Assembly]::LoadWithPartialName("PresentationFramework") | Out-Null
[System.Reflection.Assembly]::LoadWithPartialName("WindowsBase") | Out-Null
[System.Reflection.Assembly]::LoadWithPartialName("System.Windows.Forms") | Out-Null

# Compile Win32 P/Invoke Native Methods if not already registered
if (-not ([System.Management.Automation.PSTypeName]'Mayli.TestHarness.Win32Native').Type) {
    $csharpCode = @'
using System;
using System.Runtime.InteropServices;

namespace Mayli.TestHarness
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
        public int Width { get { return Right - Left; } }
        public int Height { get { return Bottom - Top; } }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
        public POINT(int x, int y) { X = x; Y = y; }
    }

    public static class Win32Native
    {
        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_TRANSPARENT = 0x00000020;
        public const int WS_EX_LAYERED = 0x00080000;
        public const int WS_EX_TOPMOST = 0x00000008;

        public const uint WM_NCHITTEST = 0x0084;
        public const uint WM_LBUTTONDOWN = 0x0201;
        public const uint WM_LBUTTONUP = 0x0202;
        public const uint WM_MOUSEMOVE = 0x0200;
        public const uint WM_CLOSE = 0x0010;
        public const uint WM_USER = 0x0400;

        public const int HTTRANSPARENT = -1;
        public const int HTCLIENT = 1;

        public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        public const uint MOUSEEVENTF_LEFTUP = 0x0004;

        public const uint MUTEX_ALL_ACCESS = 0x1F0001;

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        public static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        public static extern IntPtr WindowFromPoint(POINT Point);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr OpenMutex(uint dwDesiredAccess, bool bInheritHandle, string lpName);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr CreateMutex(IntPtr lpMutexAttributes, bool bInitialOwner, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hObject);

        public static IntPtr MakeLParam(int low, int high)
        {
            return (IntPtr)((high << 16) | (low & 0xFFFF));
        }
    }
}
'@
    Add-Type -TypeDefinition $csharpCode -ReferencedAssemblies "System.Runtime.InteropServices.dll"
}

# Global Test Reporter State
if (-not $global:TestReporter) {
    $global:TestReporter = [PSCustomObject]@{
        Passed   = 0
        Failed   = 0
        Skipped  = 0
        Results  = [System.Collections.Generic.List[PSCustomObject]]::new()
    }
}

function Reset-TestReporter {
    $global:TestReporter.Passed = 0
    $global:TestReporter.Failed = 0
    $global:TestReporter.Skipped = 0
    $global:TestReporter.Results.Clear()
}

function Report-TestResult {
    param (
        [string]$TestId,
        [string]$Description,
        [string]$Tier,
        [string]$Status, # "PASS", "FAIL", "SKIP"
        [string]$Message = "",
        [double]$DurationMs = 0
    )

    $result = [PSCustomObject]@{
        TestId      = $TestId
        Description = $Description
        Tier        = $Tier
        Status      = $Status
        Message     = $Message
        DurationMs  = [Math]::Round($DurationMs, 2)
        Timestamp   = [DateTime]::UtcNow.ToString("o")
    }

    $global:TestReporter.Results.Add($result)

    switch ($Status) {
        "PASS" {
            $global:TestReporter.Passed++
            Write-Host "  [PASS] $TestId : $Description ($($result.DurationMs) ms)" -ForegroundColor Green
        }
        "FAIL" {
            $global:TestReporter.Failed++
            Write-Host "  [FAIL] $TestId : $Description" -ForegroundColor Red
            if ($Message) {
                Write-Host "         Reason: $Message" -ForegroundColor Yellow
            }
        }
        "SKIP" {
            $global:TestReporter.Skipped++
            Write-Host "  [SKIP] $TestId : $Description" -ForegroundColor DarkGray
            if ($Message) {
                Write-Host "         Reason: $Message" -ForegroundColor DarkGray
            }
        }
    }
}

# Assertion Functions
function Assert-True {
    param([bool]$Condition, [string]$FailMessage = "Assertion failed: expected true, got false")
    if (-not $Condition) {
        throw $FailMessage
    }
}

function Assert-False {
    param([bool]$Condition, [string]$FailMessage = "Assertion failed: expected false, got true")
    if ($Condition) {
        throw $FailMessage
    }
}

function Assert-Equal {
    param($Expected, $Actual, [string]$FailMessage = "")
    if ($Expected -ne $Actual) {
        $msg = if ($FailMessage) { $FailMessage } else { "Assertion failed: expected '$Expected', got '$Actual'" }
        throw $msg
    }
}

function Assert-NotNull {
    param($Value, [string]$FailMessage = "Assertion failed: value is null")
    if ($null -eq $Value) {
        throw $FailMessage
    }
}

# Win32 Helpers for Mayli Notch
function Find-MayliNotchHwnd {
    # Check for window titled "Mayli Notch" or "Mayli"
    $hwnd = [Mayli.TestHarness.Win32Native]::FindWindow($null, "Mayli Notch")
    if ($hwnd -eq [IntPtr]::Zero) {
        $hwnd = [Mayli.TestHarness.Win32Native]::FindWindow($null, "Mayli")
    }
    return $hwnd
}

function Test-MayliSingleInstanceMutex {
    param([string]$MutexName = "Local\Mayli_SingleInstance_Mutex")
    $hMutex = [Mayli.TestHarness.Win32Native]::OpenMutex([Mayli.TestHarness.Win32Native]::MUTEX_ALL_ACCESS, $false, $MutexName)
    if ($hMutex -ne [IntPtr]::Zero) {
        [Mayli.TestHarness.Win32Native]::CloseHandle($hMutex) | Out-Null
        return $true
    }
    return $false
}

function Send-Win32HitTest {
    param(
        [IntPtr]$Hwnd,
        [int]$ScreenX,
        [int]$ScreenY
    )
    $lParam = [Mayli.TestHarness.Win32Native]::MakeLParam($ScreenX, $ScreenY)
    $result = [Mayli.TestHarness.Win32Native]::SendMessage($Hwnd, [Mayli.TestHarness.Win32Native]::WM_NCHITTEST, [IntPtr]::Zero, $lParam)
    return $result.ToInt32()
}

function Get-WindowExtendedStyleFlags {
    param([IntPtr]$Hwnd)
    return [Mayli.TestHarness.Win32Native]::GetWindowLong($Hwnd, [Mayli.TestHarness.Win32Native]::GWL_EXSTYLE)
}

function Simulate-PhysicalClick {
    param(
        [int]$ScreenX,
        [int]$ScreenY,
        [int]$HoldMs = 30
    )
    [Mayli.TestHarness.Win32Native]::SetCursorPos($ScreenX, $ScreenY) | Out-Null
    Start-Sleep -Milliseconds 10
    [Mayli.TestHarness.Win32Native]::mouse_event([Mayli.TestHarness.Win32Native]::MOUSEEVENTF_LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
    if ($HoldMs -gt 0) { Start-Sleep -Milliseconds $HoldMs }
    [Mayli.TestHarness.Win32Native]::mouse_event([Mayli.TestHarness.Win32Native]::MOUSEEVENTF_LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
}

# WPF Geometry Silhouette Builder (Exact Mathematical Mirror of NotchGeometryBuilder.cs)
function New-NotchShoulderedGeometry {
    param(
        [double]$Width = 228.0,
        [double]$Height = 35.0,
        [double]$BottomRadius = 14.0
    )
    $earW = 12.0
    $earH = [Math]::Min(10.0, $Height * 0.35)
    $r = [Math]::Max(6.0, [Math]::Min($BottomRadius, ($Height - $earH) * 0.85))
    $bodyLeft = $earW
    $bodyRight = [Math]::Max($bodyLeft + 2 * $r + 10, $Width - $earW)

    $streamGeo = [System.Windows.Media.StreamGeometry]::new()
    $ctx = $streamGeo.Open()
    try {
        $ctx.BeginFigure([System.Windows.Point]::new(0, 0), $true, $true)

        # Top-left concave shoulder into vertical wall
        $ctx.BezierTo(
            [System.Windows.Point]::new($earW * 0.65, 0),
            [System.Windows.Point]::new($bodyLeft, $earH * 0.35),
            [System.Windows.Point]::new($bodyLeft, $earH),
            $true, $true)

        # Left vertical wall
        $ctx.LineTo([System.Windows.Point]::new($bodyLeft, $Height - $r), $true, $true)

        # Bottom-left rounded corner
        $ctx.ArcTo(
            [System.Windows.Point]::new($bodyLeft + $r, $Height),
            [System.Windows.Size]::new($r, $r),
            0, $false, [System.Windows.Media.SweepDirection]::Counterclockwise, $true, $true)

        # Bottom flat edge
        $ctx.LineTo([System.Windows.Point]::new($bodyRight - $r, $Height), $true, $true)

        # Bottom-right rounded corner
        $ctx.ArcTo(
            [System.Windows.Point]::new($bodyRight, $Height - $r),
            [System.Windows.Size]::new($r, $r),
            0, $false, [System.Windows.Media.SweepDirection]::Counterclockwise, $true, $true)

        # Right vertical wall up to top-right concave shoulder
        $ctx.LineTo([System.Windows.Point]::new($bodyRight, $earH), $true, $true)

        # Top-right concave shoulder out to top edge
        $ctx.BezierTo(
            [System.Windows.Point]::new($bodyRight, $earH * 0.35),
            [System.Windows.Point]::new($Width - $earW * 0.65, 0),
            [System.Windows.Point]::new($Width, 0),
            $true, $true)

        # Top flush edge along screen top
        $ctx.LineTo([System.Windows.Point]::new(0, 0), $false, $false)
    }
    finally {
        $ctx.Close()
    }

    $streamGeo.Freeze()
    return $streamGeo
}

function Test-GeometryContainsPoint {
    param(
        [System.Windows.Media.Geometry]$Geometry,
        [double]$X,
        [double]$Y
    )
    return $Geometry.FillContains([System.Windows.Point]::new($X, $Y))
}
