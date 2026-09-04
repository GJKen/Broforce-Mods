# Custom Map Multiplayer

> [English](README.en.md)

这是一个面向 Steam 版 Broforce 的 Unity Mod Manager + Harmony Mod。默认复用官方 Steam Lobby/Steam P2P；可选的 `FRP Direct` 使用独立房间、PID 和游戏 RPC，但 Workshop 内容仍由 Steam 下载。

使用 Workshop 地图注入或 FRP Direct 时，所有参与联机的玩家必须安装相同构建的 Mod，并订阅、下载相同的 Workshop 地图；排查版本时以各端日志中的 `BUILD_INFO buildHash` 为准。加入方会读取房主发布的 Workshop ID、场景名和战役名，不再需要手工填写与房主相同的地图配置。

## 当前状态

当前版本为实验性 `0.5.0`，尚未达到稳定发布状态。

| 项目 | 状态 |
| --- | --- |
| 当前分发构建 | `buildHash=9cc86f24743c6d9109e9c1c204a385999b1ce010b58aa0303133dac47192cf84` |
| DLL SHA-256 | `69A01A8A39271A8FDD2A141078C72CA35C93D9447C584A04C6DC0C85283E062E` |
| DLL 程序集版本 | `0.5.0.0` |
| Steam 联机 | 默认路径；已验证官方大厅进入同一张 Workshop 地图及彩色延迟名单 |
| FRP Direct | 默认关闭；三机基础联机已验证，代码支持房主加最多三台远端 |

已验证：

- 双端进入、晚加入、当前地图的退出/重入，以及双方独立角色和控制。
- Steam 与 FRP Direct 的 Esc 彩色延迟名单和动态房主名；FRP 的三机基础联机、静态 `1` 人房满员提示及 Host/Client 配置自动应用。
- Workshop 地图身份由房主发布，加入方自动采用；缺少订阅时显示提示并停止加载；注入可热关闭并恢复官方地图。
- Workshop 加载会优先复用 Steam 已安装目录或旧版 UGC 本地缓存；缓存不可读时才回退到 Steam 下载，并抑制同一张地图加载期间的重复请求。
- Workshop 的入场横幅、Esc 返回大厅和主菜单动画；标准弹药箱的确定性、远端扫描抑制和重复拾取防护已在 FRP 双端验证，官方 Steam 大厅和更多地图仍需复测。
- 高密集战斗长测中，Host 掉帧已明显减轻；当前结论为观察到改善，仍需统一图形设置、交换 Host 并完成 p50/p95/p99 对照后再正式验收，详见 [Host 性能问题记录](issues/ISSUES-2026-08-30-联机房主低帧率与Host专属扫描性能问题.md)。
- Esc 菜单中的“立即进入 AFK”按钮；房主和加入方分别操作时只影响各自本地角色。主动 AFK 不会自动重新加入，用户通过正常流程回来后会恢复原槽位的生命、英雄类型和角色；普通网络掉线仍按原有流程自动恢复，详见[主动 AFK 按钮问题记录](issues/ISSUES-2026-09-01-新增ESC菜单主动AFK按钮.md)。

Workshop 酸液失败样本已确认不是槽位或 NID 串号，而是旧补丁只覆盖 `CheckForTraps`，遗漏了 `CalculateMovement` 和 `Damage` 对 `CoverInAcid` 的直达调用。当前实现维护场景级 `DoodadAcidPool` 列表，在统一 `CoverInAcid` 基入口执行加入方本地预测和房主权威校验，并将 Host 周期扫描限频；双方已实机验证房主、加入方分别进入酸液时均能正确死亡，且不会连带出生区玩家，详见 [独立 issue](issues/ISSUES-2026-08-30-Workshop联机酸液池导致双方一起死亡.md)。普通 Mook 死亡终态、关卡结束防重入、官方 Steam 道具、高延迟和长期重入仍需扩展验收；McBrover 火鸡主动引爆残留仍可复现但概率显著降低，详见 [独立 issue](issues/ISSUES-2026-08-28-McBrover火鸡主动引爆后残留实体.md)。FRP 的四机、`2` 至 `4` 人容量边界、动态降额重入和主机迁移尚未验证。

