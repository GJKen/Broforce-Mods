# Broforce 第三方地图联机 Mod：开发文档

项目概览、安装方式和日常使用说明请先看 [根目录 README](../README.md)。本文只保留当前有效的实现、测试、日志、构建和排查约定；每轮历史问题和证据见 [issues 索引](../issues/README.md)。

## 项目范围

这是一个面向 Steam 版 Broforce 的 Unity Mod Manager + Harmony Mod。默认网络路径复用官方 Steam Lobby/Steam P2P；可选的 `FRP Direct` 路径使用独立房间、PID 和游戏 RPC，并继续使用 Steam Workshop 内容下载。

当前版本为实验性的 `0.5.0`，尚未达到稳定发布状态。

当前分发构建为 `buildHash=0915020604a45c80f6cb8b465368fde880bfd5ff00938a135dcce7d878a26caf`，DLL SHA-256 为 `792177CB5ECE13EF50AEE967B32F18C3AA30804FD824667AF1468721EAFE4AE9`。默认传输仍是 `SteamLayer` 和官方 Steam Lobby；默认关闭的 `FRP Direct` 已完成公共 FRP UDP 基础双端游戏和在线玩家名单验收，但尚未覆盖断线重入、多地图、高延迟和长期稳定性，因此仍属于实验功能。当前构建新增关卡结果、Workshop `gameMode` 一致性、可选 Swap Bros 指纹和原生 AFK 流程诊断；代码与双端部署已验证，游戏内双端触发尚待验收。更早的构建与失败修复时间线只保留在对应 issue 中。

所有玩家必须：

- 安装相同构建的 Mod，并优先通过日志 `BUILD_INFO buildHash` 核对。
- 订阅并下载相同的 Workshop 地图。
- 使用 Broforce 原有线上主持/大厅界面创建或加入房间；FRP 模式还必须在双方 UMM 设置中显式开启传输原型和游戏层。

## 当前状态(更改此条目需要用户确认)

- 已验证房主和加入方可以通过官方大厅流程进入同一张 Workshop 地图。
- 双端测试已验证：过场加载期间加入可以进入地图，P2 角色可以正常创建，双方角色控制保持独立。
- UMM 设置支持 Workshop ID、可选战役名、场景名、诊断会话 ID 和日志标签。
- 线上地图注入默认关闭；关闭时只记录诊断信息。
- 已验证 `Esc` 返回路径会先进入 `VictoryCustomCampaignSteam`，再离开 Steam Lobby 并加载 `MainMenu`；当前实现会在启用 Workshop 注入的线上会话中，于 `MainMenu` 加载后调用官方 `MainMenu.TryToGoToLobby`，直接打开在线房间查看界面。
- 已验证从在线房间大厅返回主菜单时，Logo 入场动画完成前不会显示菜单文字或高亮框；普通主菜单流程和本地地图返回流程不受影响。
- 最新异地高延迟测试未再出现同一加入方生成 P2-P4 多个角色；重复 `RequestJoinGame` 防护和联机 AFK 禁用开关已通过用户实测，但该轮未附新的双方运行日志。
- 重复退出/重入多轮后的稳定性和不同 Workshop 地图兼容性仍未全部定位。
- 默认关闭的 `FRP Direct` 已通过公共 FRP UDP 端点完成双端正常游玩实测；FRP 负责房间、PID 和游戏 RPC，Steam 仅负责 Workshop 内容下载。
- 当前分发构建已通过 FRP 双方游戏名显示验收；`Esc` 在线玩家列表会显示本机和仍在线的远端玩家。
- 当前分发构建已实现 Workshop 联机道具确定性和重复拾取防护，`test003` 已通过 FRP Direct 双端实测验收；官方 Steam 大厅中的 Workshop 路径使用同一补丁判定，但尚待独立复测。

## 双端测试

本节统一记录双端联机的验收目标、MCP 调试规则、官方进房流程、UMM 配置、多轮测试和晚加入验证方式。测试结论必须结合双方运行时状态和双方日志，不得只凭单端画面判断。

### 联机稳定性目标与自主调试约定

本 Mod 的核心目标是让双方通过受支持的 Steam 或 FRP Direct 路径稳定游玩同一张第三方 Workshop 地图。后续实现、监控和验收按以下功能目标进行：

1. 进入地图后，双方都能生成角色并正常移动、跳跃、攻击和执行其它角色操作。
2. 正常过关、跳关或重启关卡后，双方必须进入同一场景，并再次满足第 1 项。
3. 双方角色全部死亡后，应在数秒内触发任务失败, 继续后必须恢复有效生命，并再次满足第 1、2 项。
4. 联机过程中至少保留一方处于非 AFK 状态并拥有可用角色。若双方都进入 AFK、角色均被移除或场上已无可用玩家，应触发关卡重启，并验证重启后能够满足3, 而不是进入无可用玩家重启循环。

已知或需要继续确认的联机现象：

- 玩家长时间没有输入时，游戏可能将其移入 AFK/观战状态并移除角色；部分情况下个别角色不会触发该流程，原因尚未确认。
- UMM 选项 `Disable automatic AFK spectator mode in online games` 可禁用本机联机角色的原生 AFK 计时；该选项默认关闭，只影响本机拥有的联机角色，不拦截手动退出、断线或正常死亡。
- 双方同时进入 AFK 后，可能出现持续无人并反复重启的情况；在确认原生预期前，将其作为疑似 Bug 记录和观测。
- 双方角色全部死亡后数秒内没有触发任务失败和关卡重启，明确视为 Bug。
- 双方死亡后虽然重启关卡，但没有恢复生命或角色，继而反复被判断死亡、任务失败并重启关卡，明确视为 Bug。

针对上述目标，AI 已获得持续的双端日志读取、MCP 监控和运行时调试授权，无需在每次操作前再次征求用户同意。授权范围包括传送角色、修改血量或生命、调整游戏速度、切换或重启关卡、模拟输入、执行安全的运行时代码，以及注入用于验证根因的临时修复。AI 应根据当前症状自行选择诊断和调试操作，记录操作前后状态及具体指令，并明确区分临时运行时修改和已经写入源码的正式修复。该授权不包括删除存档、清理用户文件或修改与本 Mod 调试无关的系统状态。

可以通过退出房间、重新进入房间和重复加载地图来验证场景、玩家槽位及角色恢复是否正确。需要实际操作游戏界面时由用户执行退出和加入，AI 在等待期间保持当前调试会话并持续监控，不结束本轮排查；必要时可先临时注入修复，再让用户重复同一流程验证。如果任一游戏客户端崩溃或 MCP 连接消失，AI 应立即停止对该客户端继续发送运行时指令，告知用户重新启动并给出需要重复的复现步骤；客户端恢复后继续同一轮监控，同时检查诊断日志、UMM `Core\Log.txt` 和游戏 `error.log`。

#### AFK 开关与原生保底行为

