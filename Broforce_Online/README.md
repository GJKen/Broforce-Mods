# Broforce 第三方地图联机 Mod

这是一个面向 Steam 版 Broforce 的 Unity Mod Manager + Harmony Mod。默认模式复用游戏原有的 Steam Lobby/Steam P2P；可选的 `FRP Direct` 模式使用独立房间、PID 和游戏 RPC，并继续通过 Steam 下载 Workshop 内容。

所有玩家必须安装相同构建的 Mod，并提前订阅、下载相同的 Workshop 地图。排查版本时以双方日志中的 `BUILD_INFO buildHash` 为准。

## 当前状态

当前版本为实验性 `0.5.0`，尚未达到稳定发布状态。

| 项目 | 当前状态 |
| --- | --- |
| 当前分发构建 | `buildHash=caf775d4805d39773b9a6b00c0569366e5a693607323133e0401033e6322e2da` |
| DLL SHA-256 | `08FFC24B5FFE1E2284DA28244360B3C95D3415ECC8E3B5C75C6594D1B153BB9A` |
| Steam 联机 | 默认路径；已验证双方能通过官方大厅进入同一张第三方 Workshop 地图 |
| FRP Direct | 默认关闭的实验路径；已验证公共 FRP UDP 双端正常游玩及 `Esc` 双方玩家名显示 |

当前已经验证：

- 双方完全准备后进入，以及创建方先进入、加入方后进入两种流程均可创建独立角色；最新异地高延迟测试未再出现同一加入方生成 P2-P4 多个角色。
- Workshop 线上会话会屏蔽“按开枪键加入游戏”横幅，同时保留攻击键加入功能；联机 AFK 禁用开关也已通过双端实测。
- Workshop 线上关卡按 `Esc` 后可跳过通关时间和地图评分界面，返回主菜单并自动进入在线房间列表；大厅返回主菜单的 Logo、文字和高亮框动画时序已修复。
- FRP Direct 已接入房间列表、PID/ServerID、P1-P4、游戏 RPC、Steam Workshop 内容加载和在线玩家名单。当前构建基于已通过正常双端游玩验收的 FRP 游戏链路，并已进一步通过双方游戏名显示验收。

当前构建已实现 Workshop 联机道具同步和重复拾取防护：普通弹药箱不再由各端按本地随机状态转换成不同特殊道具，远程角色镜像不再扫描本机道具，已消费道具的重复 `Collect` 会被忽略，弹药已满时只在本机显示原生反馈而不持续广播拾取 RPC。`test003` 已通过 FRP Direct 双端实测，原有的重复动画、重复音效和不可见道具问题未再出现；日志确认双方构建一致、普通箱确定性和满弹药抑制均实际生效。

当前构建还新增四类只读兼容性诊断：`LEVEL_OUTCOME` 记录联机关卡结束和扣命前后的生命、场景、切关及房间状态；`WORKSHOP_GAME_MODE_COMPARE` 比较下载到的 Workshop Campaign、`GameState` 和 `RoomInfo` 的 `gameMode`，不主动改写任何模式；`OPTIONAL_BRO_MOD` 通过可选公开 API 记录 Swap Bros 的版本、API 能力、有序角色表指纹和本地选择指纹；`AFK_TIMER`、`AFK_STATE` 和 `PLAYER_DROPOUT` 记录本机角色的原生 AFK 倒计时、35 秒超时和玩家槽位移除前后状态。这些诊断借鉴 Utility Mod 使用原生完成事件、状态入口和弱依赖 API 的方式，但不引入 RocketLib、不复制调试菜单，也不控制角色切换或更改原生 AFK 规则；代码已通过 .NET 3.5 标准构建，尚待游戏内双端触发验收。

UMM 设置页现在按 `Workshop 联机`、`FRP Direct`、`诊断日志` 的顺序显示三个可折叠分组，展开状态会保存。三个标题使用不同颜色，右向三角图标表示收起、下向三角图标表示展开，不再使用 `+`/`-` 文本。诊断日志支持大厅网络、Workshop、玩家生命周期、AFK、关卡结果、Workshop 对象、FRP、可选 Mod 和 Harmony 追踪九类独立开关，并以三列两行的宽布局提供 `Basic`、`Join / Rejoin`、`AFK / Failure`、`Workshop` 和 `Full` 预设；关闭某类只减少诊断输出，不改变联机行为。每个会话仍会保留 `BUILD_INFO`、会话边界、类别清单、Warning、Error 和 Unity 异常。

