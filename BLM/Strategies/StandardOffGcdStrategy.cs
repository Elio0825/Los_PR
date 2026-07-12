namespace LosPr.BLM.Strategies;

public readonly record struct BlmOffGcdCandidate(
    uint ActionId,
    string RuleId,
    string Reason)
{
    public bool HasAction => ActionId != 0;
}

public sealed class StandardOffGcdStrategy
{
    private const int AmplifierTickSafetyMs = 4000;

    public BlmOffGcdCandidate Resolve(
        BlmContext context,
        BlmDecisionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(policy);

        var recoveryTranspose = ResolveRecoveryTranspose(context);
        if (recoveryTranspose.HasAction)
        {
            return recoveryTranspose;
        }

        if (NeedsMovementInstant(context))
        {
            if (context.MoveTriplecastEnabled
                && context.TriplecastEnabled
                && context.TriplecastReady)
            {
                return new BlmOffGcdCandidate(
                    BLMSkill.三连咏唱,
                    "OGCD.MOVEMENT.TRIPLECAST",
                    "移动使下一主体读条不安全，使用三连咏唱建立瞬发资源。");
            }

        }

        if (context.LeyLinesEnabled
            && context.LeyLinesReady
            && !context.HasLeyLines
            && !context.IsMoving)
        {
            return new BlmOffGcdCandidate(
                BLMSkill.黑魔纹,
                "OGCD.LEY_LINES",
                "黑魔纹充能可用且当前未持有魔纹效果，在安全窗口释放。");
        }

        if (CanUseAmplifier(context))
        {
            return new BlmOffGcdCandidate(
                BLMSkill.详述,
                "OGCD.AMPLIFIER",
                "详述可用且不会使通晓立即溢出。");
        }

        return default;
    }

    public BlmOffGcdCandidate ResolveRecoveryTranspose(BlmContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.InIce
            && context.IceStacks == 3
            && context.UmbralHearts == 3
            && context.IsMpFull
            && !context.HasParadox
            && context.Transpose.IsReady
            ? new BlmOffGcdCandidate(
                BLMSkill.星灵移位,
                "OGCD.RECOVERY.TRANSPOSE",
                "冰针与 MP 已完成且没有冰悖论，使用星灵移位恢复转火。")
            : default;
    }

    private static bool NeedsMovementInstant(BlmContext context)
        => context.IsMoving
            && !context.HasUsableSwiftcast
            && !context.HasUsableTriplecast
            && !(context.InFire && context.HasParadox);

    private static bool CanUseAmplifier(BlmContext context)
    {
        if (!context.AmplifierEnabled
            || !context.AmplifierReady
            || context.Phase == BlmPhase.Neutral
            || context.MaxPolyglot <= 0
            || context.PolyglotStacks >= context.MaxPolyglot)
        {
            return false;
        }

        return context.PolyglotStacks < context.MaxPolyglot - 1
            || context.PolyglotTimerMs > AmplifierTickSafetyMs;
    }
}
