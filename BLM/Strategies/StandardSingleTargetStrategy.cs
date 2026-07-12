namespace LosPr.BLM.Strategies;

public sealed class StandardSingleTargetStrategy : IBlmStrategy
{
    private const long InstantStepExpireMs = 3500;
    private const long CastStepExpireMs = 5000;
    private const long TransitionExpireMs = 10000;

    public RotationMode Mode => RotationMode.SingleTarget;

    public BlmDecision Resolve(BlmDecisionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var context = input.Context;

        return context.Phase switch
        {
            BlmPhase.Ice => ResolveIce(context),
            BlmPhase.Fire => ResolveFire(context),
            _ => BlmDecision.Gcd(
                context,
                BLMSkill.冰封,
                BlmRuleId.NeutralBlizzard3,
                "中立状态先用冰封建立 UI3。",
                BlmDecisionLayer.Recovery),
        };
    }

    private static BlmDecision ResolveIce(BlmContext context)
    {
        var tracker = context.Tracker;
        if (context.IceStacks < 3)
        {
            return BlmDecision.Gcd(
                context,
                BLMSkill.冰封,
                BlmRuleId.UiLowBlizzard3,
                "UI 层数不足，冰封恢复至 UI3。",
                BlmDecisionLayer.Recovery);
        }

        if (context.UmbralHearts < 3)
        {
            if (context.ExperimentalB4TransposeDespairEnabled
                && context.HasParadox
                && !tracker.ParadoxUsedThisIce)
            {
                return BlmDecision.Gcd(
                    context,
                    BLMSkill.悖论,
                    BlmRuleId.IceParadox,
                    "实验 B4 星灵绝望路线先释放冰悖论，再使用冰澈。",
                    BlmDecisionLayer.Strategy);
            }

            var transition = context.ExperimentalB4TransposeDespairEnabled
                && tracker.ParadoxUsedThisIce
                && context.Transpose.IsReady
                ? new BlmTransitionRequest(
                    TransitionKind.IceToFire,
                    TransitionStep.CommitIceGcd,
                    TransitionDeliveryChannel.Gcd,
                    BLMSkill.冰澈,
                    TransitionExpectation.IceReadyForTransposeDespair,
                    RotationMode.SingleTarget,
                    IceToFireRoute.B4TransposeDespair,
                    CastStepExpireMs,
                    TransitionExpireMs)
                : null;
            return BlmDecision.Gcd(
                context,
                BLMSkill.冰澈,
                BlmRuleId.IceBlizzard4,
                transition is null
                    ? "冰针不足，使用唯一一发冰澈补满三枚 Umbral Hearts。"
                    : "实验路线已先消费冰悖论，冰澈后预置星灵绝望事务。",
                BlmDecisionLayer.Strategy,
                transition);
        }

        if (context.HasParadox && IsAwaitingParadoxConsumption(context))
        {
            return BlmDecision.NoAction(
                context,
                BlmNoActionReason.WaitingForGaugeReconcile,
                BlmRuleId.WaitingForGaugeReconcile,
                "冰悖论已确认释放，等待服务器清除悖论 Gauge，禁止重复提交。");
        }

        if (context.HasParadox)
        {
            var transition = context.Transpose.IsReady
                ? BuildIceToFireTransition(
                    context,
                    BLMSkill.悖论,
                    InstantStepExpireMs)
                : null;
            return BlmDecision.Gcd(
                context,
                BLMSkill.悖论,
                BlmRuleId.IceParadox,
                transition is null
                    ? "冰澈后立即释放冰悖论；星灵不可用时后续安全硬读爆炎。"
                    : "冰澈后立即释放冰悖论，并预置满蓝后的冰转火事务。",
                BlmDecisionLayer.Strategy,
                transition);
        }

        if (!context.IsMpFull)
        {
            if (tracker.PendingGaugeReconcile)
            {
                return BlmDecision.NoAction(
                    context,
                    BlmNoActionReason.WaitingForGaugeReconcile,
                    BlmRuleId.WaitingForGaugeReconcile,
                    "冰系资源动作已 Ack，等待下一 Tick Gauge，禁止基于旧 MP 重复决策。");
            }

            if (IsAwaitingIceMpGain(context))
            {
                return BlmDecision.NoAction(
                    context,
                    BlmNoActionReason.WaitingForIceMpGain,
                    BlmRuleId.WaitingForIceMpGain,
                    "最近一发 UI 冰系法术的 MP 恢复尚在服务器结算宽限内，等待真实 MP 更新。");
            }

            return BlmDecision.Gcd(
                context,
                BLMSkill.冰澈,
                BlmRuleId.IceMpRecovery,
                "Gauge 已确认且服务器结算宽限结束后 MP 仍不足，使用冰澈主动触发 UI3 MP 恢复。",
                BlmDecisionLayer.Recovery);
        }

        if (context.Transpose.IsReady)
        {
            return BlmDecision.NoAction(
                context,
                BlmNoActionReason.WaitingForTranspose,
                BlmRuleId.WaitingForTranspose,
                "MP 与冰针均已完成且没有冰悖论，等待星灵移位恢复转火。");
        }

        return BlmDecision.Gcd(
            context,
            BLMSkill.爆炎,
            BlmRuleId.IceHardFire3,
            "冰资源已完成但没有已承诺的能力尾窗，安全硬读爆炎进火。",
            BlmDecisionLayer.Strategy);
    }

