# Broforce 第三方地图联机 Mod：开发文档

这是项目的详细开发记录。项目概览、安装方式和 Git 首页说明请返回 [根目录 README](../README.md)。

## 项目目标

为 Steam 版 Broforce 制作一个 Unity Mod Manager + Harmony Mod，使所有参与者在安装同一个 Mod、并通过 Steam 创意工坊订阅相同第三方地图的前提下，可以使用官方已有的 Steam 多人游戏流程联机游玩第三方地图。

本项目是定位并修改游戏对第三方地图进入在线流程的限制。

## 已确认前提

1. 所有参与者都会安装同一个 Mod。
2. 所有参与者都会通过 Steam 创意工坊订阅同一张第三方地图。
3. 地图一致性由玩家订阅状态和 Steam 创意工坊负责；必要时再检查地图标识、名称或本地加载状态。
4. 目标是复用官方多人流程，如果行不通则考虑做一套房间、同步或网络协议。
5. `Assembly-CSharp.dll` 证实可以通过修改dll支持中文输入法输入中文(原本不支持), 后续可以考虑这个方向, 修改前需备份

## 已确认的官方联机流程

游戏中没有“创建第三方地图大厅”的独立选项。当前观察到的官方流程是：

1. 启动游戏。
2. 进入“开始”。
3. 进入“街机模式”。
4. 选择困难度。
5. 选择“离线游戏”或“线上主持游戏”。
6. 选择“线上主持游戏”后，设置房间名、密码和玩家数量限制。
7. 进入到p1-p4玩家自主确认界面, 此时已经可以让朋友在大厅加入, 优先按下攻击键的按键则默认为p1, p1确认后会自动倒数 3 2 1 游戏随即开始进入地图
8. 按 `Esc` 可以打开 Steam 好友邀请界面，通过 Steam 搜索好友并邀请加入, 并且能看见双方联机的名称

因此，本项目不应假设存在“先选择第三方地图、再创建线上大厅”的官方入口。更准确的目标是：

> 先按官方流程创建线上房间，等待房间建立后，再由 Mod 将主机和其他已加入玩家带入同一张创意工坊地图。

中文输入法属于用户已有的汉化/输入法修改背景，不作为本项目联机实现的一部分；官方是否原生支持中文输入法暂不作为本项目假设。

## 技术路线

优先使用 Wiki 中介绍的 Unity Mod Manager 与 Harmony 运行时补丁方式：

1. 使用 `broforce-tools` 创建 Mod 项目和构建配置。
2. 使用反编译工具查看官方线上房间建立完成后的状态、当前关卡启动和玩家加入流程。
3. 定位“主持游戏后直接进入官方地图”的调用链，确认地图加载参数是在房间创建时、房间建立后，还是关卡启动时确定的。
4. 搜索在线模式下的地图类型判断、官方地图限制、创意工坊地图加载和地图 ID 传递逻辑。
5. 用 Harmony 对最小必要方法打 Prefix、Postfix 或 Transpiler 补丁。
6. 先实现“双端已订阅同一地图”的最小可行版本。
7. 再处理地图标识传递、加载失败提示和版本兼容问题。

## 首要验证问题

需要先确认第三方地图无法联机的具体阻断位置：

- 官方线上房间建立后，当前官方地图是由哪个方法选择和启动的。
- 房间创建、房间建立和当前关卡启动之间，是否存在可替换的地图参数或状态。
- 房间信息是否只传递官方地图 ID。
- 其他玩家通过房间列表或 Steam 好友邀请加入后，客户端在哪个阶段接收并解析当前关卡信息。
- 客户端是否会在本地地图解析、创意工坊资源加载或进入关卡阶段拒绝第三方地图。
- 第三方地图是否可以复用官方线上房间已经建立好的网络会话和玩家同步状态。

## MVP 范围

第一版只支持以下条件：

- 所有玩家使用相同版本的 Mod。
- 所有玩家已订阅并已下载相同的创意工坊地图。
- 不自动传输地图文件。
- 不修改 Steam 网络层。
- 不保证所有第三方地图、地图脚本或其他 Mod 都兼容。

