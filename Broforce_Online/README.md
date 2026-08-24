# Broforce 第三方地图联机 Mod

这是一个面向 Steam 版 Broforce 的 Unity Mod Manager + Harmony Mod。它复用游戏原有的 Steam 多人大厅，让已经订阅同一张 Workshop 地图的玩家尝试共同进入第三方地图。

所有玩家必须安装相同构建的 Mod，并提前订阅、下载相同的 Workshop 地图。排查版本时以双方日志中的 `BUILD_INFO buildHash` 为准。

## 当前状态

当前版本为实验性 `0.5.0`：

- 已验证房主和加入方可以通过官方大厅流程进入同一张 Workshop 地图。
- 已支持实验性的晚加入处理：创建方正在进入或已经进入配置中的 Workshop 场景时，加入方会尝试自动加载同一张地图。
- 已验证双方完全准备后进入，以及创建方先进入、加入方后进入两种流程都不会多生成加入方角色。最新异地高延迟测试也未再出现同一加入方生成 P2-P4 多个角色。
- 已验证 Workshop 线上会话中房主和加入方都不再显示“按开枪键加入游戏”横幅；该处理不改变攻击键加入功能。
- 已接入 Workshop 线上关卡按 `Esc` 返回后的大厅导航：跳过 `VictoryCustomCampaignSteam` 的通关时间和地图评分界面，直接回到 `MainMenu` 并自动进入在线房间查看界面；普通本地关卡不受影响。
- 已修复从在线房间大厅返回主菜单时的菜单动画时序：Logo 入场动画完成前不会显示文字或高亮框，不再出现按钮偏上、只剩框框无法操作的问题；普通主菜单流程不受影响。
- 已支持在 UMM 设置中填写 Workshop ID、可选的战役名和场景名，并使用会话 ID 和可选日志标签关联双端日志。
- 已接入 `unity-inspector-mcp` 调试桥接，可在游戏运行时读取关卡、玩家槽位、角色对象和截图。
- 已保留官方英雄类型请求；加入方收不到回复时，会在等待 18 秒后使用本地备用生成；Workshop 线上玩家掉线重建时会保存并恢复原英雄类型，不再因原生重新分配而自动换人。
- 已修复 Workshop 掉线槽位重入时控制器绑定丢失：自动加入和攻击键触发的 `AddLocalPlayer` 都复用掉线前的本地控制器，并在角色注册阶段再次校正控制器归属。
- 已增加晚加入请求确认和超时重试：`AddLocalPlayer` 发出后若 45 秒内没有形成有效本地玩家槽位，会释放挂起状态并重新请求；`Player.Start` 或本地 `SetPlayerCharacter` 确认登记后停止重试。同一 PID 已占用槽位时，房主会拒绝重复 `RequestJoinGame`，避免高延迟重试创建额外角色。
- 已验证联机 AFK 禁用开关：双方按需开启后，长时间没有输入的本机角色不会被原生逻辑自动移入观战。
- 已知部分 Workshop 地图会在 `GeneratePole.Awake` 抛出空引用错误；该地图对象问题与晚加入角色创建问题分开处理。
- 重复退出/重入多轮后的稳定性和特定地图第 4 关通关黑屏仍未完成定位，不能视为稳定发布版本。
- `FRP Direct` 已接入房间列表、PID/ServerID、P1-P4、游戏 RPC 和 Steam Workshop 内容加载。2026-08-25 用户已确认修复构建 `buildHash=a53f0dc3a627d57efac53d36f34a84363aa16aa500754282b0305ea36cc11ec7` 能让房主与加入方通过公共 FRP UDP 端点进入同一张第三方地图并正常联机游玩。该路径仍是默认关闭的实验功能，尚未完成断线重入、多地图、高延迟和长期稳定性验证，不能视为稳定发布版本。
- 已修复 FRP 联机时 `Esc` 在线玩家列表为空的问题：FRP 层现在从原生 PID 名字同步结果中显示本机和仍在线的远端玩家，不显示内部机器 ID 或公网端点。用户已在双端实测确认双方游戏名能够正常显示。
- 名单修复构建 `buildHash=683227dab9d54673e85a8fbc3a39354778faea5e0d7290e7381ba7b54bdfe518` 已通过双端玩家名显示验收；`a53f...` 的正常双端游玩结论保持有效。

## 安装与使用

> 已验证双方可以安装不同的其它 Mod，但双方的 `BroforceOnlineDiagnostics.dll` 必须来自相同构建。是否一致应通过日志 `buildHash` 判断，不能只看文件名。

