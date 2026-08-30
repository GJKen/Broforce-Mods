# Workshop 联机酸液池导致双方一起死亡

## 状态

**已修复并通过双端实机验收（2026-08-30）。**

Workshop 地图中，一名英雄接触酸液时，另一名留在出生区的英雄不会再被错误带入死亡链。房主和加入方分别进入酸液时，也都能正常死亡。

## 根因

旧实现只转译 `TestVanDammeAnim.CheckForTraps` 中的酸液调用，但 `CalculateMovement` 和 `Damage` 还会直接调用 `CoverInAcid`，因此可以绕过原有的房主权威包装。失败日志中的玩家槽位、`requestedPlayerNum` 和英雄 NID 始终各自对应，问题不是槽位或 NID 串号。

另外，`Map.GetNearestAcid(...)` 在这张 Workshop 地图中即使场景存在有效 `DoodadAcidPool` 也会返回 `null`，不能作为权威酸液判定。

## 正式修复

实现位于 [`src/HarmonyDiagnostics.Acid.cs`](../src/HarmonyDiagnostics.Acid.cs)：

- 在 `TestVanDammeAnim.CoverInAcid` 基入口统一拦截 Workshop 在线英雄，覆盖所有原生调用路径。
- 维护场景级 `DoodadAcidPool` 列表，检查 `fluidType=Acid`、`fullness > 0.2`，使用横向 4、纵向 `-2.5..10` 的角色范围；对象列表约每秒刷新，英雄结果缓存 50ms。
- Host 以约 10Hz 扫描本机和远程英雄；地图判定通过后按英雄 NID 广播，`CoverInAcid` 入口仍保留即时检查兜底。
- 加入方本机命中后立即调用原生 `CoverInAcidRPC`，同时请求 Host 确认；远程镜像只等待 Host 授权应用。
- 请求、广播和应用按 NID 限流并用 `hasBeenCoverInAcid` 去重，Host 应用前再次校验地图酸液状态。
- 离线、普通官方联机、非配置场景和非英雄对象继续执行原生行为。

## 验收结果

测试场景：`Test Evan2 / Bromandy_Ptr1 / levelIndex=7`，双端使用同一 Workshop 地图和构建。

- 仅 P1 接触酸液：P1 正常死亡，出生区 P2 保持存活。
- 仅 P2 接触酸液：P2 正常死亡，P1 不会被连带杀死。
- 房主和加入方分别作为接触者测试均通过。
- 加入方本地预测生效后，死亡体感延迟明显降低；Host 仍执行权威校验和同步。
- 双端日志中的 `CoverInAcidRPC`、`PlayerHasDiedRPC` 和 `LEVEL_OUTCOME` 均对应正确的玩家 NID，未出现双方一起死亡的错误链路。

临时 MCP 热补丁仅用于定位和复测，已随游戏进程停止而失效，不属于正式构建。
