# PowerShell Script to Build Kuri Windows Installers (.exe) and Portable Packages
# Supports Windows 10 & Windows 11 (64-bit and 32-bit architectures)
$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "     Kuri - Windows Installer & Release Builder       " -ForegroundColor Cyan
Write-Host "     Supports Windows 10 & Windows 11 (x64 / x86)         " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$rootDir = $PSScriptRoot
$version = "1.1"
$projectDir = "$rootDir\NotchWin"
$installerDir = "$rootDir\installer"
$releaseDir = "$rootDir\release"
$installerOutDir = "$releaseDir\installer"

# Ensure output directories exist
if (-not (Test-Path $releaseDir)) { New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null }
if (-not (Test-Path $installerOutDir)) { New-Item -ItemType Directory -Path $installerOutDir -Force | Out-Null }

# Locate Inno Setup Compiler (ISCC.exe)
$isccCandidates = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    (Get-Command iscc -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -ErrorAction SilentlyContinue)
)

$isccPath = $isccCandidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1

if (-not $isccPath) {
    Write-Error "Inno Setup Compiler (ISCC.exe) was not found! Please ensure Inno Setup 6 is installed."
    exit 1
}

Write-Host "`n[+] Found Inno Setup Compiler at: $isccPath" -ForegroundColor Green

# Ensure .NET SDK environment
$env:DOTNET_ROOT = "$env:USERPROFILE\.dotnet"
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"

# Close any running Kuri/NotchWin instances to prevent file lock
Get-Process NotchWin,Kuri -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500

# -------------------------------------------------------------
# 1. Publish Self-Contained Win-x64 (64-bit)
# -------------------------------------------------------------
Write-Host "`n[1/5] Publishing self-contained Release for Windows 64-bit (win-x64)..." -ForegroundColor Yellow
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false "$projectDir\NotchWin.csproj"

$publishDirX64 = "$projectDir\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish"
if (-not (Test-Path "$publishDirX64\NotchWin.exe")) {
    Write-Error "x64 Publish failed! NotchWin.exe not found at $publishDirX64"
    exit 1
}

# -------------------------------------------------------------
# 2. Publish Self-Contained Win-x86 (32-bit)
# -------------------------------------------------------------
Write-Host "`n[2/5] Publishing self-contained Release for Windows 32-bit (win-x86)..." -ForegroundColor Yellow
dotnet publish -c Release -r win-x86 --self-contained true -p:PublishSingleFile=false "$projectDir\NotchWin.csproj"

$publishDirX86 = "$projectDir\bin\Release\net8.0-windows10.0.19041.0\win-x86\publish"
if (-not (Test-Path "$publishDirX86\NotchWin.exe")) {
    Write-Error "x86 Publish failed! NotchWin.exe not found at $publishDirX86"
    exit 1
}

# Remove stale installers of the same version before recompiling
Get-ChildItem "$installerOutDir\Kuri-Setup-v$version-*.exe" -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue

# -------------------------------------------------------------
# 3. Compile Universal Installer (x64 + x86 in one Setup.exe)
# -------------------------------------------------------------
Write-Host "`n[3/5] Compiling Universal Installer (.exe) with Inno Setup..." -ForegroundColor Yellow
& "$isccPath" /Qp "$installerDir\NotchWin-Universal.iss"
if ($LASTEXITCODE -ne 0) {
    Write-Error "Inno Setup failed compiling Universal installer!"
    exit $LASTEXITCODE
}
if (-not (Test-Path "$installerOutDir\Kuri-Setup-v$version-Universal.exe")) {
    Write-Error "Universal installer output missing: Kuri-Setup-v$version-Universal.exe"
    exit 1
}

# -------------------------------------------------------------
# 4. Compile Architecture-Specific Installers (x64 and x86)
# -------------------------------------------------------------
Write-Host "`n[4/5] Compiling Dedicated x64 and x86 Installers (.exe)..." -ForegroundColor Yellow
& "$isccPath" /Qp "$installerDir\NotchWin-x64.iss"
if ($LASTEXITCODE -ne 0) {
    Write-Error "Inno Setup failed compiling x64 installer!"
    exit $LASTEXITCODE
}
if (-not (Test-Path "$installerOutDir\Kuri-Setup-v$version-x64.exe")) {
    Write-Error "x64 installer output missing: Kuri-Setup-v$version-x64.exe"
    exit 1
}

