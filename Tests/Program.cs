namespace Los.Tests;

internal static class Program
{
    private static int Main()
    {
        var tests = new (string Name, Action Run)[]
        {
            ("决策原语与 Ack 历史", DecisionPrimitiveTests.RunAll),
            ("100级单体 Resolver 行为闭包", Level100ResolverParityTests.Run),
            ("90–99级单体 Resolver 行为闭包", Level90ResolverParityTests.RunAll),
            ("1–89级单体 Resolver 行为闭包", Level1To89ResolverParityTests.RunAll),
            ("4C-0 单体/AOE共享能力技协调", Phase4C0AbilityCoordinationTests.RunAll),
            ("4C-1 AOE GCD行为闭包", Phase4C1AoeResolverTests.RunAll),
            ("生产事实适配器", Phase3FactAdapterTests.RunAll),
            ("3B Resolver 生产执行器", ResolverExecutionTests.RunAll),
            ("Tracker 生命周期与 Manafont 对账", TrackerProductionTests.RunAll),
            ("新日志 ActionEffect 接入", ActionEffectEventTests.RunAll),
            ("Resolver Debug JSONL", ResolverDebugTests.RunAll),
            ("Boss上天 QT", BossFlightTests.RunAll),
            ("3B 结构边界", Phase3BStructureTests.RunAll),
            ("100级日常/高难5+7起手", Level100OpenerTests.RunAll),
            ("70–99级与100级核爆起手", MultiLevelOpenerTests.RunAll),
            ("PR 时间轴 QT/Hotkey/资源接入", TimelineIntegrationTests.RunAll),
            ("Hotkey 爆发药物品调用", HotkeyPotionTests.RunAll),
            ("QT/Hotkey 快捷键捕获与持久化", KeyBindingTests.RunAll),
            ("Los.dll 公共 API 边界", PublicApiTests.RunAll),
        };

        var failed = 0;
        foreach (var (name, run) in tests)
        {
            try
            {
                run();
                Console.WriteLine($"[PASS] {name}");
            }
            catch (Exception exception)
            {
                failed++;
                Console.WriteLine($"[FAIL] {name}: {exception.Message}");
            }
        }

        Console.WriteLine($"共 {tests.Length} 项，失败 {failed} 项。");
        return failed == 0 ? 0 : 1;
    }
}
