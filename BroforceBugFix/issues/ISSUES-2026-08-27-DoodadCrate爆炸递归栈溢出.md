# DoodadCrate 爆炸递归栈溢出

## 状态

- 根因：已确认。
- 修复：已实现。
- 构建：已通过。
- 开关逻辑验证：已通过。
- 离线游戏内复测：待执行。
- 官方 Steam 双端复测：待执行。

当前 `0.2.0` 构建的 DLL SHA-256：

```text
B81902C1D59F6CD6845B76841C0A619B303137664BFE0E67783674F5561C025C
```

## 现象

在官方 Steam Workshop 三方联机中，房主进入地图 `3660163376`（`Alien Predator Hard`）第 2 关后突然看到大量爆炸物和碎片，游戏严重卡顿。失败重开时房主失活；加入方没有看到同等规模的爆炸，并成功独自重载同一关。

双方构建一致：

```text
session=test005
buildHash=1ec0487aded5158b15f0ebad4fd640ad304d29e6f97a88a472397b17e37b24dc
scene=Test Evan2
levelNumber=1
```

## 根因证据

房主在 `2026-08-26T21:58:25.835Z` 抛出 `StackOverflowException`。重复栈为：

```text
DoodadCrate.CreateExplosion
MapController.DamageGround
MapController.Damage_Networked
DoodadCrate.Damage
FallingBlock.Collapse
DoodadCrate.ActuallyCollapse
DoodadCrate.EffectsDestroyed
DoodadCrate.CreateExplosion
```

原版源码中的关键顺序：

- `DoodadCrate.CreateExplosion` 创建特效后调用 `DamageGround`。
- `DoodadCrate.EffectsDestroyed` 在 `fullOfExplosiveAmmunition` 为真时再次调用 `CreateExplosion`。
- `Block.ActuallyCollapse` 先调用 `EffectsDestroyed`，随后才调用 `DestroyBlockInternal`。
- `Block.DestroyBlockInternal` 后段才写入 `destroyed=true`。

因此，相邻爆炸弹药箱能在销毁标志写入前互相伤害并递归回到同一个实例。

## 联机差异

`MapController.Damage_Networked` 先在本机执行 `damageReciever.SendMessage("Damage")`，返回后才向 `PID.TargetOthers` 发送 RPC。房主在本地调用中栈溢出，后续伤害没有完整广播；碎片和火花也属于本地特效。因此加入方没有出现同等规模的爆炸风暴。

## 地图侧条件

Steam 返回的 `ALIENPREDATOR HARD.bfg` 解包后显示，第 2 关尺寸为 43×256，包含：

- 61 个 `Crate`；
- 29 个 `AmmoCrate`；
- 308 个 `Boulder`。

房主故障前位置约为世界坐标 `(248,1256)`，对应网格 `(15.5,78.5)`。该高度附近密集布置了箱体、木块和下落物，满足触发相邻箱体连锁伤害的条件。

## 修复方案

在 `DoodadCrate.ActuallyCollapse` 添加按引用区分实例的同步重入保护。第一次调用完整执行原版代码；同一实例在调用尚未退出时发生的嵌套调用被跳过。Finalizer 负责在正常返回或抛异常时清理状态，并保留原异常。

选择方法级重入保护而不是提前写入 `destroyed=true`，是为了避免改变原版 `EffectsDestroyed`、统计、掉落物和 `DestroyBlockInternal` 的顺序。

## 开关逻辑验证

已通过反射测试验证以下组合：

| UMM Mod | 全部修复主开关 | 当前修复独立开关 | 预期结果 | 验证结果 |
| --- | --- | --- | --- | --- |
| 关闭 | 开启 | 开启 | 不安装补丁 | 通过 |
| 开启 | 关闭 | 开启 | 不安装补丁，保留独立开关值 | 通过 |
| 开启 | 开启 | 关闭 | 不安装补丁 | 通过 |
| 开启 | 开启 | 开启 | 安装补丁 | 通过 |
| 开启 | 关闭后重新开启 | 保持开启 | 恢复安装补丁 | 通过 |

该验证只覆盖设置协调、补丁安装和卸载行为，不代替离线及官方 Steam 双端游戏内复测。

## 验收标准

- 相邻爆炸弹药箱仍能发生一次正常连锁爆炸。
- UMM 日志至少记录一次递归调用被抑制。
- 不再出现 `DoodadCrate` 循环栈或 `StackOverflowException`。
- 普通木箱、弹药箱、掉落物、伤害和网络 RPC 没有可见回归。
- 原故障关卡失败后，房主与加入方均能重新加载同一关。
