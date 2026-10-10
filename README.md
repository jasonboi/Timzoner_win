# Timzoner

面向 Windows 11 x64 的轻量系统时区切换工具。通过系统托盘右键菜单选择收藏时区，配置页管理收藏和主题。

项目目前完成需求、技术方案和仓库初始化，尚未实现程序或生成安装包。

## 文档

- [产品需求](docs/PRD.md)
- [实施规格](.scratch/timezone-switcher/spec.md)
- [技术方案](docs/architecture.md)
- [验收与资源测量](docs/validation.md)
- [术语表](GLOSSARY.md)

## 技术栈

C#、.NET 10 LTS、WinForms、Windows 时区 API、System.Text.Json、Inno Setup。计划发布随包携带运行时的 Windows x64 安装程序。

开发阶段需要 .NET 10 SDK 和 Inno Setup，运行时无法替代 SDK。依赖版本将在建立可编译项目时固定，目前没有可执行的构建命令。

## 工程流程

Matt skills 已配置为本地 Markdown 任务跟踪。规格和后续任务保存在 `.scratch/`，纳入版本管理。项目指引见 [AGENTS.md](AGENTS.md)。
