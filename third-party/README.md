# SQLite 依赖许可与来源

当前锁定 Microsoft.Data.Sqlite / Microsoft.Data.Sqlite.Core 10.0.13（MIT），SQLitePCLRaw.bundle_e_sqlite3 / core / provider.e_sqlite3 / lib.e_sqlite3 2.1.13（NuGet 元数据声明 Apache-2.0）。实际本机原生 SQLite 版本为 3.53.3。

MIT 全文来自 [EF Core v10.0.13](https://github.com/dotnet/efcore/blob/v10.0.13/LICENSE.txt)。SQLitePCLRaw 标准 Apache 2.0 全文与上游总 NOTICE 来自 [v2.1.12](https://github.com/ericsink/SQLitePCL.raw/tree/v2.1.12)，与 2.1.13 包声明的许可一致；上游未列出 v2.1.13 Git 标签。只统一文本换行与行尾空格，不改许可条款。SourceGear 归属保留在本说明与上游 NOTICE 中；包版权为 Copyright 2014-2024 SourceGear, LLC。

上游 NOTICE 同时说明其其他可选变体。DayKeep 当前使用普通 e_sqlite3，不启用 SQLCipher 或 OpenSSL；SQLite 本体的公共领域声明一并保留。所有第三方材料保留原许可，不因 DayKeep 原创代码采用 AGPL 而变更。

包从 NuGet 官方源还原；项目 packages.lock.json 记录准确版本与内容哈希。构建默认以锁定模式还原。升级需要维护者审查后显式使用 RestoreLockedMode=false / force-evaluate，检查新锁文件、许可、原生版本与验证结果。
