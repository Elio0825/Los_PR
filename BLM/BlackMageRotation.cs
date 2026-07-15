using LosPr.BLM.Openers;
using LosPr.BLM.Resolvers;
using LosPr.BLM.Resolvers.Production;
using LosPr.BLM.Timeline;
using PromeRotation.Timeline.Core;

namespace LosPr.BLM;

[RotationMetadata(
    25u,
    "Los 黑魔ACR",
    "Los",
    "0.1.0",
    ContentScope = AcrContentScope.All)]
public sealed class BlackMageRotation : IRotation, IRotationMeta, IRotationLifecycle, IDisposable
{
    private const string LegacyNativeOpenerProbeQtName = "DEV起手探针";

    public static IReadOnlyDictionary<string, bool> QtList { get; } =
        new Dictionary<string, bool>
        {
            ["AOE"] = true,
            ["智能AOE"] = false,
            ["Dot"] = true,
            ["TTK"] = false,
            ["移动通晓"] = true,
            ["移动三连"] = true,
            ["即刻进冰"] = true,
            ["三连进冰"] = true,
            ["黑魔纹"] = true,
            ["详述"] = true,
            ["魔泉"] = true,
            ["倾泻资源"] = false,
            ["快速耀星"] = false,
        };

    internal static IReadOnlyDictionary<string, bool> DailyPreset { get; } =
        CreatePreset(highEnd: false);

    internal static IReadOnlyDictionary<string, bool> HighEndPreset { get; } =
        CreatePreset(highEnd: true);

    public static IReadOnlyDictionary<string, Type> Openers { get; } =
        new Dictionary<string, Type>
        {
            [BlmLevel70Opener.Name] = typeof(BlmLevel70Opener),
            [BlmLevel80Opener.Name] = typeof(BlmLevel80Opener),
            [BlmLevel90Opener.Name] = typeof(BlmLevel90Opener),
            [BlmLevel100Opener.Name] = typeof(BlmLevel100Opener),
            [BlmLevel100FlareOpener.Name] = typeof(BlmLevel100FlareOpener),
        };

    public static IJobNodeProvider NodeProvider { get; } = BlmTimelineNodeProvider.Instance;

    private readonly BlackMageSettingsStore _settingsStore;
    private readonly BlmDebugTraceService _debugTrace;
    private readonly BlmStateTracker _tracker;
    private readonly BlmResolverInputAdapter _resolverInputAdapter;
    private readonly BlmResolverExecutionService _execution;
    private readonly BlmOpenerExecutionService _openerExecution;
    private readonly BlmLevel70Opener _level70Opener;
    private readonly BlmLevel80Opener _level80Opener;
    private readonly BlmLevel90Opener _level90Opener;
    private readonly BlmLevel100Opener _level100Opener;
    private readonly BlmLevel100FlareOpener _level100FlareOpener;
    private readonly LosPr.BLM.UI.BlmConsoleWindow _consoleWindow;
    private readonly LosPr.BLM.UI.BlmQuickOverlay _quickOverlay;
    private readonly BlackMageEventHandler _eventHandler;
    private readonly Func<BlmContext> _timelineContextProvider;
    private bool _disposed;
    private DateTime _nextUiErrorLogUtc;