UMM 选项 `Disable automatic AFK spectator mode in online games` 由每台客户端独立控制。开启后只重置本机拥有角色的原生 AFK 计时，不会由房主同步给加入方，也不会处理房主进程中的远程角色；它不拦截手动退出、断线或正常死亡。

2026-08-25 双端实测确认：房主开启、加入方关闭时，加入方角色仍按原生超时进入 AFK，房主角色保持在线。需要保护双方角色时，双方必须分别开启该选项。双方都关闭时，一名玩家先进入 AFK 后，剩余端通常变成“存活玩家数等于本机玩家数”，原生逻辑会把最后一名角色的 `idleTimer` 清零。

对应原生条件位于 `Player.Update`：只有 `HeroController.GetPlayersAliveCount() > HeroController.GetLocalPlayerCount()` 时才累计本地角色的 `idleTimer`，达到 35 秒后调用 `HeroController.Dropout`。因此，单靠原生 AFK 超时通常会保留最后一个角色，不会让双方角色全部进入 AFK；若其它退出、断线或角色移除路径造成场上无人，仍需按独立问题观察重启行为。

当前构建对该原生流程增加低频只读观测，不改变倒计时或退出行为：约 5 秒记录 `AFK_TIMER event=counting`，约 30 秒记录 `event=warning`，倒计时清零记录 `event=reset`；本机确实进入 35 秒分支后记录 `AFK_STATE event=timeout-triggered`，`HeroController.DropoutRPC` 执行后记录 `PLAYER_DROPOUT event=applied`。原生 AFK 触发与网络 RPC 之间使用 2 秒关联窗口，避免回调稍晚时误记为未知原因。只有与本机 35 秒分支对应的退出写 `reason=native-afk-timeout`，其它退出保守写 `reason=unknown`。进房清理期和本来就未激活的空槽位不写 `PLAYER_DROPOUT`，避免把初始化清理当作真实退出。

上一轮官方 Steam Workshop 双端会话使用旧构建 `fcb50bff38661e2d5ecca9e79ea4a4a190d56702b3f2a719f846c30a400e112a`：房主在第三关生命归零后仍记录 `alive=1`，约 5 分钟未判定失败；加入方据用户确认已进入 AFK，但旧日志直到退出房间时才出现 `Dropout`，退出后房主才变为 `alive=0` 并触发 `LevelFinish(Fail)`。该证据只能说明远端槽位消失后失败判定恢复，不能区分“AFK 没同步给房主”和“房主仍把 AFK 槽位计入存活”。公开房间里额外出现但未成功加载 Mod/地图的成员不纳入该结论。

下一轮实测先检查启动日志 `AFK_DIAGNOSTICS_PATCH playerUpdate=True; dropoutRpc=True`。随后让目标客户端保持无输入至少 35 秒，双方都收集相同会话 ID 的普通日志和 `.trace.log`；重点对齐加入方的 `AFK_TIMER`、`AFK_STATE`、`PLAYER_DROPOUT` 与房主同一时刻的 `alive`、`slotPlaying`、`playerPresent`。若启用了防 AFK 开关，应看到一次 `AFK_STATE event=prevention-active`，且不应出现该本机槽位的 `timeout-triggered`。

### MCP 状态调试

`Broforce_src/unity-inspector-mcp` 可以通过 Unity Inspector Mod 的 TCP 服务读取当前客户端状态。开始采样前确认 MCP 的 `ping` 成功，然后使用 `game_state` 和 `inspect_player` 记录关卡、玩家槽位、`playerNum`、角色对象、生命和位置。每个联机事件完成后立即采样一次，建议至少记录：进入地图、房主退出后的主机迁移、加入方重新加入、加入方按攻击键后。

默认的 MCP 端点连接当前运行它的客户端；为内网机器配置独立的远程端点后，也可以读取另一台仍在运行的 Broforce。远程房主进程退出后仍无法继续读取其状态，因此主机退出后的记录只能说明剩余客户端看到的状态，双端结论仍需要两台机器各自的诊断日志、UMM `Core\Log.txt` 和 `error.log`。

#### MCP 监控约束

- 上述联机稳定性目标处于测试或修复阶段时，视为已经持续授权双端 MCP 读取和运行时调试，不要求用户额外发送固定口令“开始”，也不要求逐项确认。授权持续到目标完成、用户要求停止或客户端不可用；客户端重新启动并恢复连接后可以继续同一轮排查。
- 快速检查与正式监控必须区分：连通性检查、单次状态读取、截图和指定日志读取属于快速检查，应直接调用已配置端点并立即返回结果，不发送倒计时提示、不等待 40 秒，也不扩展成完整事件监控。
- 快速检查优先直接调用双端 MCP 的 `ping`；不要先扫描配置文件、枚举工具或额外测试 TCP 端口。只有 MCP 工具未加载或 `ping` 返回连接错误时，才简短报告原因；除非用户要求继续排查，否则不追加配置和端口诊断。
- 只有需要复现并持续观察联机事件时才进入正式监控。正式监控开始前发送“倒计时开始了!!!”，固定观察 40 秒，结束后发送“倒计时结束了!!!”。
- 用户确认游戏和 Unity Inspector Mod 已运行后，正式监控开始时只读取一次基础状态，并记录当前诊断会话日志及读取位置，然后立即进入监控。
- 正式监控的 40 秒内只持续采样和收集数据，不中途停下来分析、总结、判断根因、询问用户、修改代码或发送进度汇报；所有分析和后续操作统一放在“倒计时结束了!!!”之后。
- 重点关注线上房间创建、Steam Lobby、玩家加入、关卡加载和 Workshop 地图相关类与方法。先记录调用关系和关键参数，确认后的最小修改应转化为 Harmony 运行时补丁。
- 每轮监控必须同时观测运行时状态和现有诊断事件，不能只轮询 `game_state`、`inspect_player` 或只看最终玩家数量。至少要跟踪加入方的 `AddLocalPlayer`、`RequestHeroTypeFromMaster`、`Player.Start`、`SpawnHero`、`SetPlayerCharacter`，并用房主端的 `RequestJoinGame`、`AddPlayer` 对齐时序。
- 双端 MCP 都可用时默认同时观测, 只有用户明确要求不观测某一端时才可省略该端。
- `read_log` 和 `watch_log` 只读取所配置的 UMM 日志时，不能视为已经完成事件观测；还必须通过 MCP 的只读日志访问读取当前会话的诊断 `.log`、`.trace.log`。如使用只读 `execute_code` 定位或读取 `DiagnosticLog.TraceFilePath`，表达式只能解析路径和读取文件。
- 每轮正式监控固定持续 40 秒；角色消失、进入观战、场景切换或短暂连接异常都不能作为提前停止条件，结束后再统一分析结果。快速检查不受此时长约束。
- 持续调试可以由多个 40 秒纯观测窗口组成；窗口之间允许分析日志、执行已授权的运行时调试或临时修复，然后继续下一窗口，无需重新取得授权。等待用户执行退出房间、重新加入或其它复现步骤时不得结束当前排查。
- 当正式监控、必要的日志读取和分析完成，后续不再需要游戏客户端保持打开时，必须向用户发送“游戏可以关闭了!!!”。快速检查不强制发送该提示。
- 自己根据用户提出的问题来按需诊断需要监控什么事件, 日后开发必定围绕各种事件来开发

