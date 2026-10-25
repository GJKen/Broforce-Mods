# Custom Map Multiplayer：AFK 行为

[返回开发文档索引](DEVELOPMENT.md) · [测试与验收](TESTING.md) · [诊断日志](DIAGNOSTICS.md)

## 行为概览

当前实现同时支持自动 AFK 和游戏内 Esc 菜单中的“立即进入 AFK”按钮。自动 AFK 开关由每台客户端独立控制；按钮与该开关相互独立，仅作用于当前客户端实际拥有且仍在参与游戏的本地玩家。角色死亡、生命耗尽或暂时没有 character 时也可主动进入 AFK。多个本地槽位时优先匹配 `InputReader.ActiveInputID`，无法唯一确定时放弃请求，避免误操作另一角色。

主动 AFK 按钮要求房间至少有两名参与者。参与者按 `HeroController.PIDS` 中最多四个非空 PID 槽位统计；死亡、等待出生或暂时没有 character 的参与者仍计入人数。单人房间点击按钮时不会修改 AFK 状态，而是显示当前语言的短时提示。

存活角色主动 AFK 通过反射将目标 `Player.idleTimer` 设置为超过原生 35 秒阈值，继续使用原生 `Player.Update`、`HeroController.Dropout` 和 `DropoutRPC` 生命周期。原生闲置计时要求角色存活，因此死亡或暂时没有 character 时直接调用原生 `HeroController.Dropout` 并广播 `DropoutRPC`。存活和死亡角色主动进入 AFK 时使用相同的原生标题 UI，文案跟随 Mod 的语言设置：中文为“已进入旁观AFK模式”，英文为“Entered AFK spectator mode”，“跟随系统”按系统语言选择。

主动掉线会保存原控制器和英雄类型，加入 `PendingLocalWorkshopRejoins`，但跳过自动 `RequestJoinGame`。用户显式返回时恢复原控制器和英雄类型；AFK 前已死亡的角色在同一关返回后仍保持死亡，不补生命、不生成角色，HUD 保留上次死亡角色的头像及死亡表情，等待正常救援或下一回合。本机和其他客户端均保留此前显示的角色头像；原生随机头像变体不因 AFK 返回而重新选择，双头像角色保留两张死亡头像。存活时 AFK 的返回和普通网络掉线恢复继续使用原有流程。死亡状态按本地控制器记录，离开会话或切换场景后清理。

每次本地 Workshop `Player.Start` 成功时清理 `WorkshopLocalJoinRequests` 及关联槽位请求，释放已完成的加入状态。因此快速连续主动 AFK 后显式返回，不需要等待上一请求的 10 秒保护窗口过期；尚未创建 Player 的进行中请求仍保留原有重复加入保护，不将该保护改为 AFK 冷却时间。

`Disable automatic AFK spectator mode in online games` 由每台客户端独立控制，只重置本机联机角色的原生 AFK 计时，不处理远程角色，也不拦截手动退出、断线或死亡。要保护双方角色，双方必须分别开启。

原生 `Player.Update` 仅在存活玩家数大于本机玩家数时累计 `idleTimer`，35 秒后调用 `HeroController.Dropout`。因此一个玩家进入 AFK 后，最后一个本地角色通常停止累计并被保留；其它退出路径导致无人仍需独立观察。

## 日志点

- 约 5 秒：`AFK_TIMER event=counting`。
- 约 30 秒：`event=warning`；条件改变后记录 `event=reset`。
- 35 秒分支：`AFK_STATE event=timeout-triggered`。
- 槽位实际移除：`PLAYER_DROPOUT event=applied`。
- 防 AFK 生效：`AFK_STATE event=prevention-active`。
- 主动按钮请求：`AFK_STATE event=manual-requested`；主动掉线应跳过自动重入。显式返回时清理主动 AFK 状态；存活 AFK 恢复角色，死亡 AFK 则记录 `Preserved dead Workshop player after manual AFK return`，保持 0 生命并等待正常救援。
- 单人房间主动请求拒绝：`AFK_STATE event=manual-request-rejected-single-player`。

只有与本机 35 秒分支在 2 秒窗口内关联的退出标记 `reason=native-afk-timeout`，其它退出保守标记 `unknown`。旧会话证据见 [Utility Mod 借鉴与 AFK 诊断记录](../issues/ISSUES-2026-08-25-Utility-Mod代码借鉴方案与AFK诊断改进.md)。

## 相关实现与验收

- `src/HarmonyDiagnostics.Afk.cs`：原生 AFK 观测、主动目标判定、请求标记、死亡返回控制器记录和统一的本地化标题提示。
- `src/HarmonyDiagnostics.WorkshopPlayer.cs`：显式返回时恢复控制器与英雄类型、保持死亡及原头像，并在本地 `Player.Start` 成功后清理已完成的加入请求。
- `src/HarmonyDiagnostics.Lifecycle.cs`：切换场景和清理会话时释放死亡返回及头像缓存；`src/SettingsUiText.cs`：中英文提示及系统语言选择。
- 基础双端验收：房主和加入方分别点击按钮时只影响各自本地角色；主动 AFK 不安排自动 `RequestJoinGame`；显式返回后恢复控制器和英雄类型，并保留 AFK 前的死亡状态。
- 死亡 AFK、原头像恢复、中英文提示和快速连续返回的详细修复与验收记录见 [死亡后主动 AFK 问题记录](../issues/ISSUES-2026-10-26-死亡后主动AFK与返回状态.md)。
- 多本地槽位需确认按当前输入控制器选择目标，无法唯一确定时不执行。完整记录见 [主动 AFK 按钮问题记录](../issues/archive/ISSUES-2026-09-01-新增ESC菜单主动AFK按钮.md)。
- 单人房间提示及中英文实机验收已通过，完整过程统一记录见 [主动 AFK 按钮问题记录](../issues/archive/ISSUES-2026-09-01-新增ESC菜单主动AFK按钮.md)。
