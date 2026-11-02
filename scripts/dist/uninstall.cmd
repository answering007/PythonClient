@echo off
rem Removes Pike.PythonClient64: machine.config registration, GAC entries and the installed files.
rem   uninstall.cmd [-NoPause] [-InstallPath "D:\path"]
setlocal EnableExtensions

set "SCRIPT_DIR=%~dp0"
set "PAUSE_AT_END=1"
set "PASS_ARGS="

:parse
if "%~1"=="" goto parsed
if /I "%~1"=="-NoPause" (
    set "PAUSE_AT_END=0"
    shift
    goto parse
)
set "PASS_ARGS=%PASS_ARGS% %1"
shift
goto parse
:parsed

powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-ChildItem -LiteralPath '%SCRIPT_DIR%' -Recurse -File | Unblock-File -ErrorAction SilentlyContinue"

set "IS_ADMIN="
for /f "usebackq delims=" %%A in (`powershell -NoProfile -Command "([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)"`) do set "IS_ADMIN=%%A"

rem Built outside the "if" block: %VAR% inside a parenthesized block is expanded when the block is parsed
set "ELEVATED_ARGS=%PASS_ARGS%"
if "%PAUSE_AT_END%"=="0" set "ELEVATED_ARGS=%ELEVATED_ARGS% -NoPause"

if /I not "%IS_ADMIN%"=="True" (
    echo Administrator rights are required: the machine.config registration and the GAC entries are removed.
    echo Requesting elevation, confirm the UAC prompt...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs -ArgumentList '%ELEVATED_ARGS%'"
    exit /b
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%scripts\uninstall.ps1" %PASS_ARGS%
set "EXIT_CODE=%ERRORLEVEL%"

if "%PAUSE_AT_END%"=="1" pause
exit /b %EXIT_CODE%