日志中的 `NullReferenceException` 表示代码使用了尚未初始化或已经失效的对象；`DoodadCrate` 是游戏原生的箱子处理类。加入方箱子特效持续重复和相关错误循环属于独立问题，详见 [加入方箱子问题记录](issues/ISSUES-2026-08-30-加入方箱子坍塌特效持续重复.md)，不应作为 Host 战斗掉帧的直接证据。

当前范围不包括活动 AI 持续同步、敌方弹体、钱币、金色奖励、普通 `Grenade` 地形伤害或历史动态世界实验。详细实现和证据见 [开发文档索引](docs/DEVELOPMENT.md) 与 [问题记录索引](issues/README.md)。

## 安装与首次运行

1. 所有玩家安装 `r2modman`，为 Broforce 创建或选择默认的 profile，并在其中安装 UMM。启动一次游戏确认 UMM 加载成功。
2. 将 `Release\CustomMapMultiplayer.zip` 导入 r2modman 的 Broforce profile。ZIP 内已经包含 `UMM\Mods\CustomMapMultiplayer` 下的 DLL 和 `Info.json`。
3. 重启 r2modman，在 UMM 中确认 `Custom Map Multiplayer 0.5.0` 已加载。
4. 双方需订阅并下载相同 Workshop 地图，并在 `Multiplayer Options` 中开启 Workshop 地图注入。
只需房主在 UMM 填写地图的 Workshop ID；战役名可留空，场景名默认 `Test Evan2`，地图使用其它场景时再修改；加入方的 Workshop ID 可留空，mod会自动采用房主发布的地图配置；
如果加入方没有订阅房主地图，屏幕顶部会提示缺少订阅地图，你需要根据提示去订阅地图；
关闭 Workshop 地图注入和 FRP Direct 后，恢复官方创建街机线上地图。

配置图示:

<img width="781" height="417" alt="image" src="https://github.com/user-attachments/assets/48ad31e3-9103-44cd-ba2d-763c3801294f" />

6. 任意一端使用街机模式创建线上大厅，加入方找到房间直接加入即可。

### UMM 设置面板

实际 UMM 设置页采用左侧竖向功能列表、右侧显示当前功能内容的布局：

- `Multiplayer Options`：Workshop 地图注入和自动 AFK 旁观模式；主动 AFK 按钮位于游戏内 Esc 菜单。
- `FRP Direct`：直连开关、Host/Client 角色、端口、人数上限和连接参数。
- `语言`：直接点击“跟随系统”“English”或“中文”按钮切换界面语言。
- `Diagnostic Logs`：诊断会话标识、日志预设和诊断分类。

`umm-settings-preview.html` 只是静态预览；实际 UMM 界面以 `src/Plugin.cs` 和 `src/SettingsUiText.cs` 为准。

### 常用设置

- `Multiplayer Options` 中的 Workshop 地图注入开关关闭时会立即保存并清理注入状态，但不会强制中断或切走当前场景。退出当前房间并从菜单重新创建官方房间后，后续选图使用游戏原生战役流程；不需要删除已保存的 Workshop ID。
- `Diagnostic session ID` 用于关联双方同一轮日志；两端填写相同值。`Diagnostic label` 只影响日志文件名，不参与联机行为。
- `Multiplayer Options` 中的 AFK 开关由每台客户端独立控制。未勾选时显示“已启用自动 AFK 旁观模式”；勾选后显示“已禁用自动 AFK 旁观模式”。要保护双方角色，双方必须分别勾选；它不拦截手动退出、断线或正常死亡。
- Esc 菜单中的“立即进入 AFK”按钮会让当前客户端实际拥有的本地玩家立即进入原生 AFK 旁观流程，与自动 AFK 开关相互独立。按钮会按本地所有权和当前输入控制器确定目标；多本地槽位无法唯一确定时不会执行，避免误操作另一角色。主动 AFK 不会触发自动 `RequestJoinGame`，需要用户通过正常重新加入流程显式回来；回来时会恢复原槽位的生命、英雄类型和角色。普通网络掉线仍保持自动重入。
- 诊断日志预设（基础、加入/重新加入、AFK/失败、Workshop、完整）和九个诊断分类只影响日志输出，不改变联机行为。双方排查同一问题时应尽量选择相同类别。

