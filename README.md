# DayKeep · 日守

![DayKeep 回环时钟 Logo](assets/brand/daykeep-mark.png)

**每天一步，直到完成。** 面向 Windows 的轻量桌面任务与提醒工具，帮助持续跟进暂时无法办理、需要反复查看的事情。

当前版本 **0.2.0-l2：固定时间高亮提醒与每日检查**。支持任务管理、按时间/星期提醒、打开平台、今日已检查、确认开放转待办理及明确完成。保留已认可的回环时钟 Logo、半透明背景、窄窗、托盘与 Ctrl+Alt+D。

**固定时间提醒已启用，需要程序运行且电脑处于唤醒、桌面可用状态。** 同时到期的事项合并展示，提醒不主动激活键盘焦点，也不因到期分钟过去而自动消失。每隔一段时间追加、稍后提醒、关机/休眠后的完整补提醒及登录启动仍待 L3/L4。

## 打开与使用

本地自包含预览：打开 dist/l2-win-x64/DayKeep.exe，保留整个文件夹，无需安装 SDK。桌面 DayKeep 日守快捷方式指向此版本。若旧窗口仍在运行，先保存表单、从托盘退出，再重新打开加载 L2。当前仅本地交付，未公开到 GitHub，也不是正式签名安装包。

1. 点击“＋ 新建任务”，输入名称并选择普通待办或等待条件。
2. 填写多个提醒时间与星期，可加平台链接和备注；截止日期未知时留空。
3. 保持日守在托盘运行，到点查看提醒；“打开平台”只打开网页，不记录检查或完成。
4. 等待条件的事项可选择“今日已检查，尚未开放”，停掉当日检查提醒；或“已开放，转待办理”，继续保留直到明确完成。
5. 从“已完成”分类撤销完成，恢复完成前的业务状态和暂停设置；重启后仍可撤销。归档也可恢复。
6. 关闭主窗 × / Esc 收起到托盘，继续提醒；Ctrl+Alt+D 或托盘唤回。彻底退出使用托盘右键菜单。

关闭提醒窗口仅收起本次提醒，不完成任务；后续固定时间仍会提示。L2 不追补启动前或已经错过的分钟，新建/编辑/恢复只对后续时间生效。“今日已检查”按本地日期判断，次日重新按所选星期提醒。完整错过补偿、跨重启提醒去重和系统时间变化处理留在 L4。

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
pwsh -File scripts/test-reminder-live.ps1  # 等待下一实际分钟，仅本地桌面运行
pwsh -File scripts/publish-preview.ps1
pwsh -File scripts/create-shortcut.ps1
# 关闭正在运行的日守后，才运行单实例检查，以免影响已有窗口。
pwsh -File scripts/test-instance.ps1
git diff --check
```

核心检查使用真实 SQLite 文件，包含独立进程恢复、schema-1 升级、本地时间/星期和每日检查。WPF 受控检查使用可替换时钟；真实时钟另测下一实际分钟的弹窗延迟、合并和前台焦点。所有报告和合成任务均在忽略的 .cache，不读取私人任务，不代替实体快捷键、多屏、锁屏恢复或连续多日人工验收。

## 分阶段制作

| 阶段 | 内容 | 状态 |
| --- | --- | --- |
| L0 | 工程、半透明桌面界面、托盘和快捷唤起 | 已完成 |
| L1 | 两类任务、SQLite、增改、暂停、完成撤销、归档恢复 | 已完成，见阶段报告 |
| L2 | 固定时间、高亮弹窗、每日检查 | 已完成，见 L2 报告 |
| L3 | 未完成重复、延期、截止日期提醒 | 待实施 |
| L4 | 重启/休眠补提醒、去重、登录启动 | 待实施 |
| L5 | 历史记录、备份恢复、完整体验验收 | 待实施 |
| L6 | 品牌定稿、多语说明、成品与 GitHub 发行 | 待实施 |

[实施计划](IMPLEMENTATION_PLAN.md) · [项目需求](PROJECT_SPEC.md) · [L2 报告](L2_REPORT.md) · [提醒规则](docs/REMINDERS.md) · [数据模型](docs/DATA_MODEL.md)

独立原创项目，AGPL-3.0-only；第三方依赖保留各自许可。[依赖](DEPENDENCIES.md) · [第三方许可](third-party/README.md) · [隐私](PRIVACY.md) · [贡献](CONTRIBUTING.md) · [许可全文](LICENSE)