成功标准：主机先通过官方流程创建线上房间；房间建立后选择或切换到一张已订阅的第三方地图；其他安装相同 Mod 且拥有该地图的玩家能够通过房间列表或 Steam 好友邀请加入，并共同进入该关卡。

## 后续阶段

### 阶段 1：建立开发环境

- 安装并配置 Unity Mod Manager。
- 根据 Wiki 创建可加载的空 Mod。
- 确认 Visual Studio、C# 编译、Harmony 引用和 Mod 输出目录。
- 使用普通官方地图验证 Mod 不影响原有联机。

### 阶段 2：定位地图限制

- 获取当前 Steam 版本对应的游戏程序集。
- 使用 dnSpy、ILSpy 或同类工具阅读相关类和方法。
- 记录线上房间建立、官方地图自动启动、房间列表发现、Steam 好友邀请、玩家加入和地图加载的调用链。
- 不直接修改原始游戏 DLL，所有实验优先做成运行时 Harmony 补丁。

### 阶段 3：实现双端本地同图联机

- 保留官方线上房间创建、房间密码、玩家数量限制、房间列表发现和 Steam 好友邀请的原有行为。
- 在房间建立后，将主机的下一关/当前关卡启动目标替换为创意工坊地图。
- 让客户端在加入官方线上房间后解析同一张创意工坊地图，而不是重新创建另一套房间。
- 对创意工坊地图使用游戏已有的地图标识、创意工坊 ID 或本地加载路径。
- 在主机和客户端分别记录关键方法的参数与返回值。
- 用两名玩家完成“创建线上房间 → 加入 → 共同进入第三方地图”的最小联机测试。

### 阶段 4：错误处理与兼容性

- 地图未下载时给出明确提示。
- 地图 ID 或版本不一致时阻止进入并提示重新订阅或更新。
- Mod 版本不一致时给出提示。
- 记录不同地图类型、地图脚本和其他 Mod 的兼容性。

## 可以做的事情

- 实现自定义 Steam 房间服务器
- 重写官方多人同步协议
- 修改替换 `Assembly-CSharp.dll`

## 参考资料

- BroforceMods Wiki: https://github.com/alexneargarder/BroforceMods/wiki
- Wiki 介绍了 Broforce 没有官方 Mod API，通常通过阅读游戏代码并使用 Harmony 修改运行时函数；同时提供 Unity Mod Manager、`broforce-tools`、Harmony Patch 和查看游戏代码等页面。

## 当前状态

- 文档初始化完成。
- 已创建只观察、不修改游戏行为的 UMM 诊断 Mod 工程。
- 已通过系统自带的 .NET Framework 3.5 C# 编译器生成 `bin\Debug\BroforceOnlineDiagnostics.dll`。
- 已将 Mod 部署到迁移后的 r2modman profile，UMM 最新日志已识别 `1/1` 个 Mod。
- `0.1.0` 已完成 `Plugin.Load()`、启用/禁用和本地 `diagnostics.log` 的运行验证。
- 已使用官方线上主持流程采集场景链路，确认会经过 `newJoin`、`MissionScreenVietnam` 并进入官方关卡 `Test Evan2`。
- 已对 `Assembly-CSharp.dll` 完成首轮只读类/方法扫描，确认 `SteamLayer`、`RoomInfo`、`GameModeController`、`LevelSelectionController` 和创意工坊菜单类是主要候选入口。
- `0.2.0` 已加入方法级 Harmony 追踪并部署，已完成首次游戏运行验证，共产生 29 条 TRACE 记录。
- `0.3.0` 已实现主机 `GameState` 状态注入，并部署到当前 UMM profile；首次 `LoadNextScene` 注入因导致黑屏已撤回。
- 已实现测试性的线上地图状态注入，并已在朋友测试中验证双方能够进入同一张 Workshop 地图；当前仍存在英雄类型回复不同步和原生崩溃风险，尚未达到稳定可发布状态。
- 已测试开启大厅后能看见地图属性为:自定义地图,和显示地图名称为:三方图地图名称