1. 所有玩家安装 `r2modman`，为 Broforce 创建或选择同一个用途的 profile。
2. 在该 profile 中安装 `UMM`，启动一次游戏并确认 UMM 加载成功。
3. 如果该本地 Mod 尚未登记到 r2modman，在对应 profile 的 `xxxxxx\Broforce\profiles\Broforce\mods.yml` 中增加以下内容：
```
- manifestVersion: 1
  name: GJKen-BroforceOnlineDiagnostics
  authorName: GJKen
  websiteUrl: ''
  displayName: BroforceOnlineDiagnostics
  description: 测试
  gameVersion: ''
  networkMode: ''
  packageType: ''
  installMode: ''
  installedAtTime: 1786929010047
  loaders: []
  dependencies: []
  incompatibilities: []
  optionalDependencies: []
  versionNumber:
    major: 1
    minor: 0
    patch: 0
  enabled: true
  onlineSource: false
```
> `r2modman` profile 可以隔离不同的 Mod 测试环境。
4. 在 `xxxxxx\Broforce\profiles\Broforce\UMM\Mods` 下创建 `GJKen-BroforceOnlineDiagnostics` 目录，把项目根目录 `BroforceOnlineDiagnostics` 安装包中的 `BroforceOnlineDiagnostics.dll` 和 `Info.json` 复制进去。UMM 目录名必须使用 `GJKen-BroforceOnlineDiagnostics`。构建者每次运行 `BuildAndDeploy.ps1` 后，项目内安装包会自动更新，并覆盖脚本中配置的本机和内网测试部署目标。
5. 完成登记和复制后重启 `r2modman`，启动游戏并在 UMM 中确认 `Broforce Online Diagnostics 0.5.0` 已加载。然后在 UMM 设置中开启 `Inject configured workshop map into online level switching`。
6. 所有玩家订阅并下载相同的 Workshop 地图，并在 UMM 设置中填写相同的 Workshop ID。战役名不预填；场景名默认是 `Test Evan2`，如果地图使用其它场景名再按实际情况修改。

首次双端测试可以使用以下配置：

```text
Workshop ID: <实际的 Workshop 数字 ID>
Workshop campaign name: 留空
Custom level scene: Test Evan2
Diagnostic session ID: test001
两端 Diagnostic label: 任意标识或留空
```

两端的 Workshop ID 必须完全一致。`Diagnostic session ID` 可以留空；需要关联双方日志时再填写相同值。保存双方 UMM 设置后，再开启线上地图注入。

填写或修改设置后，请点击 UMM 设置面板的保存按钮；正常切换 Mod 或退出游戏时也会尝试自动保存。

如果旧配置中的 `Custom level scene` 为空，插件加载时会自动补回默认值 `Test Evan2`；已经填写其它场景名的配置不会被覆盖。

升级旧版本配置时，插件会清理旧版本遗留的测试默认值；已经填写的其它自定义值不会被覆盖。`Diagnostic label` 只用于日志文件名和关联信息，不参与联机行为；任意一端都可以创建大厅，实际网络角色由游戏大厅流程自动决定。

7. 任意一端按官方流程创建线上大厅，另一端先加入 `p1-p4` 选择界面。加入方必须按一次攻击键占用自己的位置，创建方确认双方出现在不同位置后，再按攻击键开始进入地图游玩；不要等进入地图后才选择角色。

当前版本也支持“创建方正在进入或已经进入 Workshop 地图时，另一端再加入”。加入方检测到创建方处于 Workshop 加载阶段后，会主动刷新 Lobby 数据并先加载地图；Workshop 场景和原生 `SpawnJoinedPlayers` 都就绪后，Mod 使用本机主控制器自动发起一次本地玩家加入。已有本地玩家槽位或待处理请求时直接复用，不再要求玩家在加载期间提前按攻击键，也不会追加第二个请求。host 端只在晚加入 Workshop 会话中绕过会让原生 `RequestJoinGame` 提前返回的两个保护条件，并记录即时玩家槽位状态。若发现 `playersPlaying=true` 但对应 `Player` 对象已经为空，会在原生分配前清理该明确失效槽位。启用有效 Workshop 注入配置的线上会话中，每台机器同一时间只允许一个本地加入请求；开始 `SpawnJoinedPlayers` 广播前也会移除额外的本地空槽位。角色出生后，Mod 会在物理状态稳定后向其它客户端重发角色当前的权威坐标，不再把绳索上的固定出生点覆盖回已下落的角色。正常测试仍建议先加入大厅、确认占用独立位置后，再由创建方进入地图。