    public BlackMageRotation()
    {
        _settingsStore = new BlackMageSettingsStore();
        var settingsDirectory = Path.GetDirectoryName(_settingsStore.FilePath)
            ?? throw new InvalidOperationException("Los 配置目录不可用。");
        _debugTrace = new BlmDebugTraceService(
            Path.Combine(settingsDirectory, "DebugLogs"),
            () => _settingsStore.Settings.DecisionLogging);
        var missingStoredQt = _settingsStore.Settings.QtStates.Remove("压缩冰悖论");
        missingStoredQt |= _settingsStore.Settings.QtStates.Remove("实验_B4星灵绝望");
        missingStoredQt |= _settingsStore.Settings.QtStates.Remove("不打冰悖论");
        missingStoredQt |= _settingsStore.Settings.QtStates.Remove(LegacyNativeOpenerProbeQtName);
        missingStoredQt |= _settingsStore.Settings.QtStates.Remove("70–89级高难起手");
        missingStoredQt |= _settingsStore.Settings.QtStates.Remove("90–99级高难起手");
        missingStoredQt |= _settingsStore.Settings.QtStates.Remove("100级5+7起手");
        missingStoredQt |= _settingsStore.Settings.QtStates.Remove("100级核爆起手");
        missingStoredQt |= _settingsStore.Settings.QtStates.Remove("高难起手爆发药");
        if (_settingsStore.Settings.QtStates.Remove("压缩火悖论", out var compressedFireParadox))
        {
            _settingsStore.Settings.CompressFireParadoxEnabled = compressedFireParadox;
            missingStoredQt = true;
        }
        foreach (var (name, defaultValue) in QtList)
        {
            PromeSettings.Instance.AddQt(name, defaultValue);
            if (!_settingsStore.Settings.QtStates.TryGetValue(name, out var restoredValue))
            {
                restoredValue = defaultValue;
                missingStoredQt = true;
            }

            PromeSettings.Instance.SetQt(name, restoredValue);
        }

        if (missingStoredQt)
        {
            _settingsStore.Update(settings =>
            {
                foreach (var (name, defaultValue) in QtList)
                {
                    settings.QtStates.TryAdd(name, defaultValue);
                }
            });
        }

        var clock = SystemBlmClock.Instance;
        var normalizer = new PrBlmActionIdNormalizer();
        _tracker = new BlmStateTracker(
            BlmContext.Capture(clock),
            clock,
            normalizer);
        _resolverInputAdapter = new BlmResolverInputAdapter(
            settingsProvider: () => _settingsStore.Settings);
        _execution = new BlmResolverExecutionService(_tracker, _debugTrace);
        _openerExecution = new BlmOpenerExecutionService(
            _tracker,
            normalizer,
            clock,
            _debugTrace,
            policyProvider: ReadOpenerPolicy);
        LosPr.BLM.UI.BlmHotkeyCatalog.SetOpenerActiveProvider(
            () => _openerExecution.OwnsExecution);
        _level70Opener = new BlmLevel70Opener(
            _openerExecution,
            _tracker.GetContextSnapshot);
        _level80Opener = new BlmLevel80Opener(
            _openerExecution,
            _tracker.GetContextSnapshot);
        _level90Opener = new BlmLevel90Opener(
            _openerExecution,
            _tracker.GetContextSnapshot);
        _level100Opener = new BlmLevel100Opener(
            _openerExecution,
            _tracker.GetContextSnapshot);
        _level100FlareOpener = new BlmLevel100FlareOpener(
            _openerExecution,
            _tracker.GetContextSnapshot);
        _consoleWindow = new LosPr.BLM.UI.BlmConsoleWindow(
            _settingsStore,
            _tracker.GetContextSnapshot,
            _debugTrace)
        {
            IsOpen = _settingsStore.Settings.ConsoleOpen,
        };
        _quickOverlay = new LosPr.BLM.UI.BlmQuickOverlay(
            _settingsStore,
            QtList.Keys,
            () => _consoleWindow.IsOpen,
            ToggleConsole);
        _eventHandler = new BlackMageEventHandler(
            _tracker,
            _resolverInputAdapter,
            _execution,
            clock,
            _debugTrace,
            _openerExecution);
        _timelineContextProvider = _tracker.GetContextSnapshot;
        BlmTimelineRuntime.SetContextProvider(_timelineContextProvider);

        _debugTrace.Publish(new BlmDebugEventDraft
        {
            Kind = BlmDebugEventKind.Lifecycle,
            Context = _tracker.GetContextSnapshot(),
            MonotonicMs = clock.NowMs,
            EntryPoint = "Rotation.Start",
            Reason = "Los 黑魔循环已加载",
            Detail = "Debug 内存追踪已启动；文件写入由控制台开关决定。",
        });

        Svc.PluginInterface.UiBuilder.Draw += DrawConsole;
    }

    public PAction? NextAlways()
    {
        var context = _tracker.GetContextSnapshot();
        if (_openerExecution.OwnsExecution)
        {
            return _openerExecution.Resolve(BlmResolverChannel.Always, context);
        }

        return _execution.Resolve(
            BlmResolverChannel.Always,
            context,
            HasHighPriorityAction());
    }

    public PAction? NextGcd()
    {
        var context = _tracker.GetContextSnapshot();
        if (_openerExecution.OwnsExecution)
        {
            return _openerExecution.Resolve(BlmResolverChannel.Gcd, context);
        }

        return _execution.Resolve(
            BlmResolverChannel.Gcd,
            context,
            HasHighPriorityAction());
    }

    public PAction? NextOffGcd()
    {
        var context = _tracker.GetContextSnapshot();
        if (_openerExecution.OwnsExecution)
        {
            return _openerExecution.Resolve(BlmResolverChannel.OffGcd, context);
        }

        return _execution.Resolve(
            BlmResolverChannel.OffGcd,
            context,
            HasHighPriorityAction());
    }

    public IOpener? GetOpener()
    {
        if (PRCore.Me is not { } me)
        {
            return null;
        }

        return _settingsStore.Settings.OpenerSelection switch
        {
            BlmOpenerSelection.Level70 when me.Level is >= 70 and <= 79 => _level70Opener,
            BlmOpenerSelection.Level80 when me.Level is >= 80 and <= 89 => _level80Opener,
            BlmOpenerSelection.Level90 when me.Level is >= 90 and <= 99 => _level90Opener,
            BlmOpenerSelection.Standard57 when me.Level == 100 => _level100Opener,
            BlmOpenerSelection.Flare when me.Level == 100 => _level100FlareOpener,
            _ => null,
        };
    }

    public IRotationEventHandler GetEventHandler() => _eventHandler;

    public void UpdateDebugStatus()
        => SynchronizeQtStates();

