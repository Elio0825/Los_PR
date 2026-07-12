namespace LosPr.BLM.Core;

public static class BlmSkillBook
{
    public static uint FireSpam(BlmContext context)
        => context.Level >= 60 ? BLMSkill.炽炎 : BLMSkill.火炎;

    public static uint IceSpam(BlmContext context)
        => context.Level >= 58 ? BLMSkill.冰澈 : BLMSkill.冰结;

    public static uint FireEntry(int level)
        => level >= 35 ? BLMSkill.爆炎 : BLMSkill.火炎;

    public static uint IceEntry(int level)
        => level >= 35 ? BLMSkill.冰封 : BLMSkill.冰结;

    public static uint AoeIceResourceSpell(BlmContext context)
    {
        if (!context.IsAoeMode)
        {
            return 0;
        }

        if (context.EnemyCount == 2 && context.Level >= 58)
        {
            return BLMSkill.冰澈;
        }

        if (context.Level >= 40)
        {
            return BLMSkill.玄冰;
        }

        return context.Level >= 12 ? BLMSkill.冰冻 : BLMSkill.冰结;
    }

    public static uint AoeFireSpell(BlmContext context)
    {
        if (!context.IsAoeMode)
        {
            return 0;
        }

        if (context.Level >= 50)
        {
            return BLMSkill.核爆;
        }

        return context.Level >= 18 ? BLMSkill.烈炎 : BLMSkill.火炎;
    }

    public static uint PolyglotSpell(BlmContext context)
    {
        if (context.Level < 70)
        {
            return 0;
        }

        return context.IsAoeMode || context.Level < 80
            ? BLMSkill.秽浊
            : BLMSkill.异言;
    }

    public static uint ThunderSpell(BlmContext context)
    {
        if (context.IsAoeMode)
        {
            if (context.Level >= 92)
            {
                return BLMSkill.高震雷;
            }

            if (context.Level >= 26)
            {
                return BLMSkill.震雷;
            }
        }

        if (context.Level >= 92)
        {
            return BLMSkill.高闪雷;
        }

        return context.Level >= 6 ? BLMSkill.闪雷 : 0;
    }

    public static bool ActionIdsMatch(
        uint expectedActionId,
        uint actualActionId,
        IBlmActionIdNormalizer normalizer)
    {
        ArgumentNullException.ThrowIfNull(normalizer);
        if (expectedActionId == 0 || actualActionId == 0)
        {
            return false;
        }

        if (expectedActionId == actualActionId)
        {
            return true;
        }

        var expected = normalizer.Normalize(expectedActionId);
        var actual = normalizer.Normalize(actualActionId);
        return expected != 0 && actual != 0 && expected == actual;
    }

    public static bool IsKnownGcdAction(uint actionId, IBlmActionIdNormalizer normalizer)
        => IsKnownGcdId(actionId) || IsKnownGcdId(normalizer.Normalize(actionId));

    public static bool IsIcePhaseCommitGcd(uint actionId, IBlmActionIdNormalizer normalizer)
        => IsIcePhaseCommitId(actionId) || IsIcePhaseCommitId(normalizer.Normalize(actionId));

    public static bool IsFireFinisherCommitGcd(uint actionId, IBlmActionIdNormalizer normalizer)
        => IsFireFinisherCommitId(actionId) || IsFireFinisherCommitId(normalizer.Normalize(actionId));

    public static bool IsUnlocked(uint actionId, int level) => actionId switch
    {
        BLMSkill.火炎 => level >= 1,
        BLMSkill.冰结 => level >= 1,
        BLMSkill.星灵移位 => level >= 4,
        BLMSkill.闪雷 => level >= 6,
        MageUniversalSkill.昏乱 => level >= 8,
        BLMSkill.冰冻 => level >= 12,
        MageUniversalSkill.醒梦 => level >= 14,
        BLMSkill.崩溃 => level >= 15,
        BLMSkill.烈炎 => level >= 18,
        MageUniversalSkill.即刻咏唱 => level >= 18,
        BLMSkill.震雷 => level >= 26,
        BLMSkill.魔罩 => level >= 30,
        BLMSkill.魔泉 => level >= 30,
        BLMSkill.爆炎 => level >= 35,
        BLMSkill.冰封 => level >= 35,
        BLMSkill.灵极魂 => level >= 35,
        BLMSkill.玄冰 => level >= 40,
        MageUniversalSkill.沉稳咏唱 => level >= 44,
        BLMSkill.暴雷 => level >= 45,
        BLMSkill.以太步 => level >= 50,
        BLMSkill.核爆 => level >= 50,
        BLMSkill.黑魔纹 => level >= 52,
        BLMSkill.冰澈 => level >= 58,
        BLMSkill.炽炎 => level >= 60,
        BLMSkill.魔纹步 => level >= 62,
        BLMSkill.霹雷 => level >= 64,
        BLMSkill.三连咏唱 => level >= 66,
        BLMSkill.秽浊 => level >= 70,
        BLMSkill.绝望 => level >= 72,
        BLMSkill.异言 => level >= 80,
        BLMSkill.高烈炎 => level >= 82,
        BLMSkill.高冰冻 => level >= 82,
        BLMSkill.详述 => level >= 86,
        BLMSkill.悖论 => level >= 90,
        BLMSkill.高闪雷 => level >= 92,
        BLMSkill.高震雷 => level >= 92,
        BLMSkill.魔纹重置 => level >= 96,
        BLMSkill.耀星 => level >= 100,
        _ => false,
    };

    private static bool IsKnownGcdId(uint actionId) => actionId is
        BLMSkill.火炎 or BLMSkill.烈炎 or BLMSkill.爆炎 or BLMSkill.高烈炎
        or BLMSkill.炽炎 or BLMSkill.绝望 or BLMSkill.核爆 or BLMSkill.耀星
        or BLMSkill.冰结 or BLMSkill.冰冻 or BLMSkill.冰封 or BLMSkill.高冰冻
        or BLMSkill.冰澈 or BLMSkill.玄冰 or BLMSkill.灵极魂
        or BLMSkill.闪雷 or BLMSkill.震雷 or BLMSkill.暴雷 or BLMSkill.霹雷
        or BLMSkill.高闪雷 or BLMSkill.高震雷
        or BLMSkill.秽浊 or BLMSkill.异言 or BLMSkill.悖论 or BLMSkill.崩溃;

    private static bool IsIcePhaseCommitId(uint actionId) => actionId is
        BLMSkill.冰结 or BLMSkill.冰冻 or BLMSkill.冰封 or BLMSkill.高冰冻
        or BLMSkill.冰澈 or BLMSkill.玄冰 or BLMSkill.灵极魂 or BLMSkill.悖论
        or BLMSkill.闪雷 or BLMSkill.震雷 or BLMSkill.暴雷 or BLMSkill.霹雷
        or BLMSkill.高闪雷 or BLMSkill.高震雷
        or BLMSkill.秽浊 or BLMSkill.异言 or BLMSkill.崩溃;

    private static bool IsFireFinisherCommitId(uint actionId) => actionId is
        BLMSkill.火炎 or BLMSkill.烈炎 or BLMSkill.高烈炎 or BLMSkill.炽炎
        or BLMSkill.绝望 or BLMSkill.核爆 or BLMSkill.耀星;
}