掉线后重入测试还应确认日志出现 `Saved local Workshop controller for dropout rejoin`、`Reusing saved local Workshop controller for dropout rejoin`、`Rewrote local Workshop rejoin controller to saved binding` 或 `Switched active local Workshop player to the controller that requested join`，并确认重建后的本地 `controllerNum` 与用户实际操作的控制器一致；首次没有掉线记录的晚加入仍会优先使用上次记住的本地控制器。

晚加入测试成功时，host 日志应出现 `HeroController.AddPlayer`、`Late workshop RequestJoinGame state after native handling` 和 `Workshop spawn-position rebroadcast completed with authoritative current positions`；加入方应依次出现 `Starting late workshop join load`、`Late workshop client scene loaded`、`Late workshop SpawnJoinedPlayers observed`、`Late workshop join requested a local player slot after scene readiness` 和 `Late workshop automatic join completed`，无需再次按攻击键即可创建 P2 角色。正常进入时，两端应记录 `Recorded local Workshop spawn position for exact rebroadcast` 和当前坐标重发日志，并且不再重新计算出生点。

房主通过暂停确认框退出后，原房主重新加入已经发生主机迁移的 Workshop 房间时，游戏原生流程可能遗留 `ConfirmationPause` 和原暂停控制器编号。角色即使正常生成为本地角色，`Player.GetInput` 仍会把该控制器的全部输入清零。Mod 现在会在新的有效 Workshop 线上会话开始时清除这组陈旧暂停状态并隐藏遗留暂停界面；命中时日志记录 `Cleared stale pause state before Workshop online session`。该处理不改写玩家槽位或远端控制器编号。

主机迁移后原房主重新加入无法操作的问题已完成实机验证：角色可以正常生成并恢复移动、跳跃和开火，未再复现输入被清零。该修复不影响正常大厅流程；`GeneratePole.Awake` 等 Workshop 地图对象异常仍需单独排查。

双端排查时可以在 UMM 设置中填写相同的 `Diagnostic session ID`，并为两端填写不同的可选 `Diagnostic label`。进入或加入 Steam 大厅时会自动创建新的会话日志；Harmony 详细追踪会写入同名的 `.trace.log` 文件，普通事件日志不会与上一次联机测试混在一起。日志还会记录实际网络角色，但标签本身不参与联机行为。

