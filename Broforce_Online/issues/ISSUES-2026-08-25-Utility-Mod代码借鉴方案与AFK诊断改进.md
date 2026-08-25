# ISSUES-2026-08-25 Utility Mod 代码借鉴方案与 AFK 诊断改进

## 记录范围

本文记录对 `D:\Study\C#\alexneargarder-BroforceMods\Utility Mod\Utility Mod` 源码的借鉴结论、本轮实际写入 Broforce Online Diagnostics 的改动、AFK 日志补强、构建部署结果和后续待验收项。

这里的“借鉴”是复用原生事件、状态恢复顺序和可选 API 探测思路，不是复制 Utility Mod 的调试菜单，也没有给当前项目增加 RocketLib 依赖。除原有 Workshop 联机功能外，本轮新增内容以低频、只读诊断为主。

## Utility Mod 中可借鉴的原生模式

### Workshop 下载完成后的状态恢复

Utility Mod 的 `GoToOnlineCampaignLevel` 在触发 `SteamController.LoadLevel` 前订阅 `SteamController.LevelLoadCompleteEvent`；`OnOnlineCampaignLoaded` 收到 Campaign 后解除订阅，并依次恢复 `LevelSelectionController.currentCampaign`、关卡编号、`GameState.loadMode`、`gameMode`、目标场景和会话编号，最后调用原生切场景入口。

当前项目采用相同的“等待官方完成事件后再恢复状态”原则，但保留自己的联机流程：

- 统一订阅和解除 `LevelLoadCompleteEvent`，避免重复回调。
- 下载成功后写入当前 Campaign，并恢复发布地图和在线 Campaign 标志。
- 保留房主同步或晚加入取得的权威 `levelNumber`，不在下载完成时无条件归零。
- 最后通过现有 `GameState.LoadLevel(string)` 路径继续加载，并保留重复场景加载抑制。
- 本轮新增 `WORKSHOP_GAME_MODE_COMPARE`，对照 Campaign、`GameState` 和 `RoomInfo` 的 `gameMode`；只报告差异，不按 Utility Mod 的单机跳关逻辑强制覆盖联机状态。

结论：状态恢复思路已经用于现有 Workshop 回调；本轮新增的是模式一致性观测，不是新的强制写回修复。真实双端日志仍待验收。

### 通关和失败状态的原生观测点

Utility Mod 的切关、重启和跳关功能说明，`GameModeController`、`GameState` 和玩家生命变化是比画面或场景轮询更直接的原生状态入口。

本轮新增 `HarmonyDiagnostics.LevelOutcome.cs`：

- 对 `GameModeController.LevelFinish` 和 `Player.RemoveLife` 安装前后置补丁。
- 写入 `LEVEL_OUTCOME`，包含参数、场景、玩家槽位、生命、存活人数、本地人数、总生命、直升机人数、切关标志、目标场景、`GameState` 和 `RoomInfo`。
- 只在已经建立的在线会话中记录，同时写普通日志和 `.trace.log`。
- 不调用重启、跳关或强制成功/失败方法。

结论：已实现并通过编译和方法解析检查，尚待实际触发一次扣命、失败和通关来验收日志内容。

### 将调试操作建模为可记录、重放的动作

Utility Mod 的 `MenuAction` 和集中分发方式适合把“跳关、重启、生成对象”等调试操作表达成结构化动作。这样可以记录动作类型和参数，再在受控环境中重放。

当前项目没有实现动作模型、动作历史或自动重放。用户已明确自动复现暂时不需要，本轮只记录为后续候选。现有 Unity Inspector 临时操作也没有被伪装成可重放脚本。

结论：未实现，继续保留在后续清单。

### 确定性对象注册的原生调用顺序

Utility Mod 创建动态单位、方块和 Doodad 时，会在设置父对象、写入地图结构和调用 `OnSpawned` 后使用 `Registry.RegisterDeterminsiticGameObject`；方块还会继续执行 `SetupBlock`、`RegisterBlockOnNetwork` 和 `FirstFrame`。这个顺序可作为以后新增联机动态对象时的原生参考。

