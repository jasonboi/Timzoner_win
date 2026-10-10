# Timzoner

Windows 系统时区托盘工具。产品范围见 `docs/PRD.md`，实施前读取 `.scratch/timezone-switcher/spec.md`。

## Agent skills

### Issue tracker

任务与规格保存在本地 Markdown。创建、读取或更新任务前读取 `docs/agents/issue-tracker.md`。

### Triage labels

使用 Matt skills 默认的五个状态。修改任务状态前读取 `docs/agents/triage-labels.md`。

### Domain docs

本项目使用单一领域。探索相关功能前按 `docs/agents/domain.md` 读取术语与架构决策。

## Verification

测试只通过应用行为入口替换系统适配器。真实系统时区切换必须在明确授权的人工验收中执行，并记录原时区、测试结果和恢复结果。

资源占用以 Release 实测为准，目标及测量方式见 `docs/validation.md`。未测量的数据不得写成已达标。