#### MCP 快速路径

为减少准备时间，AI 按以下固定路径使用 MCP：

- 端点示例为：`unity_inspector` = 当前可访问的本机 Broforce，`unity_inspector_remote` = 可选的另一台测试端。用户说“双端”时，应按本次实际参与测试的端点和日志来源处理，不能默认一定是内网机器。
- 连通性检查直接并行调用本次可用端点的 `ping`；不要先读取配置、枚举工具或测试 TCP 端口。异地端只有日志时不尝试 MCP。工具未加载或 `ping` 报错时，立即报告实际错误。
- 常用请求直接映射：查看总体状态用 `game_state`，查看玩家用 `inspect_player`，截图用 `take_screenshot`，查看场景对象用 `query_gameobjects`/`inspect_gameobject`，读取诊断日志用 `read_log` 或 MCP 只读日志访问。
- 正式监控开始时的固定顺序是：可用端点 `ping` → `game_state` → `inspect_player` → 记录当前会话日志位置 → 按问题相关事件持续采样 40 秒。只有日志的参与端通过同一会话文件对齐。
- 联机稳定性目标范围内的只读请求和运行时修改均已获得持续授权。AI 可以自行决定执行传送、改血或生命、改速度、切关或重启、模拟输入和安全的临时代码注入，无需逐项确认，但必须记录操作、目标、前后状态和验证结果。
- MCP 工具不可用时，不要用文档中的工具名反复尝试，也不要自行改用端口探测代替 MCP 结果；直接说明“当前会话未加载该 MCP 工具”或引用实际错误。

### 加入提示拦截

后续双端验证已确认：启用 Workshop 注入的线上会话中，房主和加入方都不再显示“按开枪键加入游戏”横幅，攻击键加入功能保持可用。

源码确认该横幅来自 `HeroController.Update` 对 `LevelTitle.ShowText` 的调用，使用本地化键 `LOC_HUD_PRESSTOJOIN`。当前 Mod 在 `LevelTitle.ShowText` 前置阶段只拦截这条精确文本，并在命中时立即隐藏已经激活的 `LevelTitle` 对象。它不修改 `AddLocalPlayer`、`RequestJoinGame`、Lobby 状态或地图加载；普通大厅和离线模式也不启用此处理。普通诊断日志命中时会记录 `Suppressed the in-game Press To Join banner for the Workshop client.`，其中 `client` 为历史日志名称，实际覆盖线上会话双方。

### 游戏大厅流程

游戏没有单独的“创建第三方地图大厅”入口。当前测试流程是：

1. 启动游戏，进入“开始” -> “街机模式”。
2. 选择困难度和“线上主持游戏”，设置房间名、密码及玩家数量限制。
3. 创建方进入 `p1-p4` 等待玩家进入。加入方必须按一次攻击键占用自己的位置；创建方确认双方处于不同的玩家位置后，再按攻击键选择任务并进入地图。不要进入地图后才选择角色。
4. Steam 模式如有需要，可使用 `Esc` 打开 Steam 好友邀请界面；FRP 模式由加入方通过线上大厅列表进入唯一的 FRP 房间。

### UMM 设置

| 设置 | 填写方式 |
| --- | --- |
| `Workshop ID` | 填写 Workshop 页面 URL 中 `id=` 后面的数字；双方必须一致。 |
| `Workshop campaign name` | 可选的地图内部战役名；不确定时留空。 |
| `Custom level scene` | 默认 `Test Evan2`。它是游戏通用场景名，不是地图名称；地图使用其它场景时再修改。 |
| `Diagnostic session ID` | 单轮测试可以留空；多轮测试建议每轮使用不同值，例如 `test001`、`test002`。双端必须一致。 |
| `Diagnostic label (optional)` | 只作为日志文件名和关联信息的标签，可留空；不参与联机行为。 |
| `Inject configured workshop map into online level switching` | 默认关闭。确认配置和地图一致后再开启。 |
| `Enable FRP Direct transport prototype` | 默认关闭。只启用独立 UDP 握手原型，不改变 Steam 游戏传输。 |
| `Route Broforce rooms and RPC through FRP Direct (experimental)` | 默认关闭。必须与传输原型开关同时开启，才会让 FRP Direct 接管房间、PID 和游戏 RPC。 |
| `FRP Direct role` | 房主选择 `Host`；加入方选择 `Client`。 |
| `Local UDP listen port` | 房主的固定 Lidgren UDP 监听端口，默认 `27045`。 |
| `FRP server endpoint (host:port)` | 加入方在一个输入框填写公共 FRP 服务地址和公网 UDP 端口，例如 `frp-use.com:27045`；IPv6 使用 `[地址]:端口`。 |
| `FRP room password` | 可选，双方必须一致；只发送挑战 HMAC，不记录密码或摘要。UMM 会把密码保存在本机设置文件中。 |

首次双端测试可以使用：

```text
Workshop ID: <实际的 Workshop 数字 ID>
Workshop campaign name: 留空
Custom level scene: Test Evan2
Diagnostic session ID: test001
两端 Diagnostic label: 任意标识或留空
```

填写或修改设置后，点击 UMM 设置面板的保存按钮。正常切换 Mod 或退出游戏时插件也会尝试保存；原生崩溃或强制终止进程时无法保证保存。旧配置中的场景名为空时，插件加载时会自动补回 `Test Evan2`，已经填写其它场景名的配置不会被覆盖。

升级旧版本配置时，插件会清理旧版本遗留的测试默认值；已经填写的其它自定义值不会被覆盖。旧设置字段仍保留以兼容已有配置，但它只作为日志标签；任意一端都可以创建大厅，实际网络角色由游戏大厅流程决定。

### 多轮测试

每次换图或重新开始一轮双端测试时，建议双方完全退出并重新启动游戏，并使用新的会话 ID。相同会话 ID 只用于关联两端日志，不影响游戏联机；不填写时插件会为本端自动生成 ID。

测试顺序应保持为：

1. 双方确认使用同一版本 DLL、同一 Workshop ID 和同一地图文件版本。
2. 双方填写相同的会话 ID；日志标签可按两台设备自行填写或留空。
3. 任意一端创建大厅并停留在 `newJoin`，另一端加入并确认出现在大厅。
4. 创建方选择任务，测试进入地图、玩家生成和后续切关卡。
5. 测试结束后按会话 ID 收集本次实际参与测试的所有端的诊断日志，同时尽量收集各端的 UMM `Core\Log.txt` 和游戏 `error.log`。对面只有日志时，先比较 `BUILD_INFO buildHash`，并在结论中注明没有 MCP 或 `error.log` 的证据边界。

### 晚加入支持

