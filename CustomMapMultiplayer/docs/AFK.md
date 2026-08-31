# Custom Map Multiplayer：AFK 行为

[返回开发文档索引](DEVELOPMENT.md) · [测试与验收](TESTING.md) · [诊断日志](DIAGNOSTICS.md)

## 行为概览

当前实现同时支持自动 AFK 和 UMM 中的“立即进入 AFK”按钮。自动 AFK 开关由每台客户端独立控制；按钮与该开关相互独立，仅作用于当前客户端实际拥有且处于可操作状态的本地玩家。多个本地槽位时优先匹配 `InputReader.ActiveInputID`，无法唯一确定时放弃请求，避免误操作另一角色。

主动 AFK 通过反射将目标 `Player.idleTimer` 设置为超过原生 35 秒阈值，继续使用原生 `Player.Update`、`HeroController.Dropout` 和 `DropoutRPC` 生命周期。主动掉线会保存原控制器和英雄类型，加入 `PendingLocalWorkshopRejoins`，但跳过自动 `RequestJoinGame`；用户通过正常流程显式回来时，在 `Player.Start` 阶段恢复生命、英雄类型和角色生成。普通网络掉线不带主动 AFK 标记，仍按原有流程自动重入。

`Disable automatic AFK spectator mode in online games` 由每台客户端独立控制，只重置本机联机角色的原生 AFK 计时，不处理远程角色，也不拦截手动退出、断线或死亡。要保护双方角色，双方必须分别开启。

原生 `Player.Update` 仅在存活玩家数大于本机玩家数时累计 `idleTimer`，35 秒后调用 `HeroController.Dropout`。因此一个玩家进入 AFK 后，最后一个本地角色通常停止累计并被保留；其它退出路径导致无人仍需独立观察。

## 日志点

- 约 5 秒：`AFK_TIMER event=counting`。
- 约 30 秒：`event=warning`；条件改变后记录 `event=reset`。
- 35 秒分支：`AFK_STATE event=timeout-triggered`。
- 槽位实际移除：`PLAYER_DROPOUT event=applied`。
- 防 AFK 生效：`AFK_STATE event=prevention-active`。
- 主动按钮请求：`AFK_STATE event=manual-requested`；主动掉线应跳过自动重入，显式回来时应出现主动 AFK 状态清理和本地生命/角色恢复记录。

只有与本机 35 秒分支在 2 秒窗口内关联的退出标记 `reason=native-afk-timeout`，其它退出保守标记 `unknown`。旧会话证据见 [Utility Mod 借鉴与 AFK 诊断记录](../issues/ISSUES-2026-08-25-Utility-Mod代码借鉴方案与AFK诊断改进.md)。

## 相关实现与验收

- 实现集中在 `src/HarmonyDiagnostics.Afk.cs`，负责原生 AFK 观测、主动目标判定、请求标记、主动掉线窗口和会话清理。
- 基础双端验收：房主和加入方分别点击按钮时只影响各自本地角色；主动 AFK 不安排自动 `RequestJoinGame`；显式回来后恢复原槽位生命、英雄类型、控制器和角色对象。
- 多本地槽位需确认按当前输入控制器选择目标，无法唯一确定时不执行。完整记录见 [主动 AFK 按钮问题记录](../issues/ISSUES-2026-09-01-新增主动AFK按钮.md)。
