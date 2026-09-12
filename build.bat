@echo off
rem ===========================================================================
rem  SSO Custom Launcher - build script
rem
rem    build.bat          Native AOT release  -> one self-contained exe
rem    build.bat debug    plain Debug build   (JIT, fast, for quick testing)
rem    build.bat run      Native AOT release, then start the exe
rem
rem  Native AOT links with MSVC's link.exe. Without the Visual Studio
rem  environment, PATH usually resolves "link" to Git's coreutils link and the
rem  publish dies with:  link: extra operand '/DEF:...'
rem  That is why this script calls vcvarsall.bat first.
rem ===========================================================================

setlocal
set "PROJECT_DIR=%~dp0"
cd /d "%PROJECT_DIR%" || exit /b 1

set "OUT_AOT=%PROJECT_DIR%bin\x64\Release\net8.0-windows\win-x64\publish\SSOLauncher.exe"
set "OUT_DEBUG=%PROJECT_DIR%bin\Debug\net8.0-windows\SSOLauncher.exe"

where dotnet >nul 2>&1
if errorlevel 1 (
    echo [ERROR] dotnet not found on PATH. Install the .NET 8 SDK.
    exit /b 1
)

if /i "%~1"=="debug" goto :debug
if /i "%~1"=="run" goto :aot
if "%~1"=="" goto :aot
echo [ERROR] Unknown option "%~1".  Use: build.bat [debug^|run]
exit /b 1

rem ---------------------------------------------------------------- debug ---
:debug
echo === Debug build ===
dotnet build -c Debug
if errorlevel 1 goto :failed
echo.
echo Done: %OUT_DEBUG%
exit /b 0

rem ------------------------------------------------------------------ aot ---
:aot
set "VCVARS=C:\Program Files\Microsoft Visual Studio\18\Insiders\VC\Auxiliary\Build\vcvarsall.bat"
if not exist "%VCVARS%" call :find_vs
if not exist "%VCVARS%" goto :no_vc

echo === Native AOT release ===
echo [1/2] MSVC environment
call "%VCVARS%" x64 >nul
if errorlevel 1 (
    echo [ERROR] vcvarsall.bat failed.
    exit /b 1
)

echo [2/2] dotnet publish -r win-x64 -c Release
dotnet publish -r win-x64 -c Release
if errorlevel 1 goto :failed

if not exist "%OUT_AOT%" (
    echo [ERROR] Publish reported success but %OUT_AOT% is missing.
    exit /b 1
)

for %%F in ("%OUT_AOT%") do set /a SIZE_MB=%%~zF/1048576
echo.
echo Done: %OUT_AOT%  (%SIZE_MB% MB, single file - no DLLs needed)

if /i "%~1"=="run" (
    echo Starting...
    start "" "%OUT_AOT%"
)
exit /b 0

rem ------------------------------------------------------------- helpers ---
:find_vs
rem Fall back to vswhere so the script keeps working on another machine or
rem after a Visual Studio upgrade moves the install.
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" exit /b 0
for /f "usebackq tokens=*" %%I in (`"%VSWHERE%" -latest -prerelease -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath 2^>nul`) do (
    if exist "%%I\VC\Auxiliary\Build\vcvarsall.bat" set "VCVARS=%%I\VC\Auxiliary\Build\vcvarsall.bat"
)
exit /b 0

:no_vc
echo [ERROR] vcvarsall.bat not found - Native AOT needs the MSVC linker.
echo         Install Visual Studio with the "Desktop development with C++"
echo         workload, or edit the VCVARS path at the top of :aot in this file.
echo.
echo         Tip: "build.bat debug" works without it.
exit /b 1

:failed
echo.
echo [ERROR] Build failed.
exit /b 1