## 补充参考：BroMaker 开发 Wiki

BroMaker Wiki 也是本项目的参考资料：

- https://github.com/alexneargarder/Bro-Maker-Abilities-Wiki/wiki

该 Wiki 主要讲解如何为 Broforce 制作自定义 Bro，包括 JSON 方式、C# 编程方式、Mod 文件、开发环境、`broforce-tools`、调试和查看 Broforce 代码。

与本项目最相关的部分是：

- `Viewing Broforce's Code`：用于学习如何阅读游戏程序集。
- `Overriding Methods to Add New Functionality`：用于学习如何覆盖或补丁现有游戏方法。
- `Glossary of Important Methods`：用于建立游戏方法名、类和调用关系的记录。
- `Setting up a Development Environment`、`Using the broforce-tools Script` 和 `Debugging`：用于搭建和调试 Mod 工程。

该 Wiki 主要面向自定义 Bro 能力，不直接提供第三方地图联机方案。但它可以为本项目提供 Mod 工程结构、方法覆盖、程序集阅读和调试方面的实际示例。

## 第一阶段：诊断 Mod 骨架

当前已创建一个只观察、不修改游戏行为的 Unity Mod Manager 插件骨架：

- `BroforceOnlineDiagnostics.csproj`：面向 `.NET Framework 3.5` 的工程文件。
- `modinfo.json`：Unity Mod Manager 插件清单。
- `LocalBroforcePath.props.example`：本机 Broforce 和 Unity Mod Manager 程序集路径示例。
- `src/Plugin.cs`：UMM 加载、启用、禁用和卸载入口。
- `src/DiagnosticsBehaviour.cs`：场景切换、当前场景和 Unity 错误日志观察。
- `src/DiagnosticLog.cs`：写入 Unity 日志和本地诊断日志。
- `src/ReflectionProbe.cs`：只读扫描 `Assembly-CSharp` 中可能与在线、房间、玩家和网络相关的类型名。
- `src/HarmonyDiagnostics.cs`：只读追踪线上房间、Steam Lobby、关卡切换和创意工坊地图入口，不修改参数或返回值。
- `src/DiagnosticSettings.cs`：保存测试 Workshop ID、地图名称、场景名和是否启用线上注入。

插件启用后会记录：

- Unity 版本和持久化数据目录。
- 当前场景、场景加载事件和场景变化。
- `Assembly-CSharp` 中匹配到的模式提示。
- Unity 错误和异常日志。
- 线上主持、Steam Lobby 创建/加入、`RoomInfo` 地图字段和 `GameState` 关卡加载参数。
- 创意工坊地图启动和关卡数据加载方法。

方法级追踪不记录房间密码、Steam ID、主机名或创意工坊作者身份。

### 0.3.0 测试性状态注入

联网主机在真正选择任务时会在 `WorldMapController.EnterMission()` 的 `AdminRPC<GameState>` 发送前注入；对于当前线上战役实际使用的任务界面，还会在任务界面淡出后的 `GameState.LoadLevel()` 前注入，并清除仍驻留的官方 `CurrentCampaign`，让游戏进入创意工坊下载分支。这样大厅进入世界地图的流程保持官方逻辑，但选择哪个官方任务都会被替换为设置中的 Workshop 地图。`GameModeController.SwitchLevel()` 仍保留同样的注入作为后续切关卡的后备路径：

- `customLevelID`：默认测试值 `456121589`。
- `loadCustomCampaign`：`true`。
- `sceneToLoad`：默认 `Test Evan2`。
- `campaignName`：默认 `the sweet taste of freedom 3`，与日志中实际解析出的 Workshop 战役名一致。

注入默认关闭，在 UMM 设置面板中勾选 `Inject configured workshop map into online level switching` 后才会生效。每个线上房间只在首次选择任务时注入一次，避免重置 Workshop 战役自身的后续关卡。这是固定测试地图的 MVP，尚不是最终的地图选择 UI。

### 更换 Workshop 地图

