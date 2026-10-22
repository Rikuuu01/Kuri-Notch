@echo off
setlocal
if exist "%USERPROFILE%\.dotnet\dotnet.exe" (
    set "DOTNET_ROOT=%USERPROFILE%\.dotnet"
    set "PATH=%USERPROFILE%\.dotnet;%PATH%"
)

echo Mem-build NotchWin dalam mode Release...
dotnet build -c Release
if %ERRORLEVEL% EQU 0 (
    echo.
    echo ========================================
    echo Build berhasil! Jalankan run.bat untuk membuka aplikasi.
    echo ========================================
) else (
    echo.
    echo ========================================
    echo Terjadi kesalahan saat build.
    echo ========================================
)
pause
endlocal
