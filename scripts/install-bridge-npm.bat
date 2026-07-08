@echo off
REM Batch wrapper for install-bridge-npm.ps1
REM Allows PowerShell script invocation from cmd.exe / C# Process.Start()
REM
REM Usage:
REM   install-bridge-npm.bat [--version v2.0.0] [--quiet]
REM
REM The batch file:
REM   1. Resolves script directory
REM   2. Invokes PowerShell with appropriate execution policy
REM   3. Preserves exit code from PowerShell

setlocal enabledelayedexpansion

REM Get the directory where this batch file is located
set "SCRIPT_DIR=%~dp0"
set "PS_SCRIPT=%SCRIPT_DIR%install-bridge-npm.ps1"

REM Build PowerShell arguments from batch arguments
set "PS_ARGS="
:parse_args
if "%~1"=="" goto run_script
if "%~1"=="--version" (
    set "PS_ARGS=!PS_ARGS! -Version ""%~2"""
    shift
    shift
    goto parse_args
)
if "%~1"=="--quiet" (
    set "PS_ARGS=!PS_ARGS! -Quiet"
    shift
    goto parse_args
)
shift
goto parse_args

:run_script
REM Invoke PowerShell with BypassPolicy for this script execution
REM Use -NoProfile to skip profile loading (faster, cleaner)
REM Use -ExecutionPolicy Bypass only for this specific script
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "!PS_SCRIPT!" !PS_ARGS!

REM Preserve exit code
exit /b !errorlevel!
