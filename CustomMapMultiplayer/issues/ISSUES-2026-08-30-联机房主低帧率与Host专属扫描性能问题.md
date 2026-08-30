# 联机房主低帧率与 Host 专属扫描性能问题

## 状态

| 项目 | 状态 |
| --- | --- |
| Host/Client 帧时间差 | 已通过 MCP 在同一 Workshop Steam 会话中确认 |
| 第一方案源码优化 | 已完成酸液扫描、实体终态、Harmony Trace 三条路径的初版优化；阶段 1 三项低风险细化优化已完成 |
| 用户实测反馈 | 最新构建长时间高密度战斗测试中，Host 掉帧明显减轻 |
| 正式性能验收 | 待完成统一图形设置、反向 Host 和 p50/p95/p99 A/B 对照 |
| 加入方箱子坍塌特效异常 | 已独立记录，不纳入本轮性能判定 |
| 本地订阅地图选择后个别地图返回主菜单 | 已记录，暂不纳入本轮性能修复 |

当前方案继续保留 UMM/Harmony Mod 架构，没有替换 `Assembly-CSharp.dll`。本 issue 的性能结论以运行时 A/B 数据为准，不能只根据一次主观体验或构建成功宣称问题已经完全解决。

## 范围与结论

本 issue 关注 Workshop 联机中 Host 帧时间明显高于 Client 的问题，重点检查 Mod 增加的 Host 专属扫描、实体终态同步和 Harmony Trace 开销。

当前最合理的结论是：Workshop 酸液扫描是首要可疑路径，实体终态遍历和 Harmony Trace 是次要可疑路径；原生 Host 的 AI、物理和网络权威负载仍需对照实验排除。第一方案已经降低了前三条 Mod 路径的开销，用户多轮测试反馈改善，但正式 A/B 尚未完成。

## 初始现象与证据

### 测试基线

- 日期：2026-08-30。
- 地图：`Test Evan2`。
- Workshop 战役：`Bromandy_Ptr1`。
- Workshop ID：`3715087178`。
- 传输：官方 Steam Lobby/Steam P2P。
- 初始采样构建：`buildHash=13f2052f9a67caaa4c319acad1cfc49b1b3667ec9643eb5af077e9a721b64a92`。
- 优化构建：`buildHash=b00a97bae0f9eb52c7001d17f6c42a5077934f07684ee1bf3cab1bfb724e6a10`。
- 初始有效会话由日志确认：本机 `SteamLayer_CreateMatch; networkRole=host`，远端 `SteamLayer_JoinLobby; networkRole=client`。

初始会话中双方都进入了 `Test Evan2`，玩家数量为 2。MCP 采样只读取帧时间和帧计数，没有传送、改速或模拟输入；两端在采样期间都能继续响应 `ping`，没有独立证据表明游戏进程崩溃。

### MCP 帧时间采样

双方角色仍存活时各采样 10 次：

| 端 | `unscaledDeltaTime` 范围 | 平均值 | 中位数 | 粗略帧率 |
| --- | ---: | ---: | ---: | ---: |
| Host | `11.9–72.4ms` | `41.1ms` | `34.7ms` | 约 `24–29 FPS` |
| Client | `8.7–22.3ms` | `13.2ms` | `9.7ms` | 约 `76–104 FPS` |

同一时段的图形设置不一致：Host 为 `vSyncCount=1`，Client 为 `vSyncCount=0`，两端 `Application.targetFrameRate=-1`。因此该数据确认了明显差异，但不能作为排除 VSync 后的正式性能结论。

双方角色全部死亡后再次采样 20 次，Host 约 `14–33ms`，Client 约 `9.5–23ms`。这只能说明死亡后负载下降，不能替代双方存活且移动中的对照。

### 角色交换证据边界

本轮没有形成完整的反向 Host 会话：本机新日志出现了 `SteamLayer_JoinLobby; networkRole=client`，远端没有对应的 `SteamLayer_CreateMatch; networkRole=host`。因此不能用 UMM 保存的 `role` 字段、日志文件名或设备名称判断 Host，后续必须以日志中的 `networkRole` 为准。

## 根因候选

