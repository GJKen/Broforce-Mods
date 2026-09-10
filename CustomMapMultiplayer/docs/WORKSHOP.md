# Custom Map Multiplayer：Workshop 与游戏状态

[返回开发文档索引](DEVELOPMENT.md) · [架构与代码职责](ARCHITECTURE.md)

## Workshop 地图注入

主机首次选择任务时，Mod 将配置写入游戏状态：

- `customLevelID` 来自 Workshop ID。
- `loadCustomCampaign=true`。
- `sceneToLoad` 来自场景设置，默认 `Test Evan2`。
- 非空的 Workshop campaign name 写入 `campaignName`；留空时保留原生值。

当前注入点为 `WorldMapController.EnterMission`、`GameState.LoadLevel`、`GameModeController.SwitchLevel` 和 `SteamController.LevelLoadCompleteEvent`。每个房间只在首次选择任务时注入一次；创建或加入新大厅时清理旧 Workshop 回调、切关和暂停网络状态。

房间信息携带 Workshop ID、场景名、可选战役名和 `loading`/`ready` 阶段。Steam 使用 Lobby 数据，FRP 通过 `FrpDirectRoomInfo` 同步。房主创建房间、选择地图、新成员加入或主机迁移时发布当前身份；加入方采用房主值作为本次会话配置，不改写本机持久化设置。

从 `JoinLobby` 开始到房主元数据到达前，Client 配置读取返回空值，不允许回退到本机保存的 ID、场景名或战役名；元数据到达后只使用房主值，避免加入方旧配置在最初几帧误加载本机地图。

加入方采用地图身份后枚举 Steam 本机订阅列表。确认未订阅时按 UMM 语言显示带 Workshop ID 的提示，清除待执行的晚加入状态，并在早于 `GameModeController.LoadNextScene` 的 `LevelSelectionController.GotoNextCampaignScene` 入口阻止指向该房主地图的切换，同时保留 `GameState.LoadLevel` 后置保护；该前置拦截已通过双端实测确认不会进入 Workshop 加载动画。订阅状态无法读取时保持原生下载流程，不误报缺图。订阅或下载不会由 Mod 自动执行。

关闭注入时必须清理 `_injectedForSession`、`GameState.loadCustomCampaign`、`customLevelID`、`sceneToLoad`、Workshop Lobby 元数据及暂停/切关状态。统一入口为 `ClearInjectedWorkshopRuntimeState`，覆盖 UMM 关闭开关、停用/卸载 Mod 和 Steam `LeaveMatch`。热关闭不会主动切换当前场景；退出并重新创建官方房间后恢复原生选图。

## Workshop 地图目录与选择页

UMM 左侧的 `Workshop 地图配置` 页面同时支持手工 ID 和已订阅地图选择。点击“选择已订阅地图”后，Workshop 配置内容区切换为地图选择页，不使用 `GUI.Window` 或屏幕坐标浮层；右上角 `X` 或 `Esc` 返回配置页。选择地图后只保存 `Settings.WorkshopId`，不会自动返回，也不会覆盖战役名或场景名。

地图目录来自 Steam 本机已订阅项目。选择页支持标题或 Workshop ID 搜索、`全部`、`最近联机地图`、`已标星`筛选、手动刷新和收藏。当前处于`已标星`且存在星标地图时，标签旁显示 `×`，点击会清空全部本地星标，但不会取消 Steam 订阅；`全部`和`最近联机地图`标签不显示该按钮。地图以两列网格显示，标题保持单行；已选地图使用高亮边框和状态提示。目录会保留未安装、未完整下载和战役文件不可读的项目，手工输入不在目录中的数字 ID 仍然有效。

Mod 不会替用户订阅、搜索社区或主动下载地图。实际地图下载仍由 Broforce/Steam 原生流程处理。`最近联机地图`只在 Workshop 地图完成加载后记录，不限制数量；旧版本按点击选择保存的历史会在设置迁移时清空一次。

## Workshop 加载缓存

`SteamController.LoadLevel` 在有效 Workshop 线上会话中会先检查 Steam 已安装的 Workshop 目录，并尝试读取目录内的本地战役文件；读取失败或内容不可用时，才继续原生 Steam UGC 下载流程。旧版 UGC 回调中的可读缓存也可以直接完成当前地图加载。

