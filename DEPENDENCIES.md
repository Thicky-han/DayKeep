# 工具链与第三方边界

- SDK：10.0.402，global.json 固定，tools/sdk.lock.json 记录微软官方 win-x64 ZIP 和 SHA-512。
- 平台：.NET 10、WPF、Windows Forms NotifyIcon；直接使用官方 SDK/框架，无第三方 NuGet 应用包。框架组件按微软分发文件中的 LICENSE/ThirdPartyNotices 等条款提供，不改标为 AGPL 原创代码。
- GitHub Actions：checkout v6、setup-dotnet v5；工作流按官方仓库标签的完整提交 SHA 固定。L0 尚无云端执行记录。
- 普通预览采用自包含发布时保留框架许可材料；运行时版本以 publish 实际输出为准。
- SQLite 是 L1 计划依赖，目前未安装、未引用；接入前记录包版本、许可和锁定方式。
- 原创 UI、托盘日出绘制和文档按仓库 LICENSE；GNU 许可全文属于标准许可文本，原样使用。
- 官方技术依据：[WPF 窗口透明](https://learn.microsoft.com/en-us/dotnet/api/system.windows.window.allowstransparency)、[.NET 安装脚本](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-install-script)、[.NET 支持政策](https://dotnet.microsoft.com/en-us/platform/support/policy)。

开发依赖与缓存不进 Git。L6 检查自包含包内框架许可和对应源码获取方式，不以此文档替代实际包审查。
