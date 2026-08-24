# FRP 内网穿透联机可行性与实施方案

## 需求背景

希望在现有 Broforce Online Mod 中增加通过公共 FRP 服务进行异地联机的能力，作为 Steam 联机之外的备用方式。公共服务商运行 `frps`；默认只有房主电脑运行服务商要求的 `frpc` 客户端，加入方不需要运行 FRP 程序。

## 当前实现与结论

当前 Mod 复用 Broforce 原生联机链路，主要依赖：

- Steam Lobby 创建、发现和加入房间。
- Steam 玩家 PID 和所有权判断。
- `SteamLayer`、`ConnectionLayer` 以及游戏原生 RPC。
- Steam 网络传输、NAT 穿透或中继。

README 中的 TCP `9999` 是 Unity Inspector MCP 的调试端口，不是游戏联机端口。转发该端口只能用于远程调试，不能让游戏数据改走 FRP。

进一步阅读 `Broforce_src/Broforce-Source/!SOURCE_ANALYSIS.md` 并检索反编译源码后，发现游戏保留了一套可复用的非 Steam 联机后端：

- `FuckNetLayer` 已实现 `ConnectionLayer`，可以继续使用游戏现有的 PID、RPC、房间和玩家同步逻辑。
- `TwoWayPeer` 基于 Lidgren UDP，支持监听端口、可靠有序消息、心跳和直接连接。
- `FuckNetID` 使用本地端点和公网端点标识连接。
- 原有非 Steam 匹配服务器端口为 UDP `10007`，中继端口为 `12000`。
- 正式 Steam 版通常优先创建 `SteamLayer`；非 Steam 路径还依赖 `GameSystems.FnServerUrl` 和旧的匹配服务器，因此不能直接通过 FRP 配置启用。

修正后的结论是：公共 FRP 架构可用。独立 `FrpDirectLayer` 已接入房间列表、Broforce 原生 PID/ServerID、RPC 字节传输和 Steam Workshop 内容加载；用户已通过公共 FRP UDP 端点完成正常双端联机游玩实测。该游戏层仍是默认关闭的实验功能，尚未完成断线重入、多地图、高延迟和长期稳定性验证，不能标记为稳定发布版本。

## 推荐方案

保留当前 Steam 联机作为默认且已验证的模式，在确认底层可行性后再增加独立的“FRP 直连”模式。不要直接替换当前稳定的 Steam 路径。

### 阶段一：确认并隔离现有非 Steam 网络层

源码调查已经完成，确认重点对象为：

1. `FuckNetLayer`：现有的非 Steam `ConnectionLayer` 实现。
2. `TwoWayPeer`：Lidgren UDP 启动、监听和连接管理。
3. `FuckNet`、`FuckNetID`：连接标识、端点交换和数据发送。
4. `ConnectionLayer`、`Networking`、`RPCController`：继续复用游戏 RPC 和玩家同步。

底层只读调查、独立 `NetPeer` 编译验证和公共 FRP 双端运行时验证均已完成。最终实现采用独立 `FrpDirectTransport`/`FrpDirectLayer`，复用游戏程序集内的 Lidgren、PID 和 RPC，不再接入旧 `FuckNetLayer` 的匹配服务器入口。

### 阶段二：固定端口并增加 FRP Direct 入口

为 Mod 增加与现有 Steam 模式并列的 `FRP Direct` 模式，优先复用 `FuckNetLayer` 和 Lidgren：

- 创建方在本机监听一个固定或可配置的 UDP 端口，例如 `27045`。
- 公共服务商提供并运行 `frps`；创建方 Windows 电脑运行服务商配置好的 `frpc`，把公网 UDP 端口转发到 `127.0.0.1:27045`。加入方默认不需要运行 `frpc`。
- 加入方填写 FRP 服务器公网地址、端口以及可选的房间密码。
- 房主和加入方不再依赖旧的 `FnServerUrl` 匹配服务器；第一版可以由房主生成房间码或直接提供 FRP 地址和端口。
- 连接端点统一使用 FRP 公网地址，不能继续把 NAT 打洞得到的本地/公网端点当作唯一地址。
- 双方先完成协议握手、心跳和断线检测，此阶段暂不进入游戏。该最小协议已经写入 Mod，并已通过公共 FRP 双端运行时验收。
- 握手时双方交换协议版本和编译期 `buildHash`。
- 日志必须记录本地 `buildHash`、对方 `buildHash` 以及匹配结果。
- `buildHash` 不一致时明确提示并拒绝进入游戏，避免不同 DLL 产生难以判断的同步问题。
- FRP token、房间密码等敏感信息不得写入日志。

