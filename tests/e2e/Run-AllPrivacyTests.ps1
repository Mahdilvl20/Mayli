# ==============================================================================
# Mayli End-to-End Privacy Test Suite Master Runner
# File: tests/e2e/Run-AllPrivacyTests.ps1
# Target Platform: Windows 10 (1903+) / Windows 11, .NET 10, WPF / C#
# ==============================================================================

[CmdletBinding()]
param (
    [string]$ReportPath = "",
    [switch]$VerboseOutput,
    [string]$Tier = ""
)

$ErrorActionPreference = "Continue"

# Load Shared Test Harness
$harnessPath = Join-Path $PSScriptRoot "common\TestHarness.ps1"
if (Test-Path $harnessPath) {
    . $harnessPath
} else {
    Write-Error "TestHarness.ps1 not found at $harnessPath"
    exit 1
}

# Reset test reporter state
Reset-TestReporter

$overallStopwatch = [System.Diagnostics.Stopwatch]::StartNew()

Write-Host ""
Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host "         Mayli E2E Privacy Indicator & Mute Toggle Master Test Suite            " -ForegroundColor Cyan
Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host "  Timestamp: $([DateTime]::UtcNow.ToString('yyyy-MM-dd HH:mm:ss')) UTC" -ForegroundColor Gray
Write-Host "  Platform:  $([System.Environment]::OSVersion.VersionString) ($([System.Environment]::GetEnvironmentVariable('PROCESSOR_ARCHITECTURE')))" -ForegroundColor Gray
Write-Host "  Directory: $PSScriptRoot" -ForegroundColor Gray
if ($Tier) {
    Write-Host "  Filter:    Running $Tier only" -ForegroundColor Yellow
}
Write-Host ""

# Define test suites to execute
$suites = @(
    @{
        Name = "Privacy Indicator E2E (Detection, Visuals, Heuristics, Tooltips)"
        Script = Join-Path $PSScriptRoot "Test-PrivacyIndicator-E2E.ps1"
    },
    @{
        Name = "Microphone Mute Toggle & WASAPI Core Audio Synchronization"
        Script = Join-Path $PSScriptRoot "Test-MicrophoneMuteToggle.ps1"
    }
)

foreach ($suite in $suites) {
    Write-Host ">>> Executing Suite: $($suite.Name)" -ForegroundColor Magenta
    if (Test-Path $suite.Script) {
        $suiteSw = [System.Diagnostics.Stopwatch]::StartNew()
        try {
            & $suite.Script -TierFilter $Tier
        } catch {
            Write-Host "  [ERROR] Unhandled exception in suite '$($suite.Name)': $_" -ForegroundColor Red
            $global:TestReporter.Failed++
        }
        $suiteSw.Stop()
        Write-Host ">>> Finished: $($suite.Name) in $($suiteSw.ElapsedMilliseconds) ms" -ForegroundColor DarkGray
        Write-Host ""
    } else {
        Write-Host "  [ERROR] Test suite script not found: $($suite.Script)" -ForegroundColor Red
        $global:TestReporter.Failed++
    }
}

$overallStopwatch.Stop()
$totalDurationMs = [Math]::Round($overallStopwatch.Elapsed.TotalMilliseconds, 2)

# Generate Tier Breakdown
$tierSummary = @{}
foreach ($res in $global:TestReporter.Results) {
    $t = if ($res.Tier) { $res.Tier } else { "Other" }
    if (-not $tierSummary.ContainsKey($t)) {
        $tierSummary[$t] = @{ Passed = 0; Failed = 0; Skipped = 0; Total = 0 }
    }
    $tierSummary[$t].Total++
    switch ($res.Status) {
        "PASS" { $tierSummary[$t].Passed++ }
        "FAIL" { $tierSummary[$t].Failed++ }
        "SKIP" { $tierSummary[$t].Skipped++ }
    }
}

Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host "                              TEST EXECUTION SUMMARY                            " -ForegroundColor Cyan
Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Tier Breakdown:" -ForegroundColor White
Write-Host "  ------------------------------------------------------------------------" -ForegroundColor Gray
Write-Host ("  {0,-12} | {1,8} | {2,8} | {3,8} | {4,8} | {5,8}" -f "Tier", "Total", "Passed", "Failed", "Skipped", "Rate") -ForegroundColor Gray
Write-Host "  ------------------------------------------------------------------------" -ForegroundColor Gray

