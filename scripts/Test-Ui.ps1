param([Parameter(Mandatory)][int]$ProcessId, [Parameter(Mandatory)][string]$Executable, [Parameter(Mandatory)][string]$ArtifactDirectory)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class ComboInteraction {
  [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hwnd, uint message, IntPtr wparam, IntPtr lparam);
  [DllImport("user32.dll")] public static extern IntPtr GetParent(IntPtr hwnd);
  [DllImport("user32.dll")] public static extern int GetDlgCtrlID(IntPtr hwnd);
  [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hwnd);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
}
'@
function Get-Window {
    for ($taskAttempt = 0; $taskAttempt -lt 30; $taskAttempt++) {
        $taskProcess = Get-Process -Id $ProcessId
        $taskProcess.Refresh()
        if ($taskProcess.MainWindowHandle -ne [IntPtr]::Zero) {
            $taskWindow = [Windows.Automation.AutomationElement]::FromHandle($taskProcess.MainWindowHandle)
            if ($taskWindow.Current.Name -like 'Timzoner*') { return $taskWindow }
        }
        Start-Sleep -Milliseconds 100
    }
    throw 'Settings window is not open.'
}
function Find-Control([string]$Id) {
    $taskControl = (Get-Window).FindFirst([Windows.Automation.TreeScope]::Descendants, [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::AutomationIdProperty, $Id))
    if (!$taskControl) { throw "Control not found: $Id" }; return $taskControl
}
function Invoke-Control([string]$Id) {
    $taskControl = Find-Control $Id
    ([Windows.Automation.InvokePattern]$taskControl.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern)).Invoke()
    Start-Sleep -Milliseconds 150
}
function List-Items([string]$Id) {
    return (Find-Control $Id).FindAll([Windows.Automation.TreeScope]::Children, [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::ListItem))
}
function Select-Item($Item) { ([Windows.Automation.SelectionItemPattern]$Item.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern)).Select() }
function Set-Theme([int]$Index) {
    $taskCombo = Find-Control 'Theme'
    $taskComboHandle = [IntPtr]$taskCombo.Current.NativeWindowHandle
    [void][ComboInteraction]::SendMessage($taskComboHandle, 0x14e, [IntPtr]$Index, [IntPtr]::Zero)
    $taskControlId = [ComboInteraction]::GetDlgCtrlID($taskComboHandle)
    [void][ComboInteraction]::SendMessage([ComboInteraction]::GetParent($taskComboHandle), 0x111, [IntPtr](0x10000 -bor ($taskControlId -band 0xffff)), $taskComboHandle)
    Start-Sleep -Milliseconds 300
}
function Open-Settings {
    $taskSecond = Start-Process -FilePath $Executable -ArgumentList '--settings' -PassThru
    $taskSecond.WaitForExit(5000) | Out-Null
    Start-Sleep -Milliseconds 200
}
if (!$env:TIMZONER_TEST_DATA) { throw 'UI acceptance requires an isolated TIMZONER_TEST_DATA directory.' }
$taskSettingsPath = Join-Path $env:TIMZONER_TEST_DATA 'settings.json'
$taskOriginal = [IO.File]::ReadAllBytes($taskSettingsPath)
$taskAssertions = [Collections.Generic.List[string]]::new()
try {
    Invoke-Control 'CancelSettings'
    Open-Settings
    $taskOriginalSaveCaption = (Find-Control 'SaveSettings').Current.Name
    $taskBefore = (List-Items 'FavoriteZones').Count
    $taskSearch = Find-Control 'Search'
    ([Windows.Automation.ValuePattern]$taskSearch.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern)).SetValue('Hawaiian Standard Time')
    Start-Sleep -Milliseconds 200
    if ((List-Items 'AvailableZones').Count -ne 1) { throw 'Search did not find exactly one Hawaiian time zone.' }
    $taskAssertions.Add('Search by Windows ID')
    Invoke-Control 'AddFavorite'
    if ((List-Items 'FavoriteZones').Count -ne $taskBefore + 1) { throw 'Add favorite failed.' }
    Invoke-Control 'AddFavorite'
    if ((List-Items 'FavoriteZones').Count -ne $taskBefore + 1) { throw 'Duplicate favorite was accepted.' }
    $taskAssertions.Add('Add and duplicate prevention')
    $taskAddedName = (List-Items 'FavoriteZones')[$taskBefore].Current.Name
    Invoke-Control 'MoveUp'
    if ((List-Items 'FavoriteZones')[$taskBefore - 1].Current.Name -ne $taskAddedName) { throw 'Move up did not change order.' }
    Invoke-Control 'MoveDown'
    if ((List-Items 'FavoriteZones')[$taskBefore].Current.Name -ne $taskAddedName) { throw 'Move down did not restore order.' }
    $taskAssertions.Add('Move up and down')
    Set-Theme 1
    & (Join-Path $PSScriptRoot 'Inspect-Window.ps1') -ProcessId $ProcessId -ScreenshotPath (Join-Path $ArtifactDirectory 'settings-light.png') | Out-Null
    Set-Theme 2
    & (Join-Path $PSScriptRoot 'Inspect-Window.ps1') -ProcessId $ProcessId -ScreenshotPath (Join-Path $ArtifactDirectory 'settings-dark.png') | Out-Null
    $taskAssertions.Add('Light and dark live preview')
    $taskBounds = (Get-Window).Current.BoundingRectangle
    foreach ($taskId in @('Search','AvailableZones','FavoriteZones','AddFavorite','MoveUp','MoveDown','RemoveFavorite','Theme','StartWithWindows','LanguageToggle','SaveSettings','CancelSettings')) {
        if (!$taskBounds.Contains((Find-Control $taskId).Current.BoundingRectangle)) { throw "Control is clipped outside the window: $taskId" }
    }
    $taskAssertions.Add('All configuration controls fit inside the current display window')
    if ((Find-Control 'LanguageToggle').Current.BoundingRectangle.Top -lt (Find-Control 'Theme').Current.BoundingRectangle.Bottom) { throw 'Language toggle is not below appearance.' }
    Invoke-Control 'LanguageToggle'
    if ((Find-Control 'SaveSettings').Current.Name -ne 'Save' -or (Find-Control 'AddFavorite').Current.Name -ne 'Add favorite') { throw 'English language preview failed.' }
    $taskBounds = (Get-Window).Current.BoundingRectangle
    foreach ($taskId in @('AddFavorite','MoveUp','MoveDown','RemoveFavorite','Theme','StartWithWindows','LanguageToggle','SaveSettings','CancelSettings')) {
        if (!$taskBounds.Contains((Find-Control $taskId).Current.BoundingRectangle)) { throw "English control is clipped outside the window: $taskId" }
    }
    & (Join-Path $PSScriptRoot 'Inspect-Window.ps1') -ProcessId $ProcessId -ScreenshotPath (Join-Path $ArtifactDirectory 'settings-english.png') | Out-Null
    $taskAssertions.Add('Language button below appearance previews English')
    if ((List-Items 'FavoriteZones')[$taskBefore].Current.Name -ne '(UTC-10:00) Hawaiian Standard Time') { throw 'English favorite label is missing its UTC offset.' }
    $taskNormalBounds = (Get-Window).Current.BoundingRectangle
    $taskWindowHandle = [IntPtr](Get-Window).Current.NativeWindowHandle
    $taskDpiScale = [ComboInteraction]::GetDpiForWindow($taskWindowHandle) / 96.0
    [void][ComboInteraction]::SetWindowPos($taskWindowHandle, [IntPtr]::Zero, 0, 0, [int](380 * $taskDpiScale), [int](460 * $taskDpiScale), 0x16)
    Start-Sleep -Milliseconds 500
    $taskViewport = Find-Control 'SettingsViewport'
    $taskViewportHandle = [IntPtr]$taskViewport.Current.NativeWindowHandle
    foreach ($taskId in @('AvailableZones','FavoriteZones')) {
        if ((Find-Control $taskId).Current.BoundingRectangle.Width -gt $taskViewport.Current.BoundingRectangle.Width) { throw "Small-window list exceeds viewport width: $taskId" }
    }
    $taskSearch = Find-Control 'Search'
    ([Windows.Automation.ValuePattern]$taskSearch.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern)).SetValue('')
    Start-Sleep -Milliseconds 250
    & (Join-Path $PSScriptRoot 'Inspect-Window.ps1') -ProcessId $ProcessId -ScreenshotPath (Join-Path $ArtifactDirectory 'settings-small-top.png') | Out-Null
    [void][ComboInteraction]::SendMessage($taskViewportHandle, 0x115, [IntPtr]7, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 250
    $taskBounds = (Get-Window).Current.BoundingRectangle
    & (Join-Path $PSScriptRoot 'Inspect-Window.ps1') -ProcessId $ProcessId -ScreenshotPath (Join-Path $ArtifactDirectory 'settings-small-bottom.png') | Out-Null
    foreach ($taskId in @('MoveUp','MoveDown','RemoveFavorite','LanguageToggle','SaveSettings','CancelSettings')) {
        if (!$taskBounds.Contains((Find-Control $taskId).Current.BoundingRectangle)) { throw "Small-window control cannot be reached by scrolling: $taskId" }
    }
    [void][ComboInteraction]::SendMessage($taskViewportHandle, 0x115, [IntPtr]::Zero, [IntPtr]::Zero)
    [void][ComboInteraction]::SetWindowPos($taskWindowHandle, [IntPtr]::Zero, 0, 0, [int]$taskNormalBounds.Width, [int]$taskNormalBounds.Height, 0x16)
    Start-Sleep -Milliseconds 250
    $taskAssertions.Add('English UTC offset, narrow lists, and scroll access to all favorite and save controls')
    Invoke-Control 'CancelSettings'
    if ([Convert]::ToBase64String([IO.File]::ReadAllBytes($taskSettingsPath)) -ne [Convert]::ToBase64String($taskOriginal)) { throw 'Cancel changed configuration.' }
    $taskAssertions.Add('Cancel preserves file')
    Open-Settings
    if ((Find-Control 'SaveSettings').Current.Name -ne $taskOriginalSaveCaption) { throw 'Cancel did not restore the original language.' }
    $taskSearch = Find-Control 'Search'
    ([Windows.Automation.ValuePattern]$taskSearch.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern)).SetValue('Hawaiian Standard Time')
    Start-Sleep -Milliseconds 200
    Invoke-Control 'AddFavorite'
    Invoke-Control 'MoveUp'
    Set-Theme 2
    Invoke-Control 'LanguageToggle'
    $taskAddedName = '(UTC-10:00) Hawaiian Standard Time'
    Invoke-Control 'SaveSettings'
    $taskSaved = Get-Content -LiteralPath $taskSettingsPath -Raw | ConvertFrom-Json
    if ($taskSaved.favoriteTimeZoneIds.Count -ne $taskBefore + 1 -or $taskSaved.favoriteTimeZoneIds[$taskBefore - 1] -ne 'Hawaiian Standard Time' -or $taskSaved.theme -ne 'dark' -or $taskSaved.language -ne 'en') { throw 'Save did not persist modified favorites, order, theme and language.' }
    Open-Settings
    if ((Find-Control 'SaveSettings').Current.Name -ne 'Save') { throw 'Saved English language was not restored.' }
    if ((List-Items 'FavoriteZones').Count -ne $taskBefore + 1 -or (List-Items 'FavoriteZones')[$taskBefore - 1].Current.Name -ne $taskAddedName) { throw 'Reopen did not retain saved order.' }
    $taskCombo = Find-Control 'Theme'
    if ([ComboInteraction]::SendMessage([IntPtr]$taskCombo.Current.NativeWindowHandle, 0x147, [IntPtr]::Zero, [IntPtr]::Zero).ToInt32() -ne 2) { throw 'Reopen did not retain theme.' }
    $taskAssertions.Add('Save modified favorites, order, theme and English language, then reopen')
    Select-Item (List-Items 'FavoriteZones')[$taskBefore - 1]
    Invoke-Control 'RemoveFavorite'
    Invoke-Control 'SaveSettings'
    $taskSaved = Get-Content -LiteralPath $taskSettingsPath -Raw | ConvertFrom-Json
    if ($taskSaved.favoriteTimeZoneIds.Count -ne $taskBefore -or $taskSaved.favoriteTimeZoneIds -contains 'Hawaiian Standard Time') { throw 'Delete was not persisted.' }
    $taskAssertions.Add('Delete and persist favorite removal')
    Open-Settings
    for ($taskCycle = 0; $taskCycle -lt 20; $taskCycle++) { Invoke-Control 'CancelSettings'; Open-Settings }
    $taskAssertions.Add('20 window close/reopen cycles with one process')
    $taskAssertions | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $ArtifactDirectory 'ui-checks.json') -Encoding utf8
    $taskAssertions
} finally {
    # Only the app-specific configuration used by this acceptance test is restored.
    [IO.File]::WriteAllBytes($taskSettingsPath, $taskOriginal)
}