### 阶段三：玩家加入和游戏数据同步

建立基础连接后，再接入玩家和 RPC：

- 由创建方作为权威端统一分配 P1-P4 槽位。
- 同一连接或玩家标识只能占用一个槽位。
- 创建方决定角色生成、出生位置、退出、重入和观战状态。
- 加入方只发送输入和必要请求，避免自行重复创建角色。
- 为输入、角色状态和可靠事件分别设计传输策略。
- 对加入请求增加唯一编号和幂等处理，避免高延迟重试再次生成 P2-P4 多个角色。
- 复用现有 AFK 开关语义，且只处理本机拥有的角色。

### 阶段四：补齐联机生命周期

基础游戏同步正常后，继续支持：

- 断线检测和重新加入。
- 角色死亡、复活和观战。
- Workshop 地图加载和场景切换。
- 通关、跳关、返回大厅及返回主菜单。
- 创建方退出后的处理；第一版可以直接结束房间，暂不实现主机迁移。
- 超时、丢包、乱序和重复数据包的处理。

## FRP 配置示例

以下配置同时适用于独立握手模式和 FRP Direct 游戏层。是否接管 Broforce 房间/PID/RPC 由 Mod 中独立的游戏层开关决定；`frpc` 的 UDP 转发配置不需要因此增加第二个端口。配置字段必须以公共服务商提供的模板为准。

创建方 `frpc.toml` 示例：

```toml
serverAddr = "frp.example.com"
serverPort = 7000
auth.token = "从公共服务商获取的 token"

[[proxies]]
name = "broforce-online"
type = "udp"
localIP = "127.0.0.1"
localPort = 27045
remotePort = 27045
```

加入方在 Mod 的单一端点输入框中填写：

```text
FRP server endpoint: frp.example.com:27045
```

公共服务商需要允许分配对应的公网 UDP 端口；房主 Windows 防火墙需要允许 `frpc.exe` 和本地游戏 UDP 端口。由于 `frpc` 主动连接公共 `frps`，默认不要求房主在家用路由器上配置入站端口映射。如果后续调查发现游戏需要 TCP，或可靠控制通道和实时数据通道需要分开，则分别增加 TCP/UDP 代理。

### 公共 FRP 服务的实际要求

- 服务商必须支持 UDP 代理；只有 TCP 代理不能承载现有 Lidgren UDP 数据。
- 服务商必须允许分配固定的 `remotePort`，并说明端口是否独占、是否有流量或时长限制。
- 房主 Windows 防火墙需要允许 `frpc.exe` 和本地游戏 UDP 端口；通常不需要家用路由器入站映射，因为 `frpc` 主动连接公共 `frps`。
- 房主需要在开房期间保持 `frpc` 进程和隧道在线。
- 加入方只连接公共服务的公网地址和 UDP 端口，不连接房主家庭公网 IP。
- 公共服务商能够看到连接 IP、时间、端口和流量；FRP token 不得写入游戏日志或发给加入方。

### 用户操作

1. 房主向公共 FRP 服务商申请 UDP 隧道、服务器地址、服务端口、认证 token 和可用公网端口。
2. 房主在 Windows 上保存服务商提供的 `frpc` 配置并启动 `frpc.exe`。
3. 房主在 Mod 中同时启用 `FRP Direct transport prototype` 和 `Route Broforce rooms and RPC through FRP Direct (experimental)`，选择 `Host` 并应用设置；Mod 固定监听本地 UDP 端口。
4. 房主把公网地址、UDP 端口、房间密码或房间码以及 Mod 版本发给对方，不发送 FRP token。
5. 加入方同样启用两个开关，选择 `Client`，在一个输入框中填写完整公网端点（例如 `frp-use.com:27045`）和相同的房间密码并应用设置；默认不需要安装或启动 `frpc`。
6. 双方完成密码挑战、协议版本和 `buildHash` 校验后，房主按原方式创建线上大厅；加入方按原方式打开大厅列表并加入唯一的 FRP 房间。
7. 依次验证大厅条目、PID/ServerID、P1-P4、角色生成、输入、场景与 Workshop RPC；该基础链路已于 2026-08-25 完成双端实测，后续测试继续覆盖断线重入、多地图和长期稳定性。
8. 测试结束后，房主退出房间并关闭 `frpc`，释放公共 UDP 端口。

## 测试计划

按以下顺序进行，任何阶段失败都先修复，不提前扩大测试范围：