不需要重新编译或更换 DLL。主机和朋友都要订阅同一张 Workshop 地图，并在 UMM 设置中填写相同的值：

1. `Workshop ID`：填写 Workshop 页面 URL 中 `id=` 后面的数字。
2. `Workshop campaign name`：填写地图下载后日志里的内部战役名；不确定时可以先留空，因为这是可选项。网页标题不一定等于内部战役名。
3. `Custom level scene`：标准 Workshop 战役通常继续填写 `Test Evan2`，它是游戏通用场景名，不是地图名称。

保存 UMM 设置后，双方完全退出并重新启动游戏，再开启注入、创建大厅和加入。每次换图都必须让双方拥有相同 Workshop ID 和相同地图文件版本。

实际逆向确认的线上首关调用链为：

```text
LevelSelectionController.GotoNextCampaignScene
  -> GameModeController.LoadNextScene(MissionScreenVietnam)
  -> MissionScreenController/Fader.Update
  -> GameState.LoadLevel(Test Evan2)
```

当前版本同时保留三个观测/注入点：`WorldMapController.EnterMission`、`GameState.LoadLevel` 和 `GameModeController.SwitchLevel`。实测线上战役首关没有调用 `WorldMapController.EnterMission`，因此首关注入以 `GameState.LoadLevel` 为主。

2026-08-17 13:01 的测试中，`GameState.LoadLevel` 注入已经执行，但官方 `CurrentCampaign` 仍驻留，游戏没有进入 Workshop 下载分支，导致 `GameState.LoadLevel("Test Evan2")` 每 5 秒重复。随后清除 `CurrentCampaign` 后，2026-08-17 13:07 的日志确认已经进入 Workshop 分支，但仍停留在 `MissionScreenVietnam`，原因是只调用了 `SteamController.LoadLevel`，没有注册官方的 `SteamController.LevelLoadCompleteEvent` 完成回调；因此下载完成后没有恢复 `currentCampaign` 并继续加载场景。

当前修复已订阅该完成事件：Workshop 下载成功后将 `Campaign` 写回 `LevelSelectionController.currentCampaign`，设置线上自定义战役状态，再调用原生 `GameState.LoadLevel("")` 进入 `LoadingScreen`。插件停用/卸载时会解除订阅，避免重复回调。2026-08-17 21:17 已按 .NET Framework 3.5 重新编译并部署；下一次测试必须完全重启游戏，日志应出现 `Workshop level-load completion callback subscribed.`、`Workshop level-load completed; resuming GameState.LoadLevel.`，随后进入 `LoadingScreen` 和目标地图，不应再每 5 秒重复 `GameState.LoadLevel("Test Evan2")`。

2026-08-17 21:22 的测试中，第一次大厅已成功下载并进入三方图，但 `SteamController.OnLevelLoadComplete` 在同一大厅重复触发，造成重复的 `GameState.LoadLevel("")`。退出后第二次大厅的 `LevelSelectionController.shownHelicopterIntro` 仍为 `true`，官方流程因此跳过 `MissionScreenVietnam`，直接使用残留的 `sceneToLoad="Test Evan2"`；当时 `loadCustomCampaign=False`，所以实际加载的是官方同名场景。当前修复会在新建或加入大厅时清理 Workshop 状态、重置 `shownHelicopterIntro`，并且每个大厅只处理一次 Workshop 完成回调。

朋友客户端日志（最新会话从 `2026-08-17T13:39:39Z` 开始）确认 Steam Lobby 加入成功，但没有收到主机的有效任务状态：`13:40:10` 的 `SteamLayer.LobbyJoined_Callback(ioFailure=False)` 之后，约一分钟内没有 `GameModeController.LoadNextScene`、`MissionScreenVietnam` 或 Workshop 完成记录，随后在 `13:41:11` 离开大厅。第二次加入在 `13:41:35` 收到 `sceneToLoad="WorldMap3D"`、`returnToWorldMap=True`、`campaignName=""`、`loadCustomCampaign=False`，并在 `13:41:40` 返回主菜单。这表明卡点发生在主机状态同步/房间时序，而不是 Steam 加入失败或 Workshop 地图下载失败；较早会话已经证明该客户端可以收到 `customLevelID="456121589"` 的自定义战役状态。该日志未发现最新会话中的新异常，当前 DLL 无需更新。

