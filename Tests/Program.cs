namespace Los.Tests;

internal static class Program
{
    private static int Main()
    {
        var tests = new (string Name, Action Run)[]
        {
            ("决策原语与 Ack 历史", DecisionPrimitiveTests.RunAll),
            ("100级单体 Resolver 行为闭包", Level100ResolverParityTests.Run),
            ("生产事实适配器", Phase3FactAdapterTests.RunAll),
            ("3B Resolver 生产执行器", ResolverExecutionTests.RunAll),
            ("Tracker 生命周期与 Manafont 对账", TrackerProductionTests.RunAll),
            ("Resolver Debug JSONL", ResolverDebugTests.RunAll),
            ("3B 结构边界", Phase3BStructureTests.RunAll),
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