### 1. Workshop 酸液扫描（最高优先级）

优化前，`HarmonyDiagnostics.Update` 在有效 Workshop Host 会话中持续调用 `TryApplyWorkshopRemoteHeroAcid`，英雄查询缓存失效后会通过 `FindObjectsOfType<DoodadAcidPool>()` 扫描场景。Host 同时检查本机和远程英雄，Client 不进入同一条 Host 分支，因此符合“Host 掉帧、Client 正常”的方向。

第一方案已经把酸池对象列表改为场景级缓存，约每秒刷新一次；英雄结果缓存约 `50ms`；Host 周期性扫描限频约 `10Hz`。当前仍需用 A/B 数据确认这条路径对帧时间的实际贡献。

### 2. Host 侧死亡实体终态同步

优化前，`TrySubmitEntityFinalStates` 每帧复制并遍历整个 `EntityFinalStates` 字典。普通网络 Mook 主要由 Host 拥有，敌人死亡越多，Host 的历史状态集合越容易增长。

第一方案已改为待提交实体候选集合、空集合快速返回和可复用删除列表；阶段 1 又补充了终态记录 TTL 清理和 Mook 资格判断缓存。

### 3. Harmony Trace 构造

优化前，`TracePrefix` 会为高频网络和玩家方法执行参数格式化、反射读取和字符串构造，日志类别过滤发生在消息构建之后。

第一方案已在构造消息前检查 `HarmonyTrace` 开关，并缓存方法参数、字段、属性和方法描述。Trace 开启时，高频方法仍会先构造消息，再由去重逻辑抑制写入，因此仍有进一步优化空间。

### 4. 原生 Host AI、物理和网络负载

Host 原生负责 AI、物理和网络权威模拟，地图自身的对象数量或 Unity 错误也可能造成 Host 更重。当前没有完成“离线 Host、官方联机、Workshop 联机”的统一设置对照，不能把全部差异归因于 Mod。

## 已完成的第一方案

### Workshop 酸液扫描

- `DoodadAcidPool` 列表改为场景级低频缓存，避免按英雄反复查找全场景对象。
- 查询时实时读取酸池位置和 `fullness`；英雄查询结果缓存约 `50ms`。
- Host 权威周期扫描约 `10Hz`；`CoverInAcid` 入口仍保留即时检查。
- Host 收到请求后继续按当前地图状态进行权威校验，缓存不改变授权条件。
- 场景切换和离开会话时清理酸池、英雄查询和时间戳状态。

### 实体终态同步

- 只遍历尚未完成终态提交的实体候选。
- 空 pending 集合直接返回。
- 使用可复用 key 列表，移除每帧复制整个字典的分配。
- 保持原有 RPC 幂等、序列号和 Host 权威行为，不改变网络协议。

### Harmony Trace

- Trace 关闭时在 `BuildTraceMessage` 前直接短路，跳过参数格式化和反射读取。
- 缓存方法参数、字段、属性和方法描述的反射元数据。
- 功能性生命周期、Workshop 和实体 Hook 不因 Trace 开关关闭而停止。

### 构建与部署

- 已完成标准 Release 构建，优化构建 `buildHash=b00a97bae0f9eb52c7001d17f6c42a5077934f07684ee1bf3cab1bfb724e6a10`。
- 阶段 1 候选源码已完成 Release 编译并部署到本机及测试机 UMM，构建 `buildHash=33da8cf07956ccbe8cb9e4d73c0ba7d92e2e7249bfea42eafc7fbf1ac8aaf0b0`；尚未替代正式分发构建。
- 当前高密度战斗减负候选已完成 Release 编译并部署到本机及测试机 UMM，构建 `buildHash=993e95efdc78a50e7ba6b25fb2495cb01e90d2a0cf551c058b6a43377904c9e3`；仍需运行时 A/B 验证收益。
- 本机 UMM 和内网 UMM 部署 DLL 与仓库构建产物一致；当前尚未完成正式双端运行时 A/B 验收。

## 更新后的实施方案（2026-08-30）

