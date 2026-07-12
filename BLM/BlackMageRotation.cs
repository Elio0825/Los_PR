namespace LosPr.BLM;

[RotationMetadata(25u, "Los 黑魔智能循环", "Los", "0.1.0")]
public sealed class BlackMageRotation : IRotation, IRotationMeta, IDisposable
{
    public static IReadOnlyDictionary<string, bool> QtList { get; } =
        new Dictionary<string, bool>
        {
            ["AOE"] = true,
            ["智能AOE"] = false,
            ["Dot"] = true,
            ["TTK"] = false,
            ["移动通晓"] = true,
            ["移动三连"] = true,
            ["压缩火悖论"] = true,
            ["即刻进冰"] = true,
            ["三连进冰"] = true,
            ["黑魔纹"] = true,
            ["详述"] = true,
            ["实验_B4星灵绝望"] = false,
        };

    public static IReadOnlyDictionary<string, Type> Openers { get; } =
        new Dictionary<string, Type>();

    private readonly BlackMageSettingsStore _settingsStore;
    private readonly BlmDebugTraceService _debugTrace;
    private readonly BlmCoordinator _coordinator;
    private readonly BlmFollowUpCoordinator _followUp;
    private readonly BlmStateTracker _tracker;
    private readonly BlmActionDispatcher _dispatcher;
    private readonly LosPr.BLM.UI.BlmConsoleWindow _consoleWindow;
    private readonly BlackMageEventHandler _eventHandler;
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
        _coordinator = new BlmCoordinator(clock, normalizer);
        _followUp = new BlmFollowUpCoordinator(clock);
        _tracker = new BlmStateTracker(
            _coordinator,
            BlmContext.Capture(clock),
            clock,
            normalizer,
            _followUp);
        _dispatcher = new BlmActionDispatcher(
            _coordinator,
            _followUp,
            normalizer: normalizer,
            debugSink: _debugTrace);
        _consoleWindow = new LosPr.BLM.UI.BlmConsoleWindow(
            _settingsStore,
            _tracker.GetContextSnapshot,
            _debugTrace)
        {
            IsOpen = _settingsStore.Settings.ConsoleOpen,
        };
        _eventHandler = new BlackMageEventHandler(
            _tracker,
            _dispatcher,
            clock,
            _debugTrace);

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
        if (PromeRotation.Updaters.ActionUpdater.HasActiveCommand())
        {
            return null;
        }

        return _dispatcher.ResolveAlways(
            _tracker.GetContextSnapshot(),
            BuildDecisionPolicy());
    }

    public PAction? NextGcd()
        => _dispatcher.ResolveGcd(
            _tracker.GetContextSnapshot(),
            BuildDecisionPolicy());

    public PAction? NextOffGcd()
        => _dispatcher.ResolveOffGcd(
            _tracker.GetContextSnapshot(),
            BuildDecisionPolicy());

    public IOpener? GetOpener() => null;

    public IRotationEventHandler GetEventHandler() => _eventHandler;

    public void UpdateDebugStatus()
        => SynchronizeQtStates();

    public void DrawQTs()
    {
    }

    public void DrawSettings()
    {
        ImGui.TextUnformatted("Los 黑魔独立控制台");
        ImGui.TextDisabled("当前阶段：100级标准单体循环与 Transition 已接入");

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

        _settingsStore.Update(settings => settings.ConsoleOpen = _consoleWindow.IsOpen);
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

    private static BlmDecisionPolicy BuildDecisionPolicy()
    {
        var highPriorityQueueActive = false;
        try
        {
            highPriorityQueueActive = ActionQueueManager.HasHighPriorityAction();
        }
        catch
        {
        }

        return BlmDecisionPolicy.Default with
        {
            HighPriorityQueueActive = highPriorityQueueActive,
        };
    }
}
