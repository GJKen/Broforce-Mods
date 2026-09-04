# ISSUES-2026-09-01 新增 ESC 菜单主动 AFK 按钮

## 状态

**主动 AFK 按钮功能已实现，开发过程中发现的联机生命周期问题和 Esc 菜单语言切换后的按钮错位均已修复，并通过实机验收。**

本文记录在游戏内 Esc 菜单中新增“立即进入 AFK”按钮这一整项功能，以及开发和联调过程中发现、修复的相关问题。普通网络掉线的自动恢复逻辑仍需保留。

## 功能范围

- 在游戏内 Esc 菜单中新增“立即进入 AFK”按钮。
- 点击按钮后，仅让当前客户端对应的本地玩家进入游戏原生 AFK 旁观流程。
- 主动 AFK 使用原生 `Player.idleTimer` 触发 AFK 超时，不改变手动退出、网络断线和正常死亡的语义。
- 原有“禁用联机自动 AFK 旁观模式”开关与主动 AFK 按钮相互独立：开关控制长时间不操作是否自动 AFK，按钮用于用户主动选择立即 AFK。
- 主动 AFK 后，用户可以通过正常的重新加入流程回来，并恢复原槽位的生命、英雄类型和角色。

## 最初问题：按钮作用到了错误的角色

早期实现只在本地玩家数组中遍历符合条件的对象，缺少对本地 PID、输入控制器和实际可操作槽位的联合确认。联机时房主和加入方都能看到玩家对象，单纯根据数组中的玩家对象判断会把主动 AFK 请求作用到错误的角色，出现“加入方按按钮，结果 AFK 了房主角色”。

修复后，按钮目标必须满足本地玩家和本地 PID 的所有权条件；有多个候选角色时优先匹配当前输入控制器，只有唯一候选时才允许执行。无法唯一确定目标时放弃请求，避免误操作另一端或另一槽位。

## 开发过程中发现的联机问题

### 阶段一：主动 AFK 被当成普通掉线，触发重复重入

主动 AFK 最终会由原生 AFK 超时进入 `Dropout` / `DropoutRPC`，而普通网络掉线也使用同一组生命周期回调。原有的 `RememberLocalWorkshopDropout()` 无法仅凭回调本身区分两种情况，因此会为主动 AFK 安排自动 `RequestJoinGame`。多次 AFK 或重入时，自动加入请求、本地 `Player.Start` 和远端掉线通知发生时序竞争，可能造成重复重新加入。

修复方式：

- 为每个玩家槽位增加短生命周期的主动 AFK 掉线标记。
- `RequestLocalAfk()` 设置标记；`Player.Update` 消费一次性请求标记后继续刷新回调窗口，以覆盖延迟到达的 `DropoutRPC`。
- 主动 AFK 的掉线不再安排自动 `RequestJoinGame`，也不再重置已有的晚加入自动请求。
- 普通网络掉线、远端掉线和原生异常移除继续走原有的自动恢复路径。

### 阶段二：阻止自动重入后，显式回来时角色没有生成

第一阶段如果只跳过主动 AFK 的掉线记录，虽然可以阻止自动重入，却会丢失“这个槽位等待用户回来”的状态。加入方再次回来时，房主端槽位已经重新注册，控制器、槽位编号和玩家标识也正确，但新建的本地 `Player` 仍是 `lives=0`、`character=null`，没有进入英雄同步和角色生成流程，最后表现为回来后角色死亡。

修复方式：

- 主动 AFK 的掉线仍保存原槽位的控制器和英雄类型，并加入 `PendingLocalWorkshopRejoins`。该集合表示等待用户显式回来，不表示立即自动重入。
- 用户回来触发 `Player.Start` 时，进入已有的待恢复流程，恢复 `lives`，清理角色已离开本回合的状态，并清除主动 AFK 标记。
- 随后继续执行 `RespawnBro(false)`、英雄类型同步和原生角色生成路径，避免 `Player` 停留在 `lives=0`、`character=null`。
- 会话清理时重置主动 AFK 标记和回调窗口，避免状态泄漏到下一局。