阶段 1 三项低风险候选已合并到当前源码并完成 Release 编译；新的构建先作为候选基线保留，不把 Trace 降频和聚合统计直接合并进正式验收结论。每个阶段完成后都要重新构建，并保留新的 `BUILD_INFO buildHash`。

### 阶段 0：隔离加入方箱子坍塌异常

- `CustomMapMultiplayer` 暂不新增 `DoodadCrate` 修复；该问题由独立 issue 记录：`ISSUES-2026-08-30-加入方箱子坍塌特效持续重复.md`。
- 出现箱子特效持续重复或 `DoodadCrate` 错误循环的会话，不计入性能 p50/p95/p99 对照。
- `BroforceBugFix` 的递归保护只能作为相关证据，不能直接视为覆盖了本次加入方空引用循环。
- 后续偶然复现时优先保留现场和两端日志，不进行临时运行时修复，不把箱子异常与 Host 扫描性能问题混为一谈。

### 阶段 1：低风险、无协议变化的代码优化

已实施且不改变网络协议、RPC 顺序和授权条件的三项改动：

1. 酸液类型使用 `DoodadBloodPool.FluidType.Acid` 直接比较，移除 `fluidType.ToString()` 分配。
2. 为普通网络 Mook 的 `PolymorphicAI` 资格和类型判断增加按对象缓存；场景切换、会话结束或对象失效时清理。
3. 为已经完成终态提交且不再有 pending 状态的 `EntityFinalStates` 增加 `15s` TTL 清理；pending 的现有超时和幂等行为保持不变。

阶段 1 已完成编译；下一步先做启动和最小 Workshop 联机回归，再进入性能 A/B；不在这一阶段加入 Trace 结构重写或新的网络行为。

### 阶段 2：按数据决定是否优化 Harmony Trace

只有阶段 1 的 A/B 仍显示 Trace 路径有明显贡献时才实施：

- 对 `UpdateOnlinePlayerList`、`MonitorPlayerDropin`、`RoomInfo.RefreshInfo` 等高频方法在构造消息前做低频采样或默认跳过。
- 保留 `HarmonyTrace` 开关关闭时的现有提前短路。
- 为诊断日志增加显式类别写入接口，避免 Trace 写入前再次从完整消息推断类别。
- Warning、Error、会话边界和功能性 Workshop/实体 Hook 不能因 Trace 降频而丢失。

### 阶段 3：增加低开销性能观测

为酸液扫描、实体终态候选和 Trace 抑制增加每 `1–5` 秒一次的聚合统计，例如调用次数、候选数量、总耗时和最大单次耗时。统计只写聚合结果，禁止逐帧性能日志；诊断关闭时不得改变游戏功能路径。

### 阶段 4：正式性能验收

- 统一两端图形设置，关闭 Harmony 详细跟踪；每组使用新会话。
- 按 A/B/C/D 对照矩阵执行，至少 3 轮，并在轮次之间交换 Host。
- 只使用没有箱子错误循环、没有未解释会话中断的有效窗口；地图自身已有 Unity 错误单独计数。
- 统计双方存活静止 5 秒、移动/攻击 10 秒以及酸液、普通 Mook 死亡和双方死亡阶段的 Host/Client p50、p95、p99。
- 只有性能四项门槛和功能回归全部满足，才标记第一方案正式验收通过；单轮体感改善仍标记为“观察到改善”。

### 暂缓项目

- 酸液扫描隔离构建（D 组）在没有独立、可重复的正式构建前不使用临时 MCP 注入替代。
- 加入方箱子坍塌问题、本地订阅地图返回主菜单问题和原生 Host AI/物理负载不并入本方案代码修复。

## 正式验收计划

### 对照矩阵

| 对照 | Mod | Workshop 注入 | 目的 |
| --- | --- | --- | --- |
| A | 关闭 | 不适用 | 测量原生离线 Host 负载 |
| B | 开启 | 关闭 | 测量官方联机和 Mod 基础开销 |
| C | 开启 | 开启 | 测量当前 Workshop 联机构建 |
| D | 开启 | 开启，酸液扫描隔离或缓存构建 | 验证酸液扫描贡献 |

