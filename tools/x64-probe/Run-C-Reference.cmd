@echo off
setlocal
cd /d "%~dp0"
title FFdecsa C Reference Benchmark

set "BUILD_TOOLS_BOOTSTRAP=%TEMP%\FFDecsaSharp-vs_BuildTools.exe"
set "ZIG_EXE="
if exist "%~dp0zig\zig.exe" set "ZIG_EXE=%~dp0zig\zig.exe"
if not defined ZIG_EXE if exist "%~dp0tools\zig\zig.exe" set "ZIG_EXE=%~dp0tools\zig\zig.exe"
if not defined ZIG_EXE for /f "delims=" %%I in ('where zig.exe 2^>nul') do if not defined ZIG_EXE set "ZIG_EXE=%%I"
set "CLANG_EXE="
for /f "delims=" %%I in ('where clang.exe 2^>nul') do if not defined CLANG_EXE set "CLANG_EXE=%%I"
if not defined CLANG_EXE if exist "%ProgramFiles%\LLVM\bin\clang.exe" set "CLANG_EXE=%ProgramFiles%\LLVM\bin\clang.exe"

if defined ZIG_EXE goto :compile_zig
if defined CLANG_EXE goto :compile_clang

set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if exist "%VSWHERE%" goto :configure_msvc

echo No C compiler was found. Downloading Microsoft Visual C++ Build Tools from Microsoft's CDN.
echo This one-time setup can take 10-20 minutes and may require Windows elevation.
call :download_build_tools
if errorlevel 1 goto :done

"%BUILD_TOOLS_BOOTSTRAP%" --passive --wait --norestart --nocache --add Microsoft.VisualStudio.Workload.VCTools --add Microsoft.VisualStudio.Component.VC.Tools.x86.x64 --add Microsoft.VisualStudio.Component.Windows11SDK.22621 --includeRecommended
if errorlevel 1 (
    echo Build Tools installation failed. Approve any Windows elevation prompt and run this file again.
    goto :done
)

if not exist "%VSWHERE%" (
    echo Build Tools installed but its locator was not found. Close this window and run it again.
    goto :done
)

:configure_msvc
set "VS_INSTALLATION="
for /f "usebackq delims=" %%I in (`"%VSWHERE%" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath`) do set "VS_INSTALLATION=%%I"
if not defined VS_INSTALLATION (
    echo Visual C++ Build Tools were not found after installation.
    goto :done
)
call "%VS_INSTALLATION%\Common7\Tools\VsDevCmd.bat" -arch=x64 -host_arch=x64 >nul
if errorlevel 1 (
    echo Visual C++ environment setup failed.
    goto :done
)

if defined INCLUDE if exist "%VCToolsInstallDir%include\stdio.h" if defined WindowsSdkDir if exist "%WindowsSdkDir%Include\%WindowsSDKVersion%um\Windows.h" goto :compile_msvc

if defined MSVC_REPAIR_ATTEMPTED (
    echo Visual C++ or Windows SDK headers are still unavailable after repair.
    echo Open Visual Studio Installer, modify Build Tools, and select Desktop development with C++ plus a Windows SDK.
    goto :done
)

echo C++ or Windows SDK headers were not installed with this Build Tools instance. Repairing the workload now.
set "MSVC_REPAIR_ATTEMPTED=1"
call :download_build_tools
if errorlevel 1 goto :done
"%BUILD_TOOLS_BOOTSTRAP%" modify --installPath "%VS_INSTALLATION%" --passive --wait --norestart --add Microsoft.VisualStudio.Workload.VCTools --add Microsoft.VisualStudio.Component.VC.Tools.x86.x64 --add Microsoft.VisualStudio.Component.Windows11SDK.22621 --includeRecommended
if errorlevel 1 (
    echo Build Tools repair failed.
    goto :done
)
goto :configure_msvc

:compile_msvc
echo.
echo Compiling FFdecsa PARALLEL_128_2LONG with MSVC /O2...
cl.exe /nologo /O2 /GL /DPARALLEL_MODE=1283 /FI"%~dp0c-reference\ffdecsa_msvc_compat.h" /I"%~dp0ffdecsa-reference" "%~dp0c-reference\ffdecsa_benchmark.c" "%~dp0ffdecsa-reference\FFdecsa.c" /Fe:"%~dp0ffdecsa-reference.exe" /link /LTCG
if errorlevel 1 goto :compile_failed
goto :run

:compile_clang
echo.
echo Compiling FFdecsa PARALLEL_128_2LONG with Clang -O3 -march=native...
"%CLANG_EXE%" -O3 -march=native -DPARALLEL_MODE=1283 -I"%~dp0ffdecsa-reference" "%~dp0c-reference\ffdecsa_benchmark.c" "%~dp0ffdecsa-reference\FFdecsa.c" -o "%~dp0ffdecsa-reference.exe"
if errorlevel 1 goto :compile_failed
goto :run

:compile_zig
echo.
echo Compiling FFdecsa PARALLEL_128_2LONG with portable Zig C compiler -O3 -march=native...
"%ZIG_EXE%" cc -O3 -march=native -DPARALLEL_MODE=1283 -I"%~dp0ffdecsa-reference" "%~dp0c-reference\ffdecsa_benchmark.c" "%~dp0ffdecsa-reference\FFdecsa.c" -o "%~dp0ffdecsa-reference.exe"
if errorlevel 1 goto :compile_failed
goto :run

:compile_failed
echo Compilation failed.
goto :done

:run
echo.
echo Running C reference benchmark. Copy the complete JSON line below.
"%~dp0ffdecsa-reference.exe"

:done
echo.
echo The window will remain open so you can copy the result.
if /i "%~1"=="--no-pause" exit /b
pause
exit /b

:download_build_tools
if not defined BUILD_TOOLS_BOOTSTRAP set "BUILD_TOOLS_BOOTSTRAP=%TEMP%\FFDecsaSharp-vs_BuildTools.exe"
if exist "%BUILD_TOOLS_BOOTSTRAP%" exit /b 0
powershell -NoProfile -ExecutionPolicy Bypass -Command "$ProgressPreference='SilentlyContinue'; Invoke-WebRequest -UseBasicParsing -Uri 'https://aka.ms/vs/17/release/vs_BuildTools.exe' -OutFile '%BUILD_TOOLS_BOOTSTRAP%'"
if errorlevel 1 (
    echo Build Tools download failed. Download the official installer from https://visualstudio.microsoft.com/downloads/ then run this file again.
    exit /b 1
)
exit /b 0