## 最终行为

    点击“立即进入 AFK”
      -> 按当前本地输入控制器确定本地玩家
      -> 触发原生 AFK 超时 / Dropout
      -> 保存槽位恢复信息，不安排自动重入
      -> 用户显式回来触发 Player.Start
      -> 恢复 lives、英雄类型和角色生成

    普通网络掉线
      -> Dropout
      -> 保存槽位恢复信息
      -> 按原有流程自动 RequestJoinGame
      -> Player.Start
      -> 恢复角色

最终区别是：主动 AFK 由用户显式回来触发恢复，普通网络掉线仍可自动重入；按钮只作用于当前客户端实际拥有的角色，不会把房主和加入方的角色混淆。

## 单人房间主动 AFK 提示

### 问题与规则

主动 AFK 只适用于有效的多人联机房间。单人房间点击按钮时不应修改 `idleTimer`，也不应写入主动 AFK 的生命周期状态；按钮仍保持可见，点击后向当前客户端明确提示原因。

参与者数量按 `HeroController.PIDS` 中最多四个非空 PID 槽位统计，而不是按存活角色数量或 `character` 对象数量统计。死亡、等待出生或暂时没有角色对象的玩家仍属于房间参与者。`PIDS` 尚未初始化、正在切关或已经离开房间时，继续沿用已有上下文拒绝逻辑，不误报“单人房间”。

### 实现与本地化

`RequestLocalAfk()` 在已有联机、切关和本地玩家上下文检查之后、确定 AFK 目标之前执行参与者数量判断。参与者少于两人时：

- 使用现有的 `Plugin.ShowFrpDirectNotice()` 显示 5 秒限时提示，不新增通知 UI、计时器或 `MonoBehaviour`。
- 记录 `AFK_STATE event=manual-request-rejected-single-player`。
- 直接返回，不写入 `idleTimer`，不改变主动 AFK、掉线或重新加入状态。

提示通过 `SettingsUiLocalization.Get()` 获取，与 Esc 菜单按钮使用同一套语言设置：English 为 `AFK is unavailable while you are the only player in the room.`，中文为 `房间只有你一人时无法进入 AFK。`。重复点击只刷新现有提示计时，不生成额外 UI。

### 验证

单人房间的 English 和中文提示、多人房间的原有主动 AFK 流程、死亡或未出生玩家仍计入参与者、未初始化状态拒绝，以及重复点击提示刷新均已通过实机验收。该限制不影响自动 AFK、普通网络掉线、重新加入或 Workshop 角色恢复逻辑。

## Esc 菜单语言切换导致功能错位

### 问题现象

主动 AFK 按钮最初通过 UMM 语言设置同时注册英文和中文两个 RocketLib Action，再通过可见性只显示其中一个。联机地图中切换 UMM 语言、关闭 UMM 后打开游戏内 Esc 菜单时，文字可能已经切换，但菜单项目和动作索引发生错位，曾出现以下现象：

- 画面上的“重开关卡”实际执行了 AFK。
- 画面上的“返回主菜单”实际执行了“重开关卡”。
- 某些菜单重建状态下曾出现三个英文 `Enter AFK now` 项。

这不是单纯的翻译错误，而是菜单 UI 项目数组和动作定义数组没有保持同一顺序。

### 根因

RocketLib 的 `RegisterAction` 注册文本是静态的，同时参与 Action 的注入和去重。语言切换时分别注册 English 和中文 Action，会让两个项目共享 `After OPTIONS` 插入位置；PauseMenu 重建后，RocketLib 的 `masterItems` 与 Broforce 已生成的 `items` 可能长度或索引不一致。

菜单显示使用一个索引，原生输入处理却从另一个数组的同一索引读取动作，于是自定义 AFK 项插入后的原生项目整体向前错位。原有的中文字体和材质修复只能处理视觉显示，不能修复数组生命周期或动作索引。