& "$isccPath" /Qp "$installerDir\NotchWin-x86.iss"
if ($LASTEXITCODE -ne 0) {
    Write-Error "Inno Setup failed compiling x86 installer!"
    exit $LASTEXITCODE
}
if (-not (Test-Path "$installerOutDir\Kuri-Setup-v$version-x86.exe")) {
    Write-Error "x86 installer output missing: Kuri-Setup-v$version-x86.exe"
    exit 1
}

# -------------------------------------------------------------
# 5. Create Standalone Portable ZIP packages
# -------------------------------------------------------------
Write-Host "`n[5/5] Creating portable ZIP distributions..." -ForegroundColor Yellow

# Helper script for Desktop shortcut in portable folders
$shortcutScript = @"
@echo off
set "TARGET=%~dp0NotchWin.exe"
set "SHORTCUT=%USERPROFILE%\Desktop\Kuri.lnk"
powershell -NoProfile -Command "`$ws = New-Object -ComObject WScript.Shell; `$s = `$ws.CreateShortcut('%SHORTCUT%'); `$s.TargetPath = '%TARGET%'; `$s.WorkingDirectory = '%~dp0'; `$s.IconLocation = '%TARGET%,0'; `$s.Save()"
echo [OK] Pintasan Kuri berhasil dibuat di Desktop Anda!
pause
"@

$runScript = @"
@echo off
start "" "%~dp0NotchWin.exe"
"@

# Portable x64
$portableX64 = "$releaseDir\Kuri-v$version-win-x64"
if (Test-Path $portableX64) { Remove-Item -Recurse -Force $portableX64 }
New-Item -ItemType Directory -Path $portableX64 -Force | Out-Null
Copy-Item -Path "$publishDirX64\*" -Destination $portableX64 -Recurse -Force
Set-Content -Path "$portableX64\Buat-Shortcut-Desktop.bat" -Value $shortcutScript
Set-Content -Path "$portableX64\Jalankan-Kuri.bat" -Value $runScript

$zipX64 = "$releaseDir\Kuri-v$version-win-x64.zip"
if (Test-Path $zipX64) { Remove-Item -Force $zipX64 }
Compress-Archive -Path "$portableX64\*" -DestinationPath $zipX64 -CompressionLevel Optimal

# Portable x86
$portableX86 = "$releaseDir\Kuri-v$version-win-x86"
if (Test-Path $portableX86) { Remove-Item -Recurse -Force $portableX86 }
New-Item -ItemType Directory -Path $portableX86 -Force | Out-Null
Copy-Item -Path "$publishDirX86\*" -Destination $portableX86 -Recurse -Force
Set-Content -Path "$portableX86\Buat-Shortcut-Desktop.bat" -Value $shortcutScript
Set-Content -Path "$portableX86\Jalankan-Kuri.bat" -Value $runScript

$zipX86 = "$releaseDir\Kuri-v$version-win-x86.zip"
if (Test-Path $zipX86) { Remove-Item -Force $zipX86 }
Compress-Archive -Path "$portableX86\*" -DestinationPath $zipX86 -CompressionLevel Optimal

# -------------------------------------------------------------
# Summary
# -------------------------------------------------------------
Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host "     ALL INSTALLERS & PACKAGES CREATED SUCCESSFULLY!      " -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green

Write-Host "`n[Paket Instalasi (.exe)]:" -ForegroundColor Cyan
Get-ChildItem "$installerOutDir\*.exe" | ForEach-Object {
    $sizeMb = [math]::Round($_.Length / 1MB, 2)
    Write-Host "  -> $($_.Name) ($sizeMb MB)" -ForegroundColor White
}

Write-Host "`n[Paket Portable (.zip)]:" -ForegroundColor Cyan
Get-ChildItem "$releaseDir\*.zip" | ForEach-Object {
    $sizeMb = [math]::Round($_.Length / 1MB, 2)
    Write-Host "  -> $($_.Name) ($sizeMb MB)" -ForegroundColor White
}

Write-Host "`nLokasi Folder Rilis: $releaseDir" -ForegroundColor Yellow
Write-Host "==========================================================" -ForegroundColor Green
