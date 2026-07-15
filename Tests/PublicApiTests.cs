using LosPr.BLM;
using PromeRotation.Rotation;

namespace Los.Tests;

internal static class PublicApiTests
{
    private static readonly string[] ExpectedExportedTypes =
    [
        "LosPr.BLM.BlackMageRotation",
        "LosPr.BLM.Openers.BlmCountdownOpenerBase",
        "LosPr.BLM.Openers.BlmLevel100FlareOpener",
        "LosPr.BLM.Openers.BlmLevel100Opener",
        "LosPr.BLM.Openers.BlmLevel70Opener",
        "LosPr.BLM.Openers.BlmLevel80Opener",
        "LosPr.BLM.Openers.BlmLevel90Opener",
    ];

    public static void RunAll()
    {
        var metadata = (RotationMetadataAttribute)typeof(BlackMageRotation)
            .GetCustomAttributes(typeof(RotationMetadataAttribute), inherit: false)
            .Single();
        AssertEx.Equal("Los 黑魔ACR", metadata.RotationName, "PR 中显示的 ACR 名称错误");
        AssertEx.Equal(AcrContentScope.All, metadata.ContentScope, "Los 必须声明同时支持日常和高难");

        var actual = typeof(BlackMageRotation).Assembly
            .GetExportedTypes()
            .Select(type => type.FullName ?? type.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        AssertEx.Equal(
            string.Join('|', ExpectedExportedTypes),
            string.Join('|', actual),
            "Los.dll 对外类型必须仅包含 PR 反射入口和正式起手类型");
    }
}
