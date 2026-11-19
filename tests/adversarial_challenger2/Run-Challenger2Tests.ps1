# ==============================================================================
# Challenger 2 Adversarial Test Suite Runner
# Multi-App Concurrency, Boundary Hit-Testing Coordinates, Dead Process Filtering
# ==============================================================================

[CmdletBinding()]
param ()

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$projPath = Join-Path $scriptDir "Challenger2Tests.csproj"

Write-Host "Running Challenger 2 Adversarial Stress Harness via .NET 10..." -ForegroundColor Cyan
& dotnet run --project $projPath -c Debug
$exitCode = $LASTEXITCODE

if ($exitCode -eq 0) {
    Write-Host "`nAll Challenger 2 tests passed successfully." -ForegroundColor Green
} else {
    Write-Host "`nChallenger 2 test suite identified empirical failure(s)." -ForegroundColor Yellow
}

exit $exitCode
