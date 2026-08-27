# DemolitionBro 炸弹主动引爆联机同步

## 状态

已实现、构建并完成双端联机实测，用户确认测试无问题。

## 历史事实与当前状态

- 已废弃的 `ISSUES-2026-08-27-第三方地图动态世界同步.md` 曾包含一次主动引爆同步实验，用户当时实测确认 `DemolitionBro` 主动引爆恢复正常。
- 相关实验代码后来已按用户要求丢弃，当前源码中不存在是预期状态，不能描述为代码遗漏或回归。
- 废弃 issue 仅作为与 `DemolitionBro` 直接相关的历史线索，不作为本次实现或当前验收状态的依据。

## 根因

原版 `DemolitionBro.FireWeapon` 只在拥有端通过 `ProjectileController.SpawnProjectileOverNetwork(..., synced: false)` 创建并记录 `currentBomb`。第二次按攻击键时，`DemolitionBro.UseFire` 在拥有端当前调用栈直接执行 `currentBomb.Death()`。

网络生成会为各端的炸弹副本注册同一稳定 NID，但这次主动 `Death()` 本身没有广播。远端副本因此可能继续运行到自身寿命结束或进入本地碰撞路径，造成爆炸时间、位置不一致，或在拥有端主动引爆后延迟出现第二次爆炸。

## 严格范围

本 issue 只处理 `DemolitionBro.currentBomb` 的主动引爆通知：

- 不处理 `McBrover` 火鸡。
- 不修改普通 `Grenade`、地形伤害、其他角色投掷物或通用 `Projectile` 死亡逻辑。
- 不处理敌人死亡、尸体、钱币、拾取物或其他动态实体。
- 离线模式和非 `DemolitionBro.UseFire` 主动引爆调用保持原行为。
- 同一游戏 RPC 路由应覆盖官方地图和 Workshop 地图，并同时适用于 Steam 官方联机与 FRP Direct 联机。

## 最小方案

只转译 `DemolitionBro.UseFire` 中已经确认的 `Projectile.Death()` 调用点：

1. 拥有端取得炸弹稳定 NID 与当前权威坐标，向 `PID.TargetOthers` 发送主动引爆事件。
2. 发送后仍在当前调用栈直接调用该实例的原版虚拟 `Death()`，不等待 RPC，也不让 RPC 在拥有端再次执行。
3. 远端按 NID 从 `Registry` 查找自己的 `Projectile` 副本，校正到权威坐标后调用一次原版虚拟 `Death()`。
4. 按 NID 记录已发送和已处理事件；重复通知直接忽略。对象已自然销毁、碰撞销毁或未注册时不再执行 `Death()`。状态在场景或网络会话清理时重置。
5. 诊断日志区分拥有端发送、远端 NID 命中并引爆、NID 未注册和重复事件忽略。

## 验收标准

- 双方安装相同 DLL，并完全退出、重启游戏后测试。
- `DemolitionBro` 投出炸弹后第二次按攻击键，拥有端立即引爆。
- 另一端在拥有端给出的权威位置看到同一次爆炸。
- 炸弹原寿命到期时不再出现第二次爆炸。
- Steam 官方地图、Steam Workshop 地图、FRP Direct 官方地图和 FRP Direct Workshop 地图均经过双端实测。
- 日志能分别观察到拥有端发送和远端 NID 命中；异常路径能区分 NID 未注册与重复事件忽略。
- 上述联机实测已完成，用户确认主动引爆同步无问题。