下一次联机测试必须让主机创建大厅后停留在 `newJoin`，朋友先加入并确认出现在大厅，再由主机选择任务。双方应完全退出游戏后各自开始一次干净测试，并同时提供同一时间段的 `diagnostics.log` 和 UMM `Core\\Log.txt`；仅凭客户端日志无法判断主机是否已经离开任务、推送了返回世界地图状态，或房间状态是否过期。

2026-08-17 22:49 的日志 6 已同时对照朋友端日志和本机主机日志。朋友端成功执行 `OnJoinedLobby`，注册了双方 PID，执行了 `DeserializeForJoin` 并进入目标 Workshop 地图；地图内也执行了 `SpawnJoinedPlayers` 和 P1 的 `AddPlayer`。但是朋友端随后没有执行 `Player.SpawnHero`、`Player.InstantiateHero` 或 `RegisterHeroToPlayer`。主机端稍后加入 P2 时完整执行了上述英雄生成流程，因此当前断点已经缩小到朋友 P1 的玩家对象初始化或向主机请求英雄类型的阶段，不再是大厅加入、PID 注册或地图加载问题。诊断已继续增加 `Player.Awake/Start/RespawnBro`、玩家丢弃流程和英雄类型请求/响应流程的追踪。

2026-08-17 23:06 的日志 7 进一步确认：朋友端 P1 已完成 `Player.Awake`、`Player.Start` 和 `Player.RespawnBro`，并调用 `RequestHeroTypeFromMaster`；约 4 秒后，主机执行了对应 P1 的 `RequestHeroTypeFromMasterRPC`，但朋友端始终没有收到 `RecieveHeroTypeFromMaster`，因此 P1 一直停在 `_awaitingHeroTypeFromServer=True` 且没有角色。主机本地 P2 的同一请求/回复链路立即成功。当前恢复机制仅在配置的 Workshop 场景中检查本地玩家：保留游戏原本的英雄类型请求，等待 18 秒仍无回复时，使用游戏自己的 `GetHeroType` 和 `Player.SpawnHero` 做一次本地备用生成。已有角色、远程玩家和正常及时收到回复的玩家不会进入恢复分支。

2026-08-18 的日志 8 使用 Workshop ID `3715087178`，下载结果的真实内部战役名为 `Bromandy_Ptr1`，共 15 关；即使设置中的战役名填成 `666`，地图仍成功进入和切换关卡，进一步证明该字段可以留空。备用生成在每次关卡加载后都成功生成朋友端 P1。日志同时证明 Mod 的两次重复请求会制造多条迟到回复，却没有证明它们比游戏原始请求更早成功，因此已删除主动重试，只保留“等待原始请求 18 秒后本地生成”的单一备用路径。多个原始回复在旧版 15 秒保护期之后到达，曾造成角色连续被替换；迟到回复保护现改为状态判断：走过备用生成的玩家只有在确实等待新英雄回复时才接受回复，其余旧回复全部跳过。该日志的 56,788 行中有 53,718 行是重复 `NullReferenceException`；相同 Unity 错误现每 5 秒最多记录一次，并在首次记录中保留调用栈。

### 当前英雄回复策略

日志已经证明官方的 `RequestHeroTypeFromMaster` 回复链路在部分朋友客户端上会丢失，但没有证明存在一条可靠的替代网络传输方法。因此当前代码保留官方请求作为首选；如果本地玩家在 Workshop 场景中等待 18 秒仍没有回复，就使用游戏自己的 `GetHeroType` 和 `Player.SpawnHero` 在本地生成一个英雄。这个备用方案不是把回复重新发送给朋友，而是绕过丢失的网络回复。之前尝试的主动重试会制造迟到回复并导致角色被连续替换，已经删除；迟到回复保护仍需保留。

