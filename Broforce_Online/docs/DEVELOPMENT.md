# Broforce 第三方地图联机 Mod：开发文档

项目概览、安装方式和日常使用说明请先看 [根目录 README](../README.md)。本文只保留当前有效的实现、测试、日志、构建和排查约定。

## 项目范围

这是一个面向 Steam 版 Broforce 的 Unity Mod Manager + Harmony Mod。它复用官方 Steam 多人大厅，让已经安装相同 Mod、并订阅相同 Workshop 地图的玩家尝试共同进入第三方地图。

当前版本为实验性的 `0.3.0`，尚未达到稳定发布状态。

所有玩家必须：

- 安装相同版本的 Mod。
- 订阅并下载相同的 Workshop 地图。
- 使用官方线上主持流程创建或加入大厅。

## 当前状态

- 已验证主机和朋友可以通过官方大厅流程进入同一张 Workshop 地图。
- `test009` 双端测试已验证：过场加载期间加入可以进入地图，P2 角色可以正常创建，双方角色控制保持独立。
- UMM 设置支持 Workshop ID、可选战役名、场景名、诊断会话 ID 和端角色。
- 线上地图注入默认关闭；关闭时只记录诊断信息，不改变游戏行为。
- 仍存在朋友端英雄状态不同步、原生崩溃和地图兼容性风险。

## 双端测试

### 官方流程

游戏没有单独的“创建第三方地图大厅”入口。当前测试流程是：

1. 启动游戏，进入“开始” -> “街机模式”。
2. 选择困难度和“线上主持游戏”，设置房间名、密码及玩家数量限制。
3. 创建方进入 `p1-p4` 等待玩家进入。加入方必须按一次攻击键占用自己的位置；创建方确认双方处于不同的玩家位置后，再按攻击键选择任务并进入地图。不要进入地图后才选择角色。
4. 如有需要，使用 `Esc` 打开 Steam 好友邀请界面。

### UMM 设置

| 设置 | 填写方式 |
| --- | --- |
| `Workshop ID` | 填写 Workshop 页面 URL 中 `id=` 后面的数字；双方必须一致。 |
| `Workshop campaign name` | 可选的地图内部战役名；不确定时留空。 |
| `Custom level scene` | 默认 `Test Evan2`。它是游戏通用场景名，不是地图名称；地图使用其它场景时再修改。 |
| `Diagnostic session ID` | 单轮测试可以留空；多轮测试建议每轮使用不同值，例如 `test001`、`test002`。双端必须一致。 |
| `Diagnostic label (optional)` | 只作为日志文件名和关联信息的标签，可留空；不参与联机行为。 |
| `Inject configured workshop map into online level switching` | 默认关闭。确认配置和地图一致后再开启。 |

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
5. 测试结束后按会话 ID 收集双方日志，同时收集 UMM `Core\Log.txt` 和游戏 `error.log`。

### 晚加入支持

当前版本在 `ConnectionLayer.OnJoinedLobby` 后检查创建方传来的 `RoomInfo.CurrentSceneName` 和 Steam Lobby 阶段。如果创建方处于 Workshop 的 `loading` 或 `ready` 阶段，加入方会主动刷新 Lobby 数据，通过原生 `HeroController.AddLocalPlayer(-1, 1)` 申请独立的本地玩家槽位，并使用本地 `Workshop ID` 并行加载地图。

这是实验性分支，依赖创建方和加入方使用相同版本 Mod。创建方处于 `newJoin` 或任务选择界面时，加入方不会启动晚加入地图加载；进入 Workshop 过场后即可触发，最多等待约 120 秒。host 端只在晚加入 Workshop 会话中放宽 `HeroController.RequestJoinGame` 的关卡完成和控制器注册保护，使加入方的 P2 请求能够创建角色。晚加入后仍可能受到玩家状态、英雄同步和地图脚本影响，因此稳定测试仍应优先使用“先加入大厅、创建方后进入地图”的顺序。

## 当前实现

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

加入方晚加入时，如果房间信息或 Lobby 阶段显示创建方正在进入配置中的 Workshop 场景，`ConnectionLayer.OnJoinedLobby` 会刷新 Lobby 数据，申请一个本地玩家槽位并执行一次 Workshop 加载；地图下载完成后复用同一个完成回调继续原生流程。host 端的 `RequestJoinGame` 补丁只对晚加入 Workshop 会话放宽两个原生保护条件，普通大厅仍使用原生判断。

### 英雄回复策略

部分朋友客户端可能收不到官方 `RequestHeroTypeFromMaster` 回复。当前策略是：

- 保留游戏原本的请求和回复流程。
- Workshop 场景中的本地玩家等待 18 秒仍无回复时，使用游戏自己的 `GetHeroType` 和 `Player.SpawnHero` 做一次本地备用生成。
- 已有角色、远程玩家和正常收到回复的玩家不进入备用分支。
- 备用生成后，只有仍处于等待新英雄回复状态时才接受迟到回复，避免旧回复重复替换角色。

