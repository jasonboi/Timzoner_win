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
    $taskPublish = Join-Path $taskRoot 'artifacts/release'
    if (Test-Path $taskPublish) {
        $taskResolved = (Resolve-Path -LiteralPath $taskPublish).Path
        if ($taskResolved -ne [IO.Path]::GetFullPath($taskPublish)) { throw 'Unexpected publish directory.' }
        Remove-Item -LiteralPath $taskResolved -Recurse -Force
    }
    & $taskDotnet publish src/Timzoner -c Release -r win-x64 --self-contained false -p:DebugType=none -p:DebugSymbols=false -o $taskPublish --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    if ($Installer) {
        if (!$InnoCompiler) { $InnoCompiler = Join-Path $taskRoot '.tools/inno/ISCC.exe' }
        if (!(Test-Path $InnoCompiler)) { throw 'Supply -InnoCompiler with the path to Inno Setup ISCC.exe.' }
        & $InnoCompiler installer/Timzoner.iss
        if ($LASTEXITCODE -ne 0) { throw 'Installer build failed.' }
    }
} finally { Pop-Location }
