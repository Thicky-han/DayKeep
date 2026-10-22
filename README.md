# DayKeep · 日守

![DayKeep 回环时钟 Logo](assets/brand/daykeep-mark.png)

**每天一步，直到完成。** 面向 Windows 的轻量桌面任务与提醒工具，帮助持续跟进暂时无法办理、需要反复查看的事情。

当前版本 **0.1.1-l1：真实任务管理与本地保存、新版回环时钟 Logo**。新建、编辑、暂停/恢复、完成/撤销、归档/恢复已可用；默认空列表，不自动写入示例任务。半透明背景、窄窗、置顶、托盘与 Ctrl+Alt+D 保留。

**定时弹窗、每日检查、重复提醒和登录启动尚未接入。表单中的时间与星期目前只保存，不会触发提醒。** 下一阶段 L2 接入固定时间的高亮提醒，L3 实现未完成追加与延期。

## 打开与使用

本地自包含预览：打开 dist/l1-logo-win-x64/DayKeep.exe，保留整个文件夹，无需安装 SDK。桌面 DayKeep 日守快捷方式指向此版本。若旧窗口仍在运行，先保存表单、从托盘退出，再重新打开以加载新 Logo。当前仅本地交付，未公开到 GitHub，也不是正式签名安装包。

1. 点击“＋ 新建任务”，输入名称并选择普通待办或等待条件。
2. 可填平台链接、备注、多个时间及提醒星期；截止日期未知时留空。
3. 保存后在“跟进中”查看任务，编辑、暂停或确认完成。
4. 从“已完成”分类撤销完成，恢复完成前的业务状态和暂停设置；重启后仍可撤销。
5. 归档只收起事项，不删除数据；从“已归档”恢复。
6. 关闭 × / Esc 收起到托盘；Ctrl+Alt+D、单击托盘或再次启动可唤回。彻底退出使用托盘右键菜单。

任务、检查记录模型、状态操作记录与外观保存在当前用户 LocalApplicationData 的 DayKeep 文件夹。读写错误会提示并保留原文件，不会自动清空数据库。导出与恢复界面留在 L5。详细步骤见 [使用说明](INSTALL.md)。

## 开发运行

Windows x64、PowerShell 7、global.json 固定的 .NET 10 SDK：

```powershell
pwsh -File scripts/bootstrap.ps1   # 已有对应 SDK 可跳过
pwsh -File scripts/build.ps1
pwsh -File scripts/run.ps1
```

SDK 从微软官方来源下载并验证 SHA-512，只装入忽略的 .tools。应用包从 NuGet 官方源还原，准确版本与内容哈希在各项目 packages.lock.json 中，默认锁定还原。

```powershell
pwsh -File scripts/check.ps1
pwsh -File scripts/test-core.ps1
pwsh -File scripts/test-shell.ps1
pwsh -File scripts/publish-preview.ps1
pwsh -File scripts/create-shortcut.ps1
# 关闭正在运行的日守后，才运行单实例检查，以免影响已有窗口。
pwsh -File scripts/test-instance.ps1
git diff --check
```

核心检查使用真实 SQLite 文件，包含独立进程写入/重启恢复。WPF 检查在隔离数据库运行表单、按钮与真实布局渲染，报告保存在忽略的 .cache，不读取私人任务。它们不代替实体快捷键、多屏、连续多日提醒的人工验收。

## 分阶段制作

| 阶段 | 内容 | 状态 |
| --- | --- | --- |
| L0 | 工程、半透明桌面界面、托盘和快捷唤起 | 已完成 |
| L1 | 两类任务、SQLite、增改、暂停、完成撤销、归档恢复 | 已完成，见阶段报告 |
| L2 | 固定时间、高亮弹窗、每日检查 | 待实施 |
| L3 | 未完成重复、延期、截止日期提醒 | 待实施 |
| L4 | 重启/休眠补提醒、去重、登录启动 | 待实施 |
| L5 | 历史记录、备份恢复、完整体验验收 | 待实施 |
| L6 | 品牌定稿、多语说明、成品与 GitHub 发行 | 待实施 |

[实施计划](IMPLEMENTATION_PLAN.md) · [项目需求](PROJECT_SPEC.md) · [L1 报告](L1_REPORT.md) · [数据模型](docs/DATA_MODEL.md) · [界面规范](docs/DESIGN.md)

独立原创项目，AGPL-3.0-only；第三方依赖保留各自许可。[依赖](DEPENDENCIES.md) · [第三方许可](third-party/README.md) · [隐私](PRIVACY.md) · [贡献](CONTRIBUTING.md) · [许可全文](LICENSE)
