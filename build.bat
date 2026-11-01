@echo off
rem ============================================================
rem  NovaCalc build script (Windows). The Future of Calculation.
rem  Порядок: MSBuild -> dotnet SDK (+ReferenceAssemblies) -> csc.exe из .NET Framework 4.8
rem  После успешной сборки: запуск NovaCalc.exe --selftest и копирование в dist\
rem ============================================================
setlocal EnableExtensions
pushd "%~dp0"

where msbuild >nul 2>nul
if %errorlevel%==0 (
    set "MSB=msbuild"
    goto :do_build
)

rem Ищем MSBuild из Visual Studio / Build Tools в стандартных местах
for %%Y in (2022 2019) do (
  for %%E in (Enterprise Professional Community BuildTools) do (
    if exist "%ProgramFiles%\Microsoft Visual Studio\%%Y\%%E\MSBuild\Current\Bin\MSBuild.exe" (
      set "MSB=%ProgramFiles%\Microsoft Visual Studio\%%Y\%%E\MSBuild\Current\Bin\MSBuild.exe"
    )
  )
)
if defined MSB goto :do_build

where dotnet >nul 2>nul
if %errorlevel%==0 goto :try_dotnet

goto :try_csc

:do_build
echo [NovaCalc] Building with MSBuild: "%MSB%"
"%MSB%" NovaCalc.csproj /t:Rebuild /p:Configuration=Release /v:minimal /nologo
if %errorlevel% neq 0 (
    echo [NovaCalc] MSBuild failed, trying fallback compilers...
    goto :try_dotnet_nobuild
)
set "EXE=bin\Release\NovaCalc.exe"
goto :finish

:try_dotnet_nobuild
where dotnet >nul 2>nul
if %errorlevel%==0 goto :try_dotnet
goto :try_csc

:try_dotnet
echo [NovaCalc] Building with dotnet SDK + Microsoft.NETFramework.ReferenceAssemblies (build-time only)
dotnet build NovaCalc.csproj -c Release -o bin\Release /v:minimal -nologo
if %errorlevel% neq 0 goto :try_csc
set "EXE=bin\Release\NovaCalc.exe"
goto :finish

:try_csc
echo [NovaCalc] Building with built-in .NET Framework C# compiler (no additional tools needed)
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo [NovaCalc] ERROR: csc.exe not found. Install .NET Framework 4.8 or Visual Studio Build Tools.
    popd
    exit /b 1
)
if not exist "bin\Release" mkdir "bin\Release"
"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu ^
  /out:bin\Release\NovaCalc.exe ^
  /win32manifest:app.manifest ^
  /reference:System.dll ^
  /reference:System.Core.dll ^
  /reference:System.Drawing.dll ^
  /reference:System.Windows.Forms.dll ^
  /reference:System.Net.dll ^
  src\Program.cs src\Engine\*.cs src\Localization\*.cs src\Services\*.cs src\Converters\*.cs src\Themes\*.cs src\UI\*.cs
if %errorlevel% neq 0 (
    echo [NovaCalc] ERROR: compilation failed.
    popd
    exit /b 1
)
set "EXE=bin\Release\NovaCalc.exe"
goto :finish

:finish
if not exist "%EXE%" (
    echo [NovaCalc] ERROR: %EXE% not produced.
    popd
    exit /b 1
)

echo [NovaCalc] Running selftest...
"%EXE%" --selftest
if %errorlevel% neq 0 (
    echo [NovaCalc] SELFTEST FAILED - aborting.
    popd
    exit /b 1
)

if not exist "dist" mkdir "dist"
copy /y "%EXE%" "dist\NovaCalc.exe" >nul
echo [NovaCalc] OK. Final single file: dist\NovaCalc.exe
popd
exit /b 0
