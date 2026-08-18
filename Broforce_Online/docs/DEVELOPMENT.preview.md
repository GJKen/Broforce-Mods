# Broforce 第三方地图联机 Mod：开发文档（精简预览）

这是项目当前有效的开发、构建、测试和 Git 协作约定。项目概览和安装方式请参阅 [根目录 README](../README.md)。

## 当前状态

当前版本为实验性的 `0.3.0`。

- 主机和朋友已经可以通过官方 Steam 大厅流程进入同一张 Workshop 地图。
- 已加入实验性的晚加入处理：主机已经进入配置中的 Workshop 场景后，客户端加入大厅时会尝试自动加载同一张地图。
- 最近一次双端测试已验证晚加入和双方独立角色控制；仍需更多地图和测试轮次验证。
- UMM 设置支持 Workshop ID、可选的战役名和场景名，以及诊断会话 ID 和端角色。
- Mod 保留官方英雄类型请求；朋友端收不到回复时，等待 18 秒后使用本地备用生成。
- 仍存在英雄状态不同步和 Broforce 原生崩溃风险，尚未达到稳定发布状态。

当前 Mod 不自动创建房间，不修改原始 `Assembly-CSharp.dll`，也不传输地图文件。关闭 UMM 设置中的线上注入时，Mod 只记录诊断信息，不改变游戏行为。

## 项目范围与前提

- 所有玩家必须安装相同版本的 Mod。
- 所有玩家必须订阅并下载相同的 Workshop 地图。
- 地图文件由 Steam Workshop 提供，不由 Mod 自动传输。
- Mod 复用官方 Steam 多人大厅和网络流程，不修改 Steam 网络层。
- 不保证所有第三方地图、地图脚本或其他 Mod 都兼容。

## 官方联机流程

游戏没有独立的“创建第三方地图大厅”入口。当前可用流程是：

1. 启动游戏，进入“开始”。
2. 进入“街机模式”，选择困难度。
3. 选择“线上主持游戏”。
4. 设置房间名、密码和玩家数量限制。
5. 进入 `p1-p4` 玩家确认界面后，让朋友加入大厅并按一次攻击键占用独立位置；主机确认双方位置不同后，再选择任务。
6. 主机按官方流程选择任务，Mod 在任务加载过程中替换为配置中的 Workshop 地图。
7. 如果需要，可以使用 `Esc` 打开 Steam 好友邀请界面。

主机和朋友必须在 UMM 设置中填写相同的 Workshop ID。战役名可以留空；标准 Workshop 战役通常使用 `Test Evan2` 作为场景名。

正常测试仍建议朋友先加入并在 `p1-p4` 界面按一次攻击键占用独立位置，再由主机选择任务。晚加入分支只在房间信息中的当前场景与配置的 `Custom level scene` 一致时尝试触发，并会先申请一个独立的本地玩家槽位。

## 当前实现

### Workshop 地图注入

主机和客户端的首关 Workshop 加载状态会在以下官方流程节点进行处理：

- `WorldMapController.EnterMission`：保留作为世界地图流程的注入点。
- `GameState.LoadLevel`：当前线上战役首关的主要注入点。
- `GameModeController.SwitchLevel`：后续切换关卡的备用注入点。
- `SteamController.LevelLoadCompleteEvent`：Workshop 下载完成后恢复官方战役状态，并继续原生加载流程。

每个线上房间只在首次选择任务时注入一次，避免重置 Workshop 战役自己的后续关卡。创建或加入新大厅时会清理上一次 Workshop 状态，并重置官方流程中的残留状态。

UMM 设置项如下：

- `Workshop ID`：Workshop 页面 URL 中 `id=` 后面的数字。
- `Workshop campaign name`：可选的地图内部战役名。无法确定时可以留空，网页标题不一定等于内部战役名。
- `Custom level scene`：新配置默认使用 `Test Evan2`；如果地图使用其它场景名，再按实际场景修改。它是通用场景名，不是地图名称。
- `Diagnostic session ID`：单轮测试可以留空；多轮测试建议每轮使用不同值，双端填写相同值。
- `Diagnostic role (host or client)`：主机填写 `host`，朋友填写 `client`；留空时插件会按创建或加入大厅自动推断。
- `Inject configured workshop map into online level switching`：线上地图注入开关，默认关闭。

