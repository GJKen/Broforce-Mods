# Test_Evan2 原生地图对象与投掷物清理空引用

## 状态

| 项目 | 状态 |
| --- | --- |
| 双端异常证据 | 原始问题已在同一 Workshop 会话的 Host/Client 日志中确认；`5582c...` 回归未再出现旧异常 |
| Host 极端卡顿关联 | 最新回归确认重载窗口是主要停顿来源，酸池扫描是独立性能热点 |
| `TorturedVillager` 空对象 | 已定位为男女村民 prefab 单侧缺失，回退保护已在双端回归中命中 |
| `Map.RemoveProjectile` 空引用 | 已定位为 Map 与 Projectile 销毁顺序，空列表保护已在双端回归中命中 |
| Client `DoodadCrate` 空引用 | 已定位到初始化过渡期及缺失 `pickup` 的半初始化状态；最新回归未复现，保护分支尚未直接命中 |
| 当前源码修复 | 已实现、构建、部署并完成一次双端回归 |
| 目标异常验收 | 通过（箱体保护分支未直接命中） |

本 issue 记录 `Test_Evan2` 联机中原生地图对象加载和投掷物清理异常。它与 Host 低帧率问题有直接时间关联，但不把异常直接归因于 `CustomMapMultiplayer` 的酸液、实体终态或 Trace 热路径。

## 复现会话

- 日期：2026-08-31（日志时间为 UTC 的 2026-08-30 16:15 至 16:31）。
- 传输：官方 Steam Lobby/Steam P2P。
- 地图：`Test_Evan2`。
- Workshop ID：`3715087178`。
- Host 会话：`auto-20260830-161541-404-bea3dc7b`。
- Client 会话：`auto-20260830-161555-676-4e00f435`。
- 双端 `buildHash`：`1e424ad4eca60c5112057233c054225665ea79cb4c253e4d0ac5efeb80a0b7e9`。
- Host 日志文件：`diagnostics-host-auto-20260830-161541-404-bea3dc7b-20260830-161541-404.log`。
- Client 日志文件：`diagnostics-client-auto-20260830-161555-676-4e00f435-20260830-161555-676.log`。

## 用户可见现象

用户在地图中游玩数分钟时，Host 出现数次明显停顿；停顿并非持续存在。Client 也记录到地图加载和箱体处理错误，但后半段双方的性能统计恢复稳定。

## 性能证据

`PERF_SUMMARY` 每约 2 秒聚合一次帧时间。按 `scene=Test_Evan2` 汇总当前日志：

| 端 | 统计窗口 | 帧数 | 加权平均帧耗时 | 窗口最大 p95 | 窗口最大 p99 |
| --- | ---: | ---: | ---: | ---: | ---: |
| Host | 499 | 93,207 | 10.760 ms | 221 ms | 250 ms（直方图上限） |
| Client | 496 | 114,662 | 8.678 ms | 170 ms | 250 ms（直方图上限） |

Host 的极端窗口如下：

| UTC 时间 | 窗口平均 | p95 | p99 | 酸液权威路径总耗时 | 实体提交总耗时 |
| --- | ---: | ---: | ---: | ---: | ---: |
| `16:16:06.016` | 142.827 ms | 179 ms | 250 ms | 0.405 ms | 0.015 ms |
| `16:17:48.844` | 43.592 ms | 212 ms | 250 ms | 1.830 ms | 0.025 ms |
| `16:19:29.326` | 46.615 ms | 213 ms | 250 ms | 1.692 ms | 0.729 ms |
| `16:20:09.431` | 44.566 ms | 221 ms | 250 ms | 1.976 ms | 0.028 ms |

上述窗口内没有与 200 ms 卡顿相匹配的 Mod 热路径耗时。酸池刷新在其它普通窗口中最高约 17 ms，可能增加 Host 基线负担，但不能单独解释这些极端停顿。`16:20:09` 之后 Host 的尾部窗口恢复到约 10 ms 平均、14 至 15 ms p95；Client 尾部约 8.2 ms 平均、9 至 11 ms p95。

## 异常调用链

### Host：空对象实例化

多次出现以下原生 Unity 异常：

```text
ArgumentException: The Object you want to instantiate is null.
UnityEngine.Object.Instantiate[Villager] (.Villager original)
TorturedVillager.Awake ()
UnityEngine.Object:Instantiate(Doodad, Vector3, Quaternion)
MonoMod.Utils.DynamicMethodDefinition:Map.PlaceDoodad_Patch2(Map, DoodadInfo)
Map:LoadArea()
Map:SetupBlocksCoroutine()
```