1. 本机双进程完成连接、握手、心跳和 `buildHash` 校验。
2. 局域网双端完成单个加入方的角色生成和操作。
3. 通过 FRP 完成异地单加入方测试。
4. 模拟高延迟、丢包、乱序和重复加入请求。
5. 验证同一加入方始终只有一个角色，不出现 P2-P4 重复角色。
6. 验证 AFK 开关、死亡、观战、断线重入和场景切换。
7. 最后再测试多加入方和长时间游戏。

每轮测试必须保存创建方与加入方日志，并记录：

- 本地和远端 `buildHash`。
- 传输模式为 Steam 还是 FRP Direct。
- 会话 ID、连接 ID 和玩家槽位分配。
- 加入请求唯一编号及其接收、确认和去重结果。
- 延迟、超时、断线和重连事件。

## 工作量和风险

由于已确认存在 `FuckNetLayer`、`TwoWayPeer` 和 Lidgren UDP，预计不需要从零实现可靠传输、RPC 封装和插值同步。主要工作变为：

- 让 Steam 开启时仍可明确选择 `FRP Direct`，不破坏默认 `SteamLayer`。
- 将客户端随机监听端口改为固定或可配置端口。
- 绕过旧匹配服务器的建房、列表和 NAT 打洞流程。
- 增加 FRP 公网端点的房间信息和首次握手。
- 处理 FRP 单一公网端点下的玩家连接映射、断线和重复连接。
- 增加 `buildHash`、协议版本和房间密码校验。

实际实现没有接入 `FuckNetLayer` 或旧匹配服务器，而是新增轻量房间协调协议并复用原生 PID/RPC。公共 FRP 双端游玩已验证该基础兼容性；后续风险集中在断线重入、多地图、高延迟和长期稳定性。

FRP 只负责把公共公网端口转到房主机器，不能自动替代 Steam Lobby、玩家身份、RPC 和游戏状态同步。最终稳定性还取决于公共 FRP 服务的 UDP 支持、带宽、端口策略、跨地区线路质量和房主上行网络。

## 当前决策

1. 当前已验证的 Steam 联机保持默认，不做破坏性替换。
2. 已完成 `Assembly-CSharp` 网络层只读调查，确认存在可复用的 `FuckNetLayer`/Lidgren UDP 路径。
3. 最小原型代码已完成：固定 UDP 端口、FRP 地址直连、密码挑战、握手、心跳和 `buildHash` 校验；标准 .NET 3.5 构建和公共 FRP 双端运行时验收均已通过。
4. 房间查询、加入确认、稳定机器 ID、原生 PID/ServerID、RPCBatcher/RecieveBytes 和 Workshop ready/phase 房间元数据均已接入，并已完成公共 FRP 双端正常游玩实测。
5. Steam 保持默认路径；FRP Direct 只在双方显式开启两个相关开关后接管房间和 RPC，不作为现有联机的强制依赖。
6. 第一版不内置 `frps`，也不自动安装第三方 FRP；默认由用户手动启动公共服务商提供的 `frpc`。

## 源码证据

本次结论来自 `D:\Study\C#\Broforce-Mods\Broforce_src\Broforce-Source`：

- `!SOURCE_ANALYSIS.md`：说明源码树包含 `Networking` 和 `Lidgren`。
- `FuckNetLayer.cs`：实现非 Steam `ConnectionLayer`，负责创建、加入和发送游戏数据。
- `TwoWayPeer.cs`：使用 `NetPeerConfiguration.Port` 启动 Lidgren UDP，并通过 `Connect`/`SendMessage` 建立可靠有序连接。
- `FuckNet.cs`：创建客户端、发送 RPC/控制消息并维护连接。
- `Utility/Platforms/Desktop/DesktopPlatform.cs`：说明正式桌面平台在 Steam 启用时通常选择 `SteamLayer`，否则才选择 `FuckNetLayer`。
- `MatchMaking.cs`：记录原有匹配服务器 UDP `10007` 和中继 UDP `12000`。

## 2026-08-25 最小原型实施记录

本轮已完成：

- 新增 `src/FrpDirectTransport.cs`，直接引用游戏程序集内的 `Lidgren.Network`。
- 新增 Host/Client、固定本地 UDP 端口、FRP 公网地址/端口和可选房间密码设置。
- 实现随机挑战与 HMAC-SHA256 密码证明；协议版本或 `buildHash` 不一致时拒绝连接。
- 实现 5 秒应用层心跳、18 秒心跳超时和普通断线后的 5 秒重连。
- 使用异步重启状态等待旧 Lidgren peer 完全释放端口，再应用新设置。
- 标准 `.NET Framework 3.5` 构建和本机/内网部署成功；传输原型构建 `a0b8c065561ec16ee9465b78f2562f68c0870e92481a7ac8268ae94e17168d9e` 已通过用户公共 FRP 双端验收。
- 后续 UI 改进将加入方的公网地址和端口合并为单一 `host:port` 字段，并保留旧设置自动迁移。