当前版本在 `ConnectionLayer.OnJoinedLobby` 后检查创建方传来的 `RoomInfo.CurrentSceneName` 和房间的 Workshop 阶段元数据。Steam 模式从 Lobby 数据读取，FRP 模式由 `FrpDirectRoomInfo` 同步相同的 `loading`/`ready` 阶段。创建方进入加载阶段后，加入方会主动刷新房间数据并使用本地 `Workshop ID` 并行加载地图。客户端 Workshop 场景和原生 `SpawnJoinedPlayers` 都就绪后，Mod 等待 250ms 让玩家列表稳定，再使用本机主控制器调用一次原生 `HeroController.AddLocalPlayer(-1, controllerId)`；已有本地槽位或待处理请求时直接复用。

自动加入请求发出后，若 45 秒内没有观察到本地 `Player.Start` 或本地 `SetPlayerCharacter` 确认有效槽位，Mod 会清除挂起请求并重新调用一次 `AddLocalPlayer`；确认本地槽位建立后停止重试。普通重复加入保护使用 10 秒限频窗口，不等于晚加入请求的 45 秒总超时。实际成为 Steam Lobby Host 的端也会启用晚加入 `RequestJoinGame` 的保护放行，不再只依赖最初是否通过 `CreateMatch` 创建大厅。

这是实验性分支，依赖创建方和加入方使用相同版本 Mod。创建方处于 `newJoin` 或任务选择界面时，加入方不会启动晚加入地图加载；进入 Workshop 过场后即可触发，最多等待约 120 秒。host 端只在晚加入 Workshop 会话中放宽 `HeroController.RequestJoinGame` 的关卡完成和控制器注册保护，使加入方的 P2 请求能够创建角色。晚加入后仍可能受到玩家状态、英雄同步和地图脚本影响，因此稳定测试仍应优先使用“先加入大厅、创建方后进入地图”的顺序。

## 当前实现

### FRP Direct 实验游戏层

`FrpDirectTransport` 直接复用 `Assembly-CSharp.dll` 中的 `Lidgren.Network.NetPeer`，使用独立应用标识 `BroforceOnlineDiagnostics.FrpDirect.v1`。只开启传输原型开关时仍保持隔离；同时开启游戏层开关后，`FrpDirectNetworkManager` 才让平台工厂返回独立 `FrpDirectLayer`：

- Host 固定监听设置中的 UDP 端口，默认 `27045`，允许一台远端机器连接。
- Client 使用临时本地端口，解析设置中的单一 FRP 公网 `host:port`；普通断线后每 5 秒重试。旧版分离保存的地址和端口在设置版本 4 中自动合并。
- Lidgren 连接完成后，Host 发送一次随机挑战。Client 使用房间密码、挑战值、双方协议版本和双方 `buildHash` 计算 HMAC-SHA256；密码和认证摘要均不写入日志。
- Host 同时验证协议版本、`buildHash` 和 HMAC。任一不匹配都会以固定原因码拒绝；认证失败或版本不匹配后 Client 不自动重试，必须修改或重新应用设置。
- 协议 v2 增加房间查询/状态、加入确认/拒绝、离开通知和 `GameData`。`FrpDirectLayer` 复用原生 `GeneratePlayerID`、`BroadcastPlayerID`、`RPCBatcher` 和 `ConnectionLayer.RecieveBytes`，游戏 RPC 不经过 Steam P2P。
- `FrpDirectLayer.GetAllOnlinePlayerNames` 按本机、远端的稳定顺序返回成员名。名字来自原生 `Connect.SetPlayerName` RPC 建立的 PID 名字表；断开的远端不再显示，且不会把 FRP 机器 ID 或公网端点暴露到界面。
- FRP 层对 Broforce 的内容来源报告 `LayerType.Steam`，只用于让 Workshop campaign 继续通过 `SteamController` 下载；实际房间、PID 和 RPC 仍由 FRP 层处理。
- 握手完成后 Client 每 5 秒发送应用层心跳，Host 回应序号；正常持续 Update 时 60 秒没有有效心跳才断开。Unity 主线程因场景加载停顿超过 10 秒时，恢复后重启心跳窗口，避免把本机加载停顿误判为远端断线。Lidgren 自身的连接 ping/timeout 继续保留。
- 本地离房、房主离开或 FRP 配置变化时，会清除待处理的 Workshop 完成回调状态、`switchingLevel`、`nextScene` 和暂停的网络流，避免返回菜单后旧状态再次拉起关卡。

房间密码只保护握手，不为后续 UDP 内容提供加密。UMM 会将该字段保存在本机 Mod 设置文件中，测试应使用独立的临时密码。FRP token 不属于 Mod 设置，也不会参与协议。

2026-08-25 用户已通过公共端点 `frp-use.com:27045/UDP` 完成真实双端验收：公网 UDP 转发、Lidgren 连接、密码挑战、协议、`buildHash`、房间查询、PID/ServerID、P1-P4、Workshop 内容加载和游戏 RPC 均已推进到正常双端游玩；当前分发构建又通过了双方 `Esc` 玩家名显示验收。FRP 第一版仍只支持房主和一台远端机器，不支持主机迁移。首轮失败根因与各轮构建证据见 [FRP Direct 实施与验收记录](../issues/archive/ISSUES-2026-08-24-FRP内网穿透联机方案.md)。

### Workshop 地图注入

主机选择任务时，Mod 将配置映射到游戏的 `GameState`：

- `customLevelID` 来自 `Workshop ID`。
- `loadCustomCampaign` 设置为 `true`。
- `sceneToLoad` 来自 `Custom level scene`，默认是 `Test Evan2`。
- `campaignName` 来自可选的 `Workshop campaign name`，留空时保留游戏当前流程提供的值。

当前保留的观测/注入点：

- `WorldMapController.EnterMission`：世界地图流程的观测/后备注入点。
- `GameState.LoadLevel`：当前线上战役首关的主要注入点。
- `GameModeController.SwitchLevel`：后续切关卡的后备注入点。
- `SteamController.LevelLoadCompleteEvent`：Workshop 下载完成后恢复官方战役状态并继续加载。

每个线上房间只在首次选择任务时注入一次。创建或加入新大厅时会清理上一次 Workshop 状态并重置官方流程残留状态，避免重复回调或错误复用上一大厅的场景。

加入方晚加入时，如果房间信息或 Lobby 阶段显示创建方正在进入配置中的 Workshop 场景，`ConnectionLayer.OnJoinedLobby` 会刷新 Lobby 数据并先执行一次 Workshop 加载；地图下载完成后复用同一个完成回调继续原生流程。晚加入状态机在配置场景的 `sceneLoaded` 回调和 `SpawnJoinedPlayers` 都发生后才自动申请本地玩家槽位，避免玩家在加载阶段按下的攻击键丢失，也避免在原生玩家列表建立前发出无效请求。host 端的 `RequestJoinGame` 补丁只对晚加入 Workshop 会话绕过两个会使原生方法提前返回的保护条件，普通大厅仍使用原生判断。进入原生请求前会记录 `GetNextUnusedPlayerNumber()` 和四个玩家槽位；如果发现已标记为 playing 但对应 `Player` 对象为空，会清理该明确失效槽位。请求成功后，拥有角色的一端会在物理状态稳定后向其它客户端重发该角色当前的权威 `SetSpawnPositon` 坐标，并保留出生类型；不得通过 `WorkOutSpawnPosition` 重新计算出生位置，因为它会把已经不是首次部署的角色改判为中途空投。启用有效 Workshop 注入配置的线上会话中，每台机器同一时间只允许一个本地 `AddLocalPlayer` 请求；已有本地槽位或请求时晚加入流程直接复用，且 `SpawnJoinedPlayers` 会在广播前清理额外的本地空槽位。明确本地掉线后才释放请求锁以允许正常重入。