同一张地图已有加载请求时，后续重复请求会被抑制，直到加载完成或请求超时；离开房间、切换会话和关闭注入时会清理该请求状态，避免旧地图的加载回调影响后续官方地图流程。

## 房主退出与 Host migration

Steam 房主离开后，网络层可能先把当前加入方标记为新的 Host。Mod 会在 `ConnectionLayer.RemovePlayer` 清理旧房主前记录其 PID，用于确认角色变化来自旧 Host 离开；成员统计包含当前本地加入方，不会因为没有其它远端 PID 而被误判为需要退出：

- 只要当前加入方仍在 Steam 房间内，即使它是唯一剩余成员，也计为有效成员并继续执行官方 Host migration；不会主动调用 `LeaveMatch` 或返回原生 `MainMenu`。
- Host promotion 会保留 `GameState.loadCustomCampaign=true`，重新发布当前地图、关卡编号、Workshop 身份和大厅 ready 状态。
- 原生主菜单退出保护只用于明确的 `LeaveMatch` 或关闭注入清理，不参与正常的 Steam Host migration。
- 只有连接表中已没有任何有效成员时，才清理会话并返回原生 `MainMenu`。

FRP Direct 的房主退出仍按现有协议结束房间，不支持主机迁移；用户在排除本轮新增退出保护的实验构建中确认加入方会直接返回主菜单，没有复现本 issue 的黑屏。Steam 单加入方房主退出后会保留房间并完成官方 Host migration，已由用户验收；多加入方和异常状态边界见[Workshop 房主退出后加入方返回黑屏](../issues/ISSUES-2026-09-05-Workshop房主退出后加入方返回黑屏.md)。

## 晚加入与重入

晚加入流程等待玩家列表稳定 250ms，再用本机主控制器调用一次 `HeroController.AddLocalPlayer(-1, controllerId)`。已有本地槽位或待处理请求时复用，避免重复生成 P2-P4；45 秒内没有观察到本地 `Player.Start` 或 `SetPlayerCharacter` 时允许重试一次。创建方仍在 `newJoin`/选关界面时不启动地图加载，进入 Workshop 过场后最长等待约 120 秒。

Host 只在晚加入 Workshop 会话中放宽 `RequestJoinGame` 的关卡完成和控制器注册保护。请求成功后，拥有角色的一端重发权威 `SetSpawnPositon`；房主调用 `InstantiationController.SendInstantiatedPrefabs(requesteeID)`，只向新 PID 重放 buffered `PlayerPrefab` 和角色实例。

成功判据：

- Host 出现 `Late workshop RequestJoinGame state after native handling`、`Workshop spawn-position rebroadcast completed with authoritative current positions`；重入时还应出现 `Late workshop replayed buffered network instances to the joining client`。
- Client 依次出现 `Starting late workshop join load`、`Late workshop client scene loaded`、`Late workshop SpawnJoinedPlayers observed`、`Late workshop join requested a local player slot after scene readiness` 和 `Late workshop automatic join completed`。
- 最终场景中双方有正确的 P1/P2；重入客户端重新记录远程房主 P1 的 `Player.Start`、`RegisterHeroToPlayer` 和 `SetPlayerCharacter`。

完整根因和历史方案见 [重入与第 4 关黑屏记录](../issues/ISSUES-2026-08-22-重复退出重入加入方失败与3781818421进入第4关黑屏.md)。

## 英雄与控制器恢复

游戏原本的 `RequestHeroTypeFromMaster` 流程继续保留。Workshop 本地玩家等待 18 秒仍无回复时，使用原生 `GetHeroType` 和 `Player.SpawnHero` 做一次本地备用生成；已有角色、远程玩家和正常收到回复的玩家不进入备用分支。

Workshop 玩家发生 `Dropout` 后，按槽位保存英雄类型和本地 `playerControllerIDs`。重新请求英雄、备用生成和 `AddLocalPlayer` 优先恢复这些值；`Player.Start`/`登记` 阶段修正原生写回的错误控制器。角色存在但无法操作时，优先比较掉线前后的控制器绑定，而不是只检查 `character`。

