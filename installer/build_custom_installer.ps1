# Mayli Modern Setup Builder
$ErrorActionPreference = "Stop"

$root = (Get-Item $PSScriptRoot\..).FullName
$srcMayli = "$root\src\Mayli"
$srcInstaller = "$root\src\Mayli.Installer"
$distDir = "$root\dist"
$tempPayloadDir = "$env:TEMP\mayli_payload_temp"
$payloadZipPath = "$srcInstaller\Payload\app_payload.zip"

Write-Host ">>> [1/4] Publishing Mayli core application..." -ForegroundColor Cyan
if (Test-Path $tempPayloadDir) { Remove-Item -Recurse -Force $tempPayloadDir }
dotnet publish "$srcMayli\Mayli.csproj" -c Release -r win-x64 --self-contained false -o $tempPayloadDir

Write-Host ">>> [2/4] Packaging payload archive ($payloadZipPath)..." -ForegroundColor Cyan
Add-Type -AssemblyName System.IO.Compression.FileSystem
New-Item -ItemType Directory -Path "$srcInstaller\Payload" -Force | Out-Null
if (Test-Path $payloadZipPath) { Remove-Item -Force $payloadZipPath }

[System.IO.Compression.ZipFile]::CreateFromDirectory($tempPayloadDir, $payloadZipPath, [System.IO.Compression.CompressionLevel]::Optimal, $false)
$zipSize = (Get-Item $payloadZipPath).Length / 1MB
Write-Host "    Payload archive created: $([math]::Round($zipSize, 2)) MB" -ForegroundColor Green

Write-Host ">>> [3/4] Building Modern Single-File Installer..." -ForegroundColor Cyan
New-Item -ItemType Directory -Path $distDir -Force | Out-Null
dotnet publish "$srcInstaller\Mayli.Installer.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "$distDir\modern_setup"

# Copy resulting Mayli_Setup.exe to dist
$setupExe = "$distDir\modern_setup\Mayli_Setup.exe"
$finalSetupExe = "$distDir\Mayli_Modern_Setup_v1.1.65.exe"
if (Test-Path $setupExe) {
    Copy-Item $setupExe $finalSetupExe -Force
    $finalSize = (Get-Item $finalSetupExe).Length / 1MB
    Write-Host ">>> [4/4] SUCCESS! Generated: $finalSetupExe ($([math]::Round($finalSize, 2)) MB)" -ForegroundColor Green
} else {
    Write-Error "Could not locate generated Mayli_Setup.exe"
}

# Cleanup temporary payload folder
Remove-Item -Recurse -Force $tempPayloadDir -ErrorAction SilentlyContinue
Write-Host "All build steps finished successfully!" -ForegroundColor Green
