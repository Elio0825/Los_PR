using LosPr.BLM.UI;
using PromeRotation.Timeline.Core;
using System.Linq;

namespace LosPr.BLM.Timeline;

internal enum BlmTimelineResourceType
{
    FireState,
    IceState,
    FireStacks,
    IceStacks,
    AstralSoulStacks,
    Firestarter,
    UmbralHearts,
    Paradox,
    PolyglotStacks,
}

internal enum BlmTimelineCompare
{
    Greater,
    GreaterOrEqual,
    Equal,
    LessOrEqual,
    Less,
    NotEqual,
}

internal static class BlmTimelineRuntime
{
    private static Func<BlmContext>? _contextProvider;

    public static void SetContextProvider(Func<BlmContext>? provider)
        => Volatile.Write(ref _contextProvider, provider);

    public static void ClearContextProvider(Func<BlmContext> provider)
        => Interlocked.CompareExchange(ref _contextProvider, null, provider);

    public static bool TryGetContext(out BlmContext context)
    {
        try
        {
            context = Volatile.Read(ref _contextProvider)?.Invoke() ?? BlmContext.Unavailable;
            return context.IsAvailable;
        }
        catch
        {
            context = BlmContext.Unavailable;
            return false;
        }
    }
}

internal sealed class BlmTimelineNodeProvider : IJobNodeProvider
{
    public static BlmTimelineNodeProvider Instance { get; } = new();

    private BlmTimelineNodeProvider()
    {
    }

    public void RegisterNodes(RotationNodeContext context)
    {
        ActionFactory.Register(
            context,
            BlmTimelineHotkeyAction.TypeKey,
            BlmTimelineHotkeyAction.FromDto);
        ConditionFactory.Register(
            context,
            BlmTimelineQtCondition.TypeKey,
            BlmTimelineQtCondition.FromDto);
        ConditionFactory.Register(
            context,
            BlmTimelineResourceCondition.TypeKey,
            BlmTimelineResourceCondition.FromDto);
    }

    public IReadOnlyList<(string DisplayName, string Description, Func<ICondition> Create)>
        GetConditionDescriptors()
        =>
        [
            (
                "黑魔/QT检测",
                "检测 Los 当前 QT 是否为指定状态。",
                () => new BlmTimelineQtCondition("AOE", true)),
            (
                "黑魔/资源",
                "检测火冰状态、层数、火苗、冰针、悖论或通晓资源。",
                () => new BlmTimelineResourceCondition(
                    BlmTimelineResourceType.FireState,
                    BlmTimelineCompare.GreaterOrEqual,
                    0,
                    true)),
        ];

    public IReadOnlyList<(string DisplayName, string Description, Func<PromeRotation.Timeline.Core.IAction> Create)>
        GetActionDescriptors()
        =>
        [
            (
                "黑魔/Hotkey",
                "调用 Los Hotkey，并沿用当前队列、起手保护和可用性检查。",
                () => new BlmTimelineHotkeyAction("limit_break")),
        ];
}

