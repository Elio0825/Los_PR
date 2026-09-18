# LOS 台服适配与发布

台服构建目标为 Dalamud API13、.NET 9，使用 `PromeRotation.SDK.TC` `0.1.0-preview.2`，对应 PR 编译引用 `1.3.1.4`。国服默认构建继续使用 API15 和 .NET 10。

## 范围

- 一份源码编译两个客户端版本，Resolver、起手、热键和 Boss 上天逻辑共用。
- 仅处理编译接口、平台标识、目录和发布方式的差异。
- 按用户要求跳过两服黑魔技能机制对比，沿用现有循环规则。
- API12 不属于这次台服发布目标。

## 标识与输出

| 项目 | 国服 | 台服 |
| --- | --- | --- |
| `ClientRegion` | `CN`（默认） | `TC` |
| 作者／配置标识 | `Los` | `Los-TC` |
| 程序集 | `Los.dll` | `Los.TC.dll` |
| Release 输出 | `bin/Release/net10.0-windows/` | `bin/TC/Release/net9.0-windows/` |
| API | 15 | 13 |
| 发布仓库 | `Elio0825/Los_PR` | `Elio0825/Los_PR-TC` |

两个客户端各有独立的中间文件目录，避免 restore 结果和生成文件互相污染。国服原有命令不需要增加参数。

## 构建

```powershell
dotnet build Los.csproj -c Release -p:TreatWarningsAsErrors=true
dotnet build Los.csproj -c Release -p:ClientRegion=TC -p:TreatWarningsAsErrors=true
```

台服运行测试需使用台服组件，不能用本机国服 `PromeRotation.dll` 或 Dalamud DLL 冒充台服依赖。使用 SDK 内程序集进行的测试须标明为离线测试，不代表游戏内验证。

没有台服安装时，可运行构建、离线测试与打包：

```powershell
.\scripts\New-AcrRelease.ps1 -ClientRegion TC -Version 1.0.0 -SdkOfflineTests
```

离线依赖只写入 `artifacts/test-dependencies/TC/`，使用 SDK 中的程序集，并补齐官方 NuGet 的 Serilog 4.0.0 和 Lumina 6.7.0。补充包经过固定 SHA-512 校验；这是测试夹具，不修改产品 SDK，也不进入发布 ZIP。TC SDK 的 Lumina.Excel 引用 Lumina 6.0.0.0，但 SDK 自带 Lumina 程序集版本为 0.0.0.0，因此离线表行测试使用兼容的官方 Lumina 6.7.0（程序集版本 6.0.0.0）。

## 发布来源

主源码仓库负责维护源码、构建脚本和可复用台服工作流；台服仓库只维护发布入口、版本和源码来源记录。

台服工作流接受数字版本号和主仓库完整提交 SHA，以该 SHA 检出源码并编译。台服发布标签属于台服仓库，发布说明同时记录主仓库源码 SHA，避免把发布仓库提交与实际编译源码混淆。

发布操作：

1. 在主仓库提交需要发布的代码，确保 `BLM/BlackMageRotation.cs` 的 `RotationMetadata` 版本与发布版本一致，并推送提交。
2. 在台服仓库 Actions 运行 `Release LOS TC`，输入版本号和上述主仓库完整 40 位提交 SHA。
3. 工作流编译台服、运行全部离线测试、验证清单与压缩包，再创建台服 Release。源码不干净、版本不匹配、测试失败或同版本已发布时均拒绝发布。

台服仓库入口固定可复用工作流的提交，更新构建流程时需显式更新该引用；`source_commit` 单独选择本次编译的源码。台服工作流不临时修改源码版本，因此 `build-info.json` 能对应干净的已提交源码。

发布由台服仓库自己的 `GITHUB_TOKEN` 完成，只授予该仓库 `contents: write`，不需要跨仓库个人令牌。源码仓库公开可读。

发布包只包含 `Los.TC.dll` 和 `Los.TC.deps.json`。`repo.json` 使用 `author=Los-TC`、`apiVersion=13`，下载地址指向台服仓库，源码地址指向 `Los_PR`；SHA-256 必须与该次实际上传的压缩包一致。

## 更新入口

- 国服：<https://github.com/Elio0825/Los_PR/releases/latest/download/repo.json>
- 台服：<https://github.com/Elio0825/Los_PR-TC/releases/latest/download/repo.json>

台服首版沿用 `v1.0.0`。国服已发布的 `v1.0.0` 标签和附件不覆盖；后续两个客户端可以独立发布，源码仍集中维护。

## 验证边界

发布前检查双环境构建、回归测试、DLL 作者标识、实际目标框架、依赖清单、压缩包文件列表和 SHA-256。游戏内还需确认加载、倒计时、热键回执和战斗生命周期；此类实机结果须单独记录，不由编译结果推断。

2026-09-19 本机结果：CN 与 TC 的 Release 构建均为 0 警告、0 错误，各通过 20 组测试。国服使用真实安装的 PR 1.5.10.3 / Dalamud 15.0.3.5；台服使用上述 SDK 离线夹具。新增测试覆盖平台元数据、配置隔离、程序集与框架、公共类型边界，以及真实 Lumina 表行的 4/8/24 人队伍组成映射。两服打包脚本均已通过；台服游戏内验证待完成。