每轮使用新会话，双方统一 `QualitySettings.vSyncCount` 和 `Application.targetFrameRate`，并保留双方 `BUILD_INFO`、`sessionId`、`networkRole`。双方角色存活时先静止 5 秒，再移动和攻击 10 秒；记录 `unscaledDeltaTime` 的 p50、p95、p99。随后分别观察酸液接触、大量敌人死亡和双方角色全部死亡后的帧时间。

MCP 只做受限、只读采样，不进行全场景对象枚举；临时运行时注入只能作为定位证据，不能替代正式构建验收。

## 验收标准

验收不要求阅读源码，只需要按对照矩阵完成游戏测试并提交两端日志和帧时间记录。旧的初始数据中 Host 与 Client 的 VSync 设置不同，只能证明曾经存在差异，不能直接作为最终通过依据；正式验收必须先用统一设置重新建立基线。

### 测试前置条件

- 两端运行同一个优化构建，日志中的 `BUILD_INFO buildHash` 必须一致。
- 每轮使用新会话；Host 日志必须出现 `CreateMatch/networkRole=host`，Client 日志必须出现 `JoinLobby/networkRole=client`。
- 两端统一 `vSyncCount` 和 `Application.targetFrameRate`。性能对照时两端都关闭 `Harmony 详细跟踪`，其它开关保持一致。
- 每个对照至少完成 3 轮，不能只凭单轮结果判断。

### 性能通过条件

以相同地图、相同设置和相同动作窗口的 Host `unscaledDeltaTime` 为主指标：

1. 每轮双方存活后静止 5 秒，再移动/攻击 10 秒；有效窗口至少记录 20 个样本，并计算 Host 的 p50、p95、p99。
2. 优化构建相对统一设置下的优化前基线，3 轮结果的中位数必须满足：Host p95 帧时间降低至少 `20%`。
3. 优化构建的 Host p99 不得比基线恶化超过 `10%`，Client p95 不得恶化超过 `10%`。
4. 官方联机且注入关闭的对照组不能出现超过 `10%` 的帧时间回归。

达到以上四项，才可标记为“性能优化验收通过”。只出现“体感变好”或单轮帧率升高，标记为“观察到改善”，不能标记为正式通过。

### 功能和回归通过条件

- Workshop 地图在注入开启时，Host 和 Client 均能进入并正常游玩；至少完成一次加载、移动、攻击和退出。
- 酸液测试中，进入酸液的角色仍按 Host 权威结果死亡或同步；离开酸液区域不能出现误判。
- 大量普通敌人死亡后，加入方能看到稳定的尸体终态，不能出现重复终态、位置跳回或明显残留。
- 注入关闭后，官方联机仍能创建、加入、退出并回到原生流程。
- 性能验收会话中不能出现由本次优化新增的 Mod 异常；地图自身已有的 Unity 错误要单独列出，不能用“没有任何 Unity 错误”作为不现实的要求。

### 结果判定

| 结果 | 判定 |
| --- | --- |
| 性能四项和功能回归全部通过 | 第一方案正式验收通过 |
| 性能指标通过，但存在独立的“本地订阅地图选择后返回主菜单” | 性能方案通过；另一个问题保持未解决并单独跟踪 |
| 任一性能硬指标未达到，或出现酸液/实体同步回归 | 性能方案不通过，继续做单项优化和复测 |

每轮记录至少包含：地图 Workshop ID、注入开关、Trace 开关、双方 buildHash、sessionId、networkRole、帧时间 p50/p95/p99、Unity/Mod 错误数量以及最终判定。

## 独立后续问题：本地订阅地图选择后返回主菜单

用户在优化构建上完成多轮测试后补充了准确场景：

- Mod 保持启用，Workshop 地图注入开关关闭。
- 不创建或加入联机房间，不经过 `CreateMatch`/`JoinLobby` 流程。
- 使用游戏原生的“订阅地图后在本地选择自定义地图”功能。
- 正常流程应为“选择自定义地图 -> 进入 P1-P4 玩家界面”；实际表现是个别地图在加载过程中直接返回 `MainMenu`。