晚加入测试成功判据：host 日志出现 `HeroController.AddPlayer`、`Late workshop RequestJoinGame state after native handling` 和 `Workshop spawn-position rebroadcast completed with authoritative current positions`；加入方日志按顺序出现 `Starting late workshop join load`、`Late workshop client scene loaded`、`Late workshop SpawnJoinedPlayers observed`、`Late workshop join requested a local player slot after scene readiness` 和 `Late workshop automatic join completed`；无需再次按攻击键即可创建 P2 角色。普通进入时，双方都应记录 `Recorded local Workshop spawn position for exact rebroadcast` 和当前坐标重发日志，并且不会重新计算远程角色出生点。

### Workshop 道具同步与重复拾取防护

双端 MCP 观测确认，同一地图位置的普通箱会在各机器的 `CrateBlock.CreatePickupable` 中依据本机 `UnityEngine.Random` 和解锁进度随机转换，造成两端生成的道具数量、类型和网络对象顺序分叉。远程角色镜像也会执行本机 `PickupPickupables` 扫描；当弹药已满导致 `Pickupable.Collect` 不消费道具时，原生逻辑会逐帧重新发送 `TargetAll Collect`，从而重复播放动画和音效。

当前补丁只在有效 Workshop 线上会话中启用：

- 普通 `Standard` 箱保持标准弹药内容，避免各端本地随机选择不同特殊道具；地图明确配置的特殊箱保持原类型。
- 只有本机拥有的角色扫描本机道具，远程角色镜像不代替其它玩家发起拾取。
- 已收集或已停用道具再次收到 `Collect` 时直接忽略，避免队列中重复 RPC 重播动画和音效。
- 弹药已满时每次连续接触只在当前玩家本机调用一次原生 `Collect` 反馈，不发送无法消费道具的 `TargetAll` RPC；离开道具后才允许再次反馈，未消费道具另有 0.5 秒退避保护，消耗弹药后仍可正常拾取。

离线游戏、未启用有效 Workshop 注入的普通线上大厅和显式特殊道具箱继续使用原生行为。补丁依据 `IsWorkshopOnlineSession()` 启用，不依赖 `SteamLayer` 或 `FrpDirectLayer`，因此官方 Steam 大厅和 FRP Direct 进入配置的 Workshop 地图时都会覆盖；官方大厅中的原版关卡仍使用原生行为。

`test003` 使用 `buildHash=3e456a6c6f077b5e466fd6bc191b649b42dd70364f23bc5b8b3a1c1b4d8fba62` 完成约 301 秒 FRP Direct 双端实测并推进到第 9 关。现场未再观察到不可见道具、重复动画或重复音效；日志记录一次 `Workshop online standard ammo crates now keep deterministic standard contents` 和一次 `Suppressed Workshop ammo-full pickup RPC retries`，没有形成重复 `Collect` 或拾取 RPC 洪泛，最后以 `SteamLayer_LeaveMatch` 正常结束。该轮同时出现与道具无关的 `EffectsController.CreateExplosion`、`BroBase.Start` 和 `BroBase.TrySpawnDrone` 空引用，未阻断进入后续关卡。

后续在官方 Steam 大厅独立复测时，双方必须完全退出并重启游戏，先核对日志中的 `BUILD_INFO buildHash`；然后确认同一位置的道具数量和类型一致，弹药已满站在箱子上不会持续播放动画或音效，消耗弹药后能正常拾取一次，并检查 MechDrop、RCCar 等显式特殊箱仍保持原类型。

### 关卡结果与兼容性诊断

当前构建为联机问题增加四类低频、只读诊断，不改变原生关卡结果、Workshop 模式、角色选择或 AFK 规则：

- `LEVEL_OUTCOME` 在 `GameModeController.LevelFinish` 和 `Player.RemoveLife` 前后各采集一次状态，记录结果参数、当前场景、玩家槽位和生命、存活人数、本地人数、总生命、直升机人数、`levelFinished`、`switchingLevel`、`waitingForAllPlayersToReady`、目标场景、`GameState` 关卡/模式以及 `RoomInfo` 关卡/场景/模式。它只在已经建立的在线会话中写入普通日志和 `.trace.log`，用于分析全员死亡未重启、重启循环和其它关卡结果异常。
- `WORKSHOP_GAME_MODE_COMPARE` 在 `SteamController.LevelLoadCompleteEvent` 返回有效 `Campaign` 后比较 `campaign.header.gameMode`、`GameState.gameMode` 与 `RoomInfo.gameMode`。至少两个可读取来源不一致时写警告；该诊断明确标记 `action=observe-only`，不会把任一来源写回其它状态。
- `OPTIONAL_BRO_MOD` 使用 `UnityModManager.FindMod("Swap Bros Mod")` 和 `Swap_Bros_Mod.API` 做弱依赖探测。日志包含 Mod/程序集版本、模块 ID、可用 API、有序角色表 SHA-256、P1-P4 本地选择 SHA-256 和经过清洗的选择名称；未安装、未启用、API 缺失或调用失败时安全降级。当前 Mod 不引用 RocketLib、不调用换人 API，也不会因为指纹不同自动拒绝 Steam 或 FRP 会话。
- `AFK_TIMER`、`AFK_STATE` 和 `PLAYER_DROPOUT` 观察本机原生 AFK 倒计时、35 秒触发和槽位移除前后状态；具体字段、旧日志证据和双端验收方式见前文“AFK 开关与原生保底行为”。

双端分析时，先比较 `BUILD_INFO buildHash`，再比较双方 `OPTIONAL_BRO_MOD` 的安装/启用状态、版本、`rosterHash` 和 `selectedHash`。角色表或选择指纹不同只能证明双方可选角色环境不同，不能单独证明某次英雄生成失败的根因；仍需结合英雄请求、生成和双方错误日志。首次实测还应确认 `Level outcome diagnostics enabled; patched methods=2.`，并分别触发一次扣命和通关/失败来检查 `LEVEL_OUTCOME` 前后状态是否完整。

#### Utility Mod 借鉴边界

本轮参考 Utility Mod 的 Workshop 完成事件、原生关卡状态入口和 Swap Bros 公开 API 弱依赖方式，但没有复制其调试菜单或主动修改游戏状态的功能。当前落地范围如下：