每轮测试结束后，收集所有参与端的诊断 `.log`、`.trace.log`，并尽量同时保存 UMM `Core\Log.txt` 和游戏 `error.log`。Windows 日志目录为 `%USERPROFILE%\AppData\LocalLow\Free Lives\Broforce\CustomMapMultiplayer\`，不是 DLL 部署目录；UMM 中的“打开诊断日志目录”按钮也会打开这里。只有单端日志时，结论必须明确证据缺口，不能单独断定网络根因。

排查酸液池导致的异常死亡时，重点对齐双方同一会话的 `PLAYER_ACID` 事件：它记录 `CoverInAcid`、`CoverInAcidRPC` 和 `PlayerHasDiedRPC` 前后对应的玩家槽位、RPC 请求槽位、角色 NID、`IsMine`、坐标、`acidMeltTimer` 和 `hasBeenCoverInAcid`，并通过低频 `authority-gate` 标明 `host-check`、`client-request`、`authority-wait` 或 `native-fallback` 决策。

## FRP Direct 联机

FRP Direct 总开关默认关闭。

`Host`/`Client` 角色仍由用户明确选择。切换角色会立即保存并自动切换连接：Host 只使用本地 UDP 监听端口，完全忽略已保存的 Client 公网地址；Client 只使用 FRP 公网 `host:port`，完全忽略 Host 的本地监听端口。两套配置分别保留，切回原角色时无需重新填写。设置页不再提供手动 Apply 按钮；总开关和角色立即生效，端口、地址和密码在停止输入后自动保存并重连。心跳、超时检测和普通断线重试均由传输层自动处理。

房主：

```text
FRP Direct role: Host
Local UDP listen port: 27045
FRP room player limit: 点击 1、2、3、4 中的一个按钮，立即生效
FRP room password: 所有参与方约定的临时密码，或留空
```

人数按钮设置整个房间的角色上限：`1` 只允许房主，`2` 允许房主加一名加入方，`3` 允许房主加两名加入方，`4` 允许房主加三名加入方。该设置不会突破 Broforce 原生四人上限。房主可以在已经进入地图后打开 UMM 并直接切换人数，无需重启 FRP；新上限立即用于后续加入，已经在房间里的玩家不会被踢出。例如当前有三人时改为 `1`，三人仍可继续游戏，但退出的玩家不能重新加入，直到上限再次调高。

正常启动后应显示 `Listening on UDP 27045`；`frpc` 将公网 UDP 端口转发到 `127.0.0.1:27045`。修改当前角色使用的连接参数会自动重启连接，因此应在开始或结束联机时调整。

加入方：

```text
FRP Direct role: Client
FRP server endpoint: 服务商提供的完整 host:port（IPv6 使用 [地址]:端口）
FRP room password: 与房主一致
```

所有参与方使用同一标准构建且密码一致时，Client 状态应为 `Handshake complete; heartbeat active`，Host 会显示已认证客户端数量。协议版本、`buildHash` 或密码不一致会拒绝握手且不会自动降级。密码会保存在本机 UMM 设置文件中，但不会写入日志或通过网络明文发送；请使用临时密码，不要复用其它账号密码。FRP token 只属于 `frpc`，不要填入 Mod。

按 `Esc` 打开原生在线玩家名单时，FRP 玩家显示为 `xxxms | 玩家名`：`0-80ms` 为绿色、`81-150ms` 为黄色、`151ms` 以上为红色，首个 RTT 样本到达前显示灰色 `--ms`。房主显示为 `HOST | 房主名`，房主名使用 4 秒一轮的动态彩色渐变。这里的延迟表示每台机器到房主的往返时间；多人房间由房主把各连接的测量结果同步给加入方。

握手完成后，房主照常创建线上大厅，各加入方在在线大厅列表中选择唯一的 FRP 房间；所有玩家进入 `p1-p4` 后分别占位，再由房主进入 Workshop 地图。房间按房主当前选择的 `1` 至 `4` 人上限接受加入方；达到上限后的加入请求会被拒绝。地图内降低上限只关闭后续空位，不移除现有成员；提高上限会立即重新开放空位。客户端之间的 RPC 由房主定向中继。房主加两台加入方的三机基础联机及静态 `1` 人房满员提示已经通过用户实测；四机、`2` 至 `4` 人容量边界和动态降额重入仍需专项验收。FRP 当前不支持主机迁移；用户对照测试确认房主退出后加入方直接返回主菜单，没有复现本 issue 的 Steam 黑屏。完整协议和历史失败记录见 [FRP Direct 验收记录](issues/archive/ISSUES-2026-08-24-FRP内网穿透联机方案.md)。

FRP 房间列表显示人数已满时，加入方点击房间会直接在屏幕顶部看到“房主设置的房间人数已达上限，暂时无法加入。”；若点击时仍有空位、请求到达房主时才满员，房主返回的 `room_full` 也会显示同一提示。提示会在最后一次触发 5 秒后自动消失；反复点击不会叠加，只会重新开始 5 秒计时。未订阅房主地图的提示仍保持常驻，不受该计时影响。

## 构建

项目面向 .NET Framework 3.5。构建或部署前必须读取项目根目录的 `LocalBroforcePath.props`：

- `BroforceManagedPath`：本机 Broforce `Managed` 目录。
- `UnityModManagerPath`：本机 UMM 核心目录。
- `TestDeployModPath`：本机测试机部署目录；值为空表示明确关闭额外测试部署。

该文件包含本机专用路径，只用于执行构建或部署，不得写入公开文件、提交信息、日志摘录或对外回复。首次使用时，复制 `LocalBroforcePath.props.example` 为 `LocalBroforcePath.props` 并填写本机路径，然后从项目根目录运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\BuildAndDeploy.ps1
```

