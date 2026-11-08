@echo off
setlocal enabledelayedexpansion

title Mem-build Kuri (NotchWin) - Release Mode

:: Menyiapkan environment .NET jika terpasang di user profile
if exist "%USERPROFILE%\.dotnet\dotnet.exe" (
    set "DOTNET_ROOT=%USERPROFILE%\.dotnet"
    set "PATH=%USERPROFILE%\.dotnet;%PATH%"
)

:: Verifikasi apakah dotnet CLI tersedia
where dotnet >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] .NET SDK tidak ditemukan di sistem atau PATH.
    echo Pastikan .NET 8.0 SDK sudah terpasang.
    pause
    exit /b 1
)

echo ========================================================
echo   Mem-build Kuri (NotchWin) - Release Mode
echo ========================================================
echo.

:: Menutup proses Kuri/NotchWin yang sedang berjalan agar file tidak terkunci (MSB3026)
echo [1/2] Menutup proses Kuri/NotchWin yang aktif (jika ada)...
taskkill /f /im NotchWin.exe >nul 2>nul
taskkill /f /im Kuri.exe >nul 2>nul

echo [2/2] Mengompilasi proyek dengan dotnet build...
echo.
dotnet build "%~dp0NotchWin\NotchWin.csproj" -c Release

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ========================================================
    echo   [SUKSES] Build berhasil tanpa error!
    echo   Lokasi file:
    echo   %~dp0NotchWin\bin\Release\net8.0-windows10.0.19041.0\NotchWin.exe
    echo ========================================================
    echo.
    echo Jalankan "run.bat" atau buka NotchWin.exe untuk memulai.
    echo.
) else (
    echo.
    echo ========================================================
    echo   [GAGAL] Terjadi kesalahan saat kompilasi.
    echo   Silakan periksa pesan error di atas.
    echo ========================================================
    echo.
)

pause
endlocal
