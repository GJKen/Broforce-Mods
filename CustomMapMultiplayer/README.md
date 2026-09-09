# Custom Map Multiplayer

> [English](README.en.md)

这是一个面向 Steam 版 Broforce 的 Unity Mod Manager + Harmony Mod。默认复用官方 Steam Lobby/Steam P2P；可选的 `FRP 直连` 使用独立房间、PID 和游戏 RPC，但 Workshop 内容仍由 Steam 下载。

使用 Workshop 地图注入或 FRP 直连 时，所有参与联机的玩家必须安装相同构建的 Mod，并订阅、下载相同的 Workshop 地图；排查版本时以各端日志中的 `BUILD_INFO buildHash` 为准。加入方会读取房主发布的 Workshop ID、场景名和战役名，不再需要手工填写与房主相同的地图配置。

## 当前状态

当前版本为实验性 `0.5.0`，尚未达到稳定发布状态。

| 项目 | 状态 |
| --- | --- |
| 当前分发构建 | `buildHash=cc364ae4180a8e861aaf9cdf9741069a7ae207b104a502046f3d95efe77489a9` |
| DLL SHA-256 | `6950B137789CE3C1F78E1A65DE928BA11C6FDD60FAB140FEBC5F5B46F9958F9A` |
| DLL 程序集版本 | `0.5.0.0` |
| Steam 联机 | 默认路径；已验证官方大厅进入同一张 Workshop 地图及彩色延迟名单 |
| FRP 直连 | 默认关闭；三机基础联机已验证，代码支持房主加最多三台远端 |

### 已验证

- 双端进入、晚加入、当前地图的退出/重入，以及双方独立角色和控制，详见 [Workshop 与游戏状态](docs/WORKSHOP.md)。
- Steam 与 FRP 直连 的 Esc 彩色延迟名单和动态房主名；FRP 的三机基础联机、静态 `1` 人房满员提示及 Host/Client 配置自动应用，详见 [网络与房间](docs/NETWORKING.md) 和 [FRP Direct 实施与验收记录](issues/archive/ISSUES-2026-08-24-FRP内网穿透联机方案.md)。
- Workshop 地图身份由房主发布，加入方自动采用；缺少订阅时显示提示并停止加载；注入可热关闭并恢复官方地图，详见 [Workshop 与游戏状态](docs/WORKSHOP.md)、[Workshop 缺图加载记录](issues/archive/ISSUES-2026-09-04-Workshop缺图仍进入加载动画.md) 和 [关闭 Workshop 注入记录](issues/archive/ISSUES-2026-08-28-关闭Workshop注入后恢复官方地图.md)。
- Workshop 加载会优先复用 Steam 已安装目录或旧版 UGC 本地缓存；缓存不可读时才回退到 Steam 下载，并抑制同一张地图加载期间的重复请求，详见 [Workshop 与游戏状态](docs/WORKSHOP.md)。
- Workshop 的入场横幅、Esc 返回大厅和主菜单动画；标准弹药箱的确定性、远端扫描抑制和重复拾取防护已在 FRP 双端验证，官方 Steam 大厅和更多地图仍需复测，详见 [网络与房间](docs/NETWORKING.md) 和 [Workshop 与游戏状态](docs/WORKSHOP.md)。
- 高密集战斗长测中，Host 掉帧已明显减轻；当前结论为观察到改善，仍需统一图形设置、交换 Host 并完成 p50/p95/p99 对照后再正式验收，详见 [Host 性能问题记录](issues/ISSUES-2026-08-30-联机房主低帧率与Host专属扫描性能问题.md)。
- Esc 菜单中的“立即进入 AFK”按钮；房主和加入方分别操作时只影响各自本地角色。主动 AFK 不会自动重新加入，用户通过正常流程回来后会恢复原槽位的生命、英雄类型和角色；普通网络掉线仍按原有流程自动恢复，详见[主动 AFK 按钮问题记录](issues/archive/ISSUES-2026-09-01-新增ESC菜单主动AFK按钮.md)。
- 联机聊天已接入中文输入法、中文符号、数字和原生编辑操作，并提供 500 个 UTF-16 字符上限、长消息 viewport、视觉行 Up/Down、输入框右下角字数显示和输入框激活时的当前键盘角色操作拦截；上述功能均已通过窄范围真实键盘测试，Esc 后再次呼出和 Enter 发送问题已修复，完整输入矩阵与聊天历史专项仍待补充，详见[聊天输入专题](docs/CHAT.md)。
- Swap Bros 2.1.5 的 `Always spawn as chosen bro` 兼容已通过离线和 Steam 联机测试；运行时选角查询使用 `Swap_Bros_Mod.Main.GetSelectedBroHeroType(Int32)`，首次生成和重生最终均使用锁定角色，详见 [Swap Bros 问题记录](<issues/ISSUES-2026-09-07-Swap Bros Always spawn as chosen bro与联机角色生成冲突.md>)。
- 房主和加入方分别进入酸液时，实际接触者均能正常死亡，出生区玩家不会被连带杀死。投掷酸液命中死亡也已通过用户实机验收，详见 [酸液池 issue](issues/archive/ISSUES-2026-08-30-Workshop联机酸液池导致双方一起死亡.md) 和 [投掷酸液 issue](issues/ISSUES-2026-09-07-投掷酸液命中角色未正常死亡.md)。

