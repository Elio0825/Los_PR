# Los 黑魔 PR

方案 C 第三阶段 3B 与第四阶段 4A 均已完成自动化和真机 Gate。当前由同一 Resolver 引擎接管 `NextGcd`、`NextOffGcd`、`NextAlways` 三个生产入口，生产范围为 90–100 级标准单体。旧 Transaction/Follow-up 常规技能图保持删除状态。

## 已实现

- 独立 `960 x 660` 控制台，包含概览、作战、控制、系统四页签
- 黑魔 Gauge、Buff、等级、雷 DoT、目标、动作可用性与 PR QT 事实采集
- ActionEffect pre-gauge Ack 与下一 Framework Tick Gauge 对账
- `CombatSerial`、`StateGeneration`、动作历史、通用签发 Pending 与生命周期隔离
- 每个 Framework Tick 构造一份同代、同目标、同时间戳的不可变 `DecisionFrame`
- 三个 PR 入口按 GCD、OffGCD、Always 通道交付 Resolver 候选
- 星灵移位 Hold：只阻断主循环冰三，并避免其他 OffGCD 抢占火末星灵窗口
- GCD/能力 Ack 前防重复；高优队列和 generation 漂移 fail closed，目标切换会放弃旧目标 Pending
- 火末星灵、即刻/三连进冰、移动绝望后耀星与任意 Gauge 异常状态恢复
- 常规魔泉作为普通 OffGCD Resolver；火末无法正常插入时仅桥接同一候选到 Always，并在交付前阻止冰封
- 魔泉 Ack 后以 Gauge 资源恢复事实完成对账，并从魔泉后火四计数重新续火
- 90–99 级独立主循环：保留 los-ae 的冰火条件、火悖论局部计数和 800 MP 绝望预算
- 等级实时路由：90–99 级使用 `GCD.单体90_99`，100 级继续优先使用 `GCD.单体100`
- 90–99 级普通火末由主 GCD 冰封直接回冰；开启 TTK 时保留 los-ae 的低蓝星灵特例，冰资源完整后仍由星灵转火
- Resolver Debug JSONL Schema 2；保留 Ack/Gauge/Resolver/Pending，不再记录旧 Transition、Follow-up 或 Shadow

## 当前范围

当前自动循环开放 **90–100 级标准单体**，两个等级段均已通过自动化与真机 Gate。以下功能仍未进入生产范围：

- 两目标及三目标以上 AOE
- 89 级及以下同步循环
- 起手、时间轴与特殊固定序列
- 完整多目标 DOT、药水和副本/目标施法扩展事实

魔泉已属于标准 OffGCD Resolver，不再存在独立 `ManafontPlan`。只有未来确实需要固定多步承诺的特殊循环，才允许进入独立序列层。

## Resolver 目录

- `BLM/Resolvers/GCD/通用/`：TTK、强制恢复、雷、异言、双 DOT 和瞬发触发等通用 GCD，每项按中文技能或职责独立成文件。
- `BLM/Resolvers/GCD/单体/`：`单体100.cs`、`单体90_99.cs` 等等级段主循环；后续等级段只新增对应文件。
- `BLM/Resolvers/能力技/`：星灵移位、即刻、三连咏唱、魔泉、黑魔纹等能力技，每个技能一个中文文件。
- `BLM/Resolvers/共享/`：只保存 Resolver 读取的事实原语，不放技能顺序。
- `BLM/Resolvers/引擎/`：只保存 Manifest、等级路由和三通道 `DecisionFrame` 组装，不放单个技能条件。

拆分保留原 namespace 和既有类型名，并使用 `partial static class` 组合实现，因此没有改变现有公共 API。普通技能问题应定位到对应中文文件；跨等级共用能力技不会在每个等级段重复实现。

## 构建与测试

```powershell
dotnet build .\Los.csproj -c Release -p:PromeRotationDir=C:\Users\Administrator\AppData\Roaming\XIVLauncherCN\installedPlugins\PromeRotation\1.5.5.2 -p:TreatWarningsAsErrors=true
dotnet build .\Tests\Los.Tests.csproj -c Release -p:PromeRotationDir=C:\Users\Administrator\AppData\Roaming\XIVLauncherCN\installedPlugins\PromeRotation\1.5.5.2 -p:TreatWarningsAsErrors=true
dotnet run --project .\Tests\Los.Tests.csproj -c Release --no-build -p:PromeRotationDir=C:\Users\Administrator\AppData\Roaming\XIVLauncherCN\installedPlugins\PromeRotation\1.5.5.2
```

当前验证使用 PromeRotation `1.5.5.2`。`PromeRotationDir` 与 `DalamudHooksDir` 均可通过 MSBuild 属性覆盖。

输出文件：

```text
bin\Release\net10.0-windows\Los.dll
```

把 `Los.dll` 放入 PromeRotation 配置目录下的 `ACR\Los\`。目录名必须与 `[RotationMetadata]` 的作者 `Los` 一致。

Los 设置保存在：

```text
<PromeRotation 配置目录>\Settings\ACRConfig\Los\LosSettings.json
```

PR 运行时 `QuickToggles` 是当前会话事实源。Los 只在检测到变化时把 QT 镜像到 JSON，并在下次加载时恢复。

## 下一步

4A 真机 Gate 已关闭。下一阶段 4B 将一次迁入 `单体72_89.cs`、`单体60_71.cs`、`单体35_59.cs` 和 `单体1_34.cs`，并在共享能力技文件中一次补全低等级单体分支。AOE 及其能力技协调层保持后置，不能与 4B 同时激活。最终真机证据见 `D:\ACR\Los重构进度\16_第四阶段4A_真机Gate已完成.md`。
