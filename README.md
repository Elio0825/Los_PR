# Los 黑魔法师循环

Los 是面向 PromeRotation 的《最终幻想 XIV》黑魔法师 ACR，实现多等级单体/AOE 循环、分级起手、QT 与 Hotkey 面板、时间轴接入和中文诊断日志。

> 本项目是第三方战斗辅助实现，与 Square Enix、Dalamud 或 PromeRotation 官方无关。使用前请自行了解服务器规则和相关风险。

## 当前功能

- 1–100 级单体循环和分级路由
- 多等级 AOE 循环及共享能力技协调
- 70、80、90、100 级标准 5+7 起手
- 100 级核爆起手
- 日常模式：无倒计时，进战后执行 5+7，不使用爆发药
- 高难模式：支持倒计时预读，可按设置使用爆发药
- 起手被手动技能打断后按检查点恢复，完整执行到魔泉后交回常规 ACR
- QT、Hotkey 独立面板及单项显示控制
- 键盘单键、组合键和鼠标侧键绑定
- 时间轴控制 QT、Hotkey，并检测火冰状态、层数、火苗、冰针、悖论和通晓资源
- 概览、战斗、风格、热键、Debug 五页控制台
- 中文可读 Debug 日志，同时保留机器可分析的 JSONL
- 配置、QT 状态、窗口位置和缩放持久化

“危险循环”目前仅保留界面与风险确认项，尚未接入实际战斗逻辑。

## 设计边界

循环由统一 Resolver 引擎生成同一帧的 GCD、Always 和 OffGCD 候选，并通过 Tracker 对 ActionEffect、Gauge 和 Pending 状态进行对账。普通循环、起手、Hotkey 和时间轴各自拥有明确的执行边界，避免手动技能或高优先队列破坏固定起手顺序。

爆发药保留两条不同的底层投递协议：

- Hotkey 沿用 PR 物品编码和 Framework Tick 重试
- 起手使用独立的 HQ/NQ 归一化、队列和冷却确认

两条路径不可直接合并。

## 环境要求

- Windows
- .NET 10 SDK
- PromeRotation `1.5.6.1` 或更高兼容版本（运行时）

`Los.csproj` 使用 `PromeRotation.SDK.API15` NuGet 包提供编译期引用，因此普通构建不依赖本机 XIVLauncherCN 安装目录。本地测试仍使用真实的 PromeRotation 与 Dalamud DLL，可通过 MSBuild 属性指定路径：

```powershell
-p:PromeRotationDir=<PromeRotation 插件目录>
-p:DalamudHooksDir=<Dalamud Hooks 目录>
```

## 构建与测试

```powershell
dotnet build .\Los.csproj -c Release `
  -p:TreatWarningsAsErrors=true

dotnet build .\Tests\Los.Tests.csproj -c Release `
  -p:PromeRotationDir=<PromeRotation 插件目录> `
  -p:DalamudHooksDir=<Dalamud Hooks 目录> `
  -p:TreatWarningsAsErrors=true

dotnet run --project .\Tests\Los.Tests.csproj -c Release --no-build `
  -p:PromeRotationDir=<PromeRotation 插件目录> `
  -p:DalamudHooksDir=<Dalamud Hooks 目录>
```

当前测试覆盖 18 个测试组，包括多等级单体/AOE Resolver、Tracker 生命周期、新日志 ActionEffect 接入、起手恢复、时间轴、爆发药 Hotkey、快捷键持久化、Debug 日志和 DLL 公共 API 边界。

## 自动发布

推送 `v*` 标签，或在 GitHub Actions 中手动运行 `Build and Release` 并填写版本号，即可自动完成 API15 SDK 恢复、Release 构建、`Los.zip` 和 `repo.json` 生成、SHA-256 计算与 GitHub Release 发布：

```powershell
git tag v0.1.4
git push origin v0.1.4
```

自动发布不会打包 PromeRotation、Dalamud 或 ECommons，仅包含运行所需的 `Los.dll` 与 `Los.deps.json`。

生成 PR 下载中心所需的 `Los.zip` 和 `repo.json`：

```powershell
.\scripts\New-AcrRelease.ps1 `
  -PromeRotationDir <PromeRotation 插件目录> `
  -DalamudHooksDir <Dalamud Hooks 目录> `
  -Version 0.1.4
```

构建输出：

```text
bin\Release\net10.0-windows\Los.dll
```

## 安装

将 `Los.dll` 放入 PromeRotation 配置目录下的：

```text
ACR\Los\
```

目录名需要与 `RotationMetadata` 中的作者名 `Los` 一致。设置文件保存在：

```text
<PromeRotation 配置目录>\Settings\ACRConfig\Los\LosSettings.json
```

Debug 日志位于同一配置目录下的 `DebugLogs` 文件夹。

## 项目结构

```text
BLM/Core/          状态、技能与 Tracker
BLM/Resolvers/     单体、AOE、能力技和统一决策引擎
BLM/Openers/       分级起手定义与执行器
BLM/Timeline/      PR 时间轴动作和条件节点
BLM/UI/            控制台、QT、Hotkey 与主题
BLM/Diagnostics/   中文日志、JSONL 与诊断模型
Tests/             无外部测试框架的回归测试程序
```

## 开源许可

本项目采用 [MIT License](LICENSE) 开源。
