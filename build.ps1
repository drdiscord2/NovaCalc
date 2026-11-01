# NovaCalc build script (PowerShell). The Future of Calculation.
# Порядок: MSBuild -> dotnet SDK -> встроенный csc.exe .NET Framework.
# После сборки: --selftest и копирование в dist\
$ErrorActionPreference = 'Continue'
Set-Location $PSScriptRoot

function Invoke-Csc {
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
    if (-not (Test-Path $csc)) { Write-Host '[NovaCalc] csc.exe not found'; return $false }
    New-Item -ItemType Directory -Force bin\Release | Out-Null
    $refs = @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Net.dll') | ForEach-Object { "/reference:$_" }
    $srcs = @('src\Program.cs') + (Get-ChildItem src\Engine,src\Localization,src\Services,src\Converters,src\Themes,src\UI -Filter *.cs | ForEach-Object { $_.FullName })
    & $csc /nologo /target:winexe /optimize+ /platform:anycpu /out:bin\Release\NovaCalc.exe /win32manifest:app.manifest @refs @srcs
    return ($LASTEXITCODE -eq 0)
}

$built = $false
$msbuild = Get-Command msbuild -ErrorAction SilentlyContinue
if (-not $msbuild) {
    foreach ($y in 2022,2019) { foreach ($e in 'Enterprise','Professional','Community','BuildTools') {
        $p = "${env:ProgramFiles}\Microsoft Visual Studio\$y\$e\MSBuild\Current\Bin\MSBuild.exe"
        if (Test-Path $p) { $msbuild = $p } } }
}
if ($msbuild) {
    Write-Host "[NovaCalc] Building with MSBuild: $msbuild"
    & $msbuild NovaCalc.csproj /t:Rebuild /p:Configuration=Release /v:minimal /nologo
    $built = ($LASTEXITCODE -eq 0)
}
if (-not $built -and (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host '[NovaCalc] Building with dotnet SDK...'
    dotnet build NovaCalc.csproj -c Release -v minimal
    $built = ($LASTEXITCODE -eq 0)
}
if (-not $built) {
    Write-Host '[NovaCalc] Building with built-in csc.exe (.NET Framework)...'
    $built = Invoke-Csc
}
if (-not $built -or -not (Test-Path bin\Release\NovaCalc.exe)) {
    Write-Host '[NovaCalc] ERROR: build failed.'; exit 1
}

Write-Host '[NovaCalc] Running selftest...'
& bin\Release\NovaCalc.exe --selftest
if ($LASTEXITCODE -ne 0) { Write-Host '[NovaCalc] SELFTEST FAILED - aborting.'; exit 1 }

New-Item -ItemType Directory -Force dist | Out-Null
Copy-Item bin\Release\NovaCalc.exe dist\NovaCalc.exe -Force
Write-Host '[NovaCalc] OK. Final single file: dist\NovaCalc.exe'
exit 0
