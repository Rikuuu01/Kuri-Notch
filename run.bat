@echo off
setlocal enabledelayedexpansion

title Kuri (NotchWin) - Build and Run

:: 1. Menyiapkan environment .NET jika terpasang di user profile
if exist "%USERPROFILE%\.dotnet\dotnet.exe" (
    set "DOTNET_ROOT=%USERPROFILE%\.dotnet"
    set "PATH=%USERPROFILE%\.dotnet;%PATH%"
)

:: 2. Verifikasi apakah dotnet CLI tersedia
where dotnet >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] .NET SDK tidak ditemukan di sistem atau PATH.
    echo Pastikan .NET 8.0 SDK sudah terpasang.
    pause
    exit /b 1
)

echo ========================================================
echo   Kuri (NotchWin) - 1-Click Build and Run
echo ========================================================
echo.

:: 3. Menutup proses Kuri/NotchWin yang sedang aktif agar file tidak terkunci saat build
echo [1/3] Menutup proses Kuri/NotchWin yang aktif (jika ada)...
taskkill /f /im NotchWin.exe >nul 2>nul
taskkill /f /im Kuri.exe >nul 2>nul

:: 4. Mengompilasi proyek
echo [2/3] Mengompilasi proyek dengan dotnet build...
dotnet build "%~dp0NotchWin\NotchWin.csproj" -c Release

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ========================================================
    echo   [GAGAL] Terjadi kesalahan saat kompilasi.
    echo   Silakan periksa pesan error di atas.
    echo ========================================================
    echo.
    pause
    exit /b 1
)

echo.
echo ========================================================
echo   [SUKSES] Kompilasi berhasil!
echo ========================================================
echo.

:: 5. Menjalankan aplikasi
set "EXE_PATH=%~dp0NotchWin\bin\Release\net8.0-windows10.0.19041.0\NotchWin.exe"
if not exist "%EXE_PATH%" (
    set "EXE_PATH=%~dp0NotchWin\bin\Release\net8.0-windows10.0.19041.0\win-x64\NotchWin.exe"
)
if not exist "%EXE_PATH%" (
    set "EXE_PATH=%~dp0NotchWin\bin\Debug\net8.0-windows10.0.19041.0\NotchWin.exe"
)

if exist "%EXE_PATH%" (
    echo [3/3] Menjalankan Kuri...
    start "" "%EXE_PATH%"
) else (
    echo [ERROR] Executable NotchWin.exe tidak ditemukan di:
    echo %EXE_PATH%
    pause
)

endlocal