当前仍需继续验证：新增诊断在真实双端会话中的日志内容，尤其是 AFK 触发后房主是否仍把已移除槽位计入存活人数；道具修复在官方 Steam 大厅 Workshop 会话中的独立复测、重复退出/重入、多地图兼容、高延迟和长期稳定性。部分 Workshop 地图还可能在 `GeneratePole.Awake`、`BroBase` 或特效销毁流程抛出自身运行错误。详细实现、测试边界和历史证据见 [开发与测试文档](docs/DEVELOPMENT.md) 与 [问题记录索引](issues/README.md)。

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

需要精简日志时，展开“诊断日志”分组选择预设，再按问题需要逐项调整类别。双方排查同一问题时原则上选择相同类别，以便对齐日志；旧版本配置升级后日志类别默认恢复为完整诊断。

如果旧配置中的 `Custom level scene` 为空，插件加载时会自动补回默认值 `Test Evan2`；已经填写其它场景名的配置不会被覆盖。

升级旧版本配置时，插件会清理旧版本遗留的测试默认值；已经填写的其它自定义值不会被覆盖。`Diagnostic label` 只用于日志文件名和关联信息，不参与联机行为；任意一端都可以创建大厅，实际网络角色由游戏大厅流程自动决定。

7. 任意一端按官方流程创建线上大厅，另一端先加入 `p1-p4` 选择界面。加入方必须按一次攻击键占用自己的位置，创建方确认双方出现在不同位置后，再按攻击键开始进入地图游玩；不要等进入地图后才选择角色。

当前版本也支持“创建方正在进入或已经进入 Workshop 地图时，另一端再加入”。加入方会等待 Workshop 场景和原生玩家生成流程就绪，再使用本机主控制器请求一次玩家槽位；已有槽位或待处理请求时不会重复追加。正常测试仍建议先加入大厅、确认双方占用不同位置后，再由创建方进入地图。

