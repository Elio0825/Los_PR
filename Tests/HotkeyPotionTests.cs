using LosPr.BLM.UI;

namespace Los.Tests;

internal static class HotkeyPotionTests
{
    public static void RunAll()
    {
        PreservesPrPotionItemEncoding();
    }

    private static void PreservesPrPotionItemEncoding()
    {
        var highQuality = BlmHotkeyCatalog.BuildPotionDispatchParameters(1_049_237u);
        AssertEx.Equal(1_049_237u, highQuality.ActionId, "HQ 药水必须保留 PR 返回的完整物品 ID");
        AssertEx.Equal(0xFFFFu, highQuality.ExtraParam, "HQ 药水必须使用 PR 的物品调用参数");

        var normalQuality = BlmHotkeyCatalog.BuildPotionDispatchParameters(49_237u);
        AssertEx.Equal(49_237u, normalQuality.ActionId, "NQ 药水物品 ID 不得被改写");
        AssertEx.Equal(0xFFFFu, normalQuality.ExtraParam, "NQ 药水也必须使用 PR 的物品调用参数");
    }
}
