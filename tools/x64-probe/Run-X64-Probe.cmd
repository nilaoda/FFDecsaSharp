@echo off
setlocal
cd /d "%~dp0"
title FFDecsaSharp x64 Performance Probe

echo.
echo Running five managed x64 paths. This normally completes in under two minutes.
echo Copy all five complete JSON lines below and send them back for analysis.
echo.
echo [1/5] Default packed S-box and permutation output layout
set "FFDECSA_X64_STATE_UPDATE="
set "FFDECSA_X64_BLOCK_LOOKUP="
set "FFDECSA_X64_BLOCK_LAYOUT="
FFDecsaSharp.PerfHarness.exe --probe
set "baseline_exit_code=%ERRORLEVEL%"

echo.
echo [2/5] Legacy scalar lookup and separate byte-column layout control
set "FFDECSA_X64_STATE_UPDATE=vector256"
set "FFDECSA_X64_BLOCK_LOOKUP=scalar"
set "FFDECSA_X64_BLOCK_LAYOUT=separate"
FFDecsaSharp.PerfHarness.exe --probe
set "scalar_exit_code=%ERRORLEVEL%"
set "FFDECSA_X64_STATE_UPDATE="
set "FFDECSA_X64_BLOCK_LOOKUP="
set "FFDECSA_X64_BLOCK_LAYOUT="

echo.
echo [3/5] Normalized lookup and separate byte-column layout control
set "FFDECSA_X64_STATE_UPDATE=vector256"
set "FFDECSA_X64_BLOCK_LAYOUT=separate"
FFDecsaSharp.PerfHarness.exe --probe
set "normalized_pointer_exit_code=%ERRORLEVEL%"
set "FFDECSA_X64_STATE_UPDATE="
set "FFDECSA_X64_BLOCK_LAYOUT="

echo.
echo [4/5] AVX-512 VBMI lookup with separate byte-column layout
set "FFDECSA_X64_BLOCK_LAYOUT=separate"
set "FFDECSA_X64_BLOCK_LOOKUP=vbmi"
FFDecsaSharp.PerfHarness.exe --probe
set "vbmi_exit_code=%ERRORLEVEL%"
set "FFDECSA_X64_BLOCK_LOOKUP="
set "FFDECSA_X64_BLOCK_LAYOUT="

echo.
echo [5/5] Default packed layout with forced Vector256 state update
set "FFDECSA_X64_STATE_UPDATE=vector256"
FFDecsaSharp.PerfHarness.exe --probe
set "interleaved_layout_exit_code=%ERRORLEVEL%"
set "FFDECSA_X64_STATE_UPDATE="

echo.
set "matrix_exit_code=0"
if not "%baseline_exit_code%"=="0" (
    echo Baseline probe failed with exit code %baseline_exit_code%.
    set "matrix_exit_code=%baseline_exit_code%"
)
if not "%scalar_exit_code%"=="0" (
    echo Legacy scalar lookup probe failed with exit code %scalar_exit_code%.
    set "matrix_exit_code=%scalar_exit_code%"
)
if not "%normalized_pointer_exit_code%"=="0" (
    echo Normalized lookup probe failed with exit code %normalized_pointer_exit_code%.
    set "matrix_exit_code=%normalized_pointer_exit_code%"
)
if not "%vbmi_exit_code%"=="0" (
    echo AVX-512 VBMI lookup probe failed with exit code %vbmi_exit_code%.
    set "matrix_exit_code=%vbmi_exit_code%"
)
if not "%interleaved_layout_exit_code%"=="0" (
    echo Packed transform-output probe failed with exit code %interleaved_layout_exit_code%.
    set "matrix_exit_code=%interleaved_layout_exit_code%"
)
echo The window will remain open so you can copy the result.
if /i "%~1"=="--no-pause" exit /b %matrix_exit_code%
pause
exit /b %matrix_exit_code%