首次双端测试可以使用以下配置：

```text
Workshop ID: <实际的 Workshop 数字 ID>
Workshop campaign name: 留空
Custom level scene: Test Evan2
Diagnostic session ID: test001
主机 Diagnostic role: host
朋友 Diagnostic role: client
```

两端的 Workshop ID 和 Diagnostic session ID 必须完全一致；保存双方 UMM 设置后，再开启线上地图注入。

填写或修改设置后，应点击 UMM 设置面板的保存按钮；正常切换 Mod 或退出游戏时插件也会尝试自动保存。

如果旧配置中的 `Custom level scene` 为空，插件加载时会自动补回默认值 `Test Evan2`；已经填写其它场景名的配置不会被覆盖。

升级旧版本配置时，插件会清理旧版本遗留的测试默认值；已经填写的其它自定义值不会被覆盖。

### 英雄回复策略

部分朋友客户端会丢失官方 `RequestHeroTypeFromMaster` 回复。当前策略是：

1. 保留游戏原本的英雄类型请求和回复流程。
2. Workshop 场景中的本地玩家等待 18 秒仍未收到回复时，使用游戏自己的 `GetHeroType` 和 `Player.SpawnHero` 进行本地备用生成。
3. 已有角色、远程玩家和正常收到回复的玩家不进入备用分支。
4. 备用生成后，只有确实处于等待新英雄回复状态时才接受迟到回复，其他迟到回复跳过，避免角色被重复替换。

之前的主动重试会制造迟到回复，已经删除。

## 当前问题与下一次测试

当前仍需关注：

- 朋友端英雄状态可能不同步。
- Broforce 可能发生原生崩溃，目前无法确认是否由 Steam 大厅异常字符串触发。
- 其他 Workshop 地图、地图脚本和 Mod 的兼容性尚未充分验证。

下一次双端测试应满足：

1. 双方完全退出游戏后重新启动。
2. 双方使用当前标准 DLL 和相同的 Workshop ID。
3. 主机创建大厅后停留在 `newJoin`，先让朋友加入并按攻击键占用独立位置；主机确认双方位置不同后，再选择任务。
4. 测试结束后同时收集双方对应会话的 `.log`、`.trace.log`、UMM `Core\\Log.txt` 和 `error.log`。

诊断日志清洗未配对 UTF-16 代理项，并对重复 Unity 错误进行限频；因此日志异常不能单独证明是原生崩溃原因，仍需结合双方日志和 `error.log` 判断。

## 构建与部署

项目目标为 `.NET Framework 3.5`，需要引用 Broforce 和 Unity Mod Manager 的程序集。

1. 根据 `LocalBroforcePath.props.example` 创建本机的 `LocalBroforcePath.props`。
2. 设置 `BroforceManagedPath` 为 Broforce 的 `Broforce_beta_Data/Managed` 目录。
3. 设置 `UnityModManagerPath` 为包含 `UnityModManager.dll` 和 `0Harmony.dll` 的 UMM 核心目录。
4. 运行 `powershell -ExecutionPolicy Bypass -File .\BuildAndDeploy.ps1`。
5. 脚本直接将标准文件名 `BroforceOnlineDiagnostics.dll` 生成到项目内的 `BroforceOnlineDiagnostics` 可复制安装包，再覆盖本机 UMM Mod 目录和内网测试端的同名 DLL，并只在目标缺少 `Info.json` 时从 `modinfo.json` 初始化它。

联机测试不得只直接运行 `csc.exe`，必须使用该脚本完成编译和双端同步。

Visual Studio 2022 不是必须的。构建关键是使用兼容 `.NET Framework 3.5` 的编译器，并正确引用 Broforce、Unity 和 UMM 程序集。当前已验证系统自带的 `C:\Windows\Microsoft.NET\Framework64\v3.5\csc.exe` 可以完成构建。

手工构建时必须显式引用 .NET 2.0 的 `mscorlib`、`System` 和 .NET 3.5 的 `System.Core`。不得直接使用 `v4.0.30319\csc.exe`，避免 DLL 混入 .NET 4.0 引用；Broforce 的 Unity/Mono 运行时只兼容 .NET 2.0/3.5。

每次构建只保留标准文件名，不额外生成或部署 `test6`、`test7`、日期后缀等临时 DLL。联机测试时应直接使用 UMM Mod 目录中的标准 DLL：

