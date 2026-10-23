# L1—L5 任务与存储契约

## 分层

DayKeep.Core 为 net10.0 库，包含不可变任务模型、输入验证、状态转移、TaskService、SQLite Repository 和纯 ReminderPlanner；不依赖 WPF、窗口、系统托盘或定时器。DayKeep.Desktop 的 ReminderController 通过可替换时钟调用核心，使用真实 WPF 计时器和窗口，不从 UI 假定已保存。

## 任务

TaskKind：Todo（普通待办）、WaitingForCondition（等待条件）。TaskState：Waiting、Ready、Completed。暂停为独立 Paused；归档为独立 ArchivedAt，UI 显示“已归档”但保留原业务状态。任务具有 Guid、Revision、创建/更新时间、链接、备注、可未知截止日期及 ReminderRule。

完成时保存 BeforeCompletion 和 PausedBeforeCompletion，清除活动暂停标记；撤销完成恢复原值并清空 CompletedAt。归档/恢复不改变业务状态。完成或归档后禁止编辑/暂停，需先撤销或恢复。改任务类型时将未完成任务切到该类型的初始状态，同类型编辑不重置业务状态。

ReminderRule：规范化 HH:mm 字符串（最多八个不同时间）、星期位掩码、追加间隔与每天上限。默认 09:00、30 分钟、3 次；间隔 5—1440，上限 0—24，0 关闭追加。位 0 为周一，位 6 为周日；127 每天，31 工作日。TaskDraft 与表单同时验证这些参数。

## 数据库 schema 5

固定文件名 tasks.db；PRAGMA application_id=1145786417，user_version=5。schema 1 增加检查结果，schema 1/2 增加进度，schema 1/2/3 增加日期预算和投递表，所需变更在一个升级事务内执行。L3 的预算、延期、下一追加和最近展示时间迁入，旧版本未保存的收起状态不能追溯。保留任务、事件、检查与完成快照；未知/更新/损坏库不清空，升级失败保留原版本。旧 L1—L4 拒绝 schema 5。

- tasks：任务字段、修订号、完成撤销快照与归档时间；约束业务枚举、暂停、星期、次数、完成字段一致性。
- task_events：任务 ID、操作类型、UTC 时间；与任务写入同一事务。
- task_checks：独立检查 ID、任务 ID、本地日期、UTC 时间、备注及结构化结果。尚未开放不完成任务，只抑制本日 Waiting 检查；确认开放切到 Ready，不能设置 CompletedAt。暂停/普通待办不能调用等待检查路径。
- reminder_progress：每任务一行，保存当前追加/延期计划与进度 Version，UTC 时间带偏移。写进度核对 Version，投递同时核对任务 Revision。
- reminder_days：主键为任务/本地日期，保存 ExtraCount 和 CoveredAt。预算与覆盖时刻不倒退；新日期从 0 开始，回到旧日期恢复旧值。快照使用所请求日期的覆盖时刻，必要时从该日锚点恢复追加，避免未来日期压住校正后的正常提醒。
- reminder_deliveries：稳定投递 ID、任务修订、本地时刻、类型/代数、计划/排队 UTC 和状态。Fixed 使用本地身份，Repeat/Snooze 另包含 UTC 实例。Pending=0、Shown=1、Dismissed=2、Canceled=3。同一任务新投递取代旧活动项，业务处理取消或更新活动修订，保持当前卡片有效。

不做永久删除，记录通过外键关联。使用参数化 SQL，连接关闭时不留全局连接池。每次写入按原 Revision 条件更新，旧表单冲突不覆盖；任务、事件和检查任何一项失败，整个事务回滚。锁定默认最多等待约 1 秒再报告，之后允许重试。

ReminderSnapshot 在一个读事务内取任务、当日检查、当日预算、进度和投递。投递快照仅取当前日期前后八天的身份加全部未结束项，带日期/状态索引，不每秒加载全部历史；最近有效固定时刻最多在前七天。初始化未成功时禁止快照和写入。明确操作推进业务修订并写事件；排队投递与计划/预算在同一事务。准备投递后才显示，崩溃恢复未结束记录；关闭先持久收起再关窗，不把“准备投递”当成用户已阅读。

## 错误与验证

表单错误就地显示；保存失败保留表单。加载失败时显式提示并禁止写入，保留已展示数据但禁用操作，用户可重新读取。错误提示不直接暴露 SQL 或个人绝对路径。SQLite quick_check 在打开已识别库时执行；它不等同于完整备份恢复机制。

真实文件检查包含独立进程投递/延期/预算恢复、事务故障、只读/占用、schema-1/2/3 升级和回滚、未来/损坏/无关库。WPF 覆盖恢复合并、持久收起、预算、日期校正与失败显示。登录启动由注册表适配器独立管理，不在 SQLite 保存重复开关值；测试用非 Run 的临时隔离键。L5 历史与备份 UI 已实现，契约见 BACKUP_FORMAT.md。schema 5 新增 app_metadata 的 revision_floor，在导入时防止同 ID 被移除又恢复后旧修订重新生效。