Host 首次记录时间为 `16:16:03.824Z`，之后在 `16:16:36`、`16:17:48`、`16:19:28` 和 `16:20:07` 附近重复出现。首次和后续异常都靠近高 p95 窗口或地图区域加载。

### Host：投掷物销毁清理

```text
NullReferenceException: Object reference not set to an instance of an object
Map.RemoveProjectile (.Projectile projectile)
Projectile.DeregisterProjectile ()
Projectile.OnDestroy ()
```

该异常在 `16:19:27.794Z` 和 `16:20:07.800Z` 出现，后一条之后未再观察到新的 Unity 异常，而性能窗口恢复稳定。

### Client：相同对象异常及箱体异常

Client 同样出现 `TorturedVillager.Awake` 的空对象实例化异常，并额外记录：

```text
NullReferenceException: Object reference not set to an instance of an object
DoodadCrate.SetupBlockAtStart ()
FallingBlock.Update ()
BoulderBlock.Update ()
```

以及：

```text
NullReferenceException: Object reference not set to an instance of an object
DoodadCrate.DestroyBlockInternal (Boolean CollapseBlocksAround)
Block.ActuallyCollapse (...)
DoodadCrate.ActuallyCollapse_Patch2 (...)
```

Client 最后一条相关异常约在 `16:19:27.332Z`；之后的性能窗口保持在约 8 至 11 ms p95。

## 静态定位结论

1. `TorturedVillager.Awake` 只会在随机选中的 `maleVillagerPrefab` 或 `femaleVillagerPrefab` 为空时于 `Instantiate<Villager>` 抛出当前异常。双端多轮加载呈一致的失败/成功交替，说明 captured-villager Doodad 本身有效、其男女村民引用中恰有一个缺失。
2. `Map.OnDestroy` 会先把静态 `projectiles` 和 `damageableProjectiles` 列表设为 `null`，随后场景中的 `Projectile.OnDestroy` 仍调用 `Map.RemoveProjectile`。双端异常均紧随 `LoadNextScene/LoadSceneCore`，确认是原生销毁顺序问题，不是 Host 专属问题。
3. `DoodadCrate.DestroyBlockInternal` 在奖励标志为真时无条件调用 `pickup.Launch`；若初始化未完成且 `pickup` 为空，异常会发生在原生 `Block.DestroyBlockInternal` 提交 `destroyed` 状态之前，解释了后续重复坍塌特效和顺序重试。
4. `Map.PlaceDoodad_Patch2` 来自 Utility Mod 的两个 Prefix；双端当前 `disableEnemySpawn=false`、`maxCageSpawns=false`，均直接放行，且 captured villager 不属于改写目标。`DoodadCrate.ActuallyCollapse_Patch2` 的现有保护只防同栈递归，不覆盖本次顺序重试。
5. 极端帧时间窗口同时包含完整场景重载。当前更准确的边界是：重载是主要停顿来源，`TorturedVillager` 和 `Projectile` 分别是加载、卸载阶段的伴随错误，不能把整段 200 ms 停顿归因于异常本身。

## 已实现修复

- `Map.RemoveProjectile`：仅当任一静态列表已为空时接管调用，对仍存在的列表执行移除并跳过原生空引用；正常游戏路径继续执行原方法。
- `TorturedVillager.Awake`：仅限有效 Workshop 联机会话；男女 prefab 恰有一个为空时用现有非空引用补齐，保留原生随机选择和后续初始化。
- `DoodadCrate.SetupBlockAtStart`：仅限有效 Workshop 联机会话；Map、PickupableController 或对象所属 Map 尚未就绪时延后一帧重试。
- `DoodadCrate.DestroyBlockInternal`：奖励标志有效但 `pickup` 缺失时清除无效奖励状态并继续原生销毁，使 `destroyed` 状态能够提交。
- 未修改网络协议、RPC 顺序、关卡推进或完整 `Assembly-CSharp.dll`。
- Release 构建及双端部署完成：`buildHash=5582c884d77196a8c222ae957a670043caae125ca885740bba53476204ef3ccf`。

## 最新双端回归（2026-08-31）

