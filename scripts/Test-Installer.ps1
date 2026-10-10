param([Parameter(Mandatory)][string]$InstallerPath)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskUninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{46F5E6F8-A0F2-48C8-8F28-CCDB55134FF0}_is1'
if (Test-Path $taskUninstallKey) { throw 'An existing Timzoner installation must be preserved. Run this test in a clean user profile.' }
$taskDestination = Join-Path $taskRoot 'artifacts/installer-test/中文目录 App'
$taskProfile = Join-Path $taskRoot ('artifacts/installer-test/profile-' + [Guid]::NewGuid().ToString('N'))
$taskExe = Join-Path $taskDestination 'Timzoner.exe'
$taskUninstaller = Join-Path $taskDestination 'unins000.exe'
$taskRunKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$taskRun = Get-Item -LiteralPath $taskRunKey
$taskHadStartup = $taskRun.GetValueNames() -contains 'Timzoner'
$taskOldStartup = $taskRun.GetValue('Timzoner')
$taskOldKind = if ($taskHadStartup) { $taskRun.GetValueKind('Timzoner') } else { $null }
$taskOldTestDirectory = $env:TIMZONER_TEST_DATA
$env:TIMZONER_TEST_DATA = $taskProfile
$taskChecks = [Collections.Generic.List[string]]::new()
function Install-App([string]$LogName) {
    $taskLog = Join-Path $taskRoot "artifacts/$LogName"
    $taskSetup = Start-Process -FilePath $InstallerPath -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/NOICONS',('/DIR="' + $taskDestination + '"'),('/LOG="' + $taskLog + '"')) -WindowStyle Hidden -Wait -PassThru
    if ($taskSetup.ExitCode -ne 0) { throw "Installer exited with $($taskSetup.ExitCode)." }
    if (!(Test-Path $taskExe) -or !(Test-Path $taskUninstallKey)) { throw 'Installed executable or uninstall registration is missing.' }
}
try {
    Install-App 'install-smoke.log'
    $taskChecks.Add('Fresh per-user installation into a path containing Chinese characters and spaces')
    if (!(Test-Path (Join-Path $taskDestination 'licenses/dotnet-LICENSE.txt'))) { throw 'Runtime license is missing.' }
    $taskSuffix = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value + '-' + [Diagnostics.Process]::GetCurrentProcess().SessionId
    $taskSuffix += '-test-' + [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($taskProfile))).Substring(0,16)
    $taskCreated = $false
    $taskBusyMutex = [Threading.Mutex]::new($true, ('Local\Timzoner-' + $taskSuffix), [ref]$taskCreated)
    try {
        if (!$taskCreated) { throw 'Busy-uninstall test profile is already in use.' }
        $taskBlocked = Start-Process -FilePath $taskUninstaller -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART' -WindowStyle Hidden -Wait -PassThru
        if ($taskBlocked.ExitCode -eq 0 -or !(Test-Path $taskExe) -or !(Test-Path $taskUninstallKey)) { throw 'Uninstaller did not abort when the instance was busy.' }
        $taskChecks.Add('Busy instance prevents uninstall and leaves program and registration intact')
    } finally {
        if ($taskCreated) { $taskBusyMutex.ReleaseMutex() }
        $taskBusyMutex.Dispose()
    }
    $taskClock = [Diagnostics.Stopwatch]::StartNew()
    $taskApp = Start-Process -FilePath $taskExe -WindowStyle Hidden -PassThru
    for ($taskAttempt = 0; $taskAttempt -lt 100; $taskAttempt++) {
        if (Test-Path (Join-Path $taskProfile 'settings.json')) { break }
        if ($taskApp.HasExited) { throw 'Installed application exited during startup.' }
        Start-Sleep -Milliseconds 100
    }
    $taskSettingsFile = Join-Path $taskProfile 'settings.json'
    if (!(Test-Path $taskSettingsFile)) { throw 'Installed application did not create its configuration.' }
    $taskStartMilliseconds = $taskClock.ElapsedMilliseconds
    $taskConfiguration = [IO.File]::ReadAllBytes($taskSettingsFile)
    Start-Sleep -Seconds 2
    Install-App 'upgrade-smoke.log'
    $taskApp.Refresh()
    if (!$taskApp.HasExited) { throw 'Upgrade did not stop the running application.' }
    if ([Convert]::ToBase64String([IO.File]::ReadAllBytes($taskSettingsFile)) -ne [Convert]::ToBase64String($taskConfiguration)) { throw 'Upgrade changed configuration.' }
    $taskChecks.Add('Upgrade closes the running instance and preserves configuration')
    $taskApp = Start-Process -FilePath $taskExe -WindowStyle Hidden -PassThru
    Start-Sleep -Seconds 2
    Set-ItemProperty -LiteralPath $taskRunKey -Name Timzoner -Value ('"' + $taskExe + '" --startup')
    $taskUninstall = Start-Process -FilePath $taskUninstaller -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART' -WindowStyle Hidden -Wait -PassThru
    if ($taskUninstall.ExitCode -ne 0) { throw 'Uninstall failed.' }
    $taskApp.Refresh()
    if (!$taskApp.HasExited -or (Test-Path $taskExe) -or (Test-Path $taskUninstallKey)) { throw 'Uninstall left the process, executable or registration behind.' }
    if ((Get-Item -LiteralPath $taskRunKey).GetValueNames() -contains 'Timzoner') { throw 'Uninstall left its startup entry behind.' }
    if (!(Test-Path $taskSettingsFile)) { throw 'Silent uninstall removed configuration.' }
    $taskChecks.Add('Uninstall closes the running instance, removes program and startup entry, and preserves configuration')
    [pscustomobject]@{ Checks = $taskChecks; FirstConfigurationMilliseconds = $taskStartMilliseconds } | ConvertTo-Json -Depth 3 | Set-Content (Join-Path $taskRoot 'artifacts/installer-checks.json') -Encoding utf8
    $taskChecks
} finally {
    if ($taskHadStartup) {
        $taskWritableRun = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Software\Microsoft\Windows\CurrentVersion\Run', $true)
        try { $taskWritableRun.SetValue('Timzoner', $taskOldStartup, $taskOldKind) } finally { $taskWritableRun.Dispose() }
    } else { Remove-ItemProperty -LiteralPath $taskRunKey -Name Timzoner -ErrorAction SilentlyContinue }
    $env:TIMZONER_TEST_DATA = $taskOldTestDirectory
}
