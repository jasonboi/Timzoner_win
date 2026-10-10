using System.Text.Json;

namespace Timzoner.Core;

public sealed class JsonSettingsStore(string path) : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    public LoadedSettings Load(string currentId)
    {
        var defaults = new Settings { FavoriteTimeZoneIds = [currentId] };
        if (!File.Exists(path))
        {
            try { Save(defaults); return new(defaults); }
            catch (Exception ex) when (IsFileError(ex)) { return new(defaults, AppText.Get("无法创建配置，修改后请重试保存。", "Unable to create the settings file. Try saving again after editing. ") + ex.Message); }
        }
        try { return Read(path); }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            try
            {
                File.Copy(path, path + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"), false);
                if (File.Exists(path + ".bak"))
                {
                    try
                    {
                        var backup = Read(path + ".bak");
                        if (backup.ReadOnly) return new(defaults, AppText.Get("备份来自较新版本，配置已保护。请升级 Timzoner。", "The backup was created by a newer version. Your settings have been protected. Update Timzoner."), true);
                        // Keep the last good backup intact while replacing the corrupt primary.
                        WriteAtomic(backup.Value, false);
                        return backup with
                        {
                            Notice = backup.Value.Language == "en"
                                ? "The settings file was damaged. The original file was kept and settings were restored from a backup."
                                : "配置损坏，已保留原文件并从备份恢复。"
                        };
                    }
                    catch (Exception invalidBackup) when (invalidBackup is JsonException or InvalidDataException)
                    {
                        File.Copy(path + ".bak", path + ".bak.corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"), false);
                    }
                }
                WriteAtomic(defaults, false);
                return new(defaults, AppText.Get("配置损坏，已保留原文件并恢复默认设置。", "The settings file was damaged. The original file was kept and default settings were restored."));
            }
            catch (Exception recovery) when (IsFileError(recovery) || recovery is JsonException)
            {
                return new(defaults, AppText.Get("配置无法恢复，原文件已保留。请备份配置后重试。", "Unable to restore your settings. The original file was kept. Back up the settings file and try again. ") + recovery.Message, true);
            }
        }
        catch (Exception ex) when (IsFileError(ex)) { return new(defaults, AppText.Get("配置无法读取，已禁止覆盖。", "Unable to read your settings. Saving has been disabled to protect them. ") + ex.Message, true); }
    }

    private static LoadedSettings Read(string source)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(source));
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException(AppText.Get("配置必须为对象。", "The settings must be a JSON object."));
        if (!document.RootElement.TryGetProperty("schemaVersion", out var version) || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number))
            throw new InvalidDataException(AppText.Get("配置缺少版本。", "The settings file has no valid schema version."));
        if (number > 1) return new(new Settings(), AppText.Get("配置来自较新版本，当前仅可切换时区，保存已禁用。请升级 Timzoner。", "These settings were created by a newer version. You can change time zones, but saving is disabled. Update Timzoner."), true);
        if (number != 1) throw new InvalidDataException(AppText.Get("配置版本无效。", "The settings schema version is invalid."));
        var value = JsonSerializer.Deserialize<Settings>(document.RootElement, Options) ?? throw new InvalidDataException(AppText.Get("配置为空。", "The settings are empty."));
        if (value.FavoriteTimeZoneIds is null || value.FavoriteTimeZoneIds.Any(string.IsNullOrWhiteSpace) ||
            value.Theme is not ("system" or "light" or "dark") || value.Language is not ("zh-CN" or "en"))
            throw new InvalidDataException(AppText.Get("配置字段无效。", "A settings field is invalid."));
        return new(value with { FavoriteTimeZoneIds = value.FavoriteTimeZoneIds.Distinct(StringComparer.OrdinalIgnoreCase).ToArray() });
    }

    public void Save(Settings settings) => WriteAtomic(settings, true);

    private void WriteAtomic(Settings settings, bool backup)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, ".settings-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, settings, Options);
                stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temporary, path, backup ? path + ".bak" : null);
            else File.Move(temporary, path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static bool IsFileError(Exception ex) => ex is IOException or UnauthorizedAccessException or System.Security.SecurityException;
}
