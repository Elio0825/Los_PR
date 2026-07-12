using LosPr.BLM.Strategies;

namespace LosPr.BLM.Engine;

/// <summary>
/// Step 2 L1/L2 pure decision engine. Step 3 must resolve or cancel active L0 intents first.
/// </summary>
public sealed class BlmDecisionEngine
{
    private readonly IBlmStrategy _singleTargetStrategy;
    private readonly BlmOverrideChain _overrideChain;

    public BlmDecisionEngine(
        IBlmStrategy? singleTargetStrategy = null,
        BlmOverrideChain? overrideChain = null)
    {
        _singleTargetStrategy = singleTargetStrategy
            ?? new StandardSingleTargetStrategy();
        _overrideChain = overrideChain ?? new BlmOverrideChain();
    }

    public BlmDecision Resolve(
        BlmContext context,
        BlmDecisionPolicy? policy = null)
        => Resolve(new BlmDecisionInput
        {
            Context = context,
            Policy = policy ?? BlmDecisionPolicy.Default,
        });

    public BlmDecision Resolve(BlmDecisionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var context = input.Context;
        ArgumentNullException.ThrowIfNull(context);
        var policy = input.Policy ?? BlmDecisionPolicy.Default;
        if (!ReferenceEquals(policy, input.Policy))
        {
            input = input with { Policy = policy };
        }

        var gate = ResolveGate(context, policy);
        if (gate is not null)
        {
            return gate;
        }

        var strategyDecision = _singleTargetStrategy.Resolve(input);

        return _overrideChain.Resolve(input, strategyDecision);
    }

    private static BlmDecision? ResolveGate(
        BlmContext context,
        BlmDecisionPolicy policy)
    {
        if (!context.IsAvailable)
        {
            return BlmDecision.NoAction(
                context,
                BlmNoActionReason.SnapshotUnavailable,
                BlmRuleId.SnapshotUnavailable,
                "游戏状态快照不可用。");
        }

        if (!policy.Enabled || context.AcrState != AcrState.On)
        {
            return BlmDecision.NoAction(
                context,
                BlmNoActionReason.Disabled,
                BlmRuleId.Disabled,
                "ACR 或纯决策策略已关闭。");
        }

        if (context.JobId != 25
            || context.Level != 100
            || IsUnsupportedMode(context))
        {
            return BlmDecision.NoAction(
                context,
                BlmNoActionReason.UnsupportedLevelOrMode,
                BlmRuleId.UnsupportedScope,
                "Step 2 仅支持 100 级黑魔标准单体模式。");
        }

        if (!context.HasTarget || !context.HasValidTarget)
        {
            return BlmDecision.NoAction(
                context,
                BlmNoActionReason.NoTarget,
                BlmRuleId.NoTarget,
                "没有有效敌对目标。");
        }

        if (!context.InRange)
        {
            return BlmDecision.NoAction(
                context,
                BlmNoActionReason.OutOfRange,
                BlmRuleId.OutOfRange,
                "当前目标不在技能射程内。");
        }

        if (!context.IsAlive || !context.CanAct)
        {
            return BlmDecision.NoAction(
                context,
                BlmNoActionReason.CannotAct,
                BlmRuleId.CannotAct,
                "角色死亡或当前无法行动。");
        }

        if (policy.HighPriorityQueueActive)
        {
            return BlmDecision.NoAction(
                context,
                BlmNoActionReason.HighPriorityQueue,
                BlmRuleId.HighPriorityQueue,
                "PR 高优先级动作队列正在接管。");
        }

        return null;
    }

    private static bool IsUnsupportedMode(BlmContext context)
        => context.Mode != RotationMode.SingleTarget;
}
