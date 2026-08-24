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

修正后的结论是：公共 FRP 架构本身可行，但当前 DLL 仍不能只安装 `frpc`、开放本地端口就直接联机。必须先改造现有 `FuckNetLayer`/Lidgren 直连层，固定监听端口，绕过旧匹配服务器，并在 Mod 中实现 FRP 公网端点交换和房间握手。

## 推荐方案

保留当前 Steam 联机作为默认且已验证的模式，在确认底层可行性后再增加独立的“FRP 直连”模式。不要直接替换当前稳定的 Steam 路径。

### 阶段一：确认并隔离现有非 Steam 网络层

源码调查已经完成，确认重点对象为：

1. `FuckNetLayer`：现有的非 Steam `ConnectionLayer` 实现。
2. `TwoWayPeer`：Lidgren UDP 启动、监听和连接管理。
3. `FuckNet`、`FuckNetID`：连接标识、端点交换和数据发送。
4. `ConnectionLayer`、`Networking`、`RPCController`：继续复用游戏 RPC 和玩家同步。

下一步不是重写底层可靠传输，而是运行时验证这套路径在当前版本 DLL 中是否完整可用，并确认旧匹配服务器依赖是否可以移除。

### 阶段二：固定端口并增加 FRP Direct 入口

为 Mod 增加与现有 Steam 模式并列的 `FRP Direct` 模式，优先复用 `FuckNetLayer` 和 Lidgren：

- 创建方在本机监听一个固定或可配置的 UDP 端口，例如 `27045`。
- 公共服务商提供并运行 `frps`；创建方 Windows 电脑运行服务商配置好的 `frpc`，把公网 UDP 端口转发到 `127.0.0.1:27045`。加入方默认不需要运行 `frpc`。
- 加入方填写 FRP 服务器公网地址、端口以及可选的房间密码。
- 房主和加入方不再依赖旧的 `FnServerUrl` 匹配服务器；第一版可以由房主生成房间码或直接提供 FRP 地址和端口。
- 连接端点统一使用 FRP 公网地址，不能继续把 NAT 打洞得到的本地/公网端点当作唯一地址。
- 双方先完成协议握手、心跳和断线检测，此阶段暂不进入游戏。
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

以下配置表示未来固定 Lidgren UDP 监听端口后的转发方式；当前版本尚未接入 `FRP Direct`，直接配置不会改变现有 Steam 联机路径。配置字段必须以公共服务商提供的模板为准。

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

加入方在 Mod 中填写：

```text
服务器：frp.example.com
端口：27045
```

公共服务商需要允许分配对应的公网 UDP 端口；房主 Windows 防火墙需要允许 `frpc.exe` 和本地游戏 UDP 端口。由于 `frpc` 主动连接公共 `frps`，默认不要求房主在家用路由器上配置入站端口映射。如果后续调查发现游戏需要 TCP，或可靠控制通道和实时数据通道需要分开，则分别增加 TCP/UDP 代理。

### 公共 FRP 服务的实际要求

- 服务商必须支持 UDP 代理；只有 TCP 代理不能承载现有 Lidgren UDP 数据。
- 服务商必须允许分配固定的 `remotePort`，并说明端口是否独占、是否有流量或时长限制。
- 房主 Windows 防火墙需要允许 `frpc.exe` 和本地游戏 UDP 端口；通常不需要家用路由器入站映射，因为 `frpc` 主动连接公共 `frps`。
- 房主需要在开房期间保持 `frpc` 进程和隧道在线。
- 加入方只连接公共服务的公网地址和 UDP 端口，不连接房主家庭公网 IP。
- 公共服务商能够看到连接 IP、时间、端口和流量；FRP token 不得写入游戏日志或发给加入方。

### 计划中的用户操作

该流程仅适用于 `FRP Direct` 实现完成后的版本，当前 DLL 不能按此流程使用。

1. 房主向公共 FRP 服务商申请 UDP 隧道、服务器地址、服务端口、认证 token 和可用公网端口。
2. 房主在 Windows 上保存服务商提供的 `frpc` 配置并启动 `frpc.exe`。
3. 房主在 Mod 中选择“FRP 直连模式”，点击“创建 FRP 房间”；Mod 固定监听本地 UDP 端口并显示公网地址、端口、房间密码或房间码、`buildHash`。
4. 房主把公网地址、UDP 端口、房间密码或房间码以及 Mod 版本发给对方，不发送 FRP token。
5. 加入方在 Mod 中选择“加入 FRP 房间”，输入公网地址、UDP 端口和房间信息；默认不需要安装或启动 `frpc`。
6. 双方完成握手、协议版本和 `buildHash` 校验后，房主统一分配玩家槽位并进入游戏。
7. 测试结束后，房主退出房间并关闭 `frpc`，释放公共 UDP 端口。

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

仍需重点验证 `FuckNetLayer` 与当前 Steam 版本资源、场景和 RPC 的兼容性；如果旧匹配服务器流程与房间状态深度耦合，仍可能需要补写一层轻量房间协调协议。

FRP 只负责把公共公网端口转到房主机器，不能自动替代 Steam Lobby、玩家身份、RPC 和游戏状态同步。最终稳定性还取决于公共 FRP 服务的 UDP 支持、带宽、端口策略、跨地区线路质量和房主上行网络。

## 当前决策

1. 当前已验证的 Steam 联机保持默认，不做破坏性替换。
2. 已完成 `Assembly-CSharp` 网络层只读调查，确认存在可复用的 `FuckNetLayer`/Lidgren UDP 路径。
3. 下一步先做最小原型：固定 UDP 端口、FRP 地址直连、握手、心跳和 `buildHash` 校验。
4. 最小原型通过后，再接入玩家加入、角色生成和现有 RPC。
5. 在最小原型验证前，不修改 Steam 默认路径，也不把 FRP 配置作为现有联机的强制依赖。
6. 第一版不内置 `frps`，也不自动安装第三方 FRP；默认由用户手动启动公共服务商提供的 `frpc`。

## 源码证据

本次结论来自 `D:\Study\C#\Broforce-Mods\Broforce_src\Broforce-Source`：

- `!SOURCE_ANALYSIS.md`：说明源码树包含 `Networking` 和 `Lidgren`。
- `FuckNetLayer.cs`：实现非 Steam `ConnectionLayer`，负责创建、加入和发送游戏数据。
- `TwoWayPeer.cs`：使用 `NetPeerConfiguration.Port` 启动 Lidgren UDP，并通过 `Connect`/`SendMessage` 建立可靠有序连接。
- `FuckNet.cs`：创建客户端、发送 RPC/控制消息并维护连接。
- `Utility/Platforms/Desktop/DesktopPlatform.cs`：说明正式桌面平台在 Steam 启用时通常选择 `SteamLayer`，否则才选择 `FuckNetLayer`。
- `MatchMaking.cs`：记录原有匹配服务器 UDP `10007` 和中继 UDP `12000`。
