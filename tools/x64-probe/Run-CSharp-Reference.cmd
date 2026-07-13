@echo off
setlocal
cd /d "%~dp0"
title FFDecsaSharp Managed Reference Benchmark

echo Running the normalized managed C# reference benchmark.
echo This normally completes in well under two minutes.
echo Copy the complete JSON line below and send it back for analysis.
echo.
FFDecsaSharp.PerfHarness.exe
set "reference_exit_code=%ERRORLEVEL%"

echo.
if not "%reference_exit_code%"=="0" echo Managed reference benchmark failed with exit code %reference_exit_code%.
echo The window will remain open so you can copy the result.
if /i "%~1"=="--no-pause" exit /b %reference_exit_code%
pause
exit /b %reference_exit_code%
