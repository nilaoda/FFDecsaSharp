@echo off
setlocal
cd /d "%~dp0"
title FFDecsaSharp Complete x64 Benchmark Matrix

echo Running the complete managed probe and normalized C# / C benchmark matrix.
echo The first C run can install Microsoft Build Tools and take 10-20 minutes.
echo.
call "%~dp0Run-X64-Probe.cmd" --no-pause
echo.
call "%~dp0Run-CSharp-Reference.cmd" --no-pause
echo.
call "%~dp0Run-C-Reference.cmd" --no-pause
echo.
echo The complete benchmark matrix finished. Copy all JSON lines above and send them back for analysis.
pause
