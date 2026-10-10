$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskOriginalTestDirectory = $env:TIMZONER_TEST_DATA
$env:TIMZONER_TEST_DATA = Join-Path $taskRoot 'artifacts/acceptance-final-profile'
$taskExe = Join-Path $taskRoot 'artifacts/release/Timzoner.exe'
$taskProcess = Start-Process -FilePath $taskExe -ArgumentList '--settings' -PassThru
$taskProcess.Id | Set-Content (Join-Path $taskRoot 'artifacts/acceptance-process-id.txt')
try {
    Start-Sleep -Seconds 3
    powershell.exe -NoProfile -File (Join-Path $PSScriptRoot 'Test-Ui.ps1') -ProcessId $taskProcess.Id -Executable $taskExe -ArtifactDirectory (Join-Path $taskRoot 'artifacts')
    if ($LASTEXITCODE -ne 0) { throw 'UI acceptance failed.' }
    & (Join-Path $PSScriptRoot 'Measure-Resources.ps1') -ProcessId $taskProcess.Id -WarmupSeconds 30 -DurationSeconds 60 -MemoryLimitMiB 100 -CpuLimitPercent $null -OutputPath (Join-Path $taskRoot 'artifacts/config-resources.json')
    Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes
    $taskProcess.Refresh()
    $taskWindow = [Windows.Automation.AutomationElement]::FromHandle($taskProcess.MainWindowHandle)
    $taskCancel = $taskWindow.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::AutomationIdProperty,'CancelSettings'))
    ([Windows.Automation.InvokePattern]$taskCancel.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern)).Invoke()
    & (Join-Path $PSScriptRoot 'Measure-Resources.ps1') -ProcessId $taskProcess.Id -WarmupSeconds 30 -DurationSeconds 300 -OutputPath (Join-Path $taskRoot 'artifacts/final-resources.json')
} finally {
    Start-Process -FilePath $taskExe -ArgumentList '--exit' -WindowStyle Hidden -Wait
    $env:TIMZONER_TEST_DATA = $taskOriginalTestDirectory
}