| 候选方案 | 当前状态 |
| --- | --- |
| Workshop 下载完成后的状态恢复 | 现有回调已保留 Campaign、发布/在线标志和权威关卡号；本轮只新增 `gameMode` 一致性观测，不强制写回 |
| 通关和失败状态的原生观测点 | 已新增 `LEVEL_OUTCOME`，待双端触发验收 |
| 调试操作记录与重放 | 未实现，用户暂不需要自动复现 |
| 确定性对象注册顺序 | 未实现；当前道具确定性修复不生成动态对象，不能等同于 `Registry.RegisterDeterminsiticGameObject` |
| 可选 Mod 弱依赖 | 已新增只读 `OPTIONAL_BRO_MOD`，不引用 RocketLib、不调用换人 API |
| 设置写入前序列化验证 | 未实现；当前仍直接调用 `UnityModManager.ModSettings.Save`，异常时只记录错误 |

完整源码依据、AFK 日志增补、构建哈希和后续清单见 [Utility Mod 代码借鉴方案与 AFK 诊断改进](../issues/ISSUES-2026-08-25-Utility-Mod代码借鉴方案与AFK诊断改进.md)。

### 英雄回复策略

部分加入方客户端可能收不到官方 `RequestHeroTypeFromMaster` 回复。当前策略是：

- 保留游戏原本的请求和回复流程。
- Workshop 场景中的本地玩家等待 18 秒仍无回复时，使用游戏自己的 `GetHeroType` 和 `Player.SpawnHero` 做一次本地备用生成。
- Workshop 线上玩家发生本地或远程 `Dropout` 后，按玩家槽位保存掉线前的英雄类型；主机重新处理 `RequestHeroTypeFromMasterRPC`、客户端接收英雄回复和本地备用生成时都优先恢复该类型，避免掉线重建后被原生随机换成另一个角色。
- 本地 `DropoutRPC` 同时保存该槽位的 `playerControllerIDs`；自动重入和原生攻击键重入的 `AddLocalPlayer` 都改用保存的控制器，`Player.Start`/角色登记阶段若发现原生写回了其它控制器，也会恢复 `playerControllerIDs` 和 `Player.controllerNum`。
- 已有角色、远程玩家和正常收到回复的玩家不进入备用分支。
- 备用生成后，只有仍处于等待新英雄回复状态时才接受迟到回复，避免旧回复重复替换角色。

主动重试已经删除，因为它会制造迟到回复；备用生成也不是重新发送网络回复，不能保证所有同步问题都被解决。

本轮 MCP 双端日志确认：掉线重建链本身已经恢复了玩家槽位和控制器，但原生主机请求会把 `preferedNextHero` 传为 `None`，导致同一槽位重新选择其它英雄。当前补丁只在明确发生 `Dropout` 的 Workshop 线上槽位启用英雄类型保持；普通死亡、正常换人和普通大厅流程不受影响。

控制器重入修复的验证日志为 `Saved local Workshop controller for dropout rejoin`、`Reusing saved local Workshop controller for dropout rejoin`、`Rewrote local Workshop rejoin controller to saved binding`、`Switched active local Workshop player to the controller that requested join` 和 `Restored saved local Workshop controller binding`。如果重建角色仍有生命但无法操作，应优先比较掉线前后的 `playerControllerIDs`、`Player.controllerNum` 和实际输入控制器，而不是只比较 `character` 是否存在。

### 主机迁移后重新加入的输入恢复

MCP 双端观测确认，原房主通过暂停确认框离开后再以 client 身份加入原房间时，角色创建、英雄回复和 `SetPlayerCharacter` 都可以成功，但退出端可能把 `PauseController.pauseStatus=ConfirmationPause` 和 `pausedByController` 带入新会话。游戏原生 `PauseController.SetPause(UnPaused)` 对 `ConfirmationPause` 不会真正复位，而 `Player.GetInput` 只要发现本地角色的 `controllerNum` 等于 `pausedByController`，就会直接清空该角色的全部输入。

新的有效 Workshop 线上会话在 `SteamLayer.CreateMatch` 或 `SteamLayer.JoinLobby` 开始时会把陈旧暂停状态恢复为 `UnPaused`，将暂停控制器重置为 `-1`，并隐藏仍存在的暂停相机和线上玩家列表。命中时普通日志记录 `Cleared stale pause state before Workshop online session`，同时保留修复前的暂停状态和控制器编号。该处理只在启用了有效 Workshop 注入配置时执行，不修改 `playerControllerIDs`；不同机器上的本地控制器都使用编号 `0` 是允许的，原生本地输入查询会结合 `PID.IsMine` 判断所属端。

修复后已完成实机验证：房主退出并发生主机迁移后，原房主重新加入房间可以正常生成本地角色，并恢复移动、跳跃和开火；输入被暂停状态清零的问题未再复现。Workshop 地图自身的 `GeneratePole.Awake` 异常仍按独立地图兼容性问题记录。

### Workshop 线上 Esc 返回大厅

MCP 观测到按 `Esc` 返回时的调用链为：

```text
GameModeController.LoadNextScene(VictoryCustomCampaignSteam)
VictoryCustomCampaignSteam（通关时间）
CustomLevelRatingMenuSteam（地图评分）
MainMenu
```

原生流程会先进入 `VictoryCustomCampaignSteam` 显示通关时间，再进入 `CustomLevelRatingMenuSteam` 显示地图评分。玩家从评分界面选择“返回主菜单”时，该菜单还会把 `GameState.immediatelyGoToCustomCampaign` 设置为 `true`，导致 `MainMenu.Start` 自动打开自定义战役界面。

Mod 在确认当前是有效 Workshop 线上会话、暂停状态为 `MenuPause` 或 `ConfirmationPause` 且下一场景为 `VictoryCustomCampaignSteam` 后，将这次 `GameModeController.LoadNextScene` 携带的 `GameState.sceneToLoad` 直接改为 `MainMenu`，同时关闭 `loadCustomCampaign` 并清除 `immediatelyGoToCustomCampaign`。因此通关时间和地图评分两个界面都不会加载。`MainMenu.Awake` 随后通过原生 `RecreateConnectObject` 调用 `Connect.Disconnect` 清理旧 Lobby。

`MainMenu` 的菜单项由 `DelayInitializeMenu` 创建。普通启动仍使用原生约 3 秒等待；Workshop Esc 返回大厅时，Mod 将这次协程的等待临时改为 0 秒，让在线房间浏览器尽快打开。等待期间只隐藏主菜单的 Logo 和菜单视觉，不禁用 `MainMenu` 根对象，因此原生初始化协程可以完整执行。`MainMenu.InitializeMenu` 的 Harmony 后置补丁会在菜单项创建完成的同一帧再次隐藏菜单视觉，清除退出地图遗留的 `ConfirmationPause` 和暂停控制器，再调用 `MainMenu.TryToGoToLobby(MultiplayerPlayMode.Online)`。平台联机状态检查使用原生等待层，成功后只显示在线房间列表，不会渲染中间主菜单。

