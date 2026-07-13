@echo off
setlocal
cd /d "%~dp0"
title FFDecsaSharp x64 JIT Disassembly

set "COMPlus_ReadyToRun=0"
set "COMPlus_TieredCompilation=0"
set "COMPlus_TieredPGO=0"
set "COMPlus_JitDisasmDiffable=1"
set "COMPlus_JitDisasm=FFDecsaSharp.CSA.CsaBlockCipher:*"
set "COMPlus_JitStdOutFile=%~dp0jit-disasm-block-core.txt"
set "FFDECSA_X64_STATE_UPDATE="
set "FFDECSA_X64_BLOCK_LOOKUP="

del /q "%COMPlus_JitStdOutFile%" 2>nul

echo.
echo Compiling the managed block core with stable Tier-1 JIT settings.
echo This is a diagnostic run, not a throughput measurement.
echo.
FFDecsaSharp.PerfHarness.exe --probe
set "probe_exit_code=%ERRORLEVEL%"

echo.
if exist "%COMPlus_JitStdOutFile%" (
    echo JIT disassembly was written to:
    echo %COMPlus_JitStdOutFile%
    echo.
    echo Copy the complete jit-disasm-block-core.txt file and the JSON line above.
) else (
    echo No JIT disassembly file was created. Send the complete window output instead.
)

if not "%probe_exit_code%"=="0" echo The probe failed with exit code %probe_exit_code%.
echo The window will remain open so you can copy the result.
pause
exit /b %probe_exit_code%