标准脚本会生成 `Release\CustomMapMultiplayer.zip` 并部署到本机 UMM 目录，同时计算并嵌入 SHA-256 `buildHash`。部署时会同步覆盖 DLL 和 `Info.json`，使名称、版本和入口与当前构建一致；DLL 程序集版本从 `modinfo.json` 的版本自动生成。可选测试部署目录仅从未提交的 `LocalBroforcePath.props` 读取；不要把测试机地址、共享路径或用户名写入仓库。已配置的部署路径不可访问、目录创建失败或 DLL 复制失败时，构建视为失败，不要继续双端测试。不要用未经标准脚本验证的 IDE/手工构建代替；这类构建会记录 `UNBUILT`。

## 项目结构与文档

```text
src/                              Mod 源码
src/SettingsUiText.cs             UMM 设置界面中英文文案
CustomMapMultiplayer.csproj C# 工程文件
BuildAndDeploy.ps1                .NET 3.5 构建和部署脚本
Release/                  r2modman 安装包与 UMM 插件文件
README.md                         默认中文说明文档
README.en.md                      英文说明文档
modinfo.json                      UMM 清单模板
LocalBroforcePath.props.example   本机路径配置示例
docs/DEVELOPMENT.md               开发文档索引（专题文档见 docs/）
issues/                           历史问题、测试证据和验收记录
umm-settings-preview.html         UMM 设置界面预览
```

- [开发文档索引](docs/DEVELOPMENT.md)
- [问题记录索引](issues/README.md)
- [BroforceMods Wiki](https://github.com/alexneargarder/BroforceMods/wiki)
- [Viewing Broforce's Code](https://github.com/alexneargarder/BroforceMods/wiki/Viewing-Broforce's-Code)
