# DayKeep · 日守

**每天一步，直到完成。** 一个面向 Windows 的轻量桌面提醒工具：固定时间提醒，持续跟进尚未完成的事情。

适合每天查看专利缴费入口、追踪报销进度、等待通知，以及暂时无法办理但不能忘记的事项。**“今天已检查”与“整件事已完成”分别记录**，是本项目的核心设计。

## 当前状态

版本 **0.0.1-l0：桌面界面与工程基础预览**。这是独立原创项目，采用 AGPL-3.0-only，沿用 NameFirst Pro 的分阶段交付方式。本地仓库默认分支 main；尚未公开到 GitHub，尚未发行正式安装包。

当前可用：半透明背景、清晰不透明文字、完整/窄窗切换、窗口置顶、托盘收起与打开、Ctrl+Alt+D 快捷键、单实例唤回、外观设置保存、手动提醒样式预览。

**当前没有真实任务管理、定时提醒、重复提醒、开机启动或缴费状态检测。界面里的三条事项均为明确标注的示例，不会产生提醒。** 实际事项在后续阶段接入，请勿将 L0 用作重要事项的唯一提醒工具。

## 本地运行

Windows x64、PowerShell 7、global.json 中指定的 .NET 10 SDK：

```powershell
# 未安装 SDK 时可使用隔离的本地安装，无需管理员权限。
pwsh -File scripts/bootstrap.ps1
pwsh -File scripts/build.ps1
pwsh -File scripts/run.ps1
```

SDK 安装来源是微软官方 builds.dotnet.microsoft.com，版本和 SHA-512 锁定在 tools/sdk.lock.json；只放入被 Git 忽略的 .tools。已有匹配 SDK 时可以跳过 bootstrap。普通用户后续使用自包含成品，无需安装 SDK。

生成本地自包含预览（不是正式发行）：

```powershell
pwsh -File scripts/publish-preview.ps1
# 打开 dist/l0-win-x64/DayKeep.exe
pwsh -File scripts/create-shortcut.ps1
```

关闭 × 或按 Esc 会收起到托盘。单击托盘或按 Ctrl+Alt+D 打开；托盘右键可彻底退出。快捷键冲突时在底部说明，托盘仍可使用。详情见 [使用说明](INSTALL.md)。

## 制作步骤

| 阶段 | 内容 | 状态 |
| --- | --- | --- |
| L0 | 独立工程、半透明桌面界面、托盘和快捷唤起 | 已完成；20 项界面检查与实际单实例启动通过 |
| L1 | 任务管理、状态与本地数据库 | 待实施 |
| L2 | 固定时间、高亮提醒和每日检查 | 待实施 |
| L3 | 未完成重复、延期与截止日期 | 待实施 |
| L4 | 重启/休眠补提醒、去重和登录启动 | 待实施 |
| L5 | 记录、备份恢复、完整体验验收 | 待实施 |
| L6 | 品牌定稿、多语说明、成品与 GitHub 发行 | 待实施 |

[完整实施计划](IMPLEMENTATION_PLAN.md) · [项目需求](PROJECT_SPEC.md) · [界面设计](docs/DESIGN.md) · [L0 报告](L0_REPORT.md)

## 开发和验证

```powershell
pwsh -File scripts/check.ps1
pwsh -File scripts/build.ps1
pwsh -File scripts/test-shell.ps1
pwsh -File scripts/test-instance.ps1
git diff --check
```

界面检查在真实 WPF 中运行，截图与 JSON 报告保存在 .cache。不会将受控交互验证描述为真实用户连续多日验收。当前无第三方应用运行时包；.NET/WPF 和 Windows 系统组件不属于原创代码，遵循其各自许可。详见 [依赖](DEPENDENCIES.md)、[隐私](PRIVACY.md)、[贡献](CONTRIBUTING.md)、[许可](LICENSE)。