foreach ($tierName in ($tierSummary.Keys | Sort-Object)) {
    $data = $tierSummary[$tierName]
    $rate = if ($data.Total -gt 0) { [Math]::Round(($data.Passed / $data.Total) * 100, 1) } else { 0 }
    $rateStr = "$rate%"
    $color = if ($data.Failed -eq 0) { [ConsoleColor]::Green } else { [ConsoleColor]::Red }
    Write-Host ("  {0,-12} | {1,8} | {2,8} | {3,8} | {4,8} | {5,8}" -f $tierName, $data.Total, $data.Passed, $data.Failed, $data.Skipped, $rateStr) -ForegroundColor $color
}

Write-Host "  ------------------------------------------------------------------------" -ForegroundColor Gray
Write-Host ""

$totalTests = $global:TestReporter.Passed + $global:TestReporter.Failed + $global:TestReporter.Skipped
$overallPassRate = if ($totalTests -gt 0) { [Math]::Round(($global:TestReporter.Passed / $totalTests) * 100, 1) } else { 0 }

Write-Host "  Total Tests:       $totalTests" -ForegroundColor White
Write-Host "  Total Passed:      $($global:TestReporter.Passed)" -ForegroundColor Green
if ($global:TestReporter.Failed -gt 0) {
    Write-Host "  Total Failed:      $($global:TestReporter.Failed)" -ForegroundColor Red
} else {
    Write-Host "  Total Failed:      0" -ForegroundColor Green
}
if ($global:TestReporter.Skipped -gt 0) {
    Write-Host "  Total Skipped:     $($global:TestReporter.Skipped)" -ForegroundColor Yellow
}
Write-Host "  Pass Rate:         $overallPassRate%" -ForegroundColor $(if ($global:TestReporter.Failed -eq 0) { [ConsoleColor]::Green } else { [ConsoleColor]::Red })
Write-Host "  Elapsed Time:      $($totalDurationMs) ms" -ForegroundColor Gray
Write-Host ""

# Export JSON report if requested
if ($ReportPath) {
    try {
        $reportDir = [System.IO.Path]::GetDirectoryName([System.IO.Path]::GetFullPath($ReportPath))
        if (-not (Test-Path $reportDir)) {
            New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
        }
        $reportObject = [PSCustomObject]@{
            Timestamp        = [DateTime]::UtcNow.ToString("o")
            OSVersion        = [System.Environment]::OSVersion.VersionString
            Architecture     = [System.Environment]::GetEnvironmentVariable("PROCESSOR_ARCHITECTURE")
            TotalTests       = $totalTests
            Passed           = $global:TestReporter.Passed
            Failed           = $global:TestReporter.Failed
            Skipped          = $global:TestReporter.Skipped
            PassRatePercent  = $overallPassRate
            DurationMs       = $totalDurationMs
            TierSummary      = $tierSummary
            Results          = $global:TestReporter.Results
        }
        $reportJson = $reportObject | ConvertTo-Json -Depth 5
        [System.IO.File]::WriteAllText($ReportPath, $reportJson)
        Write-Host "  [REPORT] Artifact written to: $ReportPath" -ForegroundColor DarkCyan
    } catch {
        Write-Warning "Could not export report to ${ReportPath}: $_"
    }
}

Write-Host "================================================================================" -ForegroundColor Cyan
if ($global:TestReporter.Failed -eq 0) {
    Write-Host "  RESULT: ALL TESTS PASSED (100% Verification Success)" -ForegroundColor Green
    Write-Host "================================================================================" -ForegroundColor Cyan
    Write-Host ""
    exit 0
} else {
    Write-Host "  RESULT: TEST SUITE FAILED ($($global:TestReporter.Failed) failure(s))" -ForegroundColor Red
    Write-Host "================================================================================" -ForegroundColor Cyan
    Write-Host ""
    exit $global:TestReporter.Failed
}
