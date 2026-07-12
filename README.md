# Los 黑魔 PR

当前已完成 PromeRotation 外置 ACR 的 Step 3：独立控制台、事实层、事件确认式 Tracker、100 级标准单体纯决策、PR 三入口调度、Transition 与移动绝望 Follow-up 均已接入。ACR 已会向 PR 返回真实 `PAction`。

## 已实现

- 独立 `960 x 660` 控制台，最小尺寸 `760 x 520`
- 概览、作战、控制、系统四页签，以及 ACR On / Hold / Off 控制
- 黑魔技能、Buff、等级解锁、雷 DoT、MP、AF/UI、冰晶、通晓与 Astral Soul 事实
- PR QT 开关、预设、Los JSON 持久化、UI 缩放和窗口状态记忆
- ActionEffect pre-gauge Ack 与下一 Framework Tick Gauge 对账
- `CombatSerial`、`StateGeneration`、阶段 serial、历史可信度及生命周期隔离
- 100 级标准单体 FireBudget、Strategy、Override 与移动读条安全判断
- `NextGcd`、`NextOffGcd`、`NextAlways` 到 PR `PAction` 的真实映射
- Existing Firestarter、AF1 Paradox 赤字恢复和单体 FireToIce Transition
- `OffGcdOrAlways` 白名单、`0.6s` 边界和 `Serial + StepIndex` 一次性入队
- 移动绝望后的耀星 Follow-up，采用双 Ack、双 Gauge 对账
- 高优队列让位、冻结目标、丢 Ack/超时/目标变化的安全收敛

## 当前范围

当前自动循环只开放 **100 级标准单体**。以下功能仍按设计保持关闭，不能视为已实现：

- 常规 Manafont 与 Manafont Plan
- 两目标及三目标以上 AOE
- 低等级同步循环
- 起手、时间轴与实验循环

## 构建与测试

```powershell
dotnet build .\Los.csproj -c Release -p:TreatWarningsAsErrors=true
dotnet run --project .\Tests\Los.Tests.csproj -c Release
```

默认引用本机 PR `1.5.2.3` 和当前 Dalamud Hooks。其他机器可通过 `PromeRotationDir`、`DalamudHooksDir` MSBuild 属性覆盖路径。

输出文件：

```text
bin\Release\net10.0-windows\Los.dll
```

把 `Los.dll` 放入 PromeRotation 配置目录下的 `ACR\Los\`。目录名必须与 `[RotationMetadata]` 的作者 `Los` 一致。加载黑魔 ACR 后控制台默认打开；关闭后可从 PR 设置窗口中的“打开独立控制台”重新打开。

Los 设置保存在：

```text
<PromeRotation 配置目录>\Settings\ACRConfig\Los\LosSettings.json
```

PR 运行时 `QuickToggles` 是当前会话的事实源。无论状态来自 Los 控制台、PR 原生 QT 窗、命令还是时间轴，Los 都只在检测到变化时把结果镜像到上述 JSON，并在下次加载时恢复。

## 下一阶段

先完成 Step 3 真机木桩 Gate，再进入 Step 4 常规 Manafont：冻结 `ModeAtRequest`，在 Manafont Ack 后用下一 Tick 资源事实激活独立计划，并保持单体/AOE 预算隔离。详细进度与验证记录位于 `D:\ACR\Los重构进度`。
