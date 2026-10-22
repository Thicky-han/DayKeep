using System.IO;
using System.Text.Json;

namespace DayKeep.Infrastructure;

public sealed record ShellSettings(double SurfaceOpacity = 0.88, bool Compact = false, bool Pinned = false);

public sealed class SettingsStore
{
    public string FilePath { get; }
    public string? LastError { get; private set; }

    public SettingsStore(string? filePath = null) => FilePath = filePath ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DayKeep", "ui-settings.json");

    public ShellSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new();
            var settings = JsonSerializer.Deserialize<ShellSettings>(File.ReadAllText(FilePath)) ?? new();
            return settings with { SurfaceOpacity = double.IsFinite(settings.SurfaceOpacity) ? Math.Clamp(settings.SurfaceOpacity, 0.65, 0.98) : 0.88 };
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            LastError = "界面设置无法读取，已使用默认外观；原文件已保留。";
            return new();
        }
    }

    public void Save(ShellSettings settings)
    {
        var temporary = FilePath + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings));
            File.Move(temporary, FilePath, true);
            LastError = null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            LastError = "界面设置未能保存，本次调整仍然有效。";
        }
    }
}