该现象不是每张地图都能复现，当前没有记录失败地图的 Workshop ID。它属于本地自定义地图加载回归，不属于本轮 Host 帧率优化，也不应与联机退房回菜单混为一谈。

日志证据边界：

- 当前查看的最新 Host/Client 日志属于联机 `Test Evan2` 会话；两端成功进入地图，之后的 `SteamLayer_LeaveMatch` 是退房路径，不能证明或排除这个本地加载问题。
- 更早联机会话中的 `NullReferenceException`、`ArgumentException: The Object you want to instantiate is null` 和 `MissingMethodException` 也没有和该本地失败地图建立唯一因果关系。
- 当前诊断日志没有记录 `EnableOnlineWorkshopInjection=false` 配置快照；“关闭注入”来自本地测试操作。

该问题只作记录，不改变本轮性能优化、注入清理、构建或部署内容。后续复现时需要保留：失败地图 Workshop ID/名称、注入开关状态、是否存在联机房间、选择地图前后的实际场景名、预期的 P1-P4 界面是否出现，以及返回主菜单前的 Unity/Mod 错误。

## 长时间高密度战斗复盘（2026-08-30）

本次长测的有效双端会话为 `Test Evan2 / 3715087178`，双方使用阶段 1 构建 `buildHash=33da8cf07956ccbe8cb9e4d73c0ba7d92e2e7249bfea42eafc7fbf1ac8aaf0b0`。Host 会话约 20 分钟，记录 263 个 `ENTITY_FINAL death-owner` 和 167 个尸体终态提交；加入方记录 245 个死亡应用。Host 在约 `+208s`、`+559s`、`+770s` 和 `+876s` 出现死亡集中，其中单秒最高 11 个死亡事件，和用户反馈的高密度战斗掉帧时段方向一致。

这批日志没有帧时间、扫描耗时或 CPU 数据，不能证明掉帧由酸液扫描单独造成。约 `+876s` 的 11 个死亡事件附近没有 Unity 异常；另一些时段同时出现 `NullReferenceException`、`IndexOutOfRangeException` 或原生 Mook/沙虫 `Damage` 错误，说明地图/原生 AI、物理和网络权威负载仍可能是主要来源。加入方在约 `+290s` 至 `+395s` 还出现每 5 秒抑制数百条重复 `NullReferenceException`，这类会话不应作为纯性能 A/B 样本。

酸液不需要逐帧扫描：当前 `CoverInAcid` 基入口仍即时处理，Host 兜底地图扫描只以约 10Hz 检查遗漏入口。后续保持该授权和判定逻辑，先通过统一设置下的帧时间对照确认是否需要继续降低扫描频率。

针对本轮暴露的高峰开销，新增候选构建 `buildHash=993e95efdc78a50e7ba6b25fb2495cb01e90d2a0cf551c058b6a43377904c9e3`：高频实体终态和酸液观察仍写入诊断文件，但不再同步调用 Unity `Debug.Log`；`Connect`/`ConnectionLayer` 的热路径反射元数据改为缓存。该改动不改变酸液判定、RPC 顺序、终态协议或授权条件，已部署到本机和内网测试机；用户随后进行了长时间高密度战斗测试，反馈 Host 掉帧明显减轻。

日志术语说明：`NullReferenceException` 表示代码使用了尚未初始化或已经失效的对象；`DoodadCrate` 是游戏原生的箱子处理类。加入方箱子特效持续重复及其错误循环属于独立问题，不能直接归因于 Host 性能路径，也不计入本方案的性能 A/B 样本。

## 当前结论

第一方案已覆盖当前确认的三条 Mod 性能路径，阶段 1 低风险细化优化和本轮高频日志/反射减负候选均已落地；用户使用 `993e...` 构建长时间测试后反馈高密度战斗 Host 掉帧明显减轻，当前标记为“观察到改善”。正式结论仍需统一设置采集 Host/Client p50、p95、p99，并交换 Host；只有对照数据仍显示 Mod 路径有明显贡献时，才继续做酸液扫描降频或 Trace 聚合。不要把原生 Host 负载、重复 Unity 异常、`DoodadCrate` 错误循环或“本地订阅地图选择后个别地图回菜单”混入本轮性能判定。
