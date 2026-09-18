using LosPr.BLM;
using LosPr.BLM.Compatibility;
using Lumina.Excel;
using Lumina.Excel.Sheets;
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
#if LOS_TC
        const string expectedRegion = "TC";
        const string expectedAuthor = "Los-TC";
        const string expectedAssembly = "Los.TC";
        const string expectedName = "Los 黑魔ACR（台服）";
        const string expectedFramework = ".NETCoreApp,Version=v9.0";
#else
        const string expectedRegion = "CN";
        const string expectedAuthor = "Los";
        const string expectedAssembly = "Los";
        const string expectedName = "Los 黑魔ACR";
        const string expectedFramework = ".NETCoreApp,Version=v10.0";
#endif
        AssertEx.Equal(expectedName, metadata.RotationName, "PR 中显示的 ACR 名称错误");
        AssertEx.Equal(expectedAuthor, metadata.Author, "PR 安装标识必须与目标服务器匹配");
        AssertEx.Equal(expectedRegion, LosPlatform.Region, "运行时平台标识错误");
        AssertEx.Equal(expectedAuthor, LosPlatform.Author, "配置目录与 PR 元数据的作者必须一致");
        AssertEx.Equal(
            Path.Combine("plugin-config", "Settings", "ACRConfig", expectedAuthor),
            LosPlatform.GetCompatibilitySettingsDirectory("plugin-config"),
            "不同服务器必须使用独立配置目录");
        AssertEx.Equal(expectedAssembly, typeof(BlackMageRotation).Assembly.GetName().Name!, "DLL 标识错误");
        var framework = (System.Runtime.Versioning.TargetFrameworkAttribute)typeof(BlackMageRotation)
            .Assembly.GetCustomAttributes(typeof(System.Runtime.Versioning.TargetFrameworkAttribute), false).Single();
        AssertEx.Equal(expectedFramework, framework.FrameworkName, "DLL 目标框架必须匹配服务器环境");
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

        DutyCompositionReadsTheSameSheetColumns();
        AssertEx.Equal(1, (int)PrApiCompatibility.PlayerObjectKind, "玩家对象类型必须保持游戏原始值 1");
    }

    private static void DutyCompositionReadsTheSameSheetColumns()
    {
        foreach (var (membersPerParty, partyCount) in new[] { (4, 1), (8, 1), (8, 3) })
        {
            // 使用真实 Lumina 读表实现验证两套表定义，避免旧版 Unknown 字段映射错列。
            var rowBytes = new byte[32];
            rowBytes[4] = (byte)membersPerParty;
            rowBytes[5] = (byte)partyCount;
            var flags = System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic;
            var page = (ExcelPage)Activator.CreateInstance(
                typeof(ExcelPage), flags, null, [null, rowBytes, (ushort)0], null)!;
            var memberType = (ContentMemberType)Activator.CreateInstance(
                typeof(ContentMemberType), flags, null, [page, 0u, 1u], null)!;
            var composition = PrApiCompatibility.ReadDutyComposition(memberType);
            AssertEx.Equal(membersPerParty, composition.MembersPerParty, "副本每队人数列读取错误");
            AssertEx.Equal(partyCount, composition.PartyCount, "副本队伍数量列读取错误");
        }
    }
}
