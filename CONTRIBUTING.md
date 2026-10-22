# 贡献与开发

先读 PROJECT_SPEC.md、IMPLEMENTATION_PLAN.md、AGENTS.md 和当前阶段报告。以当前阶段边界提交相关修改；不要把界面示例或受控验证写成已经运行的提醒服务。

使用 Windows、PowerShell 7、global.json 指定 SDK。依次执行 `pwsh -File scripts/check.ps1`、`pwsh -File scripts/build.ps1`、`pwsh -File scripts/test-core.ps1`、`pwsh -File scripts/test-shell.ps1` 与 `git diff --check`。改布局后查看 .cache 中实际 WPF 渲染。新增包先审查来源、许可和版本，再锁定。

保持 UI、领域状态、数据库和调度分层；任何“今日已检查”不能隐式完成任务，打开链接也不能假定用户处理成功。保留存储失败提示和恢复路径。

使用独立分支，例如 codex/l1-task-storage。提交说明包含问题、行为变化和实际验证。每个阶段更新报告与计划，不声称未执行的 GitHub CI 或人工长期试用通过。贡献者提交即表示有权按 AGPL-3.0-only 提供内容，无额外身份材料要求。

不要提交 .cache、.tools、bin/obj、生成包、私人备份、凭据和机器绝对路径；提交身份使用 GitHub noreply 邮箱。正式公开与发行按 L6 清单执行。
