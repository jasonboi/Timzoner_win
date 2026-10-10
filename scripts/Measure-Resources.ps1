param(
    [Parameter(Mandatory)][int]$ProcessId,
    [int]$WarmupSeconds = 30,
    [int]$DurationSeconds = 300,
    [Parameter(Mandatory)][string]$OutputPath,
    [double]$MemoryLimitMiB = 60,
    [Nullable[double]]$CpuLimitPercent = 0.1
)
$ErrorActionPreference = 'Stop'
Start-Sleep -Seconds $WarmupSeconds
$taskProcess = Get-Process -Id $ProcessId
$taskStartCpu = $taskProcess.TotalProcessorTime.TotalSeconds
$taskTimer = [Diagnostics.Stopwatch]::StartNew()
$taskSamples = [Collections.Generic.List[object]]::new()
for ($taskIndex = 0; $taskIndex -lt $DurationSeconds; $taskIndex++) {
    Start-Sleep -Seconds 1
    $taskProcess.Refresh()
    if ($taskProcess.HasExited) { throw 'The measured process exited.' }
    $taskSamples.Add([pscustomobject]@{
        Seconds = [Math]::Round($taskTimer.Elapsed.TotalSeconds, 3)
        PrivateMiB = $taskProcess.PrivateMemorySize64 / 1MB
        WorkingSetMiB = $taskProcess.WorkingSet64 / 1MB
    })
}
$taskProcess.Refresh()
$taskCpu = ($taskProcess.TotalProcessorTime.TotalSeconds - $taskStartCpu) / $taskTimer.Elapsed.TotalSeconds / [Environment]::ProcessorCount * 100
$taskPeak = ($taskSamples.PrivateMiB | Measure-Object -Maximum).Maximum
$taskReport = [pscustomobject]@{
    TimestampUtc = [DateTime]::UtcNow.ToString('o')
    OS = [Environment]::OSVersion.VersionString
    LogicalProcessors = [Environment]::ProcessorCount
    ProcessId = $ProcessId
    Executable = $taskProcess.Path
    WarmupSeconds = $WarmupSeconds
    DurationSeconds = $taskTimer.Elapsed.TotalSeconds
    CpuPercent = $taskCpu
    PeakPrivateMiB = $taskPeak
    AveragePrivateMiB = ($taskSamples.PrivateMiB | Measure-Object -Average).Average
    PeakWorkingSetMiB = ($taskSamples.WorkingSetMiB | Measure-Object -Maximum).Maximum
    MemoryLimitMiB = $MemoryLimitMiB
    CpuLimitPercent = $CpuLimitPercent
    Passed = ($taskPeak -le $MemoryLimitMiB -and ($null -eq $CpuLimitPercent -or $taskCpu -le $CpuLimitPercent))
    Samples = $taskSamples
}
$taskReport | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $OutputPath -Encoding utf8
$taskReport | Select-Object * -ExcludeProperty Samples | Format-List
if (!$taskReport.Passed) { throw 'The measured process exceeded its resource budget.' }
