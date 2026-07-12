namespace LosPr.BLM.Engine;

public sealed class BlmOverrideChain
{
    public BlmDecision Resolve(
        BlmDecisionInput input,
        BlmDecision strategyDecision)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(strategyDecision);

        if (strategyDecision.NoActionReason is
            BlmNoActionReason.WaitingForGaugeReconcile
            or BlmNoActionReason.WaitingForIceMpGain
            or BlmNoActionReason.WaitingForTranspose)
        {
            return strategyDecision;
        }

        if (strategyDecision.Layer == BlmDecisionLayer.Recovery)
        {
            return input.Context.IsMoving
                ? ResolveMoving(input, strategyDecision, allowPhaseOverrides: false)
                : strategyDecision;
        }

        if (input.Context.IsMoving)
        {
            return ResolveMoving(input, strategyDecision, allowPhaseOverrides: true);
        }

        var flareStar = ResolveFlareStar(input, requireInstant: false);
        if (flareStar is not null)
        {
            return flareStar;
        }

        var polyglot = ResolvePolyglotOvercap(input);
        if (polyglot is not null)
        {
            return polyglot;
        }

        var thunder = ResolveThunder(input);
        return thunder ?? strategyDecision;
    }

    private static BlmDecision ResolveMoving(
        BlmDecisionInput input,
        BlmDecision strategyDecision,
        bool allowPhaseOverrides)
    {
        var context = input.Context;
        if (!allowPhaseOverrides
            && BlmCastSafety.CanCastWhileMoving(
                context,
                strategyDecision.ActionId,
                input.Policy))
        {
            return strategyDecision;
        }

        if (allowPhaseOverrides)
        {
            var flareStar = ResolveFlareStar(input, requireInstant: true);
            if (flareStar is not null)
            {
                return flareStar;
            }
        }

        var overcap = ResolvePolyglotOvercap(input);
        if (overcap is not null && context.MoveXenoEnabled)
        {
            return overcap;
        }

        var thunder = ResolveThunder(input);
        if (thunder is not null)
        {
            return thunder;
        }

        if (allowPhaseOverrides
            && BlmCastSafety.CanCastWhileMoving(
                context,
                strategyDecision.ActionId,
                input.Policy))
        {
            return strategyDecision;
        }

        if (context.MoveXenoEnabled && context.PolyglotStacks > 0)
        {
            return BlmDecision.Gcd(
                context,
                BLMSkill.异言,
                BlmRuleId.PolyglotMoveFill,
                "移动中主体 GCD 无法安全读条，使用异言填充。",
                BlmDecisionLayer.Override);
        }

        if (allowPhaseOverrides
            && context.InFire
            && context.AfStacks == 3
            && context.AstralSoulFull
            && context.Mp >= BlmFireBudget.DespairMinimumMp
            && input.Policy.AllowMovementDespairReorder)
        {
            var followUp = new BlmFollowUpRequest(
                BlmFollowUpKind.FlareStarAfterMovementDespair,
                context.Tracker.StateGeneration,
                context.Tracker.CombatSerial,
                context.Tracker.FirePhaseSerial,
                context.TargetEntityId,
                BLMSkill.绝望,
                BLMSkill.耀星,
                context.Tracker.LastAckGlobalSequence,
                8000,
                "本发绝望仅用于移动换序，随后仍必须消费满层 Astral Soul。");
            return BlmDecision.Gcd(
                context,
                BLMSkill.绝望,
                BlmRuleId.MoveDespairReorder,
                "耀星当前无法瞬发，先用 100 级天然瞬发绝望维持移动 GCD。",
                BlmDecisionLayer.Override,
                followUpRequest: followUp);
        }

        if (allowPhaseOverrides
            && input.Policy.AllowEmergencyFirestarter
            && context.InFire
            && context.AfStacks == 3
            && context.HasFirestarter)
        {
            return BlmDecision.Gcd(
                context,
                BLMSkill.爆炎,
                BlmRuleId.MoveEmergencyFirestarter,
                "显式紧急策略允许消费 Firestarter；该资源不再保留给下轮进火。",
                BlmDecisionLayer.Override);
        }

        return BlmDecision.NoAction(
            context,
            BlmNoActionReason.MovementNoSafeGcd,
            BlmRuleId.MovementNoSafeGcd,
            "移动中没有当前可瞬发的合法 GCD。不能把有读条技能或保留中的 Firestarter 当作安全动作。");
    }

    private static BlmDecision? ResolveFlareStar(
        BlmDecisionInput input,
        bool requireInstant)
    {
        var context = input.Context;
        if (!context.InFire
            || context.AfStacks < 3
            || !context.AstralSoulFull
            || (requireInstant
                && !BlmCastSafety.CanCastWhileMoving(
                    context,
                    BLMSkill.耀星,
                    input.Policy)))
        {
            return null;
        }

        return BlmDecision.Gcd(
            context,
            BLMSkill.耀星,
            BlmRuleId.FlareStar,
            requireInstant
                ? "Astral Soul 已满且瞬发状态剩余时间足够，移动中释放耀星。"
                : "Astral Soul 已满，耀星优先于通晓、雷法和主体 Strategy。",
            BlmDecisionLayer.Override);
    }

    private static BlmDecision? ResolvePolyglotOvercap(BlmDecisionInput input)
    {
        var context = input.Context;
        var threshold = Math.Max(0, input.Policy.PolyglotOvercapThresholdMs);
        if (context.MaxPolyglot <= 0
            || context.PolyglotStacks < context.MaxPolyglot
            || context.PolyglotTimerMs > threshold)
        {
            return null;
        }

        var actionId = BlmSkillBook.PolyglotSpell(context);
        return actionId == 0
            ? null
            : BlmDecision.Gcd(
                context,
                actionId,
                BlmRuleId.PolyglotOvercap,
                $"通晓已满且生成计时不超过 {threshold}ms，优先防溢出。",
                BlmDecisionLayer.Override);
    }

    private static BlmDecision? ResolveThunder(BlmDecisionInput input)
    {
        var context = input.Context;
        var threshold = Math.Max(0f, input.Policy.ThunderRefreshThresholdMs);
        var dotNeedsRefresh = context.SingleTargetDot.IsMissing
            || context.SingleTargetDot.RemainingMs < threshold;
        if (!context.DotEnabled
            || !context.HasThunderhead
            || !dotNeedsRefresh)
        {
            return null;
        }

        return BlmDecision.Gcd(
            context,
            BLMSkill.高闪雷,
            BlmRuleId.ThunderRefresh,
            context.SingleTargetDot.IsMissing
                ? "当前单体高闪雷 DoT 缺失，消费 Thunderhead 刷新。"
                : $"当前单体 DoT 低于 {threshold:F0}ms 刷新阈值。",
            BlmDecisionLayer.Override);
    }

}