当前项目没有增加动态对象生成，也没有调用 `RegisterDeterminsiticGameObject`。现有 Workshop 道具确定性修复处理的是两端已有对象的类型选择和重复拾取，不等于动态对象注册。

结论：未实现；只有将来确实需要生成联机对象时才应按具体对象类型验证调用顺序，不能把 Utility Mod 的顺序机械套用到所有对象。

### 可选 Mod 的弱依赖集成

Utility Mod 的 `SwapBrosIntegration` 使用 `UnityModManager.FindMod`、程序集类型查找和公开静态方法反射，未安装或 API 缺失时安全降级。

本轮新增 `OptionalBroModDiagnostics.cs`：

- 查找 `Swap Bros Mod` 和 `Swap_Bros_Mod.API`。
- 记录安装/启用状态、Mod 版本、程序集版本、模块 ID 和 API 能力。
- 对有序角色表和 P1-P4 本地选择计算 SHA-256 指纹。
- 在诊断启动和每个网络会话开始时各采集一次。
- 不引用 RocketLib，不调用换人 API，不更改角色选择，也不因指纹不同自动拒绝会话。

结论：只读弱依赖诊断已实现，尚待安装和未安装 Swap Bros 的真实启动日志验收。

### 设置写入前先序列化验证

Utility Mod 覆盖 `Settings.Save`，先用 `XmlSerializer` 写入内存字符串；只有序列化成功才调用 UMM 保存，从而避免异常对象导致已有配置被破坏。

当前项目的 `Plugin.SaveSettings` 仍直接调用 `UnityModManager.ModSettings.Save`，并在异常时记录错误。当前设置字段均是简单标量和字符串，没有发现本轮诊断改动引入复杂可序列化对象，但“写入前预序列化”本身尚未实现。

结论：未实现，作为独立的低风险健壮性改进保留；实施时需要验证 UMM 实际序列化格式，不能假定 Utility Mod 使用的 RocketLib `XmlModSettings` 与当前基类完全一致。

## 本轮额外改进：AFK 原生流程日志

### 旧日志的证据边界

上一轮官方 Steam Workshop 会话使用 `buildHash=fcb50bff38661e2d5ecca9e79ea4a4a190d56702b3f2a719f846c30a400e112a`。房主在第三关生命归零后仍记录 `alive=1`，约 5 分钟没有判定失败；用户确认另一端已经进入 AFK。旧日志只在加入方退出房间时看到 `Dropout`，随后房主才变为 `alive=0` 并触发 `LevelFinish(Fail)`。

这只能证明远端槽位最终消失后失败判定恢复，不能区分：

1. 加入方的 AFK 没有同步给房主。
2. 房主已经收到 AFK/退出状态，但仍把对应槽位计入存活人数。

公开房间中额外出现但没有成功加载 Mod 或 Workshop 地图的成员属于正常路人尝试加入，不纳入上述结论。

### 新增观测点

新增 `HarmonyDiagnostics.Afk.cs`，并在 `Player.Update` 与 `HeroController.DropoutRPC` 上安装补丁：

- 约 5 秒无输入：`AFK_TIMER event=counting`。
- 约 30 秒无输入：`AFK_TIMER event=warning`。
- 输入恢复或原生条件不再成立：`AFK_TIMER event=reset`。
- 防 AFK 开关启用：每个本地槽位低频记录一次 `AFK_STATE event=prevention-active`。
- 确实进入原生 35 秒分支：`AFK_STATE event=timeout-triggered`。
- 槽位实际移除：`PLAYER_DROPOUT event=applied`，并记录移除前后快照。

只有能和本机 35 秒分支关联的退出才写 `reason=native-afk-timeout`；主动退出、断线或无法证明原因的路径保守写 `reason=unknown`。35 秒触发标记保留 2 秒网络回调窗口，避免 `DropoutRPC` 稍晚到达时误判。进房清理期和本来就未激活的空槽位不记录 `PLAYER_DROPOUT`。