    public void DrawQTs()
    {
        _quickOverlay.EnsureActive();
    }

    public void OnEnterAcr()
        => _quickOverlay.Activate();

    public void OnExitAcr()
        => _quickOverlay.Deactivate();

    public void DrawSettings()
    {
        ImGui.TextUnformatted("Los 黑魔独立控制台");
        ImGui.TextDisabled("起手、战斗、热键与 Debug 设置均在控制台内。");

        if (ImGui.Button("打开独立控制台", new Vector2(180f, 34f)))
        {
            _consoleWindow.IsOpen = true;
            _settingsStore.Update(settings => settings.ConsoleOpen = true);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        BlmTimelineRuntime.ClearContextProvider(_timelineContextProvider);
        LosPr.BLM.UI.BlmHotkeyCatalog.SetOpenerActiveProvider(null);
        LosPr.BLM.UI.BlmHotkeyCatalog.ClearPending();
        Svc.PluginInterface.UiBuilder.Draw -= DrawConsole;
        _eventHandler.Dispose();
        _debugTrace.Publish(new BlmDebugEventDraft
        {
            Kind = BlmDebugEventKind.Lifecycle,
            Context = _tracker.GetContextSnapshot(),
            EntryPoint = "Rotation.Dispose",
            Reason = "Los 黑魔循环正在卸载",
        });
        _tracker.DisposeState();
        _debugTrace.Dispose();
        _consoleWindow.Dispose();

        _settingsStore.Update(settings => settings.ConsoleOpen = _consoleWindow.IsOpen);
        _quickOverlay.Dispose();
        _settingsStore.Dispose();
    }

    private void DrawConsole()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _quickOverlay.Draw();
            _consoleWindow.Draw();

            if (_settingsStore.Settings.ConsoleOpen != _consoleWindow.IsOpen)
            {
                _settingsStore.Update(settings => settings.ConsoleOpen = _consoleWindow.IsOpen);
            }
        }
        catch (Exception exception)
        {
            var now = DateTime.UtcNow;
            if (now >= _nextUiErrorLogUtc)
            {
                _nextUiErrorLogUtc = now.AddSeconds(5);
                Svc.Log.Error(exception, "[Los] 绘制独立控制台失败。");
            }
        }
        finally
        {
            _settingsStore.FlushIfDue();
        }
    }

    private void ToggleConsole()
    {
        _consoleWindow.IsOpen = !_consoleWindow.IsOpen;
        _settingsStore.Update(settings => settings.ConsoleOpen = _consoleWindow.IsOpen);
    }

    private void SynchronizeQtStates()
    {
        Dictionary<string, bool>? changes = null;
        foreach (var name in QtList.Keys)
        {
            var currentValue = PromeSettings.Instance.GetQt(name);
            if (_settingsStore.Settings.QtStates.TryGetValue(name, out var storedValue)
                && storedValue == currentValue)
            {
                continue;
            }

            changes ??= new Dictionary<string, bool>(StringComparer.Ordinal);
            changes[name] = currentValue;
        }

        if (changes is null)
        {
            return;
        }

        _settingsStore.Update(settings =>
        {
            foreach (var (name, value) in changes)
            {
                settings.QtStates[name] = value;
            }
        });
    }

    private static bool HasHighPriorityAction()
    {
        var highPriorityQueueActive = false;
        try
        {
            highPriorityQueueActive = ActionQueueManager.HasHighPriorityAction();
        }
        catch
        {
        }

        return highPriorityQueueActive;
    }

    internal static IReadOnlyDictionary<string, bool> PresetFor(BlmConsoleMode mode)
        => mode == BlmConsoleMode.HighEnd ? HighEndPreset : DailyPreset;

    internal static void ApplyModeDefaults(BlackMageSettings settings, BlmConsoleMode mode)
    {
        settings.CombatMode = mode;
        settings.OpenerPotionEnabled = mode == BlmConsoleMode.HighEnd;
    }

    private BlmOpenerPolicy ReadOpenerPolicy()
    {
        var settings = _settingsStore.Settings;
        return new BlmOpenerPolicy(
            Enabled: settings.OpenerSelection == BlmOpenerSelection.Standard57,
            HighEndPotionEnabled: settings.OpenerPotionEnabled,
            Level70To89Enabled: settings.OpenerSelection is
                BlmOpenerSelection.Level70 or BlmOpenerSelection.Level80,
            Level100FlareEnabled: settings.OpenerSelection == BlmOpenerSelection.Flare,
            Level90To99Enabled: settings.OpenerSelection == BlmOpenerSelection.Level90,
            NoTriplecast: settings.OpenerNoTriplecast);
    }

    private static IReadOnlyDictionary<string, bool> CreatePreset(bool highEnd)
    {
        var values = new Dictionary<string, bool>(QtList, StringComparer.Ordinal)
        {
            ["TTK"] = false,
            ["黑魔纹"] = !highEnd,
            ["移动三连"] = !highEnd,
            ["三连进冰"] = !highEnd,
        };
        return values;
    }

}
