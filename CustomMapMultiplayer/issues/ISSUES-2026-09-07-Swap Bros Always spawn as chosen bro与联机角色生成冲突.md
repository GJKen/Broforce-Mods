# ISSUES-2026-09-07 Swap Bros Always spawn as chosen bro 与联机角色生成冲突

## 状态

已验证。Swap Bros 2.1.5 启用 `Always spawn as chosen bro` 后，离线和 Steam 联机房间均能按当前锁定角色生成。

## 现象与根因

启用 `CustomMapMultiplayer` 后，Swap Bros 的锁定角色在联机生成流程中可能被原生角色参数覆盖，导致首次生成、重生或手动换人出现随机角色。

根因是两个 Mod 都修改 `Player.SpawnHero` 的 Harmony Prefix：Swap Bros 使用当前选角，CMM 还会处理联机角色回复、重入和本地生成参数。原有兼容层错误地从 `Swap_Bros_Mod.API` 查找选角方法，并且在线状态判断会在真实联机房间中提前跳过查询。

## 最终实现

- 保留一个 CMM 的 Swap Bros 兼容 Prefix，执行顺序位于 Swap Bros Prefix 之后；没有新增重复的 `Player.SpawnHero` Prefix。
- 在 [`src/SwapBrosCompatibility.cs`](../src/SwapBrosCompatibility.cs) 中按真实运行时声明解析：
  - `Swap_Bros_Mod.Main.GetSelectedBroHeroType(Int32): HeroType`
  - `Swap_Bros_Mod.Main.settings: Swap_Bros_Mod.Settings`
  - `Swap_Bros_Mod.Settings.alwaysChosen: Boolean`
  - `Swap_Bros_Mod.Settings.ignoreForcedBros: Boolean`
- 保留 `SWAP_BROS_PREFIX_ENTRY/QUERY/EXIT` 诊断，并记录联机状态、选角查询结果和最终参数。
- 选角不可用、Swap Bros 未启用或 `alwaysChosen=false` 时保持原生生成；`ignoreForcedBros=false` 时地图强制角色优先。
- [`src/HarmonyDiagnostics.Reflection.cs`](../src/HarmonyDiagnostics.Reflection.cs) 记录 `Connect.IsOffline`、连接层、房间和房间就绪状态，用于区分离线与真实联机房间。

## 验证

当前 Release 构建已完成部署，最新联机会话为 `auto-20260907-032017-950-f40321dd`。启动日志确认 Swap Bros 2.1.5 的程序集、`Main` 类型、选角方法和设置字段均解析成功。联机日志在选角状态完成后记录了查询成功、Prefix 后的角色参数，以及最终 `InstantiateHero/RegisterHeroToPlayer` 使用锁定角色；用户同时确认离线模式和 Steam 联机房间测试通过。

后续若修改 `Player.SpawnHero`、联机角色回复、Swap Bros 版本或选角字段，需要重新执行离线、首次生成、死亡重生、`TriggerSwapBro` 和晚加入回归。