主动重试已经删除，因为它会制造迟到回复；备用生成也不是重新发送网络回复，不能保证所有同步问题都被解决。

### 代码职责

- `src/Plugin.cs`：UMM 加载、设置界面、保存和启用/禁用入口。
- `src/DiagnosticSettings.cs`：Workshop、会话和日志标签配置；新配置默认场景为 `Test Evan2`，其它测试字段为空。
- `src/DiagnosticLog.cs`：会话日志和 Harmony 追踪日志的创建、写入、刷新和清理。
- `src/DiagnosticsBehaviour.cs`：场景、Unity 错误和英雄生成状态观察。
- `src/HarmonyDiagnostics.cs`：线上房间、Steam Lobby、关卡切换、Workshop 加载和英雄请求追踪/注入。
- `src/ReflectionProbe.cs`：只读扫描 `Assembly-CSharp` 中可能相关的类型。

方法级追踪不记录房间密码、Steam ID、主机名或 Workshop 作者身份。

## 诊断日志

### 文件和会话

日志目录为：

```text
<Application.persistentDataPath>/BroforceOnlineDiagnostics/
```

插件加载时会创建启动日志；检测到 `SteamLayer.CreateMatch` 或 `SteamLayer.JoinLobby` 时会创建新的联机会话。每个会话包含普通事件日志和独立的 Harmony 详细追踪日志，例如：

```text
diagnostics-host-<session>-<utc-time>.log
diagnostics-host-<session>-<utc-time>.trace.log
```

普通 `.log` 记录关键联机事件，`.trace.log` 记录详细 Harmony 调用。每行包含 UTC 时间、会话相对时间、会话 ID、日志标签和日志级别；会话开始事件还会记录实际网络角色。普通日志约每 750ms 刷新一次，警告、错误和会话结束时立即刷新。

`SteamLayer.JoinLobby` 内部可能先调用一次 `LeaveMatch` 清理旧大厅；该调用不再被诊断系统当成正式离开，因此不会提前关闭客户端的加入会话日志。

### 日志约束

- 不直接追踪 `Update`、`RunHeroRespawnLogic` 等每帧方法；需要观察时改为追踪低频下游事件。
- 重复日志按方法、参数和状态组合限频；高频状态同步方法按方法级别合并，并在恢复记录时报告被抑制次数。
- 新增追踪后先检查本机日志增长速度；如果每秒持续写入多行，先修复限频再进行双端测试。
- 日志写入前会清洗未配对 UTF-16 代理项，避免异常字符串再次破坏 Unity 日志路径。
- 本项目不自动设置日志大小上限，也不自动删除旧日志；测试结束后按会话文件清理不需要的历史日志。

分析双端时序时，必须同时对照双方相同会话 ID 的 `.log`、`.trace.log`、UMM `Core\Log.txt` 和 `error.log`，不能仅凭单端日志判断问题位置。

## 当前已知问题

- 朋友端英雄类型回复可能丢失，当前本地备用生成只能缓解，不能替代网络同步。
- Broforce 可能发生原生崩溃；日志中的异常和崩溃时间关系不能单独证明因果，必须结合 `error.log`、双方日志和 UMM 日志分析。
- 晚加入依赖双方使用相同版本 Mod；过场期间可以并行加载，但地图脚本、网络状态或原生错误仍可能导致加入失败。
- 不同 Workshop 地图、地图脚本和其它 Mod 的兼容性尚未充分验证。
- 线上地图注入仍属于测试功能，默认关闭，不能按稳定发布版本使用。

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
<本机 UMM_PROFILE_DIR>\Mods\BroforceOnlineDiagnostics\BroforceOnlineDiagnostics.dll
\\192.168.1.181\Epan\Games\Broforce Mods\Broforce\profiles\Broforce\UMM\Mods\BroforceOnlineDiagnostics\BroforceOnlineDiagnostics.dll
```

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

复制到 UMM 时，目录名必须是 `BroforceOnlineDiagnostics`，程序集文件名必须是 `BroforceOnlineDiagnostics.dll`，不能给目录名添加 `.dll`。项目源清单名为 `modinfo.json`，部署到 UMM 目录时使用 `Info.json`。

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

重点关注线上房间创建、Steam Lobby、玩家加入、关卡加载和 Workshop 地图相关类与方法。先记录调用关系和关键参数，不直接修改原始 DLL；确认后的最小修改应转化为 Harmony 运行时补丁。

## 修改协作约定

- 提交或同步前检查上级仓库的 `git status` 和 `git diff`，不要把 `LocalBroforcePath.props`、日志、缓存或无关文件加入提交。
- `LocalBroforcePath.props` 包含机器专用路径，不应提交到公共仓库。
- 构建方式、联机行为、安装方式、日志格式或兼容性发生变化时，先同步更新 README 和本文档。
- 不要未经明确要求运行上级仓库的自动提交、推送或更新脚本。
