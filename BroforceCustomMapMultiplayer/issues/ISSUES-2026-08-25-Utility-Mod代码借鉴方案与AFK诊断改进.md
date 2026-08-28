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

## 已实施：UMM 折叠分组与可勾选日志审查

本轮已按 Utility Mod 的分组思路完成实现。设置页通过多个布尔状态保存分组展开状态，再用按钮切换并只绘制已经展开的区域。折叠只影响设置页显示，不影响功能开关。

### 设置界面折叠分组

当前 UMM 设置页使用以下折叠分组：

- `Workshop 联机`：地图注入、AFK 开关、Workshop ID、战役名和场景名。
- `FRP Direct`：传输开关、Host/Client、端点、密码、状态和应用按钮。
- `诊断日志`：会话 ID、标签、日志类别选择和诊断预设；固定排在最下方。

默认展开 `Workshop 联机`，默认收起 `诊断日志` 和 `FRP Direct`；用户展开或收起后的状态写入 UMM 设置，下次打开仍保持。旧配置迁移时使用上述默认状态。
分组标题使用不同颜色和程序生成的三角纹理：右向表示收起，下向表示展开，不使用 `+`/`-` 文本。诊断预设改为三列两行的 540px 宽布局，避免长预设名称被截断。

### 日志类别必须可勾选

诊断日志支持按问题类型独立勾选，分类如下：

- 大厅与网络会话。
- Workshop 下载、加载与场景切换。
- 玩家注册、生成、退出与重入。
- AFK 与 `Dropout`。
- 生命、失败、通关与 `LEVEL_OUTCOME`。
- 道具与对象同步。
- FRP Direct 传输。
- 可选 Mod 兼容性。
- Harmony 详细方法追踪。

分类开关只能控制诊断信息是否输出，不能关闭补丁、改变联机流程或影响游戏规则。已提供 `基础联机`、`加入/重入问题`、`AFK/失败问题`、`Workshop 加载问题` 和 `完整诊断` 快捷预设，并保留逐项勾选，方便在预设基础上微调。默认配置为完整诊断。

### AI 复查 Bug 的使用方式

以后让 AI 协助复查具体 Bug 时，AI 应在复现步骤中直接列出本轮需要勾选的日志类别。例如排查 AFK 后无法失败，只需要开启“大厅与网络会话”“玩家注册、生成、退出与重入”“AFK 与 Dropout”“生命、失败、通关与 LEVEL_OUTCOME”，不必采集 Workshop 道具、FRP 或 Harmony 全量追踪。这样可以减少无关信息、降低日志体积，并让双方时间线更容易对齐。

每次会话开始时，日志必须写出本次已经启用的类别清单，让 AI 能判断“没有事件发生”和“对应类别没有开启”之间的区别。双端复查同一问题时，原则上应使用相同类别；如两端职责不同，复现说明必须明确各端的选择。

### 不可关闭的最小核心日志

为了保证任何精简日志仍可审查，以下内容必须始终记录，不能被分类勾选关闭：

- `BUILD_INFO`。
- `SESSION_BEGIN` 和 `SESSION_END`。
- 本次会话启用的日志类别清单。
- Warning、Error 和 Unity 异常。

没有这些核心信息时，无法确认两端版本、会话范围或日志缺失原因。密码、令牌等敏感信息仍不得写入日志。

### 验收标准

1. 三个功能组可以独立展开和收起，重开设置页及重启游戏后状态保持。
2. 各日志类别可以独立勾选，关闭某类后只抑制该类诊断输出，不影响联机行为。
3. 会话开头始终记录构建信息、会话边界和已启用类别。
4. 快捷预设与逐项勾选结果一致，用户仍可在预设基础上增减类别。
5. 使用针对性类别复现 AFK、Workshop 加载和加入/重入问题时，日志足以还原对应时间线，且不夹带大量无关详细追踪。

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
| UMM 折叠分组与可勾选日志审查 | 已实现，待实际 UMM 界面验收 | 三个彩色图形折叠组持久化保存；诊断日志置底，九类日志可独立选择并支持宽预设布局，核心证据不可关闭 |

## 构建与部署

最终标准构建通过 `BuildAndDeploy.ps1` 生成并部署：

- `buildHash=caf775d4805d39773b9a6b00c0569366e5a693607323133e0401033e6322e2da`。
- DLL SHA-256：`08FFC24B5FFE1E2284DA28244360B3C95D3415ECC8E3B5C75C6594D1B153BB9A`。
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
- 实际 UMM 界面仍需验收折叠状态、预设和逐项勾选的保存行为；AI 复查 Bug 时应明确指定所需类别，避免采集过多无用信息。