从在线房间大厅返回主菜单时，Mod 复用原生 `Lobby.GoBackToMainMenu -> MainMenu.Show -> MainMenu.ShowRoutine` 调用链。`MainMenu.Show` 开始前只通过字段恢复高亮索引和普通间距，不调用会立即移动高亮框、扰乱入场前布局的 `ResetHighlightIndex`。由于原生 `MenuActive=true` 会在 `ShowRoutine` 的第一步重新激活菜单项，Mod 仅在这次返回动画期间额外关闭菜单项、菜单高亮和子 Renderer 的可见性；协程完成后再按原始 Renderer 状态恢复，因此文字不会在 Logo 动画结束前出现，也不改变原生布局和缩放动画。

如果在原生初始化前打开大厅，`MainMenu` 被隐藏时会中断初始化协程，之后从大厅返回只会显示高亮框而没有菜单项。当前后置补丁保证大厅返回时 `MainMenu.ShowRoutine` 能基于已经存在的菜单项恢复完整主菜单和输入。进入 `MainMenu` 后会先等待约 250ms，再尝试打开在线大厅；导航失败时至少保留 1 秒缓冲，若没有正在显示的平台等待层则恢复完整主菜单，最长等待 30 秒后也会执行同样的恢复，避免留下隐藏或不可操作的界面。若目标类型、实例或方法不可用，只记录警告并保留原生主菜单流程。

### 代码职责

- `src/Plugin.cs`：UMM 加载、设置界面、保存和启用/禁用入口。
- `src/DiagnosticSettings.cs`：Workshop、会话和日志标签配置；新配置默认场景为 `Test Evan2`，其它测试字段为空。
- `src/DiagnosticLog.cs`：会话日志和 Harmony 追踪日志的创建、写入、刷新和清理。
- `src/DiagnosticsBehaviour.cs`：场景、Unity 错误和英雄生成状态观察。
- `src/HarmonyDiagnostics.cs`：线上房间、Steam Lobby、关卡切换、Workshop 加载和英雄请求追踪/注入。
- `src/HarmonyDiagnostics.WorkshopPickup.cs`：Workshop 道具生成确定性、拾取所有权、重复调用幂等和弹药已满退避。
- `src/HarmonyDiagnostics.Afk.cs`：原生 AFK 倒计时、超时触发与玩家槽位移除的低频只读观测。
- `src/HarmonyDiagnostics.LevelOutcome.cs`：联机 `LevelFinish`/`RemoveLife` 的低频前后状态快照。
- `src/OptionalBroModDiagnostics.cs`：可选 Swap Bros 公开 API、版本、角色表和选择指纹的只读弱依赖诊断。
- `src/ReflectionProbe.cs`：只读扫描 `Assembly-CSharp` 中可能相关的类型。
- `src/FrpDirectTransport.cs`：Lidgren UDP 监听/直连、握手认证、版本校验、心跳、重连、房间控制消息和可靠 RPC 字节通道。
- `src/FrpDirectRoomInfo.cs`：FRP 房间信息编码，以及 Workshop ready/phase 元数据同步。
- `src/FrpDirectLayer.cs`：复用 Broforce 原生 PID、ServerID、RPCBatcher 和 RecieveBytes 的 FRP ConnectionLayer。
- `src/FrpDirectNetworkManager.cs`：按显式开关选择 FRP/Steam 层并处理 Connect.layer 生命周期。

方法级追踪不记录房间密码、Steam ID、主机名或 Workshop 作者身份。

## 诊断日志

### 文件和会话

日志目录为：

```text
<Application.persistentDataPath>/BroforceOnlineDiagnostics/
```

联机测试必须分别收集本次实际参与测试的所有端的该目录。共享 Mod 部署目录只存放 DLL 和 `Info.json`，不会集中保存运行日志；每台机器启动游戏后，日志都写入自己的 `Application.persistentDataPath/BroforceOnlineDiagnostics/`。异地加入方无法直接访问时，应让对方导出 `.log` 和 `.trace.log` 文件。

插件加载时会创建启动日志；检测到 `SteamLayer` 或 `FrpDirectLayer` 的 `CreateMatch`/`JoinLobby` 时会创建新的联机会话。每个会话包含普通事件日志和独立的 Harmony 详细追踪日志，例如：

```text
diagnostics-host-<session>-<utc-time>.log
diagnostics-host-<session>-<utc-time>.trace.log
```

普通 `.log` 记录关键联机事件，`.trace.log` 记录详细 Harmony 调用。每行包含 UTC 时间、会话相对时间、会话 ID、日志标签和日志级别；会话开始事件还会记录实际网络角色。普通日志约每 750ms 刷新一次，警告、错误和会话结束时立即刷新。

新增的 `LEVEL_OUTCOME`、`AFK_TIMER`、`AFK_STATE` 和 `PLAYER_DROPOUT` 同时写普通日志和 `.trace.log`；`WORKSHOP_GAME_MODE_COMPARE` 和 `OPTIONAL_BRO_MOD` 写普通日志。`OPTIONAL_BRO_MOD` 在诊断启用和每个网络会话开始时各采集一次，网络问题分析以对应会话文件中的第二次快照为准。

`SteamLayer.JoinLobby` 内部可能先调用一次 `LeaveMatch` 清理旧大厅；该调用不再被诊断系统当成正式离开，因此不会提前关闭客户端的加入会话日志。

每次通过标准 `BuildAndDeploy.ps1` 构建时，脚本会把本次源码、引用程序集、编译器目标和配置组成清单并计算 SHA-256 `buildHash`，再将该值作为编译期常量嵌入 DLL。启动日志、普通会话日志和 `.trace.log` 都会写入 `BUILD_INFO algorithm=SHA-256; buildHash=...`，`SESSION_BEGIN` 也会带上同一值。双端分析必须先比较该值；对面只有日志时也可据此确认是否使用同一构建。未经过标准脚本的源码/IDE 构建使用 `UNBUILT` 标记。

### 日志约束

- 不直接追踪 `Update`、`RunHeroRespawnLogic` 等每帧方法；需要观察时改为追踪低频下游事件。
- 重复日志按方法、参数和状态组合限频；高频状态同步方法按方法级别合并，并在恢复记录时报告被抑制次数。
- 已绑定本地玩家的同一控制器继续触发空槽 `AddLocalPlayer` 时静默拦截；其它控制器的额外加入尝试每个控制器每 10 秒最多记录一条警告。
- 新增追踪后先检查本机日志增长速度；如果每秒持续写入多行，先修复限频再进行双端测试。
- 日志写入前会清洗未配对 UTF-16 代理项，避免异常字符串再次破坏 Unity 日志路径。
- 本项目不自动设置日志大小上限，也不自动删除旧日志；测试结束后按会话文件清理不需要的历史日志。

分析联机时序时，必须对照本次实际参与测试的所有端相同会话 ID 的 `.log`、`.trace.log`、UMM `Core\Log.txt` 和 `error.log`，不能仅凭单端日志判断网络根因。对面只能提供诊断日志时，先使用 `BUILD_INFO buildHash` 核对 DLL，并在结论中明确缺少 MCP、UMM 日志或 `error.log` 的证据边界。只有在用户明确要求时，才可以跳过某一参与端或某类辅助日志。