internal sealed class BlmTimelineHotkeyAction :
    PromeRotation.Timeline.Core.IAction,
    ISerializableAction,
    IJobNodeDescriptor
{
    public const string TypeKey = "Los.BLM.Hotkey";
    private const string KeyParam = "key";

    private string _key;

    public BlmTimelineHotkeyAction(string key)
    {
        _key = NormalizeKey(key);
    }

    public string NodeDisplayName => $"黑魔/Hotkey-{DisplayHotkeyName(_key)}";

    public NodeParamInfo[] Params =>
    [
        new(
            KeyParam,
            "使用 Hotkey",
            "执行时调用当前 Los Hotkey 逻辑。",
            "enum",
            BlmHotkeyCatalog.Entries
                .Select(entry => (entry.Key, entry.Name))
                .ToArray()),
    ];

    public void Execute()
    {
        if (!BlmHotkeyCatalog.TryActivate(_key))
            Svc.Log.Warning($"[Los Timeline] Hotkey 当前不可执行：{_key}");
    }

    public ActionDto ToDto()
        => new()
        {
            Type = TypeKey,
            Params = new Dictionary<string, string>
            {
                [KeyParam] = _key,
            },
        };

    public string GetParam(string fieldName)
        => fieldName == KeyParam ? _key : string.Empty;

    public void SetParam(string fieldName, string value)
    {
        if (fieldName == KeyParam)
            _key = NormalizeKey(value);
    }

    public static BlmTimelineHotkeyAction FromDto(ActionDto dto)
    {
        var key = GetParam(dto.Params, KeyParam, dto.Message ?? string.Empty).Trim();
        if (!IsKnownKey(key))
            throw new InvalidOperationException($"Los 时间轴 Hotkey 不存在：{key}");

        return new BlmTimelineHotkeyAction(key);
    }

    private static string NormalizeKey(string? key)
    {
        var normalized = key?.Trim() ?? string.Empty;
        return IsKnownKey(normalized) ? normalized : string.Empty;
    }

    private static bool IsKnownKey(string key)
        => BlmHotkeyCatalog.Entries.Any(
            entry => string.Equals(entry.Key, key, StringComparison.Ordinal));

    private static string DisplayHotkeyName(string key)
    {
        foreach (var entry in BlmHotkeyCatalog.Entries)
        {
            if (string.Equals(entry.Key, key, StringComparison.Ordinal))
                return entry.Name;
        }

        return key;
    }

    private static string GetParam(
        IReadOnlyDictionary<string, string>? parameters,
        string key,
        string fallback)
        => parameters is not null && parameters.TryGetValue(key, out var value)
            ? value
            : fallback;
}

internal sealed class BlmTimelineQtCondition :
    ICondition,
    ISerializableCondition,
    IJobNodeDescriptor
{
    public const string TypeKey = "Los.BLM.Qt";
    private const string QtParam = "qt";
    private const string EnabledParam = "enabled";

    private string _qt;
    private bool _enabled;

    public BlmTimelineQtCondition(string qt, bool enabled)
    {
        _qt = NormalizeQt(qt);
        _enabled = enabled;
    }

    public string NodeDisplayName => $"黑魔/QT检测-{_qt}-{(_enabled ? "开" : "关")}";

    public NodeParamInfo[] Params =>
    [
        new(
            QtParam,
            "选择 QT",
            "只允许选择当前 Los 已注册的 QT。",
            "enum",
            BlackMageRotation.QtList.Keys.Select(key => (key, key)).ToArray()),
        new(
            EnabledParam,
            "要求开启",
            "关闭时检测该 QT 是否处于关闭状态。",
            "bool"),
    ];

    public bool EvaluateImmediate() => Evaluate();

    public bool EvaluateWait() => Evaluate();

    public ConditionDto ToDto()
        => new()
        {
            Type = TypeKey,
            Params = new Dictionary<string, string>
            {
                [QtParam] = _qt,
                [EnabledParam] = _enabled.ToString(),
            },
        };

    public string GetParam(string fieldName)
        => fieldName switch
        {
            QtParam => _qt,
            EnabledParam => _enabled.ToString(),
            _ => string.Empty,
        };

    public void SetParam(string fieldName, string value)
    {
        switch (fieldName)
        {
            case QtParam:
                _qt = NormalizeQt(value);
                break;
            case EnabledParam when bool.TryParse(value, out var enabled):
                _enabled = enabled;
                break;
        }
    }

    public static BlmTimelineQtCondition FromDto(ConditionDto dto)
    {
        var qt = GetParam(dto.Params, QtParam, dto.Target ?? dto.Mode ?? string.Empty).Trim();
        if (!BlackMageRotation.QtList.ContainsKey(qt))
            throw new InvalidOperationException($"Los 时间轴 QT 不存在：{qt}");

        return new BlmTimelineQtCondition(
            qt,
            GetBool(dto.Params, EnabledParam, !dto.Negate));
    }

    private bool Evaluate()
    {
        try
        {
            return BlackMageRotation.QtList.ContainsKey(_qt)
                && PromeSettings.Instance.GetQt(_qt) == _enabled;
        }
        catch
        {
            return false;
        }
    }

    private static string NormalizeQt(string? qt)
    {
        var normalized = qt?.Trim() ?? string.Empty;
        return BlackMageRotation.QtList.ContainsKey(normalized)
            ? normalized
            : string.Empty;
    }

    private static string GetParam(
        IReadOnlyDictionary<string, string>? parameters,
        string key,
        string fallback)
        => parameters is not null && parameters.TryGetValue(key, out var value)
            ? value
            : fallback;

    private static bool GetBool(
        IReadOnlyDictionary<string, string>? parameters,
        string key,
        bool fallback)
        => bool.TryParse(GetParam(parameters, key, string.Empty), out var value)
            ? value
            : fallback;
}

