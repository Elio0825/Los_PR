# 即刻进冰在窄窗口内未能派发

- 状态：已记录，暂不修复
- 发现日期：2026-08-06
- 影响范围：100级黑魔单体循环，绝望后的星灵移位路线
- 关联切片：`docs/known-issues/logs/2026-08-06-swiftcast-late-weave.jsonl`

## 现象

少数绝望循环中，星灵移位已经收到服务器 ActionEffect，但即刻咏唱没有实际派发，随后冰封（冰三）被交付并硬读。

## 日志证据

### 窗口 A（本地时间 22:37:27）

事件序列为：

`绝望 ActionEffect -> 异言 ActionEffect -> 星灵移位 Dispatch/ActionEffect -> 悖论 ActionEffect -> 即刻候选但未交付 -> 冰封 Dispatch -> 冰封 ActionEffect`

即刻候选出现时：

- `gcdRemainSeconds=0.449`
- `deliverableActionId=0`
- `remainingWeaves=2`
- `highPriorityQueueActive=false`
- `isCasting=false`

冰封 ActionEffect 到达时 `isCasting=true`，说明冰封实际走了读条。

### 窗口 B（本地时间 23:23:47）

事件序列为：

`绝望 ActionEffect -> 异言 ActionEffect -> 耀星 -> 星灵移位 Dispatch/ActionEffect -> 悖论 ActionEffect -> 即刻候选但未交付 -> 冰封 Dispatch -> 冰封 ActionEffect`

即刻候选出现时：

- `gcdRemainSeconds=0.400`
- `deliverableActionId=0`
- `remainingWeaves=2`
- `highPriorityQueueActive=false`
- `isCasting=false`

冰封 ActionEffect 到达时同样是 `isCasting=true`。

完整的最小事件切片见关联 JSONL 文件。原始日志位置为：

`C:\Users\Administrator\AppData\Roaming\XIVLauncherCN\pluginConfigs\PromeRotation\Settings\ACRConfig\Los\DebugLogs`

## 结论

这两个窗口不是手动 Hotkey 抢占插槽：

- LOS JSONL 中高优先队列始终为 `false`，并且剩余插入槽为 `2`。
- `C:\Users\Administrator\AppData\Roaming\XIVLauncherCN\dalamud.log` 在两个窗口附近没有新的 `Los Hotkey` 触发。
- 最近一次即刻 ActionEffect 到即刻候选出现约 39.4 秒，符合即刻约 40 秒复唱刚好在 GCD 尾部转好的特征。

真正的问题是：星灵路线使用了“即刻会在未来一个 GCD 内转好”的预测，但没有保证冷却转好后仍剩余至少 0.6 秒的安全能力技窗口。即刻在 GCD 剩余 0.4～0.45 秒时才成为候选，执行器的 `CanWeaveNow` 会拒绝派发，于是下一个冰封被硬读。

## 后续修复方向

1. 星灵/转冰保障应判断“冷却转好时仍有可用插入窗口”，不能只判断未来一个 GCD 内会转好。
2. 只有已有即刻/三连 buff，或即刻能在安全窗口内转好时，才允许依赖该保障进入星灵路线。
3. 增加回归测试：模拟即刻在 GCD 剩余 `0.40s`、`0.45s` 和 `0.61s` 转好，前两种不得把星灵路线当成可安全转冰，后者允许派发。

