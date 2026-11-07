# Mayli Native 1-Click Installer
# Installs Mayli to %LocalAppData%\Programs\Mayli and creates Start Menu & Desktop shortcuts

$ErrorActionPreference = "Stop"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "   Mayli — The Notch for Windows Installer" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# 1. Close any running Mayli instances
Write-Host "`n[1/5] Checking for running instances..." -ForegroundColor Yellow
Get-Process -Name "Mayli" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

# 2. Build Release Binaries
Write-Host "[2/5] Building Mayli in Release mode..." -ForegroundColor Yellow
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$solutionRoot = Split-Path -Parent $scriptDir
$projectPath = Join-Path $solutionRoot "src\Mayli\Mayli.csproj"

& dotnet build "$projectPath" -c Release --nologo
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed with exit code $LASTEXITCODE"
}

# 3. Copy to Installation Directory
$installDir = Join-Path $env:LOCALAPPDATA "Programs\Mayli"
Write-Host "[3/5] Installing to $installDir..." -ForegroundColor Yellow

if (-not (Test-Path $installDir)) {
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null
}

$releaseBin = Join-Path $solutionRoot "src\Mayli\bin\Release\net10.0-windows10.0.19041.0"
Copy-Item -Path "$releaseBin\*" -Destination $installDir -Recurse -Force

# 4. Create Desktop & Start Menu Shortcuts
Write-Host "[4/5] Creating shortcuts..." -ForegroundColor Yellow
$wsh = New-Object -ComObject WScript.Shell
$exePath = Join-Path $installDir "Mayli.exe"

# Desktop Shortcut
$desktopPath = [Environment]::GetFolderPath("Desktop")
$desktopLnk = Join-Path $desktopPath "Mayli.lnk"
$shortcut = $wsh.CreateShortcut($desktopLnk)
$shortcut.TargetPath = $exePath
$shortcut.WorkingDirectory = $installDir
$shortcut.Description = "Mayli — The Notch for Windows"
$shortcut.Save()

# Start Menu Shortcut
$startMenuPath = Join-Path ([Environment]::GetFolderPath("Programs")) "Mayli"
if (-not (Test-Path $startMenuPath)) {
    New-Item -ItemType Directory -Path $startMenuPath -Force | Out-Null
}
$startMenuLnk = Join-Path $startMenuPath "Mayli.lnk"
$shortcut = $wsh.CreateShortcut($startMenuLnk)
$shortcut.TargetPath = $exePath
$shortcut.WorkingDirectory = $installDir
$shortcut.Description = "Mayli — The Notch for Windows"
$shortcut.Save()

# 5. Launch Mayli
Write-Host "[5/5] Installation complete! Launching Mayli..." -ForegroundColor Green
Start-Process -FilePath $exePath

Write-Host "`nMayli is now running and docked to your screen notch!" -ForegroundColor Cyan
Write-Host "Press Ctrl+Alt+N to expand or collapse Mayli at any time." -ForegroundColor Cyan