## Workshop 出生点与复活位置

`多人游戏选项` 中的 `Workshop 复活位置修复` 默认启用。有效的 Workshop 联机注入场景中，Mod 只在角色首次部署时记录一个稳定的直接地图出生点：`CustomSpawnPoint`、非空投且坐标有效。后续 `DropInDuringGame` 复活如果得到负 X 或明显高于屏幕上限的异常位置，则使用该首次出生点备份，并继续通过原生 `Player.SetSpawnPositon` 应用。

该开关不改动运输、降落伞、检查点、救援或笼子出生，也不覆盖正常的后续复活位置。关闭后不记录或应用这项备份；开关会在运行中读取，设置保存后即可影响后续出生处理。出生点跟踪会在 Workshop 场景加载、离开房间和会话清理时重置，避免旧地图的位置泄漏到新会话。

## Workshop 道具

当前补丁只在有效 Workshop 线上会话启用：

- 普通 `Standard` 箱保持标准弹药，显式特殊箱保留原类型。
- 只有本机拥有的角色扫描本机道具。
- 已消费或停用道具的重复 `Collect` 被忽略。
- 弹药已满时只在本机提供一次原生反馈，不发送无效 `TargetAll` RPC；离开道具后可再次反馈，未消费道具有 0.5 秒退避。

离线、普通线上原版关卡和未启用有效 Workshop 注入的会话保持原生行为。当前仅有 FRP 双端道具证据，官方 Steam 大厅和更多地图仍需复测。

## Mook 终态与主动引爆

实体同步只覆盖普通、非载具、非 Boss 的网络 `PolymorphicAI` Mook：拥有端广播首次死亡事件和最终停稳位置，远端用非空 `DamageObject` 补全死亡链。终态提交只遍历待提交的 NID 候选集合；完成且不再等待其它状态的记录保留 15 秒，每 5 秒清理一次。

`DemolitionBro.currentBomb` 与 `McBrover.currentTurkey` 的二次按键各有独立 Harmony 转译：拥有端执行原版 `Projectile.Death()` 并发送 NID 与位置，远端按 NID 以会话内幂等集合处理。DemolitionBro 已有恢复实测；McBrover 残留仍可复现但概率显著降低，见 [独立 issue](../issues/ISSUES-2026-08-28-McBrover火鸡主动引爆后残留实体.md)。

## 关卡结束保护

部分 Workshop 地图会在 `GameModeController.switchingLevel=true` 后继续触发成功结束流程。当前补丁只在有效线上 Workshop 会话、配置场景和 Workshop ID 均匹配时，抑制切关期间重复的 `LevelEndSuccess`/`LevelEndSuccessSilent` 和成功结算重入；第一次结束动作、失败重试和其它场景保持原生行为。见 [3715087178 黑屏记录](../issues/ISSUES-2026-08-26-3715087178联机通关黑屏与关卡结束重入.md)。

## 酸液死亡链

统一拦截英雄 `CoverInAcid` 基入口，在 Workshop 在线场景中执行场景级 `DoodadAcidPool` 扫描、加入方本地预测、Host 权威请求/校验/应用，并在 `DamageType.Acid` 命中入口处理不产生酸液池的投掷酸液。系统记录酸液 RPC 与玩家死亡 RPC 前后状态。Host 周期扫描本机和远程英雄，Client 本机命中后先执行本地原生酸液 RPC，再请求 Host 确认；远程镜像只等待授权应用。离线、普通官方联机、非配置场景和非英雄对象执行原生行为。

当前回归已覆盖 `Test Evan2 / Bromandy_Ptr1 / levelIndex=7` 的房主和加入方分别接触酸液；实际接触者死亡，出生区玩家不再被连带死亡。投掷酸液命中角色的修复也已通过用户实机验收。完整记录见 [酸液池问题 issue](../issues/archive/ISSUES-2026-08-30-Workshop联机酸液池导致双方一起死亡.md) 和 [投掷酸液问题 issue](../issues/ISSUES-2026-09-07-投掷酸液命中角色未正常死亡.md)。