2026-08-17 21:51 的朋友测试中，`error.log` 记录 Broforce 在 `KERNELBASE.dll` 的原生断点崩溃；朋友端日志在 `SteamLayer.LobbyJoined_Callback(ioFailure=False)` 后停止，没有进入关卡。同一份朋友日志的更早会话曾反复出现 `ArgumentException: invalid utf-16 sequence`，说明 Steam 大厅返回的异常字符串可能被送入 Unity 日志；目前不能仅凭时间关系认定它就是这次原生崩溃的原因。当前修复已在 `DiagnosticLog` 和 Harmony 字段摘要处清洗未配对 UTF-16 代理项，并继续保留错误限频。新 DLL 已重新构建并部署，SHA-256 为 `AF5BCF58F86AC735051525230CB3B00C7E7A533F606D925C9F4CD32CFE986F14`。下一次测试应完全重启双方游戏；若仍崩溃，必须同时收集主机和朋友两端从启动到崩溃的 `diagnostics.log`、UMM `Core\Log.txt` 与新的 `error.log`，以区分原生 Steam 崩溃和 Mod 日志路径问题。

### DLL 构建与分发约束

- 每次构建只使用标准文件名 `BroforceOnlineDiagnostics.dll`，并覆盖项目构建目录、当前 UMM Mod 目录和 r2modman 缓存中的同名 DLL。
- 不额外生成或保留 `test6`、`test7`、日期后缀等临时测试 DLL，避免双方误用不同版本。
- 联机测试需要把当前 UMM Mod 目录中的标准 DLL 直接发给朋友：`<UMM_PROFILE_DIR>\Mods\BroforceOnlineDiagnostics\BroforceOnlineDiagnostics.dll`。

### Git 更新约定

- 本项目属于下级仓库 , 上级目录为 `D:\Study\C#\Broforce-Mods`
- 重大改动完成并确认测试结果后，上级仓库根目录的 `QuickUpdate.bat` 进行同步。
- 该脚本会执行 `git fetch origin main`、`git pull origin main`、全量 `git add .`、自动提交并推送到 `origin main`；运行前必须检查工作区，避免把无关文件一并提交。

### 诊断日志约束

- 不直接追踪 `Update`、`RunHeroRespawnLogic` 等每帧调用的方法；确需观察时应改为追踪其低频下游事件。
- 重复日志必须按“方法、参数和状态”组合限频，不能让参数交替调用绕过去重。
- 部署新增追踪后应先检查一次本机日志增长速度，发现每秒持续写入多行时先修复限频，再进行联机测试。

默认日志文件位于：

```text
<Application.persistentDataPath>/BroforceOnlineDiagnostics/diagnostics.log
```

另一台测试电脑(5700g)的日志当前通过内网共享访问：

```text
\\192.168.1.181\Users\5700G\AppData\LocalLow\Free Lives\Broforce\BroforceOnlineDiagnostics\diagnostics.log
```

每次双端联机测试结束后，应优先读取该路径的日志，并与本机同一时间段的 `diagnostics.log`、UMM `Core\Log.txt` 和 `error.log` 对照分析。该路径不可访问时，向测试人员索取5700g的日志副本，不要仅依据单端日志判断联机时序问题。

### 本机编译准备

1. 复制 `LocalBroforcePath.props.example` 为 `LocalBroforcePath.props`。
2. 将 `BroforceManagedPath` 改为 Steam Broforce 的 `Broforce_beta_Data/Managed` 目录。
3. 将 `UnityModManagerPath` 改为包含 `UnityModManager.dll` 的 UMM 核心目录。
4. 确认引用的 Unity 模块名称与当前 Broforce 版本一致。
5. 构建后把 DLL 放入 Unity Mod Manager 的对应 Mod 目录，并将项目中的 `modinfo.json` 作为 `Info.json` 一起部署。

本机已经确认 Broforce 与 UMM 程序集路径，并写入 `LocalBroforcePath.props`。虽然尚未安装 .NET SDK/MSBuild，但系统自带的 .NET Framework C# 编译器已可成功生成诊断 DLL，当前不需要为此安装 Visual Studio。