### 待复查

#### 死亡与实体终态

- **普通 Mook 死亡终态：** 已实现权威死亡事件和尸体终态提交，但仍有低概率状态不一致；晚加入死亡快照尚未实现。详见 [全联机死亡实体与尸体终态 issue](issues/ISSUES-2026-08-28-全联机死亡实体与尸体终态同步.md)。
- **McBrover 火鸡主动引爆：** 残留实体仍可复现，概率已显著降低；根因和远端生命周期链路尚未闭环。详见 [McBrover issue](issues/ISSUES-2026-08-28-McBrover火鸡主动引爆后残留实体.md)。

#### Workshop 关卡切换

- **`3715087178` 关卡结束防重入：** 防重入补丁已完成构建和部署，尚待实机通关复测。重点确认重复 `LevelEndSuccess` 不再逐帧推进关卡号，并确认 Host 与 Client 最终进入一致的下一关或结算场景。详见 [关卡结束重入 issue](issues/ISSUES-2026-08-26-3715087178联机通关黑屏与关卡结束重入.md)。
- **`3781818421` 第 4 关黑屏：** 第 3 关通关进入第 4 关时仍可能黑屏，和上面的 `3715087178` 防重入问题分开排查。详见 [重复重入与第 4 关黑屏 issue](issues/ISSUES-2026-08-22-重复退出重入加入方失败与3781818421进入第4关黑屏.md)。

#### 联机覆盖范围

- **官方 Steam 大厅和更多地图的道具同步：** FRP 双端已验证的标准弹药箱行为，尚未在官方 Steam 大厅和更多 Workshop 地图完成复测。
- **高延迟与长期重入：** 仍缺少统一延迟条件和多轮退出/重入的稳定性证据。
- **FRP Direct 扩展场景：** 四机、总人数 `2` 至 `4` 的容量边界、动态降额后的重入尚未验证；FRP Direct 不支持 Host migration，因此不把它列为待验收能力。

#### 运行时异常与性能

- **加入方箱子异常（独立问题）：** 历史 Steam 会话中，加入方日志曾在 `DoodadCrate.SetupBlockAtStart` 和 `DestroyBlockInternal` 记录 `NullReferenceException`，同时出现箱子坍塌特效持续重复；2026-08-31 的最新双端短测未复现。由于该轮没有直接触发箱体保护分支，仍需通过一次针对性的箱子打开或撞塌测试完成验收。现有证据只能确认加入方箱体处理异常，不能证明它导致 Host 战斗掉帧。详见 [加入方箱子问题记录](issues/ISSUES-2026-08-30-加入方箱子坍塌特效持续重复.md)。
- **Host 战斗低帧率（独立问题）：** 高密度战斗长测中只观察到掉帧改善，统一图形设置、交换 Host 角色及 p50/p95/p99 对照尚未完成，因此仍待正式验收。箱子异常没有 Host 同步调用栈或直接因果证据，不并入本项判断。详见 [Host 性能问题记录](issues/ISSUES-2026-08-30-联机房主低帧率与Host专属扫描性能问题.md)。

当前范围不包括活动 AI 持续同步、敌方弹体、钱币、金色奖励、普通 `Grenade` 地形伤害或历史动态世界实验。详细实现和证据见 [开发文档索引](docs/DEVELOPMENT.md) 与 [问题记录索引](issues/README.md)。

## 安装与首次运行