    private static BlmDecision ResolveFire(BlmContext context)
    {
        if (context.AfStacks < 3)
        {
            return ResolveLowAstralFire(context);
        }

        var budget = BlmFireBudget.Build(context, context.Tracker);
        if (budget.ShouldUseParadoxNow)
        {
            return BlmDecision.Gcd(
                context,
                BLMSkill.悖论,
                BlmRuleId.FireParadox,
                budget.ParadoxForcedByBudget
                    ? "MP 不足以同时保留下一发炽炎、火悖论和绝望，提前释放悖论。"
                    : context.CompressFireParadox
                    ? "压缩位置已到，释放火悖论并保留绝望预算。"
                    : "第三发炽炎后释放标准位置火悖论。",
                BlmDecisionLayer.Strategy);
        }

        if (budget.CanUseNextFire4)
        {
            return BlmDecision.Gcd(
                context,
                BLMSkill.炽炎,
                BlmRuleId.Fire4,
                $"炽炎成本 {budget.NextFire4Cost} MP，已保留后续 {budget.RequiredReserveAfterFire4} MP。",
                BlmDecisionLayer.Strategy);
        }

        if (budget.AstralSoulFull)
        {
            return BlmDecision.Gcd(
                context,
                BLMSkill.耀星,
                BlmRuleId.FlareStar,
                "Astral Soul 已满，以 Gauge 真值释放耀星。",
                BlmDecisionLayer.Strategy);
        }

        if (budget.CanUseDespair)
        {
            return BuildDespairDecision(
                context,
                budget.Fire4CountAtOrPastPlan
                    ? "炽炎预算已达上限或耀星已消费，使用绝望安全收尾。"
                    : "火段预算无法再支付主体 GCD，使用绝望收尾。");
        }

        return BlmDecision.Gcd(
            context,
            BLMSkill.冰封,
            BlmRuleId.FireBlizzard3,
            "MP 低于绝望门槛，AF3 直接冰封进冰。",
            BlmDecisionLayer.Recovery);
    }

