@echo off
setlocal
:: Menyiapkan environment .NET jika belum terdaftar secara global
if exist "%USERPROFILE%\.dotnet\dotnet.exe" (
    set "DOTNET_ROOT=%USERPROFILE%\.dotnet"
    set "PATH=%USERPROFILE%\.dotnet;%PATH%"
)

set "EXE_PATH=%~dp0NotchWin\bin\Release\net8.0-windows10.0.19041.0\NotchWin.exe"
if not exist "%EXE_PATH%" (
    set "EXE_PATH=%~dp0NotchWin\bin\Debug\net8.0-windows10.0.19041.0\NotchWin.exe"
)

if not exist "%EXE_PATH%" (
    echo Aplikasi belum di-build. Menjalankan build terlebih dahulu...
    dotnet build -c Release
)

start "" "%EXE_PATH%"
endlocal