## 当前已知问题

- 加入方英雄类型回复可能丢失，当前本地备用生成只能缓解，不能替代网络同步。
- Broforce 可能发生原生崩溃；日志中的异常和崩溃时间关系不能单独证明因果，必须结合 `error.log`、双方日志和 UMM 日志分析。
- 晚加入依赖双方使用相同版本 Mod；过场期间可以并行加载，但地图脚本、网络状态或原生错误仍可能导致加入失败。
- `test011` 已确认创建方先进入地图时，加入方可在场景就绪后自动创建 P2；仍需继续验证不同地图和控制器组合。
- `test009` 使用的 Workshop 地图曾在 `GeneratePole.Awake` 抛出 `NullReferenceException`。该错误来自地图对象初始化，当前未阻止本轮晚加入和 P2 创建，但更换地图或地图对象时仍需单独排查。
- 重复退出/重入若干轮后，加入方可能无法再次进入；现有证据不足以定位到 Lobby、PID、槽位或 `Dropout` 清理，状态仍是未修复、未定位。
- 2026-08-21 关于跳关死亡、重入回到第一关和 `BroBase.Start` NRE 的实验性修改已经全部撤销；该 issue 只作为历史分析参考，不代表当前 DLL 包含那些修复。
- 不同 Workshop 地图、地图脚本和其它 Mod 的兼容性尚未充分验证；Swap Bros 已有只读版本/API/角色表指纹诊断，但尚未完成双端兼容性验收，也不会自动阻止环境不一致的会话。
- Workshop 道具同步和重复拾取防护已通过 `test003` FRP Direct 双端实机验收；官方 Steam 大厅 Workshop 会话和更多地图仍需独立覆盖。
- 线上地图注入仍属于测试功能，默认关闭，不能按稳定发布版本使用。
- `FRP Direct` 游戏层已完成公共 FRP UDP 双端正常游玩验收，但仍是默认关闭的实验功能，不能视为稳定发布版本。
- FRP 第一版不支持主机迁移；房主退出即结束房间。断线重入、多地图、高延迟和长期稳定性仍需继续验证。
- 公网入口只能使用服务商分配的 UDP 端点；MCP TCP `9999` 和原有 FuckNet 匹配/中继端口不是 FRP Direct 游戏入口。

## 构建与部署

### 准备

1. 复制 `LocalBroforcePath.props.example` 为 `LocalBroforcePath.props`。
2. 将 `BroforceManagedPath` 设置为 Broforce 的 `Broforce_beta_Data/Managed` 目录。
3. 将 `UnityModManagerPath` 设置为包含 `UnityModManager.dll` 和 `0Harmony.dll` 的 UMM 核心目录。
4. 必须使用兼容 `.NET Framework 3.5` 的编译器和 Broforce/UMM 程序集引用。当前已验证系统自带的：

```text
C:\Windows\Microsoft.NET\Framework64\v3.5\csc.exe
```

Visual Studio 2022 不是硬性要求；不要直接使用 `v4.0.30319\csc.exe`，避免 DLL 混入 .NET 4.0 引用。

### 标准构建入口

必须从项目根目录运行，且以该脚本作为已验证的标准入口：

```powershell
powershell -ExecutionPolicy Bypass -File .\BuildAndDeploy.ps1
```

脚本会直接将编译输出写入项目安装包，然后覆盖本机和内网测试端。标准输出只有以下三个有效位置：

```text
<项目根目录>\BroforceOnlineDiagnostics\BroforceOnlineDiagnostics.dll
<本机 UMM_PROFILE_DIR>\Mods\GJKen-BroforceOnlineDiagnostics\BroforceOnlineDiagnostics.dll
\\192.168.1.181\Epan\Games\Broforce Mods\Broforce\profiles\Broforce\UMM\Mods\GJKen-BroforceOnlineDiagnostics\BroforceOnlineDiagnostics.dll
```

脚本还会输出本次 `Build hash: ...`。该值已经嵌入生成的 DLL，并会在运行时日志中按“诊断日志”章节记录；不要用 DLL 文件名、修改时间或文件大小代替 `buildHash` 判断双端版本。

项目安装包还必须包含：

```text
<项目根目录>\BroforceOnlineDiagnostics\Info.json
```

`Info.json` 是固定清单。脚本只在本机或内网目标缺少 `Info.json` 时从 `modinfo.json` 初始化，不覆盖已有清单、缓存或其它文件；DLL 始终覆盖。网络路径不可访问、目录创建失败或 DLL 复制失败时，构建部署必须失败，不能继续双端测试。

`BroforceOnlineDiagnostics.csproj` 的 `OutputPath` 也指向项目安装包目录。`bin\Debug` 不再是当前构建输出位置，旧文件不应作为测试 DLL 使用。不要只运行 `csc.exe` 或只构建 DLL，否则不会自动同步测试端；IDE/MSBuild 只有在正确读取 `LocalBroforcePath.props` 并执行构建后目标时才可作为替代入口。

## 安装和命名约定

项目内的 `BroforceOnlineDiagnostics` 文件夹是给其它玩家复制的 UMM Mod 安装包，必须同时包含：

```text
BroforceOnlineDiagnostics\
  BroforceOnlineDiagnostics.dll
  Info.json
```

复制到 UMM 时，目录名必须是 `GJKen-BroforceOnlineDiagnostics`，程序集文件名必须是 `BroforceOnlineDiagnostics.dll`，不能给目录名添加 `.dll`。项目源清单名为 `modinfo.json`，部署到 UMM 目录时使用 `Info.json`。

构建脚本不会自动更新 r2modman 缓存包。其它玩家使用时，按 README 将项目内的安装包复制到自己的 profile 的 `UMM\Mods` 目录，并让 r2modman 重新读取 Mod。

## 逆向参考

主要参考：

- [Viewing Broforce's Code](https://github.com/alexneargarder/BroforceMods/wiki/Viewing-Broforce's-Code)
- [BroforceMods Wiki](https://github.com/alexneargarder/BroforceMods/wiki)
- [BroMaker Abilities Wiki](https://github.com/alexneargarder/Bro-Maker-Abilities-Wiki/wiki)

使用 dnSpy、ILSpy 或同类工具阅读：

```text
<BROFORCE_DIR>\Broforce_beta_Data\Managed\Assembly-CSharp.dll
```


## 修改协作约定(更改此条目需要用户确认)

- 提交或同步前检查上级仓库的 `git status` 和 `git diff`，不要把 `LocalBroforcePath.props`、日志、缓存或无关文件加入提交。
- `LocalBroforcePath.props` 包含机器专用路径，不应提交到公共仓库。
- 构建方式、联机行为、安装方式、日志格式或兼容性发生变化时，先同步更新 README 和本文档。
- 不要未经明确要求运行上级仓库的自动提交、推送或更新脚本。