手工构建时必须使用 `C:\Windows\Microsoft.NET\Framework64\v3.5\csc.exe`，并显式引用 .NET 2.0 的 `mscorlib`/`System` 和 .NET 3.5 的 `System.Core`。不得使用 `v4.0.30319\csc.exe` 直接构建；该方式会在 DLL 中混入 .NET 4.0 引用，而 Broforce 的 Unity/Mono 运行时只兼容 .NET 2.0/3.5。

当前 Mod 会在设置开启且处于线上模式时替换主机/客户端本地的首关 Workshop 加载状态；未开启设置时仍保持只读诊断行为。它不会自动创建房间，也不会修改原始 `Assembly-CSharp.dll`。

## 本机开发环境信息

已确认以下路径有效：

- Broforce：`<BROFORCE_DIR>`
- Broforce Managed：`<BROFORCE_DIR>\Broforce_beta_Data\Managed`
- `Assembly-CSharp.dll`：位于上述 Managed 目录。
- r2modman 数据目录：`<R2MODMAN_DIR>`
- Unity Mod Manager：`<UMM_PROFILE_DIR>\Core`
- UMM 核心程序集：位于上述 `Core` 目录，当前使用 `UnityModManager.dll` 和 `0Harmony.dll`。
- 当前 r2modman profile 的 UMM `Mods` 目录中只部署了本项目的诊断 Mod。
- Broforce 本体长期未更新，本项目暂不把游戏版本变化作为主要变量。

本机路径已经写入未提交的 `LocalBroforcePath.props`，该文件包含机器专用路径，不应提交到公共仓库。

## Visual Studio 2022 是否必须

Visual Studio 2022 **不是技术上的硬性要求**。它是 Wiki 默认采用的 IDE，适合本项目，因为你已有 VS 项目经验，能够方便地管理 C# 工程、引用 DLL、调试和构建。

真正需要的是：

- 能编译 C# 的工具链。
- 能引用 Broforce 的 Unity 程序集和 UMM 程序集。
- 能生成 Unity Mod Manager 可加载的 DLL。
- 能针对项目要求的 .NET Framework 目标进行构建。

因此可替代 Visual Studio 的方案包括其他 C# IDE、MSBuild、Roslyn 或命令行构建工具。当前项目仍以 Visual Studio 工程格式维护，但不把 VS2022 本身视为运行时依赖。

VS2022 下载地址仅作为可选安装入口：

- https://my.visualstudio.com/Downloads?q=Visual%20Studio%20Build%20Tools%202022

## 查看 Broforce 代码(最重要)

关键参考页面：

- https://github.com/alexneargarder/BroforceMods/wiki/Viewing-Broforce's-Code

该页面对应本项目的主要逆向入口。阅读目标是：

1. 使用 dnSpy、ILSpy 或同类工具打开：
   `<BROFORCE_DIR>\Broforce_beta_Data\Managed\Assembly-CSharp.dll`
2. 定位线上房间创建、官方地图启动、玩家加入、关卡加载和创意工坊地图相关类与方法。
3. 先记录调用关系和关键参数，不直接修改原始 DLL。
4. 将确认后的最小修改转化为 Harmony 运行时补丁。

这一步是第一阶段诊断 Mod 的后续核心工作；诊断插件先负责记录场景和运行状态，程序集阅读再用于确定具体关卡/联机方法。

## 当前环境复检结果

重启后已确认：

- Broforce 游戏目录存在。
- `Broforce_beta_Data\Managed\Assembly-CSharp.dll` 存在。
- UMM 目录存在，当前使用 `r2mod\Broforce\profiles\Broforce\UMM\Core`。
- `UnityModManager.dll` 和 `0Harmony.dll` 均存在。
- 游戏目录下已存在 `Mods` 目录。
- 当前未检测到已安装的 .NET SDK、MSBuild 或 Visual Studio Build Tools。
- 系统自带的 .NET Framework C# 编译器可用。

因此不需要你现在学习 Visual Studio，也不需要立刻安装 VS2022。当前诊断 Mod 已通过系统自带编译器直接生成：