用户已确认通过公共端点 `frp-use.com:27045/UDP` 完成真实双端握手和持续心跳，结果符合预期。该次验收在当时只证明 FRP UDP 传输原型可用；后续章节记录了游戏层接入、失败根因、修复以及最终正常双端游玩验收。

## 2026-08-25 房间、PID 与 RPC 接入记录

在该阶段，源码与标准构建已完成但尚待双端实机验收。测试构建的嵌入式 `buildHash` 为 `867a32fd986fcf0d75e292da196f1cda2eab7c390fb4a04e6f36a4428f2b4df2`，并部署到项目安装包、本机 UMM 和内网测试端：

- 协议升级为 v2，新增稳定随机机器 ID，并将双方机器 ID 绑定进密码证明。
- 新增房间查询/状态、加入确认/拒绝、离开通知和可靠游戏数据消息。
- 新增 `FrpDirectRoomInfo`，同步 Broforce `RoomInfo` 与 Workshop ready/phase，且不会广播房间密码或 FRP token。
- 新增 `FrpDirectLayer : ConnectionLayer`，复用 `GeneratePlayerID`、`BroadcastPlayerID`、`RPCBatcher` 和 `ConnectionLayer.RecieveBytes`。
- 新增平台层选择管理器；只有显式开启游戏层开关时替换 Steam，退出或关闭开关后恢复默认层。
- 客户端仍使用单一 `host:port` 输入；旧地址/端口配置自动迁移。
- 第一版限制为房主加一台远端机器，不实现主机迁移。

下一轮验收顺序固定为：大厅列表出现、PID/ServerID 建立、P1-P4、角色生成、输入、普通地图、Workshop 和退出/重入。

## 2026-08-25 首轮游戏层实测、根因与修复

用户通过公共 UDP 端点完成第一轮游戏层测试。实际结果不是“完全无法加入”：

- 加入方只看到一个 FRP 房间。
- 加入方能进入 P1-P4 界面，并确认占用了 P2。
- 房主开始进入地图后画面进入 `Test Evan2`，加入方停在黑屏。
- 房主随后尝试退出，游戏出现卡死并崩溃；加入方在房主退出后离开黑屏。

本轮可取得的完整诊断文件来自实际网络角色为 Host 的本机：`diagnostics-client-test003-20260824-183616-886.log` 和同名 `.trace.log`。文件名中的 `client` 只是诊断标签，`SESSION_BEGIN.networkRole=host` 才是实际角色。该端 `BUILD_INFO` 为 `867a32fd986fcf0d75e292da196f1cda2eab7c390fb4a04e6f36a4428f2b4df2`。远端 UMM 日志只确认 Mod 正常加载；远端游戏退出后 MCP 已不可访问，且当前共享路径无法取得其 `Application.persistentDataPath` 诊断日志。本轮没有生成新的 `error.log`，因此根因结论以 Host 完整日志、trace 和 Broforce 源码分支为依据，Client 侧黑屏时的精确场景状态仍待下一轮双端日志确认。

Host 时间线：

- `+21.977s`：收到加入请求，记录 `authenticated client joined`，开始远端 PID 分配。
- `+27.234s`：进入 `MissionScreenVietnam`。
- `+28.276s`：注入 Workshop ID `3642518573`，目标场景为 `Test Evan2`。
- `+29.284s` 起：`Test Evan2` 约每 0.22 秒重复触发 `sceneLoaded`；这里的场景名是 Workshop campaign 的 Unity 承载场景，不能仅凭名称判定加载了官方测试地图。
- `+43.499s`：Host 的 P1 已生成角色并设置出生点，说明 PID/RPC 链至少推进到了角色生成前置阶段。
- `+44.301s`：Host 报应用层心跳超时；`+44.314s` 对 P2 执行 `Dropout`。该超时发生在用户退出前约 36 秒，是主线程长期加载停顿的后果，不是最初故障。
- `+80.419s`：用户退出路径开始加载 `MainMenu`。
- `+84.090s`：旧 `switchingLevel=True`、`nextScene=Test Evan2` 再次触发 `SwitchLevel`，把 Host 拉回重复加载，解释退出后的卡死。

根因来自 `GameState.LoadLevel` 的内容来源分支：

