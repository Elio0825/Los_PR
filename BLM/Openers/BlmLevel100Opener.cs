namespace LosPr.BLM.Openers;

public abstract class BlmCountdownOpenerBase : IOpener
{
    private readonly BlmOpenerExecutionService? _execution;
    private readonly Func<BlmContext>? _contextProvider;

    protected BlmCountdownOpenerBase()
    {
    }

    internal BlmCountdownOpenerBase(
        BlmOpenerExecutionService execution,
        Func<BlmContext> contextProvider)
    {
        _execution = execution ?? throw new ArgumentNullException(nameof(execution));
        _contextProvider = contextProvider ?? throw new ArgumentNullException(nameof(contextProvider));
    }

    private protected abstract BlmOpenerVariant Variant { get; }

    protected abstract string DisplayName { get; }

    public string OpenerName => DisplayName;

    // PR 的原生动作组会在重试耗尽后跳步；正式起手全部由 Los 单步执行器接管。
    public List<PAction> InCombatSequence => [];

    public void InitializeCountdown(CountDownHandler countdownHandler)
    {
        ArgumentNullException.ThrowIfNull(countdownHandler);
        if (_execution is null
            || _contextProvider is null
            || !_execution.TryArmCountdown(_contextProvider(), Variant))
        {
            return;
        }

        countdownHandler.AddAction(
            BlmOpener57Definition.PrecastRemainingMs,
            () => _execution.TryCreateCountdownPrecastAction(_contextProvider())!);
        Svc.Chat.Print($"[Los] 已武装{DisplayName}：3.5秒预读爆炎。");
    }

    internal static PAction CreateFireThreePrecastAction(BlmContext context)
    {
        var action = new PAction(BLMSkill.爆炎, ActionType.Gcd, ActionTargetType.Target)
        {
            NetworkTid = context.TargetEntityId,
        };
        return action;
    }
}

public sealed class BlmLevel100Opener : BlmCountdownOpenerBase
{
    public const string Name = "Lv.100 标准 5+7";

    public BlmLevel100Opener()
    {
    }

    internal BlmLevel100Opener(
        BlmOpenerExecutionService execution,
        Func<BlmContext> contextProvider)
        : base(execution, contextProvider)
    {
    }

    private protected override BlmOpenerVariant Variant => BlmOpenerVariant.Standard57;

    protected override string DisplayName => Name;

    internal static PAction CreatePrecastAction()
        => new(BLMSkill.爆炎, ActionType.Gcd, ActionTargetType.Target);
}

public sealed class BlmLevel70Opener : BlmCountdownOpenerBase
{
    public const string Name = "Lv.70 起手";

    public BlmLevel70Opener()
    {
    }

    internal BlmLevel70Opener(
        BlmOpenerExecutionService execution,
        Func<BlmContext> contextProvider)
        : base(execution, contextProvider)
    {
    }

    private protected override BlmOpenerVariant Variant => BlmOpenerVariant.Level70;

    protected override string DisplayName => Name;
}

public sealed class BlmLevel80Opener : BlmCountdownOpenerBase
{
    public const string Name = "Lv.80 起手";

    public BlmLevel80Opener()
    {
    }

    internal BlmLevel80Opener(
        BlmOpenerExecutionService execution,
        Func<BlmContext> contextProvider)
        : base(execution, contextProvider)
    {
    }

    private protected override BlmOpenerVariant Variant => BlmOpenerVariant.Level80;

    protected override string DisplayName => Name;
}

public sealed class BlmLevel90Opener : BlmCountdownOpenerBase
{
    public const string Name = "Lv.90 起手";

    public BlmLevel90Opener()
    {
    }

    internal BlmLevel90Opener(
        BlmOpenerExecutionService execution,
        Func<BlmContext> contextProvider)
        : base(execution, contextProvider)
    {
    }

    private protected override BlmOpenerVariant Variant => BlmOpenerVariant.Level90;

    protected override string DisplayName => Name;
}

public sealed class BlmLevel100FlareOpener : BlmCountdownOpenerBase
{
    public const string Name = "Lv.100 核爆起手";

    public BlmLevel100FlareOpener()
    {
    }

    internal BlmLevel100FlareOpener(
        BlmOpenerExecutionService execution,
        Func<BlmContext> contextProvider)
        : base(execution, contextProvider)
    {
    }

    private protected override BlmOpenerVariant Variant => BlmOpenerVariant.Flare;

    protected override string DisplayName => Name;
}