晚加入、掉线槽位恢复和控制器修复所需的诊断日志关键字统一记录在 [开发与测试文档](docs/DEVELOPMENT.md#晚加入支持) 中。重复退出/重入仍是尚未完整验收的范围。

Steam 主机迁移后原房主重新加入无法操作的问题已完成实机验证：角色可以正常生成并恢复移动、跳跃和开火。实现会清除退出流程遗留的暂停状态，但不改写玩家槽位或远端控制器编号。

双端排查时可以在 UMM 设置中填写相同的 `Diagnostic session ID`，并为两端填写不同的可选 `Diagnostic label`。通过 Steam 或 FRP Direct 创建、加入房间时会自动创建新的会话日志；Harmony 详细追踪会写入同名的 `.trace.log` 文件，普通事件日志不会与上一次联机测试混在一起。日志还会记录实际网络角色，但标签本身不参与联机行为。

每次测试结束后，必须收集本次实际参与测试的所有端的诊断 `.log`、`.trace.log`，并尽量同时保存各端的 UMM `Core\Log.txt` 和游戏 `error.log`。共享 DLL 部署目录只用于安装 Mod，不是集中日志目录；每台机器的诊断日志都写入自己的 `Application.persistentDataPath\BroforceOnlineDiagnostics\`。对面只有日志而没有 MCP 状态时，仍应先比较 `BUILD_INFO buildHash`，并在结论中明确缺失的证据。除非用户明确要求跳过，否则不得仅凭单端日志断定网络根因。

Mod 默认关闭注入；关闭注入时只记录诊断信息。

### MCP 调试

本项目可以配合 `Broforce_src/unity-inspector-mcp` 在游戏运行时读取关卡、玩家槽位、角色对象、截图和日志。双端端点、事件采样、40 秒监控窗口、运行时调试授权及客户端崩溃后的恢复流程，统一以 [开发与测试文档](docs/DEVELOPMENT.md#联机稳定性目标与自主调试约定) 为准。

### 加入提示

Workshop 线上会话中的“按开枪键加入游戏”横幅已在房主和加入方两端验证屏蔽。游戏原始提示由 `HeroController.Update` 调用 `LevelTitle.ShowText` 显示；Mod 只匹配本地化键 `LOC_HUD_PRESSTOJOIN` 对应的文本，并在命中时隐藏现有横幅。普通大厅、离线模式和攻击键加入功能不受影响。

### 联机 AFK 开关

UMM 设置中的 `Disable automatic AFK spectator mode in online games` 默认关闭。开启后，Mod 仅在联机游戏中重置本机角色的原生 AFK 计时器，防止角色因长时间没有输入而被自动移除并进入观战；手动退出、网络断开、正常死亡和离线游戏不受影响。需要保护双方角色时，双方都应开启该选项。

2026-08-25 双端实测确认：该选项由每台客户端独立控制，不会由房主同步给加入方，也不能在房主端保护远程角色。房主开启、加入方关闭时，加入方角色仍会在原生超时后进入 AFK，房主角色保持在线；需要保护双方角色时，双方必须分别开启该选项。

双方都关闭时则完全使用 Broforce 原生规则。原生 `Player.Update` 只在“存活玩家数大于本机玩家数”时累计 AFK 计时，因此一名玩家先进入 AFK 后，最后一名本地角色通常会停止累计并被保留，不会仅因无人输入而让双方角色全部进入 AFK。

当前构建会以低频日志记录原生 AFK 流程：约 5 秒写入 `AFK_TIMER event=counting`，约 30 秒写入 `event=warning`，有输入或条件改变后写入 `event=reset`；确实达到原生 35 秒分支时写入 `AFK_STATE event=timeout-triggered`，槽位实际移除后写入 `PLAYER_DROPOUT event=applied`。只有能与本机 35 秒触发对应的退出才标记 `reason=native-afk-timeout`，其它主动退出或断线统一保守记录为 `reason=unknown`。每条关键日志都带有移除前后玩家槽位、角色、生命、存活人数、本地人数和总生命，可直接检查房主是否还把 AFK 玩家算作存活。

上一轮使用旧构建 `fcb50bff...112a` 的双端日志不包含这些 AFK 观测点，只能看到加入方最终发生 `Dropout`，无法证明 AFK 倒计时何时开始、35 秒分支是否执行，或房主在槽位移除前后如何计算存活人数。公开房间里额外出现但未成功加载 Mod/地图的成员不作为该问题证据。下一轮应先确认启动日志出现 `AFK_DIAGNOSTICS_PATCH playerUpdate=True; dropoutRpc=True`，再让目标客户端保持无输入至少 35 秒并收集双方同一会话日志。

### FRP Direct 实验联机

FRP Direct 已完成公共 FRP UDP 的基础双端游戏验收，但仍是默认关闭的实验功能。开启游戏层开关后，Mod 会用 FRP 连接接管 Broforce 房间查询、PID 分配和 RPC 字节传输；不开启游戏层开关时，独立 UDP peer 仍只做握手/心跳验证，Steam 联机保持默认。

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

完成握手后按原有 Broforce 流程操作：房主打开线上建房界面并创建大厅；加入方打开线上大厅列表，等待唯一的 FRP 房间出现后选择加入。双方进入 P1-P4 后分别占用玩家位置，再由房主进入 Workshop 地图。无需额外输入房间码，也无需在游戏大厅里再次填写 FRP 地址。

2026-08-25 用户已通过公共端点 `frp-use.com:27045/UDP` 完成基础双端验收：双方能够进入同一张第三方地图并正常联机游玩；当前分发构建又通过了双方按 `Esc` 正常显示自己和对方游戏名的验收。首轮失败、Workshop 加载分支修复和各轮构建记录见 [FRP Direct 实施与验收记录](issues/archive/ISSUES-2026-08-24-FRP内网穿透联机方案.md)。

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
umm-settings-preview.html   UMM 折叠分组和日志预设的交互式界面预览
```

## 文档

- [开发与测试文档](docs/DEVELOPMENT.md)
- [问题记录索引](issues/README.md)
- [已归档：异地加入、重复角色与 AFK 验收记录](issues/archive/ISSUES-2026-08-24-联机加入方重复角色与AFK开关编译测试记录.md)
- [已归档：FRP Direct 可行性与实施方案](issues/archive/ISSUES-2026-08-24-FRP内网穿透联机方案.md)
- [Utility Mod 代码借鉴方案与 AFK 诊断改进](issues/ISSUES-2026-08-25-Utility-Mod代码借鉴方案与AFK诊断改进.md)

开发文档包含当前有效的 Steam/FRP 联机流程、Workshop 注入调用链、英雄回复问题、构建约束、日志分析和后续测试步骤。`issues` 保存每轮问题和测试证据，其中可能包含已经撤销或被后续结论取代的历史方案；阅读时以问题索引、README 和开发文档的当前状态为准。

## 参考资料

- [BroforceMods Wiki](https://github.com/alexneargarder/BroforceMods/wiki)
- [Viewing Broforce's Code](https://github.com/alexneargarder/BroforceMods/wiki/Viewing-Broforce's-Code)
