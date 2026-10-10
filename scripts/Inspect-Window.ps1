param([Parameter(Mandatory)][int]$ProcessId, [string]$ScreenshotPath)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing
$taskRoot = [Windows.Automation.AutomationElement]::RootElement
$taskCondition = [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ProcessIdProperty, $ProcessId)
$taskHandle = (Get-Process -Id $ProcessId).MainWindowHandle
$taskWindow = if ($taskHandle -ne [IntPtr]::Zero) { [Windows.Automation.AutomationElement]::FromHandle($taskHandle) } else { $taskRoot.FindFirst([Windows.Automation.TreeScope]::Children, $taskCondition) }
if (!$taskWindow) { throw 'Application window not found.' }
$taskElements = $taskWindow.FindAll([Windows.Automation.TreeScope]::Descendants, [Windows.Automation.Condition]::TrueCondition)
foreach ($taskElement in $taskElements) {
    $taskState = $taskElement.Current
    if ($taskState.AutomationId -or $taskState.Name) {
        [pscustomobject]@{Id=$taskState.AutomationId;Name=$taskState.Name;Type=$taskState.ControlType.ProgrammaticName;Enabled=$taskState.IsEnabled}
    }
}
if ($ScreenshotPath) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class WindowCapture {
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
  [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hwnd);
  public struct Rect { public int Left, Top, Right, Bottom; }
}
'@
    [void][WindowCapture]::SetThreadDpiAwarenessContext([IntPtr](-4))
    $taskHandle = [IntPtr]$taskWindow.Current.NativeWindowHandle
    $taskRect = [WindowCapture+Rect]::new()
    [void][WindowCapture]::GetWindowRect($taskHandle, [ref]$taskRect)
    $taskBitmap = [Drawing.Bitmap]::new($taskRect.Right - $taskRect.Left, $taskRect.Bottom - $taskRect.Top)
    $taskGraphics = [Drawing.Graphics]::FromImage($taskBitmap)
    $taskHdc = $taskGraphics.GetHdc()
    try {
        if (![WindowCapture]::PrintWindow($taskHandle, $taskHdc, 2)) { throw 'Window capture failed.' }
    } finally { $taskGraphics.ReleaseHdc($taskHdc); $taskGraphics.Dispose() }
    try { $taskBitmap.Save($ScreenshotPath, [Drawing.Imaging.ImageFormat]::Png) } finally { $taskBitmap.Dispose() }
}
