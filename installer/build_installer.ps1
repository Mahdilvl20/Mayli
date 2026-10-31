# Mayli Installer and Release Package Builder
# Builds Release, packages portable ZIP, and compiles Inno Setup installer if iscc is installed

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$solutionRoot = Split-Path -Parent $scriptDir
$projectPath = Join-Path $solutionRoot "src\Mayli\Mayli.csproj"
$distDir = Join-Path $solutionRoot "dist"

Write-Host ">>> [1/3] Building Mayli (Release x64)..." -ForegroundColor Cyan
& dotnet build "$projectPath" -c Release --nologo
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed with exit code $LASTEXITCODE"
}

if (-not (Test-Path $distDir)) {
    New-Item -ItemType Directory -Path $distDir -Force | Out-Null
}

$releaseBin = Join-Path $solutionRoot "src\Mayli\bin\Release\net10.0-windows"

Write-Host ">>> [2/3] Creating Portable ZIP Package..." -ForegroundColor Cyan
$zipPath = Join-Path $distDir "Mayli-v1.1.57-win-x64-portable.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path "$releaseBin\*" -DestinationPath $zipPath -Force
Write-Host "    Portable package created at: $zipPath" -ForegroundColor Green

Write-Host ">>> [3/3] Checking for Inno Setup Compiler (iscc.exe)..." -ForegroundColor Cyan
$isccCandidates = @(
    "iscc.exe",
    "$env:LocalAppData\Programs\Inno Setup 6\iscc.exe",
    "C:\Program Files (x86)\Inno Setup 6\iscc.exe",
    "C:\Program Files\Inno Setup 6\iscc.exe"
)

$isccPath = $null
foreach ($c in $isccCandidates) {
    if (Get-Command $c -ErrorAction SilentlyContinue) {
        $isccPath = $c
        break
    }
    elseif (Test-Path $c) {
        $isccPath = $c
        break
    }
}

if ($isccPath) {
    Write-Host "    Found Inno Setup at $isccPath, compiling installer..." -ForegroundColor Green
    $issFile = Join-Path $scriptDir "Mayli_Setup.iss"
    & "$isccPath" "$issFile"
    Write-Host "    Mayli_Setup.exe generated in $distDir!" -ForegroundColor Green
}
else {
    Write-Host "    Inno Setup (iscc) not found in PATH; generated portable ZIP package and standalone batch/PowerShell installer." -ForegroundColor Yellow
}

Write-Host "`nAll Mayli installer files and distribution packages ready!" -ForegroundColor Green
