param([switch]$Installer, [string]$InnoCompiler)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
Push-Location $taskRoot
try {
    $taskDotnet = Join-Path $taskRoot '.tools/dotnet/dotnet.exe'
    if (!(Test-Path $taskDotnet)) { $taskDotnet = (Get-Command dotnet -ErrorAction Stop).Source }
    $env:DOTNET_CLI_HOME = Join-Path $taskRoot '.tools/cli'
    $env:NUGET_PACKAGES = Join-Path $taskRoot '.tools/nuget'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    & $taskDotnet test Timzoner.slnx -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
    & $taskDotnet publish src/Timzoner -c Release -r win-x64 --self-contained true -o artifacts/release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    if ($Installer) {
        if (!$InnoCompiler) { $InnoCompiler = Join-Path $taskRoot '.tools/inno/ISCC.exe' }
        if (!(Test-Path $InnoCompiler)) { throw 'Supply -InnoCompiler with the path to Inno Setup ISCC.exe.' }
        & $InnoCompiler installer/Timzoner.iss
        if ($LASTEXITCODE -ne 0) { throw 'Installer build failed.' }
    }
} finally { Pop-Location }