1. 旧 `FrpDirectLayer.ConnectionType` 返回 `LayerType.Badumna`。
2. Broforce 因此调用旧 `PlaytomicController.LoadLevel`，并订阅 `PlaytomicController.LevelLoadCompleteEvent`。
3. 当前 Workshop 注入实现只通过 `SteamController.LevelLoadCompleteEvent` 恢复 campaign 并继续加载。
4. FRP 游戏数据本身已经走 `FrpDirectLayer.SendData`，但错误的内容来源枚举让 Workshop 下载/完成回调走错了分支。
5. 场景加载又阻塞 Unity 主线程，应用心跳无法及时处理；恢复 Update 后旧版 18 秒窗口立即把连接判为超时。
6. 离房前没有主动取消仍在进行的 Workshop/切关状态，`GameModeController` 在返回菜单后继续执行旧 `nextScene`。

修复后的行为：

- `FrpDirectLayer.ConnectionType` 返回 `LayerType.Steam`，只用于选择 Steam Workshop 内容下载；房间、PID 和 RPC 仍走 FRP，不会恢复 Steam P2P。
- 应用心跳正常超时改为 60 秒；检测到 Unity 主线程因加载停顿超过 10 秒时，恢复后重新开启心跳窗口。
- FRP 本地离房、收到房主离开、房主传输断开或配置变化时，清除 Workshop 完成状态、重复加载抑制状态、`switchingLevel`、`nextScene` 和 `Networking.PauseStream`。
- 新增日志明确区分“FRP 游戏 RPC”和“Steam Workshop 内容加载”。

修复构建已经通过标准 `BuildAndDeploy.ps1` 编译并部署：

```text
buildHash=a53f0dc3a627d57efac53d36f34a84363aa16aa500754282b0305ea36cc11ec7
DLL SHA-256=7D18EF9D8AB325275F6BCB939B6214ECDA772E0CBD4FDD7DD0E8CE71AA745AC2
```

项目安装包、本机 UMM 和内网测试端三份 DLL 的长度均为 `172032` 字节，文件 SHA-256 完全一致。反射验证确认协议仍为 v2、`HeartbeatTimeoutSeconds=60`、`MainThreadStallThresholdSeconds=10`，且 `FrpDirectLayer.ConnectionType` 返回枚举值 `2`，与当前游戏程序集的 `LayerType.Steam=2` 一致。

用户随后使用 `buildHash=a53f0dc3a627d57efac53d36f34a84363aa16aa500754282b0305ea36cc11ec7` 完成相同双端流程，确认房主与加入方能够进入同一张第三方地图并正常联机游玩。首轮出现的 Workshop 加载分支、主线程停顿心跳误超时和退出残留问题均未再阻断联机，因此 FRP 游戏基础链路现已通过用户实测；断线重入、多地图、高延迟和长期稳定性仍是后续范围。

## 2026-08-25 `Esc` 在线玩家名单修复

正常游玩验收后发现 FRP 双方按 `Esc` 时看不到任何在线玩家。原生 `ConnectionLayer.UpdateOnlinePlayerList` 会调用虚方法 `GetAllOnlinePlayerNames`，但基类实现固定返回 `null`；官方 `SteamLayer` 覆盖了该方法，而 `FrpDirectLayer` 当时没有覆盖，所以列表被持续清空。

当前 `FrpDirectLayer.GetAllOnlinePlayerNames` 已按本机、远端的稳定顺序返回成员：本机使用 `Connect.PlayerName`，远端使用原生 `Connect.SetPlayerName` RPC 同步到 PID 的名字；只纳入仍连接且握手有效的远端。名字尚未到达的短窗口显示通用占位名，不暴露 FRP 机器 ID、公网端点、密码或 token。远端退出后下一次列表刷新会移除该条目。

名单修复已通过标准 `BuildAndDeploy.ps1` 编译并部署：

```text
buildHash=683227dab9d54673e85a8fbc3a39354778faea5e0d7290e7381ba7b54bdfe518
DLL SHA-256=1985B88DC74693C95B1C4269283AC405636325D84A58730576A74EF66A65EB3C
```

项目安装包、本机 UMM 和内网测试端三份 DLL 的长度均为 `172032` 字节，SHA-256 完全一致。反射验证确认最终程序集中的 `GetAllOnlinePlayerNames` 声明类型是 `BroforceOnlineDiagnostics.FrpDirectLayer`、返回类型是 `System.String[]`，并正确覆盖 `ConnectionLayer` 的虚方法。用户随后完成双端实测，确认双方按 `Esc` 时能够正常看到自己和对方的游戏玩家名，因此该名单修复已通过验收。
