# Custom Map Multiplayer：诊断日志

[返回开发文档索引](DEVELOPMENT.md) · [测试与验收](TESTING.md)

## 日志目录

```text
%USERPROFILE%\AppData\LocalLow\Free Lives\Broforce\CustomMapMultiplayer\
```

远端测试参与者应从自己的 Windows 用户数据目录导出日志；公开文档不记录内网地址、共享路径或用户名：

```text
<远端用户目录>\AppData\LocalLow\Free Lives\Broforce\CustomMapMultiplayer
```

该目录是 Windows 下实际诊断日志目录，UMM 的“打开诊断日志目录”按钮、日志写入和启动日志中的 `Diagnostic log directory` 使用同一路径；不要在 UMM DLL 部署目录中查找诊断日志。

## 会话文件

插件加载时创建启动日志；`SteamLayer` 或 `FrpDirectLayer` 的 `CreateMatch`/`JoinLobby` 创建新会话。每个会话有普通事件日志和 Harmony 追踪日志：

```text
diagnostics-host-<session>-<utc-time>.log
diagnostics-host-<session>-<utc-time>.trace.log
```

普通日志约每 750ms 刷新，警告、错误和会话结束时立即刷新。诊断日志预设包括基础、加入/重新加入、AFK/失败、Workshop 和完整；九类设置只过滤诊断输出，不关闭补丁或改变游戏行为。无论类别如何选择，`BUILD_INFO`、`SESSION_BEGIN`、`SESSION_END`、`DIAGNOSTIC_CATEGORIES`、Warning、Error 和 Unity 异常始终保留。

`LEVEL_OUTCOME`、AFK 和 Dropout 事件同时写入普通日志和 trace；`WORKSHOP_GAME_MODE_COMPARE`、`OPTIONAL_BRO_MOD` 写普通日志。`OPTIONAL_BRO_MOD` 在启用诊断和每个会话开始时各采集一次，分析网络问题时使用会话中的第二次快照。

## 诊断性能

Harmony Trace 关闭时会在构造追踪消息前直接短路，跳过参数格式化和反射读取；开启时复用方法参数、方法描述、字段和属性的缓存。连接层的 `Connect` 类型、`Layer`/`IsHost`/`IsOffline` 属性以及 `ConnectionLayer.Room` 访问也使用缓存，减少高频状态检查的反射开销。上述缓存只影响诊断路径，不改变功能性 Hook、RPC 或授权行为。

标准构建把源码、引用、编译器目标和配置组成清单，计算 SHA-256 `buildHash` 并嵌入 DLL；未通过标准脚本的构建记录 `UNBUILT`。

## 日志约束

- 不直接追踪 `Update`、`RunHeroRespawnLogic` 等每帧方法；改用低频下游事件。
- 重复事件按方法、参数和状态限频；恢复记录时报告抑制数量。
- 高频实体终态和酸液观察事件仍写入普通诊断日志，但不再同步调用 Unity `Debug.Log`，避免密集战斗事件把诊断输出开销叠加到游戏帧中；Warning、Error 和 trace 行为保持不变。
- 新增追踪后先检查本机增长速度；持续每秒多行时先修复限频。
- 写入前清洗未配对 UTF-16 代理项。
- 不自动限制大小或删除旧日志；测试后按会话自行清理。

## 关联文档

- 功能日志字段见 [AFK 行为](AFK.md) 和 [Workshop 与游戏状态](WORKSHOP.md)。
- 测试取证流程见 [测试与验收](TESTING.md)。
