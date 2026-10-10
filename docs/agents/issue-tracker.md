# Local Markdown issue tracker

任务与规格保存在 `.scratch/<feature-slug>/`，该目录纳入 Git，发布到公开仓库时也会公开。

- 功能规格使用 `spec.md`。
- 实施任务各自使用 `issues/<NN>-<slug>.md`，从 `01` 开始编号。
- 文件顶部使用 `Status:` 行，取值见 `triage-labels.md`。
- 评论追加到文件末尾的 `## Comments`。
- 技能要求发布到 issue tracker 时，创建或更新上述本地文件。
- 技能要求读取任务时，读取指定路径或编号对应的文件。

PRD 保存于 `docs/PRD.md`。GitHub 用于公开代码与版本，任务以本地 Markdown 为准。

## Wayfinding

使用 wayfinder 时，工作地图位于 `.scratch/<effort>/map.md`，每个子任务单独保存于 `issues/`。
子任务使用 `Type:` 记录 research、prototype、grilling 或 task，使用 `Blocked by:` 记录依赖编号。只有依赖全部 resolved 后才可领取，领取时写入 `Status: claimed`，完成后追加 `## Answer` 并改为 `Status: resolved`，同时更新地图。
claimed 和 resolved 仅用于 wayfinder 子任务，其余任务使用默认五个状态。
