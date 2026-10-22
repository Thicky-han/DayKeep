using System.IO;
using Microsoft.Win32;

namespace DayKeep.Infrastructure;

public sealed record LoginStartupState(bool Enabled, string Detail);
public interface ILoginStartup { LoginStartupState Read(); LoginStartupState SetEnabled(bool enabled); }
public interface IStartupValue { string? Read(); void Write(string? command); }
public sealed class RegistryStartupValue(string subkey = @"Software\Microsoft\Windows\CurrentVersion\Run", string name = "DayKeep") : IStartupValue
{
    public string? Read()
    {
        using var key=Registry.CurrentUser.OpenSubKey(subkey); var value=key?.GetValue(name);
        return value is null ? null : value as string ?? throw new IOException("启动项类型无法识别，原值已保留。");
    }
    public void Write(string? command)
    {
        using var key=Registry.CurrentUser.CreateSubKey(subkey,true);
        if(command is null) key.DeleteValue(name,false); else key.SetValue(name,command,RegistryValueKind.String);
    }
}
public sealed class LoginStartup(IStartupValue store, string executable) : ILoginStartup
{
    public static LoginStartup ForCurrentProcess() => new(new RegistryStartupValue(),Environment.ProcessPath ?? "");
    private string Command => $"\"{executable}\" --background";
    public LoginStartupState Read()
    {
        var command=store.Read();
        return new(IsOwned(command), command is null ? "登录启动已关闭；需要时可手动打开日守。" : IsOwned(command)
            ? "已配置登录后在托盘运行。Windows 也可在任务管理器中管理启动项。" : "同名启动项指向其他程序，已保留；请检查 Windows 启动项。");
    }
    public LoginStartupState SetEnabled(bool enabled)
    {
        var current=store.Read();
        if(current is not null && !IsOwned(current)) throw new IOException("已有启动项无法识别，已保留。");
        if(enabled && (!Path.IsPathFullyQualified(executable) || !Path.GetFileName(executable).Equals("DayKeep.exe",StringComparison.OrdinalIgnoreCase) || executable.Contains('"') || !File.Exists(executable)))
            throw new IOException("请使用 DayKeep.exe 成品设置登录启动。");
        try
        {
            store.Write(enabled ? Command : null);
            var state=Read(); if(state.Enabled!=enabled) throw new IOException("启动项写入后核对失败。"); return state;
        }
        catch(Exception error) when(error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            try { store.Write(current); }
            catch(Exception restore) when(restore is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            { throw new IOException("登录启动未能核对或还原，请检查 Windows 启动项。",new AggregateException(error,restore)); }
            throw new IOException("登录启动未能修改，已还原原值。",error);
        }
    }
    private static bool IsOwned(string? command)
    {
        if(command is null || command.Contains('\0') || !command.StartsWith('"') || !command.EndsWith("\" --background",StringComparison.Ordinal)) return false;
        var path=command[1..^14];
        return Path.IsPathFullyQualified(path) && Path.GetFileName(path).Equals("DayKeep.exe",StringComparison.OrdinalIgnoreCase) && !path.Contains('"');
    }
}
public sealed class DisabledLoginStartup : ILoginStartup
{
    public LoginStartupState Read() => new(false,"隔离测试或预览不修改 Windows 登录启动。");
    public LoginStartupState SetEnabled(bool enabled) => throw new IOException("隔离测试未启用系统启动项。");
}
