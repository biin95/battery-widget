@echo off
setlocal
cd /d "%~dp0"

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo [ERROR] csc.exe not found. .NET Framework 4.x is required.
    exit /b 1
)

"%CSC%" -nologo -codepage:65001 -target:winexe -platform:anycpu -optimize+ ^
    -out:"build\BatteryWidget.exe" ^
    -win32manifest:"src\app.manifest" ^
    @build\app.rsp

if errorlevel 1 (
    echo [ERROR] Build failed.
    exit /b 1
)

echo [OK] build\BatteryWidget.exe
exit /b 0