关键日志包含槽位是否激活、玩家和角色是否存在、是否为本机角色、生命、角色存活状态、`idleTimer`、存活人数、本地人数和总生命，并同时写普通日志与 `.trace.log`。

### 补丁自检

启动时新增一次 `AFK_DIAGNOSTICS_PATCH`，自检本 Mod 的预期前置和后置方法是否同时挂在 `Player.Update` 和 `HeroController.DropoutRPC` 上。下一轮必须先看到：

```text
AFK_DIAGNOSTICS_PATCH playerUpdate=True; dropoutRpc=True
```

独立 PowerShell 进程可以解析两个原生目标和四个补丁方法，但 Unity 的内部调用只能在游戏 Mono 运行时中完成 Harmony 织入，因此不把离线宿主的织入失败当作游戏内验证结果。

## 本轮方案状态

| 项目 | 当前状态 | 本轮结果 |
| --- | --- | --- |
| Workshop 下载完成后的状态恢复 | 已有实现，本轮补诊断 | 保留权威关卡号；新增 `WORKSHOP_GAME_MODE_COMPARE`，只观察不写回 |
| 通关和失败状态的原生观测点 | 已实现 | 新增 `LEVEL_OUTCOME`，待真实双端触发 |
| 调试操作记录与重放 | 未实现 | 用户暂不需要自动复现 |
| 确定性对象注册顺序 | 未实现 | 仅记录 Utility Mod 原生调用顺序，当前没有动态生成对象 |
| 可选 Mod 弱依赖 | 已实现为只读诊断 | 新增 `OPTIONAL_BRO_MOD`，待安装/未安装场景验收 |
| 设置写入前预序列化 | 未实现 | 当前仍由 UMM 直接保存，异常时记录错误 |
| AFK 原生流程观测 | 已实现 | 新增倒计时、超时、退出前后状态和补丁自检，待真实双端 AFK 验收 |

## 构建与部署

最终标准构建通过 `BuildAndDeploy.ps1` 生成并部署：

- `buildHash=0915020604a45c80f6cb8b465368fde880bfd5ff00938a135dcce7d878a26caf`。
- DLL SHA-256：`792177CB5ECE13EF50AEE967B32F18C3AA30804FD824667AF1468721EAFE4AE9`。
- 项目安装包、本机 UMM 和内网测试端三份 DLL 哈希一致。
- .NET Framework 3.5 编译成功。
- 静态解析确认 `Player.Update`、`HeroController.DropoutRPC` 两个目标和四个 AFK 钩子均存在。
- `git diff --check` 通过。

该构建尚未完成新增诊断的真实游戏内双端触发验收，不能把“编译和部署成功”写成“问题已经修复”。

## 下一轮验收

1. 双方完全退出并重启游戏，确认 `BUILD_INFO buildHash` 都是本轮构建。
2. 确认启动日志出现 `AFK_DIAGNOSTICS_PATCH playerUpdate=True; dropoutRpc=True` 和 `Level outcome diagnostics enabled; patched methods=2.`。
3. 让目标客户端保持无输入至少 35 秒，对齐双方 `AFK_TIMER`、`AFK_STATE`、`PLAYER_DROPOUT` 和同一时刻的存活人数。
4. 分别触发一次扣命、全员失败和正常通关，检查 `LEVEL_OUTCOME` 的前后状态。
5. 对比双方 `WORKSHOP_GAME_MODE_COMPARE`；如果安装 Swap Bros，再对比 `OPTIONAL_BRO_MOD` 的版本、角色表和选择指纹。
6. 收集双方相同会话 ID 的普通日志、`.trace.log`，并尽量补充 UMM `Core/Log.txt` 和游戏 `error.log`。

## 后续保留项

- 将调试操作建模为可记录、重放的动作，目前按用户要求暂缓。
- 只有出现联机动态对象生成需求时，再实现并实测确定性注册顺序。
- 为当前 UMM 设置模型实现写入前序列化验证。
- 根据真实双端 AFK 日志判断是否需要修复房主存活人数，当前只观测，不提前改规则。
