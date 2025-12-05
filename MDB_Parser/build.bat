@echo off
echo ============================================
echo MDB Framework - Parser and Build Tool
echo ============================================
echo.

:: Get the directory where this batch file is located
set "SCRIPT_DIR=%~dp0"

:: Check if Python is available
where python >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Python is not installed or not in PATH.
    echo Please install Python from https://www.python.org/downloads/
    pause
    exit /b 1
)

echo [+] Running wrapper generator...
echo.

python "%SCRIPT_DIR%wrapper_generator.py"

echo.
echo ============================================
echo Done!
echo ============================================
pause
