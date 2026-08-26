# Broforce 第三方地图联机 Mod：开发文档

安装、设置和日常使用见 [根目录 README](../README.md)。本文只记录当前有效的架构、补丁行为、诊断、测试和构建约定；单轮测试、旧构建和失败方案统一保留在 [issues 索引](../issues/README.md)。

## 当前基线（更改此条目需要用户确认）

- 项目是面向 Steam 版 Broforce 的 Unity Mod Manager + Harmony Mod，目标框架为 .NET Framework 3.5。
- 默认网络路径是官方 Steam Lobby/P2P；`FRP Direct` 默认关闭，启用后接管房间、PID 和游戏 RPC，Steam 只负责 Workshop 内容下载。
- Steam Workshop 双端进入、过场晚加入、FRP 公网 UDP 双端游玩、在线玩家名、正常退出后重入和 Workshop 道具防重复已通过当前测试地图验收。
- FRP 代码支持房主加最多三台远端机器，但多客户端尚未完成实机验收，也不支持主机迁移；多地图、高延迟、异常断网和长期稳定性尚未完整覆盖。
- 当前版本、分发 `buildHash`、DLL SHA-256 和用户侧限制以 [README 当前状态](../README.md#当前状态) 为唯一来源，避免多处维护。

只使用官方 Steam 大厅彩色名单时，显示补丁只需安装在查看方。使用 Workshop 地图注入或 FRP Direct 时，所有参与端必须使用相同构建并下载相同 Workshop 地图；各端版本只以日志中的 `BUILD_INFO buildHash` 判断，不能依赖文件名、时间或大小。

## 架构与代码职责

- `src/Plugin.cs`：UMM 加载、设置界面、保存和启用/禁用入口。
- `src/DiagnosticSettings.cs`：Workshop、会话、折叠状态、FRP 和日志类别配置。
- `src/DiagnosticLog.cs`：普通会话日志、Harmony 追踪、分类过滤和刷新。
- `src/DiagnosticsBehaviour.cs`：场景、Unity 错误和英雄生成状态观察。
- `src/HarmonyDiagnostics.cs`：大厅、关卡切换、Workshop 加载、玩家和英雄流程。
- `src/HarmonyDiagnostics.WorkshopPickup.cs`：道具确定性、拾取所有权、幂等和满弹药退避。
- `src/HarmonyDiagnostics.Afk.cs`：原生 AFK 倒计时、超时和槽位移除观测。
- `src/HarmonyDiagnostics.LevelOutcome.cs`：`LevelFinish`/`RemoveLife` 前后快照。
- `src/HarmonyDiagnostics.WorkshopLevelEnd.cs`：Workshop 关卡结束动作防重入保护。
- `src/OptionalBroModDiagnostics.cs`：Swap Bros 公开 API、版本和角色指纹的只读弱依赖诊断。
- `src/ReflectionProbe.cs`：只读扫描 `Assembly-CSharp` 中的相关类型。
- `src/OnlinePlayerListFormatter.cs`：统一处理 Steam/FRP 在线名单的延迟颜色、动态房主渐变、Rich Text 转义和秒到毫秒换算。
- `src/FrpDirectTransport.cs`：Lidgren UDP、多连接握手、认证、心跳、重连和可靠字节路由。
- `src/FrpDirectRoomInfo.cs`：FRP 房间信息和 Workshop 阶段元数据。
- `src/FrpDirectLayer.cs`：复用原生 PID、ServerID、RPCBatcher 和 `RecieveBytes`，并按机器路由多客户端 RPC。
- `src/FrpDirectNetworkManager.cs`：选择 FRP/Steam 层并管理 `Connect.layer` 生命周期。

方法级追踪不记录房间密码、Steam ID、主机名或 Workshop 作者身份。

## 当前实现

### Workshop 地图注入

主机首次选择任务时，Mod 将配置写入游戏状态：

- `customLevelID` 来自 Workshop ID。
- `loadCustomCampaign=true`。
- `sceneToLoad` 来自场景设置，默认 `Test Evan2`。
- 非空的 Workshop campaign name 写入 `campaignName`；留空时保留原生值。

当前注入点为 `WorldMapController.EnterMission`、`GameState.LoadLevel`、`GameModeController.SwitchLevel` 和 `SteamController.LevelLoadCompleteEvent`。每个房间只在首次选择任务时注入一次；创建或加入新大厅时清理旧 Workshop 回调、切关和暂停网络状态。

房间信息同时携带 Workshop `loading`/`ready` 阶段。Steam 从 Lobby 数据读取，FRP 通过 `FrpDirectRoomInfo` 同步。晚加入客户端据此并行下载地图，并在场景加载和 `SpawnJoinedPlayers` 都完成后申请本地槽位。

### 晚加入与重入

晚加入流程等待玩家列表稳定 250ms，再用本机主控制器调用一次 `HeroController.AddLocalPlayer(-1, controllerId)`。已有本地槽位或待处理请求时复用，避免重复生成 P2-P4；45 秒内没有观察到本地 `Player.Start` 或 `SetPlayerCharacter` 时允许重试一次。创建方仍在 `newJoin`/选关界面时不启动地图加载，进入 Workshop 过场后最长等待约 120 秒。

Host 只在晚加入 Workshop 会话中放宽 `RequestJoinGame` 的关卡完成和控制器注册保护。请求成功后：

- 拥有角色的一端重发当前权威 `SetSpawnPositon`，不调用可能误判为空投的 `WorkOutSpawnPosition`。
- 房主调用 `InstantiationController.SendInstantiatedPrefabs(requesteeID)`，只向新 PID 重放当前 buffered `PlayerPrefab` 和角色实例，补回场景加载期间被销毁的远程房主角色。

成功判据：

- Host 出现 `Late workshop RequestJoinGame state after native handling`、`Workshop spawn-position rebroadcast completed with authoritative current positions`；重入时还应出现 `Late workshop replayed buffered network instances to the joining client`。
- Client 依次出现 `Starting late workshop join load`、`Late workshop client scene loaded`、`Late workshop SpawnJoinedPlayers observed`、`Late workshop join requested a local player slot after scene readiness` 和 `Late workshop automatic join completed`。
- 最终场景中双方均有正确的 P1/P2；重入客户端重新记录远程房主 P1 的 `Player.Start`、`RegisterHeroToPlayer` 和 `SetPlayerCharacter`。

当前修复的根因、被 RPC 安全检查拒绝的旧 wrapper 方案以及重入验收见 [2026-08-22 重入与第 4 关黑屏记录](../issues/ISSUES-2026-08-22-重复退出重入加入方失败与3781818421进入第4关黑屏.md)；FRP 协议和历史构建时间线见 [FRP Direct 实施与验收记录](../issues/archive/ISSUES-2026-08-24-FRP内网穿透联机方案.md)。

### 英雄与控制器恢复

游戏原本的 `RequestHeroTypeFromMaster` 流程继续保留。Workshop 本地玩家等待 18 秒仍无回复时，使用原生 `GetHeroType` 和 `Player.SpawnHero` 做一次本地备用生成；已有角色、远程玩家和正常收到回复的玩家不进入备用分支。

Workshop 玩家发生 `Dropout` 后，Mod 按槽位保存英雄类型和本地 `playerControllerIDs`。重新请求英雄、备用生成和 `AddLocalPlayer` 优先恢复这些值；`Player.Start`/登记阶段会修正原生写回的错误控制器。主动网络重试已删除，避免迟到回复重复替换角色。

验证日志包括 `Saved local Workshop controller for dropout rejoin`、`Reusing saved local Workshop controller for dropout rejoin`、`Rewrote local Workshop rejoin controller to saved binding` 和 `Restored saved local Workshop controller binding`。角色存在但无法操作时，应优先比较掉线前后的控制器绑定，而不是只检查 `character`。

原房主退出后作为 client 重入时，旧 `ConfirmationPause` 可能使 `Player.GetInput` 清空输入。新的有效 Workshop 会话在 `CreateMatch`/`JoinLobby` 开始时将暂停恢复为 `UnPaused`、控制器重置为 -1，并隐藏遗留的暂停相机和玩家列表；不修改玩家槽位或控制器所有权。

### Workshop 道具

原生普通箱会按各端本地随机数和解锁进度转换，远程角色镜像也会扫描本机道具；弹药已满且道具未消费时还会逐帧重复发送 `Collect`。当前补丁只在有效 Workshop 线上会话启用：

- 普通 `Standard` 箱保持标准弹药，显式特殊箱保留原类型。
- 只有本机拥有的角色扫描本机道具。
- 已消费或停用道具的重复 `Collect` 被忽略。
- 弹药已满时只在本机提供一次原生反馈，不发送无效 `TargetAll` RPC；离开道具后可再次反馈，未消费道具有 0.5 秒退避。

离线、普通线上原版关卡和未启用有效 Workshop 注入的会话保持原生行为。该补丁不依赖 Steam/FRP 层，已通过 FRP 双端实测，官方 Steam 大厅仍需独立复测。测试证据见 [issues 索引](../issues/README.md)。

### AFK 行为

`Disable automatic AFK spectator mode in online games` 由每台客户端独立控制，只重置本机联机角色的原生 AFK 计时，不处理远程角色，也不拦截手动退出、断线或死亡。要保护双方角色，双方必须分别开启。

原生 `Player.Update` 仅在存活玩家数大于本机玩家数时累计 `idleTimer`，35 秒后调用 `HeroController.Dropout`。因此一个玩家进入 AFK 后，最后一个本地角色通常停止累计并被保留；其它退出路径导致无人仍需独立观察。

当前只读日志点：

- 约 5 秒：`AFK_TIMER event=counting`。
- 约 30 秒：`event=warning`；条件改变后记录 `event=reset`。
- 35 秒分支：`AFK_STATE event=timeout-triggered`。
- 槽位实际移除：`PLAYER_DROPOUT event=applied`。
- 防 AFK 生效：`AFK_STATE event=prevention-active`。

只有与本机 35 秒分支在 2 秒窗口内关联的退出标记 `reason=native-afk-timeout`，其它退出保守标记 `unknown`。旧会话证据和下一轮诊断背景见 [Utility Mod 借鉴与 AFK 诊断记录](../issues/ISSUES-2026-08-25-Utility-Mod代码借鉴方案与AFK诊断改进.md)。

### FRP Direct 网络层

`FrpDirectTransport` 复用 `Assembly-CSharp.dll` 的 Lidgren，应用标识为 `BroforceOnlineDiagnostics.FrpDirect.v1`。只有同时开启传输原型和游戏层开关，`FrpDirectNetworkManager` 才返回独立连接层。

- Host 固定监听配置的 UDP 端口，默认 27045；设置页用 `1`、`2`、`3`、`4` 四个按钮选择房间总角色上限。`1` 只允许房主，`4` 允许房主加最多三台远端，不突破 Broforce 原生四人上限。按钮在地图内点击后立即生效，不重启传输。
- Client 使用临时端口连接完整 `host:port`，普通断线后每 5 秒重试。
- 每条连接独立维护握手、心跳和超时。Lidgren 建连后 Host 发随机挑战；Client 用密码、挑战、协议版本和双方 `buildHash` 计算 HMAC-SHA256。Host 同时验证三者和机器 ID 唯一性；失败后 Client 不自动重试。
- 协议 v4 提供房间查询/状态、加入确认/拒绝、离开通知、成员离开通知、带机器路由的 `GameData` 和房主 RTT 快照。旧协议构建会因协议不匹配而拒绝连接。
- Host 通过原生 `GeneratePlayerID` 和 `BroadcastPlayerID` 为每台已加入机器分配 PID，并把已有映射定向同步给新客户端。`RPCBatcher` 展开的具体 PID 按机器直发；客户端之间的数据经 Host 中继，目标不是 Host 时不会在 Host 本地重复执行。
- 房主创建房间及地图内调整人数时把所选上限写入原生房间 `capacity`，再向 Client 推送最新房间信息。传输层仍可保持最多三台已认证连接，实际加入人数由房间层按 `capacity - 1` 拒绝，因而满房客户端仍可查询房间状态。降低上限不会删除现有机器或 PID；只要当前成员数仍大于等于新上限，新的加入和退出后的重入都会被拒绝。
- 单个 Client 离开或断线时只清理该机器的 PID，并通知其余 Client；剩余成员和房间状态继续保留。Host 离开仍会结束所有 Client 的房间，当前不支持主机迁移。
- 在线玩家名来自原生 `Connect.SetPlayerName` 建立的 PID 名字表，不显示 FRP 机器 ID 或公网端点。Esc 在线名单显示 `xxxms | 玩家名`；RTT 未产生首个样本时显示 `--ms`，`0-80ms` 为绿色、`81-150ms` 为黄色、`151ms` 以上为红色。房主行显示 `HOST | 房主名`，房主名使用 4 秒一轮的动态暖色到青色渐变。
- RTT 表示每台机器到房主的往返时间。房主直接读取每条 Lidgren 连接的 `AverageRoundtripTime`，并每秒向 Client 同步所有已认证机器的 RTT 快照；Client 之间仍不建立直连。
- 连接层对内容来源报告 `LayerType.Steam`，仅用于继续下载 Workshop；房间和 RPC 仍走 FRP。
- Client 每 5 秒发送应用层心跳；正常 Update 下 60 秒无有效心跳才断开。主线程加载停顿超过 10 秒时恢复心跳窗口。

密码只保护握手，不加密后续 UDP 内容；UMM 会把密码保存在本机设置文件中。完整限制和验收时间线见 [FRP Direct 实施与验收记录](../issues/archive/ISSUES-2026-08-24-FRP内网穿透联机方案.md)。

### 在线玩家延迟名单

- 官方 Steam 大厅和 FRP Direct 都复用原生 `Interface.OnlinePlayerList`，显示 `xxxms | 玩家名`。首个样本尚未产生时显示灰色 `--ms`；`0-80ms` 为绿色、`81-150ms` 为黄色、`151ms` 以上为红色。
- 房主由 `PID.ServerID` 或 FRP Host 身份识别，显示 `HOST | 房主名`；房主名使用 4 秒一轮的动态暖色到青色渐变。名字中的尖括号会转义，避免注入 Unity Rich Text。
- Steam 使用游戏原生 `PingController` 暴露的 `PID.Ping`，按秒换算为毫秒。名单按 PID 映射而不是玩家名关联；核心 PID 尚未建立时回退原生 Steam 大厅名单，远端 PID 同步期间则持续格式化已有成员并在后续刷新补齐。名单不再用可能滞后的 `Room.PlayerCount` 判断是否回退，避免成员离开后短暂恢复原生样式。
- Steam 名单格式结果缓存 0.1 秒，原生 `SteamLayer.Update` 仍每帧请求名单，因此渐变以 10 FPS 更新而不会每帧重新分配富文本。Steam 显示是本地 UI 改动，不需要额外协议，也不要求其他玩家安装 Mod。
- FRP RTT 表示每台机器到房主的往返时间；Steam Ping 是当前机器通过游戏原生采样得到的对应 PID 延迟，不同玩家视角的数值可能不同。

### Esc 返回大厅

原生 Workshop 返回链为：

```text
GameModeController.LoadNextScene(VictoryCustomCampaignSteam)
VictoryCustomCampaignSteam
CustomLevelRatingMenuSteam
MainMenu
```

有效 Workshop 线上会话从暂停菜单返回时，Mod 将目标直接改为 `MainMenu`，清除 `loadCustomCampaign`/`immediatelyGoToCustomCampaign`，跳过通关时间和评分界面。`MainMenu.InitializeMenu` 完成后清理陈旧暂停状态，再调用原生 `TryToGoToLobby(Online)`。

返回主菜单的 Logo 动画复用原生 `Lobby.GoBackToMainMenu -> MainMenu.Show -> ShowRoutine`；菜单文字、高亮和 Renderer 在动画完成后恢复。打开大厅失败时恢复完整主菜单，最长等待 30 秒，避免隐藏或不可操作状态。

### 关卡结果与兼容性诊断

部分 Workshop 地图会在 `GameModeController.switchingLevel=true` 后继续逐帧触发成功结束流程。重复流程会重新执行 `DetermineLevelOutcome -> CompleteCurrentLevel`，持续增加关卡号并重置切关倒计时；地图结束动作还可能先清除 `levelFinished`，绕过原生幂等保护。当前补丁只在有效线上 Workshop 会话、配置场景和 Workshop ID 均匹配时，抑制切关期间重复的 `LevelEndSuccess`/`LevelEndSuccessSilent` 和成功结算重入；第一次结束动作、失败重试和其它场景保持原生行为。对应根因、构建和复测要求见 [3715087178 黑屏记录](../issues/ISSUES-2026-08-26-3715087178联机通关黑屏与关卡结束重入.md)。

这些诊断均为只读，不改变关卡结果、Workshop 模式、角色选择或 AFK 规则：

- `LEVEL_OUTCOME`：在 `GameModeController.LevelFinish` 和 `Player.RemoveLife` 前后记录场景、生命、槽位、存活/本机玩家数、切关和房间状态。
- `WORKSHOP_GAME_MODE_COMPARE`：比较 Campaign、`GameState` 和 `RoomInfo` 的 `gameMode`；不一致时告警，不回写。
- `OPTIONAL_BRO_MOD`：通过 `UnityModManager.FindMod("Swap Bros Mod")` 和公开 API 记录版本、API、角色表及本地选择 SHA-256；缺失或失败时安全降级。
- `AFK_TIMER`/`AFK_STATE`/`PLAYER_DROPOUT`：观察原生 AFK 和槽位移除。

本项目不引用 RocketLib，不调用换人 API，也不因为指纹不同拒绝会话。Utility Mod 的借鉴边界及未采用方案见 [对应 issue](../issues/ISSUES-2026-08-25-Utility-Mod代码借鉴方案与AFK诊断改进.md)。

## 测试与调试

### 稳定性目标

1. 进入地图后，双方都能生成角色并移动、跳跃、攻击。
2. 正常过关、跳关或重启后，双方进入同一场景并重新满足第 1 项。
3. 双方角色全部死亡后数秒内触发失败；继续后恢复有效生命和角色。
4. 至少一方保持非 AFK 且有可用角色；场上无人时应重启并恢复，不能陷入无玩家重启循环。

双方死亡后未触发失败，或重启后未恢复生命并循环失败，均视为 Bug。

### 证据要求

每轮使用新的会话 ID，并按以下顺序测试：

1. 核对双方 `BUILD_INFO buildHash`、Workshop ID 和地图文件版本。
2. 创建方停留在 `newJoin`，加入方进入并占用不同位置；需要时再覆盖晚加入/重入。
3. 记录进入地图、切关、死亡/失败、退出/重入等事件的双端状态。
4. 收集所有参与端同一会话的诊断 `.log`、`.trace.log`、UMM `Core\Log.txt` 和 `error.log`。

不得仅凭单端画面或日志断定网络根因。若远端只能提供部分日志，先核对 `buildHash`，并在结论中明确缺少 MCP、UMM 日志或 `error.log` 的证据边界。额外进入公开房间但未成功加载 Mod/地图的成员不计入相关结论。

### MCP 快速检查

`Broforce_src/unity-inspector-mcp` 可读取关卡、玩家、GameObject、截图和日志。快速检查包括连通性、单次状态、截图或指定日志读取：

1. 直接并行调用本次可用端点的 `ping`；不要先扫描配置、枚举工具或探测 TCP 端口。
2. 按需调用 `game_state`、`inspect_player`、`take_screenshot`、`query_gameobjects`/`inspect_gameobject` 或日志读取。
3. 立即返回结果，不发送倒计时提示，也不扩展成 40 秒监控。

默认端点可命名为 `unity_inspector` 和 `unity_inspector_remote`，但必须以本次实际参与测试的客户端和日志来源为准。工具未加载或 `ping` 报错时，报告实际错误；除非用户要求，不追加端口和配置诊断。

单次请求返回 `Game process died` 不能证明游戏已退出。应立即用同一端点 `ping`，必要时再用 `game_state` 复核；只有连接持续失败并有进程、UMM 日志或 `error.log` 等独立证据时，才判断客户端退出或崩溃。

### MCP 正式监控

只有需要复现并持续观察事件时才进入正式监控：

1. 开始前发送“倒计时开始了!!!”。
2. 可用端点依次执行 `ping`、`game_state`、`inspect_player`，记录当前诊断日志及读取位置。
3. 固定观察 40 秒。窗口内只采样运行时状态和诊断事件，不分析、不总结、不询问、不修改代码，也不中途发送进度；场景切换、角色消失或短暂连接异常均不提前结束。
4. 结束后发送“倒计时结束了!!!”，再统一读取日志和分析。

双端 MCP 可用时默认同时观测。每轮必须同时覆盖运行时状态与当前会话 `.log`/`.trace.log`，不能只轮询最终玩家数量或只读取 UMM 日志。玩家加入问题至少对齐 Client 的 `AddLocalPlayer`、`RequestHeroTypeFromMaster`、`Player.Start`、`SpawnHero`、`SetPlayerCharacter`，以及 Host 的 `RequestJoinGame`、`AddPlayer`。

持续调试可由多个 40 秒窗口组成；窗口之间允许分析和运行时验证。等待用户执行退出、重入或其它复现步骤时保持本轮排查，不提前结束。任一客户端连接消失时停止向该端发送运行时指令，通知用户重启并给出复现步骤；恢复后继续同一轮并检查三类日志。后续不再需要游戏保持打开时发送“游戏可以关闭了!!!”。

### 运行时调试授权

在上述联机稳定性目标处于测试或修复阶段时，AI 已持续获得双端日志读取、MCP 监控和安全运行时调试授权，无需逐项征求同意。范围包括传送、修改血量/生命、调整速度、切换或重启关卡、模拟输入、执行安全运行时代码和注入用于验证根因的临时修复。

每次操作必须记录目标、具体指令、前后状态，并区分临时运行时修改与源码中的正式修复。授权不包括删除存档、清理用户文件或修改与本 Mod 无关的系统状态；用户要求停止或客户端不可用时立即停止。

### 专项验收

- AFK：启动日志应有 `AFK_DIAGNOSTICS_PATCH playerUpdate=True; dropoutRpc=True`；目标端无输入至少 35 秒，对齐双方 `AFK_TIMER`、`AFK_STATE`、`PLAYER_DROPOUT` 和槽位/存活人数。开启防 AFK 时应有 `prevention-active`，不应有本机 `timeout-triggered`。
- 道具：双方核对同一位置的数量/类型；满弹药站在箱子上不得持续播放反馈，消耗弹药后可拾取一次；MechDrop、RCCar 等显式特殊箱保持原类型。
- 关卡结果：确认 `Level outcome diagnostics enabled; patched methods=2.`，分别触发扣命、通关和失败，检查 `LEVEL_OUTCOME` 前后快照。
- 可选 Mod：先比较双方安装/启用状态、版本、`rosterHash` 和 `selectedHash`。指纹不同只证明角色环境不同，不能单独作为英雄生成失败的根因。

## 诊断日志

日志目录：

```text
<Application.persistentDataPath>/BroforceOnlineDiagnostics/
```

当前内网加入方的 Windows 用户数据目录已通过 SMB 共享，可直接从开发机读取：

```text
\\192.168.1.181\Users\5700G\AppData\LocalLow\Free Lives\Broforce\BroforceOnlineDiagnostics
```

该 UNC 路径对应加入方本机的 `Application.persistentDataPath\BroforceOnlineDiagnostics`。分析双端会话时从这里读取加入方日志；不要在 `\\192.168.1.181\Epan\...\UMM\Mods` DLL 部署目录中查找诊断日志。

插件加载时创建启动日志；`SteamLayer` 或 `FrpDirectLayer` 的 `CreateMatch`/`JoinLobby` 创建新会话。每个会话有普通事件日志和 Harmony 追踪日志：

```text
diagnostics-host-<session>-<utc-time>.log
diagnostics-host-<session>-<utc-time>.trace.log
```

普通日志约每 750ms 刷新，警告、错误和会话结束时立即刷新。九类设置只过滤诊断输出，不关闭补丁或改变游戏行为。无论类别如何选择，`BUILD_INFO`、`SESSION_BEGIN`、`SESSION_END`、`DIAGNOSTIC_CATEGORIES`、Warning、Error 和 Unity 异常始终保留。

`LEVEL_OUTCOME`、AFK 和 Dropout 事件同时写入普通日志和 trace；`WORKSHOP_GAME_MODE_COMPARE`、`OPTIONAL_BRO_MOD` 写普通日志。`OPTIONAL_BRO_MOD` 在启用诊断和每个会话开始时各采集一次，分析网络问题时使用会话中的第二次快照。

标准构建把源码、引用、编译器目标和配置组成清单，计算 SHA-256 `buildHash` 并嵌入 DLL；未通过标准脚本的构建记录 `UNBUILT`。

日志约束：

- 不直接追踪 `Update`、`RunHeroRespawnLogic` 等每帧方法；改用低频下游事件。
- 重复事件按方法、参数和状态限频；恢复记录时报告抑制数量。
- 新增追踪后先检查本机增长速度；持续每秒多行时先修复限频。
- 写入前清洗未配对 UTF-16 代理项。
- 不自动限制大小或删除旧日志；测试后按会话自行清理。

## 当前限制

| 范围 | 当前限制或剩余验证 |
| --- | --- |
| 英雄回复 | Client 可能丢失原生英雄类型回复；18 秒备用生成只能缓解，不能替代网络同步 |
| 晚加入/重入 | 当前地图已通过；不同地图、控制器、高延迟、异常断网和长期多轮仍需覆盖 |
| AFK/失败 | 新诊断待真实双端触发；需确认远端槽位移除后 Host 的存活人数和失败判定 |
| Workshop 地图 | `GeneratePole.Awake`、`BroBase` 或特效可能抛出地图自身异常 |
| Workshop 切关 | `3715087178` 的重复结束动作保护已实现并构建，普通成功、静默成功、失败重试和最终结算仍待双端复测；`3781818421` 仍作为独立问题保留 |
| 道具 | FRP 已验收；官方 Steam 大厅和更多地图待复测 |
| 其它 Mod | Swap Bros 只有只读诊断，尚未完成兼容性验收，也不会阻止环境不一致的会话 |
| FRP Direct | 代码支持地图内动态设置 `1` 至 `4` 人上限并保留现有成员；各容量档位、降额后重入、三/四机、异常断网、多地图、高延迟、长期稳定性和主机迁移仍未实机验收 |
| 原生崩溃 | 异常与崩溃时间接近不能单独证明因果，必须结合双方诊断、UMM 日志和 `error.log` |

## 构建与部署

1. 复制 `LocalBroforcePath.props.example` 为不提交的 `LocalBroforcePath.props`。
2. 配置 `BroforceManagedPath` 为 `Broforce_beta_Data/Managed`，其中必须含 `UnityEngine.TextRenderingModule.dll`。
3. 配置 `UnityModManagerPath` 为含 `UnityModManager.dll` 和 `0Harmony.dll` 的 UMM 核心目录。
4. 使用兼容 .NET Framework 3.5 的编译器。当前验证路径：`C:\Windows\Microsoft.NET\Framework64\v3.5\csc.exe`；不要直接使用 v4 编译器。

唯一标准入口：

```powershell
powershell -ExecutionPolicy Bypass -File .\BuildAndDeploy.ps1
```

有效输出位置：

```text
<项目根目录>\BroforceOnlineDiagnostics\BroforceOnlineDiagnostics.dll
<本机 UMM_PROFILE_DIR>\Mods\GJKen-BroforceOnlineDiagnostics\BroforceOnlineDiagnostics.dll
\\192.168.1.181\Epan\Games\Broforce Mods\Broforce\profiles\Broforce\UMM\Mods\GJKen-BroforceOnlineDiagnostics\BroforceOnlineDiagnostics.dll
```

脚本输出并嵌入 `Build hash`，覆盖 DLL；项目安装包固定包含 `Info.json`。目标缺少清单时可从 `modinfo.json` 初始化，但不覆盖已有清单或其它文件。任一网络路径、目录创建或复制失败时整个部署失败，不得继续双端测试。

`BroforceOnlineDiagnostics.csproj` 的 `OutputPath` 也指向项目安装包；`bin\Debug` 旧文件不得用于测试。IDE/MSBuild 只有正确读取本机 props 并执行构建后目标时才可替代脚本。

安装包结构与命名：

```text
BroforceOnlineDiagnostics\
  BroforceOnlineDiagnostics.dll
  Info.json
```

复制到 UMM 后目录名必须为 `GJKen-BroforceOnlineDiagnostics`，程序集名保持 `BroforceOnlineDiagnostics.dll`。脚本不更新 r2modman 缓存包。

## 逆向参考

使用 dnSpy、ILSpy 等工具读取：

```text
<BROFORCE_DIR>\Broforce_beta_Data\Managed\Assembly-CSharp.dll
```

- [Viewing Broforce's Code](https://github.com/alexneargarder/BroforceMods/wiki/Viewing-Broforce's-Code)
- [BroforceMods Wiki](https://github.com/alexneargarder/BroforceMods/wiki)
- [BroMaker Abilities Wiki](https://github.com/alexneargarder/Bro-Maker-Abilities-Wiki/wiki)

## 修改协作约定（更改此条目需要用户确认）

- 提交或同步前检查上级仓库的 `git status` 和 `git diff`，不要加入 `LocalBroforcePath.props`、日志、缓存或无关文件。
- `LocalBroforcePath.props` 包含机器专用路径，不应提交。
- 构建方式、联机行为、安装方式、日志格式或兼容性变化时，同步更新 README 和本文档。
- 未经明确要求，不运行上级仓库的自动提交、推送或更新脚本。