    private static BlmDecision ResolveLowAstralFire(BlmContext context)
    {
        if (context.HasFirestarter)
        {
            return BlmDecision.Gcd(
                context,
                BLMSkill.爆炎,
                BlmRuleId.Af1FirestarterFire3,
                "AF1 优先消费已存在的 Firestarter，用爆炎升至 AF3。",
                BlmDecisionLayer.Recovery);
        }

        if (context.AfStacks == 1
            && context.HasParadox
            && context.Mp >= BlmFireBudget.FireParadoxCost)
        {
            var transition = new BlmTransitionRequest(
                TransitionKind.IceToFire,
                TransitionStep.UseAfParadox,
                TransitionDeliveryChannel.Gcd,
                BLMSkill.悖论,
                TransitionExpectation.FirestarterPresent,
                RotationMode.SingleTarget,
                IceToFireRoute.Af1ParadoxRecovery,
                InstantStepExpireMs,
                TransitionExpireMs);
            return BlmDecision.Gcd(
                context,
                BLMSkill.悖论,
                BlmRuleId.Af1Paradox,
                "AF1 缺少 Firestarter，以火悖论建立赤字恢复事务。",
                BlmDecisionLayer.Recovery,
                transition);
        }

        var fire3Cost = BlmFireBudget.Fire3Cost(context);
        if (context.Mp < fire3Cost)
        {
            return BlmDecision.Gcd(
                context,
                BLMSkill.冰封,
                BlmRuleId.FireResourceRecovery,
                $"低 AF 只有 {context.Mp} MP，无法支付当前 {fire3Cost} MP 的硬读爆炎，冰封重建冰阶段。",
                BlmDecisionLayer.Recovery);
        }

        return BlmDecision.Gcd(
            context,
            BLMSkill.爆炎,
            BlmRuleId.AfLowHardFire3,
            "低 AF 无 Firestarter/可支付悖论，硬读爆炎兜底。",
            BlmDecisionLayer.Recovery);
    }

    private static BlmDecision BuildDespairDecision(
        BlmContext context,
        string reason)
    {
        BlmTransitionRequest? transition = null;
        if (context.Transpose.IsReady
            && context.CanPlanInstantB3AfterDespair)
        {
            transition = new BlmTransitionRequest(
                TransitionKind.FireToIce,
                TransitionStep.CommitFireFinisher,
                TransitionDeliveryChannel.Gcd,
                BLMSkill.绝望,
                TransitionExpectation.FireFinisherReady,
                RotationMode.SingleTarget,
                IceToFireRoute.None,
                InstantStepExpireMs,
                TransitionExpireMs);
        }

        return BlmDecision.Gcd(
            context,
            BLMSkill.绝望,
            BlmRuleId.Despair,
            transition is null
                ? reason
                : $"{reason} 已确认保留瞬发冰封资源，预置火转冰。",
            BlmDecisionLayer.Strategy,
            transition);
    }

    private static BlmTransitionRequest BuildIceToFireTransition(
        BlmContext context,
        uint committedGcd,
        long stepExpireMs)
    {
        var hasFirestarter = context.HasFirestarter;
        return new BlmTransitionRequest(
            TransitionKind.IceToFire,
            TransitionStep.CommitIceGcd,
            TransitionDeliveryChannel.Gcd,
            committedGcd,
            hasFirestarter
                ? TransitionExpectation.IceReadyWithFirestarter
                : TransitionExpectation.IceReadyForAf1Paradox,
            RotationMode.SingleTarget,
            hasFirestarter
                ? IceToFireRoute.ExistingFirestarter
                : IceToFireRoute.Af1ParadoxRecovery,
            stepExpireMs,
            TransitionExpireMs);
    }

    private static bool IsAwaitingIceMpGain(BlmContext context)
    {
        if (context.Tracker.LastGcdId is not (
            BLMSkill.冰结 or BLMSkill.冰冻 or BLMSkill.冰封
            or BLMSkill.高冰冻 or BLMSkill.冰澈 or BLMSkill.玄冰
            or BLMSkill.灵极魂))
        {
            return false;
        }

        var elapsedMs = context.CapturedAtMs - context.Tracker.LastGcdAtMs;
        var graceMs = (long)Math.Ceiling(
            Math.Max(2.5f, context.GcdTotalSeconds) * 1000f) + 750;
        return context.Tracker.LastGcdAtMs > 0
            && elapsedMs >= 0
            && elapsedMs <= graceMs;
    }

    private static bool IsAwaitingParadoxConsumption(BlmContext context)
    {
        if (context.Tracker.LastGcdId != BLMSkill.悖论)
        {
            return false;
        }

        var elapsedMs = context.CapturedAtMs - context.Tracker.LastGcdAtMs;
        return context.Tracker.PendingGaugeReconcile
            || (context.Tracker.LastGcdAtMs > 0
                && elapsedMs >= 0
                && elapsedMs <= 1000);
    }

}