internal sealed class BlmTimelineResourceCondition :
    ICondition,
    ISerializableCondition,
    IJobNodeDescriptor
{
    public const string TypeKey = "Los.BLM.Resource";
    private const string ResourceParam = "resource";
    private const string CompareParam = "compare";
    private const string ValueParam = "value";
    private const string ExpectedParam = "expected";

    private BlmTimelineResourceType _resource;
    private BlmTimelineCompare _compare;
    private int _value;
    private bool _expected;

    public BlmTimelineResourceCondition(
        BlmTimelineResourceType resource,
        BlmTimelineCompare compare,
        int value,
        bool expected)
    {
        _resource = resource;
        _compare = compare;
        _value = value;
        _expected = expected;
    }

    public string NodeDisplayName => $"黑魔/资源-{ResourceLabel(_resource)}";

    public NodeParamInfo[] Params
    {
        get
        {
            var resource = new NodeParamInfo(
                ResourceParam,
                "资源类型",
                "资源种类与 Los-ae 保持一致。",
                "enum",
                Enum.GetValues<BlmTimelineResourceType>()
                    .Select(value => (value.ToString(), ResourceLabel(value)))
                    .ToArray());
            if (IsBoolean(_resource))
            {
                return
                [
                    resource,
                    new NodeParamInfo(
                        ExpectedParam,
                        "要求为真",
                        "关闭时要求该状态为假。",
                        "bool"),
                ];
            }

            return
            [
                resource,
                new NodeParamInfo(
                    CompareParam,
                    "判断条件",
                    "比较当前资源层数与目标值。",
                    "enum",
                    Enum.GetValues<BlmTimelineCompare>()
                        .Select(value => (value.ToString(), CompareLabel(value)))
                        .ToArray()),
                new NodeParamInfo(
                    ValueParam,
                    "目标值",
                    "用于层数比较。",
                    "int"),
            ];
        }
    }

    public bool EvaluateImmediate() => Evaluate();

    public bool EvaluateWait() => Evaluate();

    public ConditionDto ToDto()
        => new()
        {
            Type = TypeKey,
            Params = new Dictionary<string, string>
            {
                [ResourceParam] = _resource.ToString(),
                [CompareParam] = _compare.ToString(),
                [ValueParam] = _value.ToString(),
                [ExpectedParam] = _expected.ToString(),
            },
        };

    public string GetParam(string fieldName)
        => fieldName switch
        {
            ResourceParam => _resource.ToString(),
            CompareParam => _compare.ToString(),
            ValueParam => _value.ToString(),
            ExpectedParam => _expected.ToString(),
            _ => string.Empty,
        };

    public void SetParam(string fieldName, string value)
    {
        switch (fieldName)
        {
            case ResourceParam when Enum.TryParse(value, true, out BlmTimelineResourceType resource):
                _resource = resource;
                break;
            case CompareParam when Enum.TryParse(value, true, out BlmTimelineCompare compare):
                _compare = compare;
                break;
            case ValueParam when int.TryParse(value, out var target):
                _value = target;
                break;
            case ExpectedParam when bool.TryParse(value, out var expected):
                _expected = expected;
                break;
        }
    }

    public static BlmTimelineResourceCondition FromDto(ConditionDto dto)
        => new(
            GetEnum(dto.Params, ResourceParam, BlmTimelineResourceType.FireState, "资源类型"),
            GetEnum(dto.Params, CompareParam, BlmTimelineCompare.GreaterOrEqual, "比较条件"),
            GetInt(dto.Params, ValueParam, dto.Value.HasValue ? (int)dto.Value.Value : 0),
            GetBool(dto.Params, ExpectedParam, !dto.Negate));

    private bool Evaluate()
    {
        if (!BlmTimelineRuntime.TryGetContext(out var context))
            return false;

        return _resource switch
        {
            BlmTimelineResourceType.FireState => context.InFire == _expected,
            BlmTimelineResourceType.IceState => context.InIce == _expected,
            BlmTimelineResourceType.FireStacks => Compare(context.AfStacks),
            BlmTimelineResourceType.IceStacks => Compare(context.IceStacks),
            BlmTimelineResourceType.AstralSoulStacks => Compare(context.AstralSoul),
            BlmTimelineResourceType.Firestarter => context.HasFirestarter == _expected,
            BlmTimelineResourceType.UmbralHearts => Compare(context.UmbralHearts),
            BlmTimelineResourceType.Paradox => context.HasParadox == _expected,
            BlmTimelineResourceType.PolyglotStacks => Compare(context.PolyglotStacks),
            _ => false,
        };
    }

    private bool Compare(int actual)
        => _compare switch
        {
            BlmTimelineCompare.Greater => actual > _value,
            BlmTimelineCompare.GreaterOrEqual => actual >= _value,
            BlmTimelineCompare.Equal => actual == _value,
            BlmTimelineCompare.LessOrEqual => actual <= _value,
            BlmTimelineCompare.Less => actual < _value,
            BlmTimelineCompare.NotEqual => actual != _value,
            _ => false,
        };

    private static bool IsBoolean(BlmTimelineResourceType resource)
        => resource is BlmTimelineResourceType.FireState
            or BlmTimelineResourceType.IceState
            or BlmTimelineResourceType.Firestarter
            or BlmTimelineResourceType.Paradox;

    private static string ResourceLabel(BlmTimelineResourceType resource)
        => resource switch
        {
            BlmTimelineResourceType.FireState => "火状态",
            BlmTimelineResourceType.IceState => "冰状态",
            BlmTimelineResourceType.FireStacks => "火层数",
            BlmTimelineResourceType.IceStacks => "冰层数",
            BlmTimelineResourceType.AstralSoulStacks => "耀星层数",
            BlmTimelineResourceType.Firestarter => "有火苗",
            BlmTimelineResourceType.UmbralHearts => "冰针",
            BlmTimelineResourceType.Paradox => "悖论指示",
            BlmTimelineResourceType.PolyglotStacks => "通晓层数",
            _ => resource.ToString(),
        };

    private static string CompareLabel(BlmTimelineCompare compare)
        => compare switch
        {
            BlmTimelineCompare.Greater => "大于",
            BlmTimelineCompare.GreaterOrEqual => "大于等于",
            BlmTimelineCompare.Equal => "等于",
            BlmTimelineCompare.LessOrEqual => "小于等于",
            BlmTimelineCompare.Less => "小于",
            BlmTimelineCompare.NotEqual => "不等于",
            _ => compare.ToString(),
        };

    private static TEnum GetEnum<TEnum>(
        IReadOnlyDictionary<string, string>? parameters,
        string key,
        TEnum fallback,
        string displayName)
        where TEnum : struct, Enum
    {
        var raw = GetParam(parameters, key, string.Empty);
        if (string.IsNullOrWhiteSpace(raw))
            return fallback;
        if (Enum.TryParse(raw, true, out TEnum value) && Enum.IsDefined(value))
            return value;

        throw new InvalidOperationException($"Los 时间轴{displayName}无效：{raw}");
    }

    private static int GetInt(
        IReadOnlyDictionary<string, string>? parameters,
        string key,
        int fallback)
        => int.TryParse(GetParam(parameters, key, string.Empty), out var value)
            ? value
            : fallback;

    private static bool GetBool(
        IReadOnlyDictionary<string, string>? parameters,
        string key,
        bool fallback)
        => bool.TryParse(GetParam(parameters, key, string.Empty), out var value)
            ? value
            : fallback;

    private static string GetParam(
        IReadOnlyDictionary<string, string>? parameters,
        string key,
        string fallback)
        => parameters is not null && parameters.TryGetValue(key, out var value)
            ? value
            : fallback;
}