- 日志时间：UTC `2026-08-30 17:12:58` 至 `17:18:09`（本地时间 2026-08-31 01:12 至 01:18）。
- 地图：`Test_Evan2`；Workshop ID：`3715087178`；传输：官方 Steam Lobby/Steam P2P。
- Host 会话：`auto-20260830-171258-046-96c5ca4f`；Client 会话：`auto-20260830-171307-980-7423ce0f`。
- Host 日志文件：`diagnostics-host-auto-20260830-171258-046-96c5ca4f-20260830-171258-046.log`。
- Client 日志文件：`diagnostics-client-auto-20260830-171307-980-7423ce0f-20260830-171307-980.log`。
- 双端 `BUILD_INFO buildHash` 一致：`5582c884d77196a8c222ae957a670043caae125ca885740bba53476204ef3ccf`。
- 双端启动日志均出现 `Native map object safety enabled; patched methods=4`。
- Host/Client 各命中一次 `WORKSHOP_OBJECT repaired captured-villager prefab fallback; missing=male`，之后没有 `TorturedVillager.Awake` 实例化异常。
- Host/Client 各命中一次 `NATIVE_MAP guarded projectile deregistration during Map teardown`，之后没有 `Map.RemoveProjectile` 空引用异常。
- 本轮没有 `DoodadCrate.SetupBlockAtStart`、`DoodadCrate.DestroyBlockInternal` 或箱体重复坍塌异常；同时没有出现对应保护告警，因此箱体的异常分支尚未直接验证。
- Host 仅出现一次独立的原生 `MissingMethodException: ... StepOn ...`（`17:16:31.210Z`）；Client 没有 Unity 异常。
- Client 出现 5 条 `ENTITY_FINAL` 死亡/尸体终态等待超时，属于独立的低概率对象同步问题；未导致崩溃或中途断线。
- 退出阶段先发生一次明确的 Host migration：Client 于 `17:18:07.906Z` 记录从 `client` 晋升为 `host`，随后出现一条 payload `role=host` 的性能摘要；外层日志标签仍为 `role=client`。该迁移、掉线等待和清理警告均与 `LeaveMatch` 同时发生，按退出阶段处理，不作为本次地图对象修复失败。

排除 `p99=250ms` 的地图重载窗口，并按摘要 payload 的 `role` 排除 Client 晋升后的 222 帧 Host 窗口后，Host 为 `23,105` 帧、加权平均 `11.284ms`、p50/p95/p99 为 `11/17/21ms`；Client 为 `29,805` 帧、加权平均 `9.149ms`、p50/p95/p99 为 `9/14/19ms`。此前 `30,027` 帧、`9.148ms` 的 Client 数字包含该 222 帧迁移后 Host 摘要，现已修正。Host 的酸池刷新共 `217` 次、累计 `2264.130ms`、单次最高 `12.984ms`，仍应在性能 issue 中单独跟踪，不能归因于本 issue 的原生空引用保护。

## 与其它 issue 的边界

- `ISSUES-2026-08-30-联机房主低帧率与Host专属扫描性能问题.md`：本 issue 提供原生异常导致卡顿的独立证据；包含这些异常的窗口不能作为纯 Mod 性能 A/B 样本，应在原 issue 中交叉引用。
- `ISSUES-2026-08-30-加入方箱子坍塌特效持续重复.md`：Client 的 `DoodadCrate` 调用链与该 issue 重叠，箱体特效循环仍由原 issue 独立跟踪；本 issue 另外覆盖 Host 的 `TorturedVillager` 和 `Projectile` 异常，因此不合并为同一个根因。
- `ISSUES-2026-08-30-Assembly-CSharp反编译与重建方案.md`：本 issue 提供优先静态检查目标，但不代表应立即进行完整 `Assembly-CSharp.dll` 重建或替换。

## 后续工作

1. 目标异常保护已完成一次双端回归；若需要补齐箱体证据，只需让 Client 实际打开或撞塌一个 `DoodadCrate` 并重开一次地图。
2. `StepOn` 原生参数异常和 `ENTITY_FINAL` 超时分别独立跟踪，不与本 issue 合并修复。
3. 暂不重建或替换完整 `Assembly-CSharp.dll`。

## 当前结论

本次已通过双端日志和当前游戏程序集的反编译源码定位三条原生空引用链，并以四个窄范围 Harmony Prefix 完成兼容保护。`5582c...` 双端回归已确认村民实例化和投掷物清理旧异常消失，箱体异常本轮未复现但其保护分支尚未直接命中。本轮性能数据仅作观察，仍与场景重载、酸池扫描和其它原生负载分开处理。