```text
<UMM_PROFILE_DIR>\Mods\BroforceOnlineDiagnostics\BroforceOnlineDiagnostics.dll
```

## 诊断日志约束

- 不直接追踪 `Update`、`RunHeroRespawnLogic` 等每帧调用的方法；确需观察时应追踪低频下游事件。
- 重复日志必须按方法、参数和状态组合限频；高频状态同步方法按方法级别合并，并在恢复记录时报告被抑制的次数。
- 部署新增追踪后，先检查本机日志增长速度；如果每秒持续写入多行，应先修复限频，再进行联机测试。
- 详细 Harmony 追踪写入独立的 `.trace.log`，关键联机事件写入普通 `.log`。
- 本项目不自动设置日志大小上限，也不自动删除旧日志；测试结束后按会话文件清理不需要的历史日志。

日志目录位于：

```text
<Application.persistentDataPath>/BroforceOnlineDiagnostics/
```

`SteamLayer.CreateMatch` 或 `SteamLayer.JoinLobby` 时会自动创建新的联机测试会话。双端可以在 UMM 设置中填写相同的 `Diagnostic session ID`，并分别填写 `host` 和 `client`。每行包含 UTC 时间、会话相对时间、会话 ID 和端角色。分析联机时序时，必须同时对照双方对应会话的 `.log`、`.trace.log`、UMM `Core\\Log.txt` 和 `error.log`，不能仅凭单端日志判断问题发生位置。

## Git 更新约定

- 本项目位于上级仓库 `D:\Study\C#\Broforce-Mods` 的 `Broforce_Online` 子目录；Git 根目录是上级目录。
- AI 只有在用户明确要求提交、更新或推送时，才能运行上级目录的 `update.bat` 或 `QuickUpdate.bat`。
- 更新前必须检查 `git status` 和 `git diff`，确认只包含本次任务相关文件，不能把无关文件一并提交。
- AI 必须判断本次修改是否属于重大更新。重大更新包括 Mod 功能、联机流程、同步逻辑、构建方式、兼容性、安装方式、崩溃修复或版本行为变化。
- 重大更新必须使用 `update.bat`，并在提交说明中包含：

```text
重大更新：<修改内容>；影响：<影响范围>；验证：<测试结果>
```

- 普通小修改可以使用 `QuickUpdate.bat`，但它的自动日期提交说明不能用于重大更新。
- 代码或联机行为发生重大变化时，应先同步更新 README 或本文档中的当前状态和测试结论，再提交。
- `update.bat` 和 `QuickUpdate.bat` 会对上级仓库执行提交操作；运行前必须确认当前目录和待提交文件范围正确。

## 逆向与开发参考

使用 dnSpy、ILSpy 或同类工具阅读：

```text
<BROFORCE_DIR>\Broforce_beta_Data\Managed\Assembly-CSharp.dll
```

重点关注线上房间创建、玩家加入、关卡加载和 Workshop 地图相关类与方法。先记录调用关系和关键参数，不直接修改原始 DLL；确认后的最小修改应转化为 Harmony 运行时补丁。

关键参考页面：

- [Viewing Broforce's Code](https://github.com/alexneargarder/BroforceMods/wiki/Viewing-Broforce's-Code)
- [BroforceMods Wiki](https://github.com/alexneargarder/BroforceMods/wiki)
- [BroMaker Abilities Wiki](https://github.com/alexneargarder/Bro-Maker-Abilities-Wiki/wiki)

## r2modman 与 UMM 命名约定

| 位置 | 正确值 |
| --- | --- |
| `manifest.json` 的 `name` | `BroforceOnlineDiagnostics` |
| `mm_v2_manifest.json` 的 `name` | `GJKen-BroforceOnlineDiagnostics` |
| `mm_v2_manifest.json` 的 `displayName` | `BroforceOnlineDiagnostics` |
| UMM `Info.json` 的 `Id` | `BroforceOnlineDiagnostics` |
| UMM Mod 目录 | `BroforceOnlineDiagnostics` |
| 程序集文件 | `BroforceOnlineDiagnostics.dll` |

包名和 Mod 目录不带 `.dll`，只有真实程序集文件保留 `.dll` 扩展名。项目源文件名为 `modinfo.json`，部署到 UMM Mod 目录时必须命名为 `Info.json`。
