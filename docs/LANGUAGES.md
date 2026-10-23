# DayKeep 界面语言 / Interface languages

适用版本：[公开预发行版 0.7.0-beta.1](https://github.com/Thicky-han/DayKeep/releases/tag/v0.7.0-beta.1)，已获维护者确认发布。[Windows x64 ZIP](https://github.com/Thicky-han/DayKeep/releases/download/v0.7.0-beta.1/DayKeep-0.7.0-beta.1-win-x64.zip) · [对应源码](https://github.com/Thicky-han/DayKeep/releases/download/v0.7.0-beta.1/DayKeep-0.7.0-beta.1-source.zip) · [SHA256SUMS](https://github.com/Thicky-han/DayKeep/releases/download/v0.7.0-beta.1/SHA256SUMS)。实际构建、打包及公开验证进度见 [语言更新报告](../LANGUAGE_UPDATE_REPORT.md)。历史版本 0.6.0-l6 只有简体中文界面，九语 README 不代表旧版具有语言切换；原标签和附件不变，批准包内冻结文档反映打包时的候选状态。

## 入口与支持列表

主窗口顶部 **Language / 语言** 在完整窗口和窄窗均可直接找到。选项采用语言本身的名称，因此即使误选了不熟悉的语言，也能在同一入口找回。

| 选项 / Native name | 语言 / Language | 设置值 / Code |
| --- | --- | --- |
| 简体中文 | 简体中文 / Simplified Chinese | zh-CN |
| 繁體中文 | 繁体中文 / Traditional Chinese | zh-TW |
| English | 英语 / English | en |
| 日本語 | 日语 / Japanese | ja |
| 한국어 | 韩语 / Korean | ko |
| Français | 法语 / French | fr |
| Deutsch | 德语 / German | de |
| Español | 西班牙语 / Spanish | es |
| Português | 葡萄牙语 / Portuguese | pt |

## 保存与更新

选择语言后即刻更新应用文案，不要求重启。主窗、现有表单、提醒、历史与备份工具及托盘使用同一选择；未保存的任务输入继续保留。首次使用、旧版本没有语言选项或读取到不支持的语言代码时回退为简体中文，不依据文档语言改变设置。

选择存于当前用户 LocalApplicationData/DayKeep/ui-settings.json，与外观设置一起保留。保存失败会显示错误并保留之前的语言；修复文件权限、磁盘或占用后重新选择。更换程序版本目录、重开应用会继续使用已保存的语言。任务逻辑备份不含界面设置，导出/导入不会迁移或覆盖语言。

## 内容与格式边界

任务名称、用户备注、链接、私人编号和原始记录不送往翻译服务，也不自动翻译。历史的操作名称等应用标签跟随界面语言，用户输入保持原文。不存在联网翻译、账号或遥测要求。

历史搜索支持当前界面语言的操作名关键词，也兼容原有中文规范操作名，并按原词匹配当前任务名称和原始检查备注；使用不区分大小写的文字包含匹配，不翻译用户输入或改写原始记录。切换语言保留当前结果、筛选和搜索词；再次点击查询时，操作名按新选语言匹配。例如切到 English 后可搜索英语操作关键词，仍可用你填写的原名称或备注检索。

提醒仍采用电脑的本地时间和所选星期；换语言不改变时区、时刻、日预算、延期或任务状态。时间输入保持 24 小时 `HH:mm`，例如 `09:00, 14:30`；截止日期输入保持 `yyyy-MM-dd`。操作系统提供的文件选择器等原生窗口可遵循 Windows 自身的显示语言。技术标识、文件路径和底层错误详情保留其原始形式，以便排错。

文案已提供九种语言，尚未声称经过每种语言母语使用者的全面人工校对。若发现不自然的译文，可附界面位置和原文反馈，不必提供私人任务内容。

## Quick guide in English

Open **Language / 语言** at the top of the main window, in either expanded or compact mode. Choose one of the nine native names above. App text updates immediately and the choice is saved for the next launch. The default is Simplified Chinese when no supported choice is saved. A save failure shows an error and keeps the previous language.

Your task titles, notes and links remain unchanged. Scheduling still uses the computer's local time zone and selected weekdays; enter times as 24-hour `HH:mm` and deadlines as `yyyy-MM-dd`. Language is stored in ui-settings.json and is excluded from logical task backups. Native operating-system dialogs and technical error details may use their original language.

History search matches action keywords in the current interface language, also accepts the canonical Chinese action names, and searches task names and original check notes as entered. Matching is a case-insensitive substring search; original records are not rewritten. A language change preserves existing results, filters and the search text. Run the query again to match action labels in the newly selected language.

[简体中文教程](../README.md) · [English tutorial](../README.en.md) · [详细使用说明](../INSTALL.md)
