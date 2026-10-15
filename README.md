# Timzoner

[简体中文](README-CN.md)

A lightweight system tray app for switching the Windows 11 time zone.

- Switch between favorite time zones from the tray's right-click menu.
- Enable automatic time zone switching from the tray menu to follow network changes.
- Refresh network location on demand and view its time zone, applying it when automatic switching is enabled.
- See the time zone currently used by Windows.
- Search available time zones, add or remove favorites, and arrange their menu order.
- Choose light, dark, or system appearance.
- Switch the interface between English and Simplified Chinese.
- Keep running in the tray when the settings window closes.
- Optionally start when you sign in to Windows.
- Follow Windows daylight saving rules and verify each time zone change.
- Save preferences locally and keep them across upgrades.
- Install for the current user with a small app package.

## Runtime requirement

Requires [.NET 10 Desktop Runtime for Windows x64](https://dotnet.microsoft.com/en-us/download/dotnet/10.0). Under .NET Desktop Runtime, choose the Windows x64 installer. The SDK and ASP.NET Core Runtime are not required.

If Timzoner cannot start because the runtime is missing, the .NET launch dialog provides a download link. Install the Desktop Runtime, then open Timzoner again. The default runtime location is `C:\Program Files\dotnet\`. Timzoner shares this runtime with other .NET desktop apps.

Automatic switching is off by default. When enabled, [IP Info](https://ip-info.com/docs) looks up the time zone of your public network exit, so the provider sees your public IP. A VPN or proxy may affect the result. Failed lookups keep the current time zone.
