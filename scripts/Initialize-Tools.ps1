$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskTools = Join-Path $taskRoot '.tools'
New-Item -ItemType Directory -Path $taskTools -Force | Out-Null
$taskSdkVersion = (Get-Content (Join-Path $taskRoot 'global.json') -Raw | ConvertFrom-Json).sdk.version
if (!(Test-Path (Join-Path $taskTools 'dotnet/dotnet.exe'))) {
    $taskReleases = Invoke-RestMethod 'https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json'
    $taskSdk = @($taskReleases.releases | ForEach-Object { $_.sdks }) | Where-Object version -eq $taskSdkVersion | Select-Object -First 1
    if (!$taskSdk) { throw "SDK $taskSdkVersion is absent from the official release metadata." }
    $taskDownload = $taskSdk.files | Where-Object { $_.rid -eq 'win-x64' -and $_.name -like '*.zip' }
    $taskArchive = Join-Path $taskTools 'dotnet-sdk.zip'
    Invoke-WebRequest $taskDownload.url -OutFile $taskArchive
    if ((Get-FileHash $taskArchive -Algorithm SHA512).Hash -ne $taskDownload.hash) { throw 'SDK checksum mismatch.' }
    Expand-Archive $taskArchive (Join-Path $taskTools 'dotnet') -Force
}
if (!(Test-Path (Join-Path $taskTools 'inno/ISCC.exe'))) {
    $taskInno = Join-Path $taskTools 'innosetup-6.7.3.exe'
    Invoke-WebRequest 'https://github.com/jrsoftware/issrc/releases/download/is-6_7_3/innosetup-6.7.3.exe' -OutFile $taskInno
    $taskSignature = Get-AuthenticodeSignature $taskInno
    if ($taskSignature.Status -ne 'Valid' -or $taskSignature.SignerCertificate.Subject -notlike '*Pyrsys*') { throw 'Unexpected Inno Setup signature.' }
    $taskDirectory = Join-Path $taskTools 'inno'
    $taskProcess = Start-Process $taskInno -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/CURRENTUSER','/NOICONS',('/DIR="' + $taskDirectory + '"'),'/TASKS=') -WindowStyle Hidden -Wait -PassThru
    if ($taskProcess.ExitCode -ne 0) { throw 'Inno Setup installation failed.' }
}
Write-Output 'Build tools are available in .tools.'