1. 所有玩家安装 `r2modman`，为 Broforce 创建或选择默认的 profile，并在其中安装 UMM。启动一次游戏确认 UMM 加载成功。
2. 将 `Release\CustomMapMultiplayer.zip` 导入 r2modman > Settings > Profile > Import local mod。ZIP 内已经包含 `UMM\Mods\CustomMapMultiplayer` 下的 DLL 和 `Info.json`。
3. 配置 Workshop 地图和注入选项：
   - 双方提前订阅并下载相同的 Workshop 地图，在左侧独立的 `Workshop 地图配置` 页面中开启 Workshop 地图注入。
   - 房主可以手工填写数字 Workshop ID，也可以点击“选择已订阅地图”进入地图选择页；战役名可留空，场景名默认 `Test Evan2`，使用其它场景时再修改。
   - 地图选择页支持标题/ID 搜索、全部、最近联机地图、已标星筛选、刷新和收藏。切换到“已标星”且存在星标地图时，标签旁会显示 `×`；点击可一次性清除全部星标，但不会取消 Steam 订阅，其他标签不会显示此按钮。选择地图只修改 Workshop ID，不会修改战役名或场景名；选择后仍停留在选择页，可用右上角 `X` 返回配置。
   - 加入方无需填写 Workshop ID；即使本机保存的 ID 不正确，加入房间后也会自动采用房主发布的地图 ID。
   - 如果加入方没有订阅房主地图，屏幕顶部会提示缺少订阅地图，请根据提示完成订阅。
   - Mod 不自动订阅、搜索社区或主动下载地图；地图下载由 Broforce/Steam 原生流程处理。关闭 Workshop 地图注入和 FRP 直连后，退出当前房间并重新创建官方街机线上大厅，即可恢复官方地图流程。
   - 配置图示：

     ![UMM 设置界面](https://github.com/user-attachments/assets/a39d9e2c-c5e0-48fd-a3a4-67731b9a61c8)

4. 任意一端使用街机模式创建线上大厅，加入方找到房间后直接加入即可。

### UMM 设置面板

实际 UMM 设置页采用左侧竖向功能列表、右侧显示当前功能内容的布局：

- `Workshop 地图配置`：Workshop 地图注入、手工 Workshop ID、已订阅地图选择、搜索、筛选、收藏和最近联机地图；高级区域保留战役名和场景设置。
- `多人游戏选项`：自动 AFK 旁观模式，以及玩家重叠时优先执行近战的开关；主动 AFK 按钮位于游戏内 Esc 菜单。
- `FRP 直连`：直连开关、Host/Client 角色、端口、人数上限和连接参数。
- `语言`：直接点击“跟随系统”“English”或“中文”按钮切换界面语言。
- `Diagnostic Logs`：诊断会话标识、日志预设和诊断分类。

`umm-settings-preview.html` 只是静态预览；实际 UMM 界面以 `src/Plugin.cs` 和 `src/SettingsUiText.cs` 为准。

### 常用设置

- `Workshop 地图配置` 中的 Workshop 地图注入开关关闭时会立即保存并清理注入状态，但不会强制中断或切走当前场景。退出当前房间并从菜单重新创建官方房间后，后续选图使用游戏原生战役流程；不需要删除已保存的 Workshop ID。
- `多人游戏选项` 中的 AFK 开关由每台客户端独立控制。未勾选时显示“已启用自动 AFK 旁观模式”；勾选后显示“已禁用自动 AFK 旁观模式”。要保护双方角色，双方必须分别勾选；它不拦截手动退出、断线或正常死亡。
- `多人游戏选项` 中的“玩家重叠时优先执行近战”默认启用。启用后，玩家重叠时按近战会执行当前角色的近战动作，不再被原生击掌替代；关闭后恢复原生自动击掌行为。
- Esc 菜单中的“立即进入 AFK”按钮会让当前客户端实际拥有的本地玩家立即进入原生 AFK 旁观流程，与自动 AFK 开关相互独立。按钮会按本地所有权和当前输入控制器确定目标；多本地槽位无法唯一确定时不会执行，避免误操作另一角色。主动 AFK 不会触发自动 `RequestJoinGame`，需要用户通过正常重新加入流程显式回来；回来时会恢复原槽位的生命、英雄类型和角色。普通网络掉线仍保持自动重入。
- `Diagnostic session ID` 用于关联双方同一轮日志；两端填写相同值。`Diagnostic label` 只影响日志文件名，不参与联机行为。
- 诊断日志预设（基础、加入/重新加入、AFK/失败、Workshop、完整）和九个诊断分类只影响日志输出，不改变联机行为。双方排查同一问题时应尽量选择相同类别。

### 测试与日志

每轮测试结束后，收集所有参与端同一会话的诊断日志，并尽量同时保存 UMM 和游戏日志。通过 UMM 中的“打开诊断日志目录”获取 Mod 日志，不在公开文档记录用户目录、共享路径或用户名。只有单端日志时，结论必须明确证据缺口，不能单独断定网络根因。

### 酸液问题排查

排查酸液池导致的异常死亡时，重点对齐双方同一会话的 `PLAYER_ACID` 事件：它记录 `CoverInAcid`、`CoverInAcidRPC` 和 `PlayerHasDiedRPC` 前后对应的玩家槽位、RPC 请求槽位、角色 NID、`IsMine`、坐标、`acidMeltTimer` 和 `hasBeenCoverInAcid`，并通过低频 `authority-gate` 标明 `host-check`、`client-request`、`authority-wait` 或 `native-fallback` 决策。

## FRP 直连 联机

FRP 直连 总开关默认关闭。

`Host`/`Client` 角色仍由用户明确选择。切换角色会立即保存并自动切换连接：Host 只使用本地 UDP 监听端口，完全忽略已保存的 Client 公网地址；Client 只使用 FRP 公网 `host:port`，完全忽略 Host 的本地监听端口。两套配置分别保留，切回原角色时无需重新填写。设置页不再提供手动 Apply 按钮；总开关和角色立即生效，端口、地址和密码在停止输入后自动保存并重连。心跳、超时检测和普通断线重试均由传输层自动处理。

### 房主

```text
FRP Direct role: Host
Local UDP listen port: 27045
FRP room player limit: 点击 1、2、3、4 中的一个按钮，立即生效
FRP room password: 所有参与方约定的临时密码，或留空
```

人数按钮设置整个房间的角色上限：`1` 只允许房主，`2` 允许房主加一名加入方，`3` 允许房主加两名加入方，`4` 允许房主加三名加入方。该设置不会突破 Broforce 原生四人上限。房主可以在已经进入地图后打开 UMM 并直接切换人数，无需重启 FRP；新上限立即用于后续加入，已经在房间里的玩家不会被踢出。例如当前有三人时改为 `1`，三人仍可继续游戏，但退出的玩家不能重新加入，直到上限再次调高。

正常启动后应显示 `Listening on UDP 27045`；`frpc` 将公网 UDP 端口转发到 `127.0.0.1:27045`。修改当前角色使用的连接参数会自动重启连接，因此应在开始或结束联机时调整。

### 加入方

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

标准脚本会读取并保留 `Release\UMM\Mods\CustomMapMultiplayer\Info.json`，生成 `Release\CustomMapMultiplayer.zip` 并部署到本机 UMM 目录，同时计算并嵌入 SHA-256 `buildHash`。部署时会同步覆盖 DLL 和 `Info.json`，使名称、版本和入口与当前构建一致；DLL 程序集版本从该 `Info.json` 的版本自动生成。可选测试部署目录仅从未提交的 `LocalBroforcePath.props` 读取；不要把测试机地址、共享路径或用户名写入仓库。已配置的部署路径不可访问、目录创建失败或 DLL 复制失败时，构建视为失败，不要继续双端测试。不要用未经标准脚本验证的 IDE/手工构建代替；这类构建会记录 `UNBUILT`。

## 项目结构与文档

| 路径 | 说明 |
| --- | --- |
| `src/` | Mod 源码目录；源码职责和模块关系见[架构与代码职责](docs/ARCHITECTURE.md) |
| `CustomMapMultiplayer.csproj` | C# 工程文件 |
| `BuildAndDeploy.ps1` | .NET 3.5 构建和部署脚本 |
| `Release/` | r2modman 安装包与 UMM 插件文件 |
| `README.md` | 默认中文说明文档 |
| `README.en.md` | 英文说明文档 |
| `Release/UMM/Mods/CustomMapMultiplayer/Info.json` | UMM 清单和构建元数据源文件 |
| `LocalBroforcePath.props.example` | 本机路径配置示例 |
| `docs/DEVELOPMENT.md` | [开发文档索引](docs/DEVELOPMENT.md) |
| `issues/` | [历史问题、测试证据和验收记录](issues/README.md) |
| `docs/CHAT.md` | 联机聊天输入、viewport 和历史消息显示 |
| `umm-settings-preview.html` | UMM 设置界面预览 |

### 其它文档

- [BroforceMods Wiki](https://github.com/alexneargarder/BroforceMods/wiki)
- [Viewing Broforce's Code](https://github.com/alexneargarder/BroforceMods/wiki/Viewing-Broforce's-Code)
