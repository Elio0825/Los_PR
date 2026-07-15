namespace LosPr.BLM.Data;

internal sealed class BlackMageSettingsStore : IDisposable
{
    private const string Author = "Los";
    private const string SettingsFileName = "LosSettings.json";
    private static readonly TimeSpan SaveDebounce = TimeSpan.FromMilliseconds(500);
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        WriteIndented = true,
    };

    private readonly object _syncRoot = new();
    private DateTime _saveAfterUtc;
    private bool _dirty;
    private bool _disposed;

    public BlackMageSettingsStore()
    {
        FilePath = Path.Combine(ResolveSettingsDirectory(), SettingsFileName);
        Settings = Load();
    }

    public BlackMageSettings Settings { get; }

    public string FilePath { get; }

    public void Update(Action<BlackMageSettings> update)
    {
        ArgumentNullException.ThrowIfNull(update);

        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            update(Settings);
            MarkDirtyCore();
        }
    }

    public void MarkDirty()
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            MarkDirtyCore();
        }
    }

    public void RequestSave() => MarkDirty();

    public void FlushIfDue()
    {
        lock (_syncRoot)
        {
            if (_disposed || !_dirty || DateTime.UtcNow < _saveAfterUtc)
            {
                return;
            }

            SaveCore();
        }
    }

    public void SaveNow()
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            SaveCore();
        }
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            if (_dirty || !File.Exists(FilePath))
            {
                SaveCore();
            }

            _disposed = true;
        }
    }

    private BlackMageSettings Load()
    {
        if (!File.Exists(FilePath))
        {
            var defaults = new BlackMageSettings();
            defaults.Normalize();
            return defaults;
        }

        try
        {
            var json = File.ReadAllText(FilePath, Encoding.UTF8);
            var settings = JsonSerializer.Deserialize<BlackMageSettings>(json, JsonOptions)
                ?? new BlackMageSettings();
            settings.Normalize();
            return settings;
        }
        catch (Exception exception)
        {
            Svc.Log.Error(exception, $"[Los] 读取配置失败，将使用默认设置：{FilePath}");
            var defaults = new BlackMageSettings();
            defaults.Normalize();
            return defaults;
        }
    }

    private static string ResolveSettingsDirectory()
    {
        // 新版 PR 等价于直接调用 ACRAuthorSetting.GetSettingsDirectory("Los")。
        const string settingTypeName = "PromeRotation.Config.ACRAuthorSetting";
        try
        {
            var settingType = typeof(IRotation).Assembly.GetType(settingTypeName, throwOnError: false);
            var getDirectory = settingType?.GetMethod(
                "GetSettingsDirectory",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                binder: null,
                types: [typeof(string)],
                modifiers: null);

            if (getDirectory?.Invoke(null, [Author]) is string directory
                && !string.IsNullOrWhiteSpace(directory))
            {
                return directory;
            }
        }
        catch (Exception exception)
        {
            Svc.Log.Warning(exception, "[Los] 调用 ACRAuthorSetting 失败，将使用兼容配置目录。");
        }

        // PR 1.5.2.x 尚未公开 ACRAuthorSetting；目录结构与新 API 保持一致。
        var compatibilityDirectory = Path.Combine(
            Svc.PluginInterface.ConfigDirectory.FullName,
            "Settings",
            "ACRConfig",
            Author);
        Directory.CreateDirectory(compatibilityDirectory);
        Svc.Log.Warning("[Los] 当前 PR 未公开 ACRAuthorSetting，使用兼容配置目录。");
        return compatibilityDirectory;
    }

    private void MarkDirtyCore()
    {
        Settings.Normalize();
        _dirty = true;
        _saveAfterUtc = DateTime.UtcNow + SaveDebounce;
    }

    private void SaveCore()
    {
        Settings.Normalize();

        var temporaryPath = $"{FilePath}.{Environment.ProcessId}.tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var json = JsonSerializer.Serialize(Settings, JsonOptions);
            File.WriteAllText(temporaryPath, json, Utf8WithoutBom);

            if (File.Exists(FilePath))
            {
                File.Replace(temporaryPath, FilePath, null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temporaryPath, FilePath);
            }

            _dirty = false;
        }
        catch (Exception exception)
        {
            _dirty = true;
            _saveAfterUtc = DateTime.UtcNow + SaveDebounce;
            Svc.Log.Error(exception, $"[Los] 保存配置失败：{FilePath}");

            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch (Exception cleanupException)
            {
                Svc.Log.Warning(cleanupException, $"[Los] 清理配置临时文件失败：{temporaryPath}");
            }
        }
    }
}
