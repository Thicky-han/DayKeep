# L1 / L2 任务与存储契约

## 分层

DayKeep.Core 为 net10.0 库，包含不可变任务模型、输入验证、状态转移、TaskService、SQLite Repository 和纯 ReminderPlanner；不依赖 WPF、窗口、系统托盘或定时器。DayKeep.Desktop 的 ReminderController 通过可替换时钟调用核心，使用真实 WPF 计时器和窗口，不从 UI 假定已保存。

## 任务

TaskKind：Todo（普通待办）、WaitingForCondition（等待条件）。TaskState：Waiting、Ready、Completed。暂停为独立 Paused；归档为独立 ArchivedAt，UI 显示“已归档”但保留原业务状态。任务具有 Guid、Revision、创建/更新时间、链接、备注、可未知截止日期及 ReminderRule。

完成时保存 BeforeCompletion 和 PausedBeforeCompletion，清除活动暂停标记；撤销完成恢复原值并清空 CompletedAt。归档/恢复不改变业务状态。完成或归档后禁止编辑/暂停，需先撤销或恢复。改任务类型时将未完成任务切到该类型的初始状态，同类型编辑不重置业务状态。

ReminderRule：规范化 HH:mm 字符串（最多八个不同时间）、星期位掩码、未来追加间隔 30 分钟与追加上限 3。位 0 为周一，位 6 为周日；127 每天，31 工作日。L2 已使用固定时间/星期计算到期与下次时间；追加参数和 UI 留到 L3。

## 数据库 schema 2

固定文件名 tasks.db；PRAGMA application_id=1145786417，user_version=2。L1 的 schema 1 通过事务 ALTER 增加 task_checks.outcome；已有检查默认 StillWaiting=0，新确认开放 ConditionReady=1。保留任务、事件、检查与所有完成快照。未知/更新/损坏库不清空；升级失败保留版本和文件。旧 L1 会拒绝读取 schema 2。

- tasks：任务字段、修订号、完成撤销快照与归档时间；约束业务枚举、暂停、星期、次数、完成字段一致性。
- task_events：任务 ID、操作类型、UTC 时间；与任务写入同一事务。
- task_checks：独立检查 ID、任务 ID、本地日期、UTC 时间、备注及结构化结果。尚未开放不完成任务，只抑制本日 Waiting 检查；确认开放切到 Ready，不能设置 CompletedAt。暂停/普通待办不能调用等待检查路径。

不做永久删除，记录通过外键关联。使用参数化 SQL，连接关闭时不留全局连接池。每次写入按原 Revision 条件更新，旧表单冲突不覆盖；任务、事件和检查任何一项失败，整个事务回滚。锁定默认最多等待约 1 秒再报告，之后允许重试。

ReminderSnapshot 在单个读事务内取任务和本地日期检查，防止拼接两个不同状态。初始化未成功时 TaskService 拒绝提醒快照和写入。ReminderKey=(任务 ID,本地日期,固定时间) 仅在内存做当前进程去重；当前没有持久提醒账本，L4 再处理。

## 错误与验证

表单错误就地显示；保存失败保留表单。加载失败时显式提示并禁止写入，保留已展示数据但禁用操作，用户可重新读取。错误提示不直接暴露 SQL 或个人绝对路径。SQLite quick_check 在打开已识别库时执行；它不等同于完整备份恢复机制。

真实文件检查包含独立进程、事务故障、只读/占用、schema-1 升级和失败回滚、未来/损坏/无关库。WPF 检查覆盖实际提醒窗口、每天检查/确认开放、旧修订和失败显示。历史浏览与备份 UI 属于 L5，跨重启完整去重/补偿和时钟变化属于 L4。
