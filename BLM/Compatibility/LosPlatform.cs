namespace LosPr.BLM.Compatibility;

internal static class LosPlatform
{
#if LOS_TC
    internal const string Region = "TC";
    internal const string Author = "Los-TC";
    internal const string AssemblyName = "Los.TC";
    internal const string RotationName = "Los 黑魔ACR（台服）";
#else
    internal const string Region = "CN";
    internal const string Author = "Los";
    internal const string AssemblyName = "Los";
    internal const string RotationName = "Los 黑魔ACR";
#endif

    internal static string GetCompatibilitySettingsDirectory(string pluginConfigDirectory)
        => Path.Combine(pluginConfigDirectory, "Settings", "ACRConfig", Author);
}
