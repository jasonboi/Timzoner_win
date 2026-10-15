# Timzoner

[English](README.md)

轻量的 Windows 11 系统时区托盘切换工具。

- 从托盘右键菜单快速切换收藏时区。
- 可在托盘菜单开启自动切换时区，随网络变化更新。
- 可手动刷新网络环境并查看时区，开启自动切换时应用检测结果。
- 显示 Windows 实际使用的当前时区。
- 搜索可用时区，添加、删除收藏并调整菜单顺序。
- 支持浅色、深色和跟随系统外观。
- 支持简体中文与英文界面切换。
- 关闭配置窗口后继续驻留托盘。
- 可选择登录 Windows 时自动启动。
- 遵循 Windows 夏令时规则，切换后读取系统状态确认结果。
- 本地保存偏好设置，升级时保留配置。
- 按用户安装，应用安装包体积小。

## 运行环境

需要 [.NET 10 Desktop Runtime，Windows x64](https://dotnet.microsoft.com/zh-cn/download/dotnet/10.0)。在页面的 .NET Desktop Runtime 区域选择 Windows x64 安装程序，无需安装 SDK 或 ASP.NET Core Runtime。

如果缺少运行时，启动 Timzoner 时会出现 .NET 错误提示并提供下载入口。安装 Desktop Runtime 后重新打开 Timzoner 即可。运行时默认安装在 `C:\Program Files\dotnet\`，可供其他 .NET 桌面应用共用。

自动切换默认关闭。开启后通过 [IP Info](https://ip-info.com/docs) 查询公网出口的时区，服务方会看到公网 IP。VPN 或代理可能影响结果。查询失败时保留当前时区。