```text
bin\Debug\BroforceOnlineDiagnostics.dll
```

编译方式使用 Broforce 的 Unity 程序集、UMM 核心程序集和系统 C# 编译器完成。后续如果需要完整 IDE 调试，再考虑安装 VS2022；当前阶段不是必须。

## r2modman 与 UMM 安装约定

当前已经确认，r2modman 包名、UMM Mod 标识和 DLL 文件名不能混用：

| 位置 | 当前正确值 |
| --- | --- |
| `manifest.json` 的 `name` | `BroforceOnlineDiagnostics` |
| `mm_v2_manifest.json` 的 `name` | `GJKen-BroforceOnlineDiagnostics` |
| `mm_v2_manifest.json` 的 `displayName` | `BroforceOnlineDiagnostics` |
| UMM `Info.json` 的 `Id` | `BroforceOnlineDiagnostics` |
| UMM Mod 目录 | `BroforceOnlineDiagnostics` |
| 程序集文件 | `BroforceOnlineDiagnostics.dll` |

包名和 Mod 目录不带 `.dll`，只有真实程序集文件保留 `.dll` 扩展名。项目源文件名为 `modinfo.json`，部署到 UMM Mod 目录时必须命名为 `Info.json`。

当前本机路径：

- r2modman 缓存包：`<R2MODMAN_DIR>\cache\GJKen-BroforceOnlineDiagnostics\1.0.0`
- UMM 部署目录：`<UMM_PROFILE_DIR>\Mods\BroforceOnlineDiagnostics`
- UMM 日志：`<UMM_PROFILE_DIR>\Core\Log.txt`

早期安装包因缺少 `Info.json`、目录名带 `.dll` 而无法被 UMM 正确识别。当前缓存清单、profile 中的 `mods.yml`、UMM Mod 目录和 `Info.json` 已统一为上述命名。

## 当前构建产物

已验证：

- DLL 文件可以生成。
- `BroforceOnlineDiagnostics.Plugin.Load` 入口存在。
- DLL 引用了 `UnityEngine.CoreModule` 和 `UnityModManager`。
- DLL 和 `Info.json` 已部署到当前 r2modman profile 的 UMM Mod 目录。
- UMM 已成功读取 `Info.json`，最新日志为 `FINISH. SUCCESSFUL LOADED 1/1 MODS.`。
- 同一份日志同时记录 `[BroforceOnlineDiagnostics] To skip (disabled).`，说明 Mod 当时未启用，不能把 `1/1` 解释为诊断代码已完整运行。

在 2026-08-17 10:03 的启用测试中，UMM 已进入 `Plugin.Load()`，但报 `TargetInvocationException`。检查发现旧 DLL 由 .NET 4.0 编译器构建，同时引用了 `mscorlib 2.0`、`mscorlib 4.0` 和 `System.Core 4.0`，与游戏的 .NET 2.0/3.5 运行时不兼容。

当前 DLL 已使用 .NET Framework 3.5 编译器和显式参考程序集重新构建，静态检查确认关键引用为：

```text
mscorlib 2.0.0.0
System.Core 3.5.0.0
UnityEngine.CoreModule 0.0.0.0
UnityEngine.IMGUIModule 0.0.0.0
UnityModManager 0.32.4.0
0Harmony 2.3.6.0
Assembly-CSharp 0.0.0.0
```

当前 DLL 的 SHA-256 为 `AF5BCF58F86AC735051525230CB3B00C7E7A533F606D925C9F4CD32CFE986F14`，已同步到项目构建目录和当前 UMM profile。该版本还在日志写入前清洗未配对 UTF-16 代理项，避免异常字符串进入 Unity 日志路径；`Info.json` 版本仍为 `0.3.0`。

下一步：完全退出双方游戏并重新启动，双方使用当前标准 DLL 和相同 Workshop ID；主机创建大厅后先让朋友加入并确认双方都在大厅，再由主机选择任务。若仍崩溃，必须同时收集主机和朋友两端同一时间段的 `diagnostics.log`、UMM `Core\Log.txt` 和新的 `error.log`。
