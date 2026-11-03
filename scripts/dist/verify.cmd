@echo off
rem Read-only check of the installed provider: files, machine.config, GAC entries. Administrator rights are not required.
rem   verify.cmd [-NoPause] [-InstallPath "D:\path"] [-SkipFileComparison]
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

powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%scripts\Verify-Installation.ps1" -ReferencePath "%SCRIPT_DIR%lib" %PASS_ARGS%
set "EXIT_CODE=%ERRORLEVEL%"

if "%PAUSE_AT_END%"=="1" pause
exit /b %EXIT_CODE%