### 修复过程

1. 只保留一个固定的 RocketLib AFK Action，注册名固定为 `Enter AFK now`，唯一回调仍为 `HarmonyDiagnostics.RequestLocalAfk()`。不再注册中文 Action，也不再通过两个 Action 的可见性切换语言。
2. 菜单实例生成后通过唯一的 `RocketLib_...` 路由定位 AFK 项，根据 `Settings.SettingsLanguage` 更新实际 UI 文字。English 显示 `Enter AFK now`，中文显示 `立即进入 AFK`，system 按系统语言选择；不通过当前文字反向查找 Action。
3. RocketLib 注入完成后检查 PauseMenu 的 `masterItems` 和 `items`。发现长度不一致时重新调用原生 `Menu.InstantiateItems()`，确认两组数组长度和索引一致后再更新 AFK UI。
4. 在 `PauseMenu.InstantiateItems` 和 `Menu.Update` 的窄范围 Postfix 中重复执行必要的文字更新，保留中文字体和材质修复，但只处理唯一 AFK 项，不修改“选项”“重开关卡”和“返回主菜单”等原生项目。

修复后，语言切换只改变同一个 AFK 项的可见文字，不改变项目数量、顺序或动作路由。

### 回归验证

同一局中依次切换 English、中文和 system，并反复关闭、打开 Esc 菜单。每次确认：

- 只有一个 AFK 项，且文字与当前语言一致。
- 中文文字完整显示，不退化为乱码或只有 `AFK`。
- 不再出现旧语言项目或三个英文 `Enter AFK now` 项。
- “重开关卡”只重开当前关卡，“返回主菜单”只返回主菜单，AFK 项只进入主动 AFK 流程。

上述语言切换、重复打开菜单和原生相邻按钮功能均已通过实机验收。

## 涉及代码

- `src/Plugin.cs`：注册固定的 RocketLib PauseMenu AFK Action。
- `src/SettingsUiText.cs`：提供 AFK 按钮和提示的本地化文本。
- `src/HarmonyDiagnostics.cs`：在初始化时安装 PauseMenu 菜单补丁。
- `src/HarmonyDiagnostics.Afk.cs`：同步 PauseMenu 菜单数组与 AFK UI 文字，修复字体材质，确定本地 AFK 目标、记录主动 AFK 请求和清理会话状态。
- `src/HarmonyDiagnostics.Lifecycle.cs`：在掉线生命周期中区分主动 AFK 与普通掉线。
- `src/HarmonyDiagnostics.Patches.cs`：衔接原生 `Player.Update` 与 AFK 回调。
- `src/HarmonyDiagnostics.WorkshopPlayer.cs`：维护待恢复槽位，并在显式回来时恢复生命和角色生成。

## 验收结果

当前版本已完成编译，并已完成双端实机验收：

1. 房主和加入方分别点击按钮时，只会让各自本地角色进入 AFK，不会影响另一端角色，已通过验收。
2. 主动 AFK 后不会自动 `RequestJoinGame`，也不会出现重复重新加入，已通过验收。
3. 主动 AFK 后再次回来，角色能够恢复生命、英雄类型和角色对象，不会以 `lives=0`、`character=null` 进入或死亡，已通过验收。
4. 多本地槽位场景下，按钮会优先作用于当前输入控制器对应的角色；无法唯一确定时不执行，已通过验收。
5. 普通网络掉线仍按原有流程自动重入并恢复角色，已通过验收。
6. 在同一局中切换 English、中文和 system 后，Esc 菜单始终只有一个 AFK 项，文字与当前语言一致；不再出现曾经的三个英文 `Enter AFK now` 项；“重开关卡”和“返回主菜单”保持各自原生功能，不再发生 AFK 或前一项动作错位，已通过验收。
7. 单人房间点击 AFK 时不改变 AFK 状态，并显示当前语言的限时提示；多人房间仍进入原有主动 AFK 流程，已通过验收。
