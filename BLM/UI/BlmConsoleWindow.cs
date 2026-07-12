using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using LosPr.BLM.Core;
using LosPr.BLM.Data;
using LosPr.BLM.Diagnostics;
using LosPr.BLM.UI.Components;
using LosPr.BLM.UI.Layout;
using LosPr.BLM.UI.Navigation;
using LosPr.BLM.UI.Panels;
using LosPr.BLM.UI.Theme;
using PromeRotation.Data;

namespace LosPr.BLM.UI;

public sealed class BlmConsoleWindow
{
    private const string WindowTitle = "Los 黑魔控制台###LosPr_BlmConsole";

    private readonly BlackMageSettingsStore _store;
    private readonly Func<BlmContext> _contextProvider;
    private readonly IBlmDebugViewSource _debugSource;
    private readonly BlmNavigationState _navigation;
    private bool _initialPlacementApplied;
    private Vector2 _lastObservedPosition = new(float.NaN, float.NaN);
    private Vector2 _lastObservedSize = new(float.NaN, float.NaN);

    public BlmConsoleWindow(
        BlackMageSettingsStore store,
        Func<BlmContext> contextProvider,
        IBlmDebugViewSource? debugSource = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _contextProvider = contextProvider ?? throw new ArgumentNullException(nameof(contextProvider));
        _debugSource = debugSource ?? NullBlmDebugViewSource.Instance;
        _navigation = new BlmNavigationState(store.Settings.ActiveTab);
        IsOpen = store.Settings.ConsoleOpen;
    }

    public bool IsOpen { get; set; }

    public void Draw()
    {
        _store.FlushIfDue();
        if (!IsOpen)
            return;

        var settings = _store.Settings;
        var scale = LosMetrics.NormalizeScale(settings.UiScale);
        ApplyInitialPlacement(settings);
        ImGui.SetNextWindowSizeConstraints(LosMetrics.MinWindowSize, new Vector2(float.MaxValue));

        LosTheme.PushWindowStyle(scale, settings.ReduceMotion, settings.WindowOpacity);
        try
        {
            var open = IsOpen;
            var visible = ImGui.Begin(
                WindowTitle,
                ref open,
                ImGuiWindowFlags.NoCollapse
                | ImGuiWindowFlags.NoScrollbar
                | ImGuiWindowFlags.NoScrollWithMouse);
            try
            {
                if (open != IsOpen)
                {
                    IsOpen = open;
                    _store.Update(value => value.ConsoleOpen = open);
                    if (!open)
                        _store.SaveNow();
                }

                if (!visible)
                    return;

                CaptureWindowGeometry(settings);
                var snapshot = ReadSnapshot();
                DrawHeader(snapshot, scale, settings.ReduceMotion);
                DrawTabs(scale, settings.ReduceMotion);
                DrawContent(snapshot, scale, settings.ReduceMotion);
            }
            finally
            {
                ImGui.End();
            }
        }
        finally
        {
            LosTheme.PopWindowStyle();
        }

        _store.FlushIfDue();
    }

    private static void DrawHeader(BlmUiSnapshot snapshot, float scale, bool reduceMotion)
    {
        string status;
        LosStatusTone tone;
        if (!snapshot.IsAvailable)
        {
            status = "等待状态";
            tone = LosStatusTone.Warning;
        }
        else if (snapshot.AcrState == AcrState.Off)
        {
            status = "ACR Off";
            tone = LosStatusTone.Neutral;
        }
        else if (snapshot.AcrState == AcrState.Hold)
        {
            status = "ACR Hold";
            tone = LosStatusTone.Warning;
        }
        else if (snapshot.InCombat)
        {
            status = "运行中";
            tone = LosStatusTone.Success;
        }
        else
        {
            status = "待命";
            tone = LosStatusTone.Info;
        }

        LosHeader.Draw(
            "blm_console_header",
            "LOS BLACK MAGE",
            "PromeRotation 独立战斗控制台",
            status,
            tone,
            "7.5",
            scale,
            reduceMotion);
    }

    private void DrawTabs(float scale, bool reduceMotion)
    {
        var selected = _navigation.SelectedIndex;
        if (!LosTabBar.Draw(
                "blm_console_tabs",
                BlmConsoleTabInfo.Labels,
                ref selected,
                scale,
                reduceMotion))
            return;

        if (_navigation.Select(selected))
            _store.Update(value => value.ActiveTab = selected);
    }

