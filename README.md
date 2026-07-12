# Los 黑魔 PR

方案 C 第三阶段 3B 已完成自动化实现：100 级标准单体 Resolver 已接管 `NextGcd`、`NextOffGcd`、`NextAlways` 三个生产入口，旧 Transaction/Follow-up 常规技能图已删除。当前版本等待真机 Gate，不能把自动化通过等同于实战验收。

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
- 常规魔泉作为普通 OffGCD Resolver，Ack 后以 Gauge 资源恢复事实完成对账，并从魔泉后火四计数重新续火
- Resolver Debug JSONL Schema 2；保留 Ack/Gauge/Resolver/Pending，不再记录旧 Transition、Follow-up 或 Shadow

## 当前范围

当前自动循环只开放 **100 级标准单体**。以下功能仍未进入生产范围：

- 两目标及三目标以上 AOE
- 低等级同步循环
- 起手、时间轴与特殊固定序列
- 完整多目标 DOT、药水和副本/目标施法扩展事实

魔泉已属于标准 OffGCD Resolver，不再存在独立 `ManafontPlan`。只有未来确实需要固定多步承诺的特殊循环，才允许进入独立序列层。

## 构建与测试

```powershell
dotnet build .\Los.csproj -c Release -p:PromeRotationDir=C:\Users\Administrator\AppData\Roaming\XIVLauncherCN\installedPlugins\PromeRotation\1.5.4.9 -p:TreatWarningsAsErrors=true
dotnet build .\Tests\Los.Tests.csproj -c Release -p:PromeRotationDir=C:\Users\Administrator\AppData\Roaming\XIVLauncherCN\installedPlugins\PromeRotation\1.5.4.9 -p:TreatWarningsAsErrors=true
dotnet run --project .\Tests\Los.Tests.csproj -c Release --no-build -p:PromeRotationDir=C:\Users\Administrator\AppData\Roaming\XIVLauncherCN\installedPlugins\PromeRotation\1.5.4.9
```

当前验证使用 PromeRotation `1.5.4.9`。`PromeRotationDir` 与 `DalamudHooksDir` 均可通过 MSBuild 属性覆盖。

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

先执行 3B 真机 Gate，重点确认标准循环、火末星灵即刻转冰、异常接管与魔泉实际释放。通过后再进入第四阶段，增量实现低等级与 AOE；起手和特殊序列继续后置。详细变更与验收范围见 `D:\ACR\Los重构进度\12_第三阶段3B_生产切换与事务图删除_待真机Gate.md`。
