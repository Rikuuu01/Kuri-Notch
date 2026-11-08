# PowerShell Script to Package NotchWin into a Standalone Distribution
$ErrorActionPreference = "Stop"

Write-Host "===============================================" -ForegroundColor Cyan
Write-Host "      NotchWin - Release Packaging Tool        " -ForegroundColor Cyan
Write-Host "===============================================" -ForegroundColor Cyan

$projectDir = "$PSScriptRoot\NotchWin"
$version = "1.1"
$releaseDir = "$PSScriptRoot\release"
$distDir = "$releaseDir\NotchWin-v$version-win-x64"
$zipPath = "$releaseDir\NotchWin-v$version-win-x64.zip"

# Ensure .NET SDK environment
$env:DOTNET_ROOT = "$env:USERPROFILE\.dotnet"
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"

Write-Host "`n[1/4] Building and publishing self-contained Release (win-x64)..." -ForegroundColor Yellow
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false "$projectDir\NotchWin.csproj"

$publishDir = "$projectDir\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish"
if (-not (Test-Path "$publishDir\NotchWin.exe")) {
    Write-Error "Publish output not found at $publishDir"
    exit 1
}

Write-Host "`n[2/4] Preparing distribution folder..." -ForegroundColor Yellow
if (Test-Path $distDir) {
    Remove-Item -Recurse -Force $distDir
}
New-Item -ItemType Directory -Path $distDir -Force | Out-Null

# Copy publish files
Copy-Item -Path "$publishDir\*" -Destination $distDir -Recurse -Force

# Create Desktop Shortcut batch file
$shortcutScript = @"
@echo off
set "TARGET=%~dp0NotchWin.exe"
set "SHORTCUT=%USERPROFILE%\Desktop\NotchWin.lnk"
powershell -NoProfile -Command "`$ws = New-Object -ComObject WScript.Shell; `$s = `$ws.CreateShortcut('%SHORTCUT%'); `$s.TargetPath = '%TARGET%'; `$s.WorkingDirectory = '%~dp0'; `$s.IconLocation = '%TARGET%,0'; `$s.Save()"
echo [OK] Pintasan NotchWin berhasil dibuat di Desktop Anda!
pause
"@
Set-Content -Path "$distDir\Buat-Shortcut-Desktop.bat" -Value $shortcutScript

# Create Run Background batch file
$runScript = @"
@echo off
start "" "%~dp0NotchWin.exe"
"@
Set-Content -Path "$distDir\Jalankan-NotchWin.bat" -Value $runScript

# Create README.txt
$readmeContent = @"
=====================================================
          NotchWin - Dynamic Notch untuk Windows
=====================================================
Versi : 0.2.0
Mode  : Standalone Portable (Tanpa perlu install .NET)

CARA MENGGUNAKAN:
1. Klik dua kali "Jalankan-NotchWin.bat" atau "NotchWin.exe" untuk memulai.
2. Klik kanan pada notch atau ikon System Tray untuk membuka Pengaturan.
3. Untuk membuat icon di Desktop, klik "Buat-Shortcut-Desktop.bat".

FITUR UNGGULAN:
- Widget Jam & Tanggal dengan sinkronisasi baterai laptop
- Kontrol Pemutar Musik (Spotify, YouTube, Browser, Apple Music)
- Overlay Volume HUD interaktif
- Indikator Privasi Kamera (Hijau) & Mikrofon (Oranye)
- Banner Notifikasi Perangkat USB (Flashdisk dicolok/dicabut)
- Efek Animasi Meluncur & Menyusut Halus (Dynamic Island Style)
- Pilihan Warna Aksen Tema (Biru, Ungu, Zamrud, Amber, Mawar, Cyan)
- Dukungan Multi-Monitor & Pengaturan Skala Ukuran (75% - 140%)
- Mode Hemat Daya Zero-CPU saat layar sleep / mati

Dibuat untuk pengalaman Windows yang lebih hidup dan modern.
"@
Set-Content -Path "$distDir\PETUNJUK-PENGGUNAAN.txt" -Value $readmeContent

Write-Host "`n[3/4] Creating ZIP distribution package..." -ForegroundColor Yellow
if (Test-Path $zipPath) {
    Remove-Item -Force $zipPath
}
Compress-Archive -Path "$distDir\*" -DestinationPath $zipPath -CompressionLevel Optimal

Write-Host "`n[4/4] Release Package created successfully!" -ForegroundColor Green
Write-Host "Folder Paket: $distDir" -ForegroundColor Cyan
Write-Host "File ZIP    : $zipPath" -ForegroundColor Cyan
Write-Host "Ukuran ZIP  : $([math]::Round((Get-Item $zipPath).Length / 1MB, 2)) MB" -ForegroundColor Green