    private void DrawContent(BlmUiSnapshot snapshot, float scale, bool reduceMotion)
    {
        LosTheme.PushContentStyle(scale);
        try
        {
            var began = ImGui.BeginChild(
                "##blm_console_content",
                Vector2.Zero,
                false,
                ImGuiWindowFlags.AlwaysVerticalScrollbar);
            try
            {
                if (!began)
                    return;

                switch (_navigation.CurrentTab)
                {
                    case BlmConsoleTab.Overview:
                        BlmOverviewPanel.Draw(snapshot, scale, reduceMotion);
                        break;
                    case BlmConsoleTab.Combat:
                        BlmCombatPanel.Draw(_store, scale, reduceMotion);
                        break;
                    case BlmConsoleTab.Control:
                        BlmControlPanel.Draw(_store, scale, reduceMotion);
                        break;
                    case BlmConsoleTab.System:
                        BlmSystemPanel.Draw(
                            _store,
                            snapshot,
                            ReadDebugSnapshot(),
                            scale,
                            reduceMotion,
                            ResetInterface,
                            OpenLogDirectory,
                            _debugSource.ClearView);
                        break;
                }
            }
            finally
            {
                ImGui.EndChild();
            }
        }
        finally
        {
            LosTheme.PopContentStyle();
        }
    }

    private BlmUiSnapshot ReadSnapshot()
    {
        try
        {
            var context = _contextProvider() ?? BlmContext.Unavailable;
            return BlmUiSnapshot.FromContext(context);
        }
        catch (Exception exception)
        {
            return BlmUiSnapshot.FromContext(BlmContext.Unavailable with
            {
                AvailabilityText = "事实快照暂不可用",
                CaptureError = $"{exception.GetType().Name}: {exception.Message}",
            });
        }
    }

    private BlmDebugSnapshot ReadDebugSnapshot()
    {
        try
        {
            return _debugSource.GetSnapshot();
        }
        catch (Exception exception)
        {
            return new BlmDebugSnapshot
            {
                CapturedAtUtc = DateTimeOffset.UtcNow,
                WriterHealthy = false,
                LastError = $"读取 Debug 快照失败: {exception.GetType().Name}",
            };
        }
    }

    private void OpenLogDirectory()
        => _debugSource.TryOpenLogDirectory();

    private void ApplyInitialPlacement(BlackMageSettings settings)
    {
        if (_initialPlacementApplied)
            return;

        var size = settings.RememberWindow
            ? new Vector2(settings.WindowWidth, settings.WindowHeight)
            : LosMetrics.DefaultWindowSize;
        size = Vector2.Max(size, LosMetrics.MinWindowSize);
        ImGui.SetNextWindowSize(size, ImGuiCond.Always);

        Vector2 position;
        if (settings.RememberWindow
            && settings.WindowX != -1f
            && settings.WindowY != -1f
            && float.IsFinite(settings.WindowX)
            && float.IsFinite(settings.WindowY))
        {
            position = new Vector2(settings.WindowX, settings.WindowY);
        }
        else
        {
            var viewport = ImGui.GetMainViewport();
            position = viewport.Pos + (viewport.Size - size) * 0.5f;
        }

        ImGui.SetNextWindowPos(position, ImGuiCond.Always);
        _lastObservedPosition = position;
        _lastObservedSize = size;
        _initialPlacementApplied = true;
    }

    private void CaptureWindowGeometry(BlackMageSettings settings)
    {
        if (!settings.RememberWindow)
        {
            _lastObservedPosition = new Vector2(float.NaN, float.NaN);
            _lastObservedSize = new Vector2(float.NaN, float.NaN);
            return;
        }

        var position = ImGui.GetWindowPos();
        var size = ImGui.GetWindowSize();
        if (!IsFinite(position) || !IsFinite(size))
            return;

        if (NearlyEqual(position, _lastObservedPosition)
            && NearlyEqual(size, _lastObservedSize))
            return;

        _lastObservedPosition = position;
        _lastObservedSize = size;
        _store.Update(value =>
        {
            value.WindowX = position.X;
            value.WindowY = position.Y;
            value.WindowWidth = size.X;
            value.WindowHeight = size.Y;
        });
    }

    private void ResetInterface()
    {
        _store.Update(settings =>
        {
            settings.RememberWindow = true;
            settings.WindowX = -1f;
            settings.WindowY = -1f;
            settings.WindowWidth = LosMetrics.DefaultWindowSize.X;
            settings.WindowHeight = LosMetrics.DefaultWindowSize.Y;
            settings.UiScale = 1f;
            settings.WindowOpacity = 0.96f;
            settings.ReduceMotion = false;
            settings.ActiveTab = (int)BlmConsoleTab.Overview;
        });

        _navigation.Select((int)BlmConsoleTab.Overview);
        _initialPlacementApplied = false;
        _lastObservedPosition = new Vector2(float.NaN, float.NaN);
        _lastObservedSize = new Vector2(float.NaN, float.NaN);
    }

    private static bool IsFinite(Vector2 value)
        => float.IsFinite(value.X) && float.IsFinite(value.Y);

    private static bool NearlyEqual(Vector2 left, Vector2 right)
        => IsFinite(left)
           && IsFinite(right)
           && MathF.Abs(left.X - right.X) < 0.5f
           && MathF.Abs(left.Y - right.Y) < 0.5f;
}
