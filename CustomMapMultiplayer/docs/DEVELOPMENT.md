# Custom Map Multiplayer：开发文档

安装、设置和日常使用见 [根目录 README](../README.md)。本文只记录当前有效的架构、补丁行为、诊断、测试和构建约定；单轮测试、旧构建和失败方案统一保留在 [issues 索引](../issues/README.md)。

## 当前基线（更改此条目需要用户确认）

- 项目是面向 Steam 版 Broforce 的 Unity Mod Manager + Harmony Mod，目标框架为 .NET Framework 3.5。
- 默认网络路径是官方 Steam Lobby/P2P；`FRP Direct` 默认关闭，启用后接管房间、PID 和游戏 RPC，Steam 只负责 Workshop 内容下载。
- Steam Workshop 双端进入、过场晚加入、FRP 公网 UDP 双端游玩、在线玩家名和正常退出后重入已通过当前测试地图验收；Workshop 道具防重复已在 FRP 双端实测，官方 Steam 大厅和更多地图仍需独立复测。
- Workshop 酸液池已在 `Test Evan2 / Bromandy_Ptr1 / levelIndex=7` 完成房主与加入方分别接触的双端回归：实际接触者正常死亡，出生区玩家不再被连带死亡。
- FRP 的房主加两台加入方（三机）基础联机已通过用户实测；代码支持最多三台远端，但四机、动态容量边界和主机迁移尚未验收。
- FRP 单一总开关、Host/Client 角色切换、非当前角色配置隔离和无 Apply 自动应用已通过用户实测。
- Workshop 注入热关闭及退出房间后的官方地图恢复已通过用户实测。
- UMM 设置页采用左侧导航和右侧动态内容布局；`Multiplayer Options`、`FRP Direct`、语言和 `Diagnostic Logs` 四个页面，以及跟随系统/English/中文按钮已实现。
- 当前版本、分发 `buildHash`、DLL SHA-256 和用户侧限制以 [README 当前状态](../README.md#当前状态) 为唯一来源，避免多处维护。

只使用官方 Steam 大厅彩色名单时，显示补丁只需安装在查看方。使用 Workshop 地图注入或 FRP Direct 时，所有参与端必须使用相同构建并下载相同 Workshop 地图；各端版本只以日志中的 `BUILD_INFO buildHash` 判断，不能依赖文件名、时间或大小。

## 架构与代码职责

- `src/Plugin.cs`：UMM 加载、设置界面、保存和启用/禁用入口。
- `src/SettingsUiText.cs`：UMM 设置界面的中英文文案和系统语言选择。
- `src/DiagnosticSettings.cs`：Workshop、会话、当前页面、语言、FRP 和日志类别配置；旧的折叠状态字段仅为迁移和降级兼容保留，当前界面不再使用。
- `src/DiagnosticLog.cs`：普通会话日志、Harmony 追踪、分类过滤和刷新。
- `src/DiagnosticsBehaviour.cs`：场景、Unity 错误和英雄生成状态观察。
- `src/HarmonyDiagnostics.cs`：大厅、关卡切换、Workshop 加载、玩家和英雄流程。
- `src/HarmonyDiagnostics.WorkshopPickup.cs`：道具确定性、拾取所有权、幂等和满弹药退避。
- `src/HarmonyDiagnostics.Afk.cs`：原生 AFK 倒计时、超时和槽位移除观测。
- `src/HarmonyDiagnostics.LevelOutcome.cs`：`LevelFinish`/`RemoveLife` 前后快照。
- `src/HarmonyDiagnostics.Acid.cs`：统一拦截英雄 `CoverInAcid` 基入口，在 Workshop 在线场景中执行酸池扫描、加入方本地预测、房主权威请求/校验/应用，并记录酸液 RPC 和玩家死亡 RPC 前后状态。
- `src/HarmonyDiagnostics.WorkshopLevelEnd.cs`：Workshop 关卡结束动作防重入保护。
- `src/HarmonyDiagnostics.WorkshopIdentity.cs`：房主地图身份发布、加入方会话配置采用、Steam 订阅检测和缺图加载保护。
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

房间信息同时携带 Workshop ID、场景名、可选战役名和 `loading`/`ready` 阶段。Steam 使用 Lobby 数据，FRP 通过 `FrpDirectRoomInfo` 同步。房主创建房间时发布当前配置，选择地图、新成员加入和主机迁移时重新发布；加入方采用房主地图身份作为本次会话配置，不改写本机持久化设置。从 `JoinLobby` 开始到房主元数据到达前，Client 的配置读取返回空值，不允许回退到本机保存的 ID、场景名或战役名；元数据到达后只使用房主值。这样即使加入方忘记清空旧配置，也不会在最初几帧误加载本机地图。

加入方采用地图身份后枚举 Steam 本机订阅列表。确认未订阅时，屏幕顶部显示中文提示和 Workshop ID，清除待执行的晚加入状态，并阻止指向该房主地图的 `GameState.LoadLevel`；订阅状态无法读取时保持原生下载流程，不误报缺图。订阅或下载不会由 Mod 自动执行，玩家需要在 Steam 创意工坊订阅并等待下载完成后重新加入房间。

关闭注入开关不是简单停止后续 Harmony 注入。此前已经写入的 `_injectedForSession`、`GameState.loadCustomCampaign`、`customLevelID`、`sceneToLoad`、Workshop Lobby 元数据和暂停/切关状态也必须撤销，否则下一次官方选图仍会沿用 `Test Evan2`。当前实现统一通过 `ClearInjectedWorkshopRuntimeState` 清理这些状态，并覆盖三个入口：UMM 中关闭 Workshop 注入、停用或卸载整个 Mod、Steam `LeaveMatch`。热关闭不会主动切换当前场景；它清除待执行状态，使玩家退出当前房间并重新创建官方房间后回到原生选图流程。

`IsWorkshopOnlineSession` 必须同时检查 `EnableOnlineWorkshopInjection`，避免仅凭 `_injectedForSession`、活动场景名或遗留 Lobby phase 在开关关闭后继续启用 Workshop 专用补丁。新建或加入 Steam 房间时，只有注入配置仍有效或运行态确实存在本 Mod 写入的 Workshop 标记，才允许清空 `LevelSelectionController.CurrentCampaign`；普通官方联机会保留原生战役状态。Steam `JoinLobby` 内部可能调用一次清理用 `LeaveMatch`，该阶段继续由 `_joinLobbyInProgress` 和短暂保护窗口排除，不能误判成真实离房。

晚加入客户端根据上述地图身份及阶段并行下载地图，并在场景加载和 `SpawnJoinedPlayers` 都完成后申请本地槽位。缺订阅时的地图身份识别、中文提示和加载阻止已经通过一轮双端实测；加入方保留不同的本地地图配置时，官方 Steam 大厅与 FRP Direct 均已验证能够忽略残留配置、自动采用房主地图并正常加入。

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

离线、普通线上原版关卡和未启用有效 Workshop 注入的会话保持原生行为。该补丁不依赖 Steam/FRP 层；当前仅有 FRP 双端道具证据，官方 Steam 大厅和更多地图仍需独立复测。测试证据见 [issues 索引](../issues/README.md)。

### Mook 终态与主动引爆

实体同步只覆盖普通、非载具、非 Boss 的网络 `PolymorphicAI` Mook：拥有端广播首次死亡事件和最终停稳位置，远端用非空 `DamageObject` 补全死亡链。活动 AI、敌方弹体、钱币和载具不在范围内。

`DemolitionBro.currentBomb` 与 `McBrover.currentTurkey` 的二次按键各有独立 Harmony 转译：拥有端立即执行原版 `Projectile.Death()` 并发送 NID 与位置，远端按 NID 以会话内幂等集合处理。DemolitionBro 已有恢复实测；McBrover 残留仍可复现但概率显著降低，详见 [独立 issue](../issues/ISSUES-2026-08-28-McBrover火鸡主动引爆后残留实体.md)。其它投掷物保持原行为。

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

`FrpDirectTransport` 复用 Lidgren，应用标识为 `CustomMapMultiplayer.FrpDirect.v1`。`EnableFrpDirect` 同时控制传输和游戏连接层；Host/Client 配置隔离，角色、总开关和连接文本自动应用，无 Apply 按钮。

- Host 监听配置的 UDP 端口（默认 27045），可在地图内设置 `1` 至 `4` 人总上限；Client 以临时端口连接 `host:port`，普通断线后每 5 秒重试。
- Host 以挑战、密码、协议版本、双方 `buildHash` 和机器 ID 完成 HMAC-SHA256 握手。协议 v4 提供房间、加入/离开、机器路由 `GameData` 和 RTT 快照；协议不匹配或认证失败会拒绝连接。
- Host 使用原生 PID 分配与定向映射同步；客户端数据经 Host 中继，目标非 Host 的 RPC 不在 Host 重复执行。房间层按 `capacity - 1` 拒绝新加入，降额不移除既有成员或 PID。
- Client 离开或断线只清理该机器的 PID；Host 离开会结束房间，不支持主机迁移。RTT 为各机器至 Host 的往返时间，Esc 名单使用 PID 名字表、彩色延迟和动态房主名。
- Workshop 内容仍从 Steam 下载，房间和 RPC 走 FRP；密码只保护握手，不加密后续 UDP。Client 每 5 秒心跳，正常情况下 60 秒无有效心跳才断开。

三机基础联机与静态 `1` 人房满员提示已实测；四机、`2` 至 `4` 人边界、动态容量和主机迁移仍待验收。完整历史证据见 [FRP Direct 实施与验收记录](../issues/archive/ISSUES-2026-08-24-FRP内网穿透联机方案.md)。

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

除 Workshop 联机英雄酸液的主机权威校验外，这些诊断均为只读，不改变关卡结果、Workshop 模式、角色选择或 AFK 规则：

- `LEVEL_OUTCOME`：在 `GameModeController.LevelFinish` 和 `Player.RemoveLife` 前后记录场景、生命、槽位、存活/本机玩家数、切关和房间状态。
- `PLAYER_ACID`：在 `TestVanDammeAnim.CoverInAcid`、`CoverInAcidRPC` 和 `HeroController.PlayerHasDiedRPC` 前后记录 `playerNum`、RPC 请求槽位、角色 NID、`IsMine`、坐标、`acidMeltTimer` 和 `hasBeenCoverInAcid`。旧实现只转译 `CheckForTraps`，会被 `CalculateMovement` 和 `Damage` 的直达调用绕过；当前补丁改在统一 `CoverInAcid` 基入口拦截 Workshop 在线英雄。地图酸液状态通过场景中的 `DoodadAcidPool` 直接扫描并短时缓存，避免 `Map.GetNearestAcid` 在 Workshop 地图中失效。Host 同时扫描本机和远程英雄并广播经过地图验证的 NID；Client 本机英雄命中后先调用本地原生酸液 RPC，再请求 Host 确认，远程镜像只等待授权应用；权威状态尚未稳定时使用 `authority-wait` 保守阻断原生广播。离线、普通官方联机、非配置场景和非英雄对象仍执行原生行为。
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

### MCP 受控观测

`Broforce_src/unity-inspector-mcp` 用于单次检查和持续复现观测。默认同时连接本次参与会话的房主与加入方；端点不可用时只报告实际错误，不扩展成端口或配置扫描。`Game process died` 需用同一端点 `ping`，必要时用 `game_state` 复核，不能单独作为退出或崩溃结论。

需要复现问题时，先执行受限基线：确认双方 `ping`、场景、地图、传输方式和会话一致；重置增量日志读取游标但不清理磁盘日志；仅记录相关玩家、NID、所有权、状态和日志位置。随后必须明确告知用户：**“观测已准备好，请按本轮复现步骤操作。”** McBrover 火鸡问题使用：**“观测已准备好，请手动投掷并主动引爆火鸡。”** 未获明确请求不得模拟输入。

持续监控以每次用户操作为一个样本，记录序号和时间点，只跟踪已知的相关对象与 NID。对主动引爆等生命周期问题，在触发前、触发后立即、约 0.5 秒、约 2 秒和预计自然超时点读取双方受限状态及新增 `.log`/`.trace.log`；记录生成、注册、所有权、`Death()`、效果、销毁和必要的地形结果。不得宽泛枚举整个场景、所有投掷物或无关对象。

持续到取得多个正常与异常样本、根因证据足够，或用户要求停止；不使用固定倒计时，也不重复读取无变化的完整状态。证据不足时只提出一个关键缺失字段或一次受控复现要求。客户端连接消失后停止向该端发送运行时指令，待其恢复后重新确认基线和日志。

MCP 默认只读。传送、修改生命或速度、切换关卡、模拟输入、执行运行时代码和临时注入都必须获得当次明确授权，并记录目标、指令与前后状态；不得把临时运行时修改混同为正式源码修复。

### MCP 热修复记录

MCP 热修复用于缩短“定位问题 -> 验证假设”的循环，可以在不重新编译和重启游戏的情况下替换当前进程中的窄范围方法。它只存在于当前游戏进程，进程停止、关卡重载或主动卸载后即失效，不能作为发布版本或最终验收依据。

每次热修复都要单独记录以下字段：

```text
hotfixId=唯一名称或版本
time=开始/结束时间
authorization=本次明确授权来源
targets=Host、Client 或具体 MCP 端点
baseBuildHash=应用前双方 BUILD_INFO
scene/map/session=场景、Workshop ID、sessionId
patchScope=目标类型、方法、RPC 或字段；禁止写成“全场景修复”
before=应用前关键状态和日志游标
actions=实际注入、复现和回退动作
after=应用后状态、日志事件和异常
result=假设验证结果及未解决问题
handoff=正式源码文件、构建哈希和后续验收要求
```

热修复操作约束：

- 先建立双方 `ping`、场景、地图、传输方式、`sessionId` 和 `buildHash` 基线，再注入；Host 与 Client 必须分别记录补丁是否成功。
- 一个热修复只验证一个假设，限定到已知方法、NID 或对象；不宽泛枚举场景，不把自动输入结果当成人工复现。
- 记录注入前后状态和新增日志，包含正常样本、异常样本和补丁版本；连接中断时停止向该端发送指令。
- 验证通过后把实际行为迁移到正式源码，删除或卸载临时补丁，执行标准 Release 构建并重启双方游戏进行回归；热修复日志只能作为定位证据。

### 专项验收

- AFK：启动日志应有 `AFK_DIAGNOSTICS_PATCH playerUpdate=True; dropoutRpc=True`；目标端无输入至少 35 秒，对齐双方 `AFK_TIMER`、`AFK_STATE`、`PLAYER_DROPOUT` 和槽位/存活人数。开启防 AFK 时应有 `prevention-active`，不应有本机 `timeout-triggered`。
- 道具：双方核对同一位置的数量/类型；满弹药站在箱子上不得持续播放反馈，消耗弹药后可拾取一次；MechDrop、RCCar 等显式特殊箱保持原类型。金色奖励没有当前专项同步实现，不能按稳定键或权威类型作为验收依据。
- 实体终态与主动引爆：Mook 应在双方完成一次死亡链并收敛尸体终态；DemolitionBro 应只发生一次主动爆炸；McBrover 按其 [独立 issue](../issues/ISSUES-2026-08-28-McBrover火鸡主动引爆后残留实体.md) 的 NID、`Death()` 与最终销毁条件验收。
- 关卡结果：确认 `Level outcome diagnostics enabled; patched methods=2.`，分别触发扣命、通关和失败，检查 `LEVEL_OUTCOME` 前后快照。
- 酸液/死亡链（已完成回归，2026-08-30）：双方使用同一 `sessionId` 和 `buildHash`，在 `Test Evan2 / Bromandy_Ptr1 / levelIndex=7` 交换验证房主和加入方分别接触酸液。两端均由实际接触者正常死亡，出生区玩家保持存活；加入方本地预测降低了死亡体感延迟，Host 仍负责权威校验和同步。复查日志时继续对齐 `authority-gate`、`authority-request/reject/apply/applied`、`CoverInAcidRPC`、`PlayerHasDiedRPC` 与 `LEVEL_OUTCOME`，确认只有实际接触酸液的英雄 NID 进入死亡链。完整记录见 [酸液问题 issue](../issues/ISSUES-2026-08-30-Workshop联机酸液池导致双方一起死亡.md)。
- 可选 Mod：先比较双方安装/启用状态、版本、`rosterHash` 和 `selectedHash`。指纹不同只证明角色环境不同，不能单独作为英雄生成失败的根因。

## 诊断日志

日志目录：

```text
%USERPROFILE%\AppData\LocalLow\Free Lives\Broforce\CustomMapMultiplayer\
```

远端测试参与者应从自己的 Windows 用户数据目录导出日志；公开文档不记录内网地址、共享路径或用户名：

```text
<远端用户目录>\AppData\LocalLow\Free Lives\Broforce\CustomMapMultiplayer
```

该目录是 Windows 下实际诊断日志目录，UMM 的“打开诊断日志目录”按钮、日志写入和启动日志中的 `Diagnostic log directory` 使用同一路径。分析双端会话时由各参与者分别提供日志；不要在 UMM DLL 部署目录中查找诊断日志。

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
| Workshop 地图 | 缺订阅时的自动识别、中文提示和加载阻止已通过双端实测；加入方残留本地配置隔离及房主地图自动识别已通过官方 Steam 大厅和 FRP Direct 实测；酸液已完成当前测试地图房主/加入方回归，其它地图、高延迟和长期运行仍需覆盖；`GeneratePole.Awake`、`BroBase` 或特效可能抛出地图自身异常 |
| Workshop 切关 | `3715087178` 的重复结束动作保护已实现并构建，普通成功、静默成功、失败重试和最终结算仍待双端复测；`3781818421` 仍作为独立问题保留 |
| Mook 终态 | 普通网络 `PolymorphicAI` Mook 的死亡事件与尸体终态同步已实现；活动 AI、敌方弹体、钱币和载具不在当前范围，重启后双端验收仍需覆盖 |
| 主动引爆 | DemolitionBro 已有恢复实测；McBrover 火鸡 NID 同步已实现但残留仍可复现，发生概率已显著降低但根因未闭环；普通 Grenade 保持原行为 |
| 道具 | 标准弹药箱确定性、远端角色扫描抑制、重复收集抑制和满弹药退避已实现；金色奖励稳定键/权威类型同步不在当前构建，官方 Steam 大厅和更多地图待复测 |
| 其它 Mod | Swap Bros 只有只读诊断，尚未完成兼容性验收，也不会阻止环境不一致的会话 |
| FRP Direct | 三机基础联机和静态 `1` 人房满员提示已通过；代码支持地图内动态设置 `1` 至 `4` 人上限并保留现有成员；四机、`2` 至 `4` 人容量边界、降额后重入、多地图、高延迟、长期稳定性和主机迁移仍未专项验收 |
| 原生崩溃 | 异常与崩溃时间接近不能单独证明因果，必须结合双方诊断、UMM 日志和 `error.log` |

## 构建与部署

构建或部署前必须读取项目根目录的 `LocalBroforcePath.props`：

1. `BroforceManagedPath` 是本机 Broforce `Broforce_beta_Data/Managed` 目录，其中必须含 `UnityEngine.TextRenderingModule.dll`。
2. `UnityModManagerPath` 是含 `UnityModManager.dll` 和 `0Harmony.dll` 的本机 UMM 核心目录。
3. `TestDeployModPath` 是本机测试机部署目录；值为空表示明确关闭额外测试部署。
4. 该文件包含本机专用路径，只允许用于执行构建或部署，不得写入公开文件、提交信息、日志摘录或对外回复。
5. 使用兼容 .NET Framework 3.5 的编译器。当前验证路径：`C:\Windows\Microsoft.NET\Framework64\v3.5\csc.exe`；不要直接使用 v4 编译器。

唯一标准入口：

```powershell
powershell -ExecutionPolicy Bypass -File .\BuildAndDeploy.ps1
```

有效输出位置：

```text
<项目根目录>\CustomMapMultiplayer\CustomMapMultiplayer.dll
<本机 UMM_PROFILE_DIR>\Mods\GJKen-CustomMapMultiplayer\CustomMapMultiplayer.dll
```

脚本输出并嵌入 `Build hash`，覆盖 DLL；项目安装包固定包含 `Info.json`。部署目标的 `Info.json` 每次均从 `modinfo.json` 同步，保证名称、版本和入口与当前构建一致。若配置了可选测试部署目标，目录创建或复制失败时整个部署失败，不得继续双端测试。

`CustomMapMultiplayer.csproj` 的 `OutputPath` 也指向项目安装包；`bin\Debug` 旧文件不得用于测试。IDE/MSBuild 只有正确读取本机 props 并执行构建后目标时才可替代脚本。

安装包结构与命名：

```text
CustomMapMultiplayer\
  CustomMapMultiplayer.dll
  Info.json
```

复制到 UMM 后目录名必须为 `GJKen-CustomMapMultiplayer`，程序集名保持 `CustomMapMultiplayer.dll`。脚本不更新 r2modman 缓存包。

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
