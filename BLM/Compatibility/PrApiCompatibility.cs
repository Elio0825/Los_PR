using Lumina.Excel.Sheets;

namespace LosPr.BLM.Compatibility;

internal static class PrApiCompatibility
{
#if LOS_TC
    internal const ObjectKind PlayerObjectKind = ObjectKind.Player;
#else
    internal const ObjectKind PlayerObjectKind = ObjectKind.Pc;
#endif

    internal static BlmDutyComposition ReadDutyComposition(ContentMemberType memberType)
    {
#if LOS_TC
        // 台服 SDK 的旧版表定义尚未命名这两列；与新版列偏移 4、5 一致。
        return new BlmDutyComposition(memberType.Unknown4, memberType.Unknown5);
#else
        return new BlmDutyComposition(memberType.MembersPerParty, memberType.PartyCount);
#endif
    }
}