每次测试结束后，必须收集本次实际参与测试的所有端的诊断 `.log`、`.trace.log`，并尽量同时保存各端的 UMM `Core\Log.txt` 和游戏 `error.log`。共享 DLL 部署目录只用于安装 Mod，不是集中日志目录；每台机器的诊断日志都写入自己的 `Application.persistentDataPath\BroforceOnlineDiagnostics\`。对面只有日志而没有 MCP 状态时，仍应先比较 `BUILD_INFO buildHash`，并在结论中明确缺失的证据。除非用户明确要求跳过，否则不得仅凭单端日志断定网络根因。

Mod 默认关闭注入；关闭注入时只记录诊断信息。

### MCP 调试

本项目可以配合 `Broforce_src/unity-inspector-mcp` 使用。启动 Broforce 并在 UMM 中确认 `Unity Inspector Mod` 显示 `TCP Server Status: Running`、端口为 `9999` 后，支持 MCP 的客户端即可调用 `ping`、`game_state`、`inspect_player`、`take_screenshot`、`read_log` 和 `watch_log` 等工具。

联机问题建议按事件阶段采样，而不是只在最后查看画面：

1. 双方进入地图后记录一次连接、关卡和玩家状态。
2. 房主退出并完成主机迁移后再次记录。
3. 加入方重新加入、但尚未按攻击键时再次记录。
4. 按攻击键后立即记录，并比较玩家数量、`playerNum`、`character` 和本地角色标记的变化。

默认的 `unity_inspector` MCP 端点连接当前电脑；如果为另一台可访问的测试机器配置独立的 `unity_inspector_remote` 端点，也可以读取该机器上正在运行的 Broforce。异地加入方通常只有其导出的日志，不能假定 MCP 可直接连接。远程客户端退出后，MCP 无法读取已经退出的进程状态。`read_log` 和 `watch_log` 默认查找 `Default` profile；使用 `profiles\Broforce` 等自定义 r2modman profile 时，应配置实际的 UMM `Core\Log.txt` 路径或直接收集该文件。

#### 联机稳定性目标

双方进入地图、切关或重启后的角色生成、场景一致性、死亡重启和 AFK 处理，统一以 [开发与测试文档](docs/DEVELOPMENT.md#联机稳定性目标与自主调试约定) 中的四项功能目标、已知现象和自主调试规则为准。该文档是联机问题的详细测试和修复规范，README 不重复展开。

#### MCP 详细规则

双端端点、40 秒监控窗口、日志采集、运行时调试授权、用户执行退出/重加入以及客户端崩溃后的恢复流程，统一以 [开发与测试文档](docs/DEVELOPMENT.md#联机稳定性目标与自主调试约定) 中的 MCP 监控约束和快速路径为准。

### 加入提示

Workshop 线上会话中的“按开枪键加入游戏”横幅已在房主和加入方两端验证屏蔽。游戏原始提示由 `HeroController.Update` 调用 `LevelTitle.ShowText` 显示；Mod 只匹配本地化键 `LOC_HUD_PRESSTOJOIN` 对应的文本，并在命中时隐藏现有横幅。普通大厅、离线模式和攻击键加入功能不受影响。

### 联机 AFK 开关

UMM 设置中的 `Disable automatic AFK spectator mode in online games` 默认关闭。开启后，Mod 仅在联机游戏中重置本机角色的原生 AFK 计时器，防止角色因长时间没有输入而被自动移除并进入观战；手动退出、网络断开、正常死亡和离线游戏不受影响。需要保护双方角色时，双方都应开启该选项。

### FRP Direct 实验联机

FRP Direct 已从独立握手原型推进到第一轮游戏层测试版。开启游戏层开关后，Mod 会用 FRP 连接接管 Broforce 房间查询、PID 分配和 RPC 字节传输；不开启游戏层开关时，独立 UDP peer 仍只做握手/心跳验证，Steam 联机保持默认。

房主设置：

```text
Enable FRP Direct transport prototype: 开启
Route Broforce rooms and RPC through FRP Direct (experimental): 开启
FRP Direct role: Host
Local UDP listen port: 27045
FRP room password: 双方约定的临时密码，或留空
```

房主点击 `Apply / restart FRP Direct` 后，状态应显示 `Listening on UDP 27045`。`frpc` 的 UDP 代理应将服务商分配的公网端口转发到 `127.0.0.1:27045`。

加入方设置：

```text
Enable FRP Direct transport prototype: 开启
Route Broforce rooms and RPC through FRP Direct (experimental): 开启
FRP Direct role: Client
FRP server endpoint: 服务商提供的公网地址和 UDP 端口，例如 frp-use.com:27045
FRP room password: 与房主一致
```

加入方只需在一个输入框中填写完整的 `host:port`，不再分别填写地址和端口；IPv6 使用 `[地址]:端口`。旧版本保存的两个字段会在升级时自动合并。

双方使用同一标准构建且密码一致时，状态会进入 `Handshake complete; heartbeat active`，之后客户端状态中的心跳序号会持续增加。协议版本、`buildHash` 或密码不一致时会拒绝握手，不会自动降级。房间密码在界面中遮蔽并且不会写入诊断日志、房间状态或通过网络明文发送，但会由 UMM 保存在本机设置文件中，因此应使用独立的临时密码，不要复用其它账号密码。FRP token 只属于 `frpc`，不得填入 Mod。

完成握手后按原有 Broforce 流程操作：房主打开线上建房界面并创建大厅；加入方打开线上大厅列表，等待唯一的 FRP 房间出现后选择加入。第一轮验收依次确认大厅条目出现、双方 PID/ServerID 建立、进入 P1-P4 选择界面、双方角色生成和输入同步，再测试普通地图与 Workshop。无需额外输入房间码，也无需在游戏大厅里再次填写 FRP 地址。

2026-08-25 首轮公网 FRP 游戏层实测确认：加入方能看到唯一房间、进入 P1-P4，并占用 P2；首次地图切换随后失败。房主日志表明 `Test Evan2` 是承载 Workshop campaign 的 Unity 场景名，不能单凭该名称判定加载了官方测试地图；实际故障是 FRP 层被标记成旧 `Badumna` 内容来源，Broforce 因此走了废弃的 Playtomic 自定义地图加载分支，并在场景加载停顿后误触发心跳超时。后续构建改为“游戏房间/RPC 仍走 FRP，Workshop 内容走 Steam 下载”，同时加入加载停顿后的心跳宽限和离房状态清理。用户已使用 `buildHash=a53f0dc3a627d57efac53d36f34a84363aa16aa500754282b0305ea36cc11ec7` 完成双端复测，确认双方能够进入同一张第三方地图并正常联机游玩。

当前实验层只支持房主和一台远端机器之间的连接；每台机器仍可使用 Broforce 原生本地玩家槽位。重复加入请求会复用现有身份，远端离开时房主大厅继续保留。第一版尚未实现 FRP 主机迁移，房主离开会结束房间；断线重入、长时间游玩、不同 Workshop 地图和高延迟环境仍需实机验收。

关闭游戏层开关并应用设置会恢复默认 Steam 网络层；关闭原型开关、Mod 或游戏会同时停止 Lidgren UDP peer。切换网络层或角色设置前应先退出当前房间。

## 构建

项目面向 .NET Framework 3.5，使用 Broforce 和 Unity Mod Manager 的程序集引用。先根据 `LocalBroforcePath.props.example` 创建本机的 `LocalBroforcePath.props`，然后运行 `BuildAndDeploy.ps1`。

构建脚本会直接将标准文件名 `BroforceOnlineDiagnostics.dll` 生成到项目内的可复制安装包，然后自动覆盖部署到本机 UMM Mod 目录和内网测试端：

```text
<项目根目录>\BroforceOnlineDiagnostics\BroforceOnlineDiagnostics.dll
<本机 UMM>\Mods\GJKen-BroforceOnlineDiagnostics\BroforceOnlineDiagnostics.dll
\\192.168.1.181\Epan\Games\Broforce Mods\Broforce\profiles\Broforce\UMM\Mods\GJKen-BroforceOnlineDiagnostics\BroforceOnlineDiagnostics.dll
```

日常构建和部署以 `BuildAndDeploy.ps1` 为准。工程的构建后目标也指向项目安装包和两处部署目录，但只有在本机 MSBuild 正确读取 `LocalBroforcePath.props` 时才可使用；不要用未验证的 IDE/MSBuild 构建代替脚本。内网路径不可访问或 DLL 被锁定时，构建部署应视为失败，不要继续进行双端测试。

标准构建脚本会根据本次参与编译的源码、引用程序集、编译器目标和配置生成 SHA-256 `buildHash`，并在编译时嵌入 DLL。插件启动日志、普通会话 `.log` 和 Harmony `.trace.log` 都会记录 `BUILD_INFO ... buildHash=...`；双端排查时优先比较两端日志中的该值。直接使用未经过标准脚本的 IDE/手工构建会记录 `UNBUILT`，不能作为可比的正式测试构建。

当前标准程序集文件名始终为 `BroforceOnlineDiagnostics.dll`。自动部署始终覆盖 DLL；项目内安装包的 `Info.json` 是固定清单，目标目录缺少 `Info.json` 时才会从 `modinfo.json` 初始化，不覆盖已有的 `Info.json` 或其它文件。

## 项目结构

```text
src/                         Mod 源码
BroforceOnlineDiagnostics.csproj  C# 工程文件
BuildAndDeploy.ps1             .NET 3.5 构建和双端自动部署脚本
BroforceOnlineDiagnostics/   给其它玩家复制的 UMM Mod 安装包
  BroforceOnlineDiagnostics.dll
  Info.json
modinfo.json                 UMM Mod 清单模板
LocalBroforcePath.props.example   本机路径配置示例
docs/DEVELOPMENT.md          开发、逆向、测试和故障排查记录
issues/                      历史问题、测试证据、验收结果和方案记录
```

## 文档

- [开发与测试文档](docs/DEVELOPMENT.md)
- [问题记录索引](issues/README.md)
- [最新异地加入、重复角色与 AFK 验收记录](issues/ISSUES-2026-08-24-联机加入方重复角色与AFK开关编译测试记录.md)
- [FRP Direct 可行性与实施方案](issues/ISSUES-2026-08-24-FRP内网穿透联机方案.md)

开发文档包含当前有效的官方联机流程、Workshop 注入调用链、英雄回复问题、构建约束、日志分析和后续测试步骤。`issues` 保存每轮问题和测试证据，其中可能包含已经撤销或被后续结论取代的历史方案；阅读时以问题索引、README 和开发文档的当前状态为准。

## 参考资料

- [BroforceMods Wiki](https://github.com/alexneargarder/BroforceMods/wiki)
- [Viewing Broforce's Code](https://github.com/alexneargarder/BroforceMods/wiki/Viewing-Broforce's-Code)
