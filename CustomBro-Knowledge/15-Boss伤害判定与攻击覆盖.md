# Boss 伤害判定与攻击覆盖

更新日期：2026-10-19。依据本地 `Assembly-CSharp` 反编译源码和 Aquabro 的 BossTank、异形蛞蝓、最终撒旦现场诊断整理。适用于普通弹丸、近战、穿透攻击、范围技能和持续伤害。

**开发攻击时必须检查实际受伤组件，不能把“覆盖 `Unit`”或“覆盖 `DamageRelay.unit`”当作覆盖所有 Boss。** 每种攻击都应记录：发现了哪些目标、命中了哪个碰撞体、伤害转发给谁、使用什么伤害数值，以及何时允许再次伤害。

下文的类型与接口结论经过源码核对；现场验证涉及文末的 BossTank、蛞蝓头部和撒旦部件案例，不代表所有 Boss 已经实机验收。

## 1. 先确定实际受伤类型

| 类型 | 源码继承关系／用途 | 容易遗漏的地方 |
| --- | --- | --- |
| `Unit`、`Mech` | `Unit : NetworkedUnit`；`Mech : Unit` | 可以使用单位流程，但仍需处理身体范围、装甲和受伤代理 |
| `Boss`、`BossTank`、`BossGattlingMech` | `Boss : NetworkedUnit`，后二者继承 `Boss` | **不继承 `Unit`**；控制主体不一定是当前可扣血的目标 |
| `BossPiece` | `BossPiece : NetworkedUnit` | **不继承 `Unit`**；坦克履带等部件可直接接收 `Damage(DamageObject)` |
| `BossBlockPiece`、`BossWeakSpot` | `BossBlockPiece : BroforceObject`；`BossWeakSpot : BossBlockPiece` | **不继承 `Unit`**；有独立血量、伤害类型免疫和阶段限制 |
| `BossSlugFace` | `BroforceObject` | 蛞蝓头部不属于上述三类部件；多个 `DamageRelay.damageGameObject` 指向同一头部，共享血量和帧内保护 |
| `BossSlugPiece`、`BossSlugHeart` | 身体为 `MonoBehaviour`；心脏继承 `BossWeakSpot` | 身体的 `Damage` 为空；心脏走 `BossBlockPiece` 弱点路径，不能把身体视作头部血池 |
| `BossSatanFloatingHead`、`BossSatanArm` | 浮动头部与手臂各自继承 `BroforceObject` | 不属于前述部件类型；侧面代理指向头部，六臂各有独立血池；原版免疫 `Unholy` |
| `DamageRelay` | `MonoBehaviour`，受伤转发组件 | 可转发到 `unit`、`doodad`、`bossWeapon` 或 `damageGameObject`，并非只有 `Unit` 分支 |
| `BossGiantHead` 等控制器 | 例如 `BossGiantHead : MonoBehaviour` | 名字含 Boss、外观巨大、处于 Boss 根节点，都不能证明它本身接收伤害 |

源码入口：[Unit](../Broforce_src/Broforce-Source/Unit.cs)、[Mech](../Broforce_src/Broforce-Source/Mech.cs)、[Boss](../Broforce_src/Broforce-Source/Boss.cs)、[BossTank](../Broforce_src/Broforce-Source/BossTank.cs)、[BossGattlingMech](../Broforce_src/Broforce-Source/BossGattlingMech.cs)、[BossPiece](../Broforce_src/Broforce-Source/BossPiece.cs)、[BossBlockPiece](../Broforce_src/Broforce-Source/BossBlockPiece.cs)、[BossWeakSpot](../Broforce_src/Broforce-Source/BossWeakSpot.cs)、[BossGiantHead](../Broforce_src/Broforce-Source/BossGiantHead.cs)。

`Map.units`／`Map.GetUnitsInRange` 只能作为单位候选来源。对非 `Unit` 部件，还需要通过受伤碰撞体或其他经验证的部件集合发现目标；主体不在范围内，远端弱点仍可能被击中。

解析碰撞结果时，先检查碰撞体所在 GameObject 的组件和明确的转发关系。不要无条件 `GetComponentInParent<Unit>()`，也不要向整个 Boss 根节点广播伤害：这可能跳过装甲、打错接收者，或让多个独立弱点同时扣血。

## 2. 保留原版受伤入口与阶段规则

### DamageRelay

按 [DamageRelay.Damage](../Broforce_src/Broforce-Source/DamageRelay.cs) 的现有分支转发，保留销毁状态以及两个 `ignoreDamageWhile...ColliderActive` 条件。`immunity` 用于单位／物件／武器分支，`forceDamageType` 用于单位分支；最后的 `damageGameObject.SendMessage` 分支没有统一免疫过滤，不能额外把所有代理都按 `immunity` 拦截。只判断 `relay != null && relay.unit != null` 会漏掉另外三类转发目标。

命中代理后直接对其 `unit` 扣血，会绕过代理的护甲和免伤判断。若需要解析最终接收者用于去重，实际伤害仍应经过已确认的原版转发入口。不要把代理所有字段都作为独立目标各打一遍。

`unitOnlyReceiveDamageOncePerFrame`／`unitLastReceivedDamage` 是代理内部的有限防重复机制，不能替代一次施法或一个持续伤害周期的命中记录。

### BossPiece

[BossPiece.Damage](../Broforce_src/Broforce-Source/BossPiece.cs) 会处理发送者拥有权相关 RPC，并在可见范围条件通过后，对自身及 `connectedPieces` 调用 `DamageInternal`。内部还有 `damagedThisFrame` 和 `immortal` 判断。

- `immortal=true` 时不扣血可能是正常阶段规则；不能清除该字段或直接改 `health` 来“修复命中”。
- 只调用 `DamageInternal` 会跳过外层网络、可见性与关联部件处理，不应作为通用接入方式。
- `damagedThisFrame` 在 `Update` 中重置，只能限制相应帧内的扣血；不能保护跨帧穿透、多柱技能或重复 RPC。
- `connectedPieces` 会传播伤害。设计去重时应核对实际关联关系与传播范围，不能默认每个碰撞体都对应独立血池，也不能默认列表一定对称。

### BossBlockPiece／BossWeakSpot

[BossBlockPiece.Damage](../Broforce_src/Broforce-Source/BossBlockPiece.cs) 检查 `damageTypeImmunity` 和 `ignoreDamageWhileOtherPieceAlive`，随后处理血量、死亡 RPC、链条和关联结构坍塌。应把伤害交给真实受伤部件，让其保留原版阶段与死亡行为。

寻路用方块、装饰网格和控制器不能仅凭位置接近弱点就作为等价伤害目标。派生类另有重写时，还需检查其实际实现。

## 3. 接入伤害接口时检查完整调用链

[MapController](../Broforce_src/Broforce-Source/MapController.cs) 与 [DamagePolicy](../Broforce_src/Broforce-Source/DamagePolicy.cs) 是关键入口：

| 接口／路径 | 实际行为与注意事项 |
| --- | --- |
| `MapController.Damage_Networked` | 构造 `DamageObject`，按发送者／接收者类型检查策略，在接收 GameObject 上 `SendMessage("Damage", ...)`，按策略决定是否发送 RPC；必须传入正确的接收对象 |
| `MapController.DamageGround` 球形重载 | 用 `Physics.OverlapSphere` 查询碰撞体，再向每个命中对象调用 `Damage_Networked`；默认查询 `Map.groundAndDamageableObjects`，严格模式使用 `Map.groundLayerStrict` |
| `DamageGround` 的 `range` | 球形重载的实际查询半径是 **`range * 0.5f`**，调用时不能直接把它当半径；矩形重载有另一套筛选逻辑 |
| `Unit.Damage`／既有单位流程 | 适用于已确认的单位目标；不能用强制转换把 `BossPiece` 或 `BossBlockPiece` 塞进此流程 |

**`DamageGround` 不只伤害地形。** 处于相应碰撞层的 Boss 部件也可能被它命中。因此，“单位辅助方法没有发现 Boss”不能直接推导出“这次完整攻击没有 Boss 伤害”。要继续检查碰撞、场景物件、爆炸和收尾分支。

新增直接 Boss 伤害后，应检查它是否与既有 `DamageGround`／爆炸路径重复。查询层、碰撞体归属、忽略列表和共享命中记录应一致；不能依赖 Boss 的帧内保护掩盖重复请求。

网络接入应沿用经核对的原版伤害入口，并传入有效的伤害发送者，保留拥有权和归属。部分接收者自身会发送 RPC，不能看到“本地调用”就额外广播一次，也不能认为方法名含 `Networked` 就已完成联机验收。

## 4. 从目标发现到命中登记的检查顺序

1. **发现候选**：覆盖单位、代理和非 `Unit` 部件；空间查询覆盖技能当前／扫掠范围、缩放和目标体积。检查 LayerMask、Collider 是否启用、对象是否激活，以及 3D 查询的 Z 范围。
2. **确认接收关系**：保留碰撞体、原版受伤入口、最终受伤对象／关联部件信息，不把所有目标压缩为一个 `Unit`。
3. **精确接触**：大部件使用实际碰撞体几何或 `bounds` 作为范围依据；`bounds` 是包围盒，必要时还需进一步精判。可见像素技能应按当前动画帧、锚点、朝向与缩放计算接触，不能只测 `Unit.X, Unit.Y + 6` 一个点。
4. **遮挡与先后**：高速弹丸检查上一位置到当前位置的扫掠，并比较墙体／装甲／弱点的最早接触。视线检测可以处理目标自身表面，但不能因此忽略真实墙体或关闭装甲。
5. **受伤资格与预算**：检查原版阶段限制及本次攻击的去重状态。关闭弱点不能提前占用后来开放弱点的命中机会；是否耗弹／停止穿透另按攻击设计决定。
6. **提交与登记**：向正确入口提交符合技能设计的伤害，再更新相应命中记录。没有返回值的原版伤害入口不能被当成“扣血成功”的证明；需结合受伤条件及现场血量验证。

这是一份实现检查顺序，不是要求跳过原版免疫逻辑重新实现一套扣血系统。接触、受伤资格、请求提交和实际扣血应能分别观测。

## 5. 去重按受伤关系和伤害节奏设计

| 攻击形式 | 应明确的去重范围 |
| --- | --- |
| 单次弹丸／单次近战 | 同一个目标的多个碰撞体、代理与本体不能各算一次；穿透是否允许多段必须明确 |
| 同次范围爆炸 | 直接命中和爆炸是否可叠加；多个查询分支是否共享命中记录 |
| 持续区域 | 每个目标按脉冲／间隔受伤，不是每帧、每个 Collider 各扣一次 |
| 多柱／多触手 | 按技能规则共享每目标的脉冲或总预算，避免柱数放大伤害 |
| 收尾爆炸 | 与前面的持续脉冲分别记录，并明确最小间隔、是否允许额外一次伤害 |
| `BossPiece.connectedPieces` | 结合原版关联传播避免重复覆盖同一受伤组，同时保留独立部位各自受伤的能力 |

不能统一按 Boss 根节点去重，否则独立弱点会互相抢占机会；也不能统一按 Collider 去重，否则代理、本体和关联部件可能重复受伤。先确认接收与传播关系，再确定记录键和生命周期。

去重登记还需保留伤害语义。例如范围技能对普通单位和场景物件可能有不同数值，Boss 部件不能因为偶然走入场景分支就自动采用“破坏地形伤害”。吸附、击退、眩晕与扣血也应分别判断，能伤害 Boss 不等于应强制移动 Boss。

## 6. 每新增攻击都填写覆盖表

表中的“通过”应写明证据，例如离线回归、真实组件请求探测、实时扣血或远程联机；不能只写一个无范围说明的勾。

| 攻击路径 | Unit／Mech | DamageRelay 各分支 | BossPiece | BossBlockPiece／WeakSpot | 重复与阶段 | 实机／联机 |
| --- | --- | --- | --- | --- | --- | --- |
| 普通攻击：直接命中及场景碰撞 | 待验证 | 待验证 | 待验证 | 待验证 | 待验证 | 待验证 |
| 蓄力攻击：弹体及爆炸范围 | 待验证 | 待验证 | 待验证 | 待验证 | 待验证 | 待验证 |
| 近战／穿透攻击 | 待验证 | 待验证 | 待验证 | 待验证 | 待验证 | 待验证 |
| 各主动技能：逐个填写 | 待验证 | 待验证 | 待验证 | 待验证 | 待验证 | 待验证 |
| 持续伤害脉冲 | 待验证 | 待验证 | 待验证 | 待验证 | 待验证 | 待验证 |
| 收尾／延迟伤害 | 待验证 | 待验证 | 待验证 | 待验证 | 待验证 | 待验证 |

每种适用路径至少检查以下边界：

- 主体不在查询范围但远端部件接触；大部件边缘接触而中心未接触；左右朝向及高速跨帧接触。
- 不带 `DamageRelay` 的部件；代理各类转发；多个代理／Collider 指向同一受伤目标。
- 装甲关闭／弱点开放；无敌阶段；关联部件与独立部件；已死亡部件。
- 墙前可伤、墙后不伤；目标自身表面不误拦；透明像素／效果范围外不伤。
- 多帧停留、多柱重叠、持续脉冲和收尾的总伤害符合预算；直接与场景／爆炸分支不意外叠加。
- 拥有端、远程端的血量与阶段一致，没有重复伤害或只在本机扣血。

## 7. 用 MCP 定位时记录什么

先按 [MCP 使用说明](13-用Unity Inspector MCP切换游戏角色.md) 连接现场，再记录场景、角色、目标对象路径、组件类型、碰撞层、Collider 范围、当前血量、免疫／阶段字段，以及运行中角色程序集的版本或 MVID。磁盘 DLL 已更新不代表游戏进程已经加载该版本。

沿“候选发现 → 几何接触 → 伤害入口 → 原版限制 → 血量变化”逐层定位。暂停时可探测组件与请求；必要的临时拦截、克隆对象或补丁应在 `finally` 中清理，并恢复探测前的状态。暂停／单步时还要考虑 `Update` 与帧内防重复标记的重置时机。

验收记录要区分三种证据：

| 证据 | 可以说明什么 |
| --- | --- |
| 拦截到 `Damage` 请求，原方法未执行 | 证明伤害路径到达该入口；不能证明免疫检查后实际扣血 |
| 真实接收者执行后血量变化 | 证明当前组件／阶段下能够扣血；仍需检查重复、阶段转换和完整攻击流程 |
| 实时完整攻击及远程联机验证 | 检查运动、动画时序、伤害预算、阶段推进和双方同步是否符合设计 |

特效闪白、受击音效、方法被调用，均不能单独作为实际伤害证据。完整调用链探测与人工操作实时战斗也应分别标注。

## 8. Aquabro 的 BossTank 案例

2026-10-19，MCP 现场发现飞行坦克以 `BossPiece` 接收部件伤害，场景没有活动的 `DamageRelay`；座舱 `immortal=true`，应遵循原版阶段。运行中的 DLL 是精简前版本，因此该缺口不能归因于本轮公共代码提取。

临时探针在右履带的可见像素接触位置调用生产路径，并拦截 `BossPiece.Damage`，记录请求而不执行扣血：

| Aquabro 路径 | 捕获结果 |
| --- | --- |
| 普通／蓄力攻击的单位辅助分支 | 无 `BossPiece` 请求 |
| 普通戟完整场景碰撞路径 | 5 点 `Crush`，通过 `DamageGround` |
| 蓄力戟完整场景碰撞路径 | 14 点 `Explosion`，通过水爆 `DamageGround` |
| 怒海狂潮 | 24 点 `Crush`，来自场景物件分支；普通单位分支数值为 26 |
| 深海猎杀跃出 | 28 点 `Crush`，来自场景物件分支 |
| 深海漩涡吸附伤害脉冲 | 无请求 |
| 深海涌泉 | 无请求 |

这个案例说明：仅支持 `DamageRelay → Unit` 会漏掉坦克部件，但仅检查单位辅助方法又会错判普攻完全没有伤害。漩涡收尾等未列出的路径仍须单独检查。

以上是旧 DLL 的诊断结果。后续 Aquabro 已补充直接／代理的非 `Unit` 部件路径、关联部件预算、普攻接触和场景伤害去重，具体实现及实际验证范围见 [Boss 兼容修复记录](../CustomBro/Aquabro/docs/修复与验证历史.md#2026-10-19-boss-部件兼容修复)。旧版现场、版本与探针边界见 [坦克诊断记录](../CustomBro/Aquabro/docs/修复与验证历史.md#2026-10-19-bosstank-现场诊断)；仍须按本文覆盖表逐项验证其他 Boss 与联机。

## 9. Aquabro 的蛞蝓头部案例

一次历史运行时探测确认目标关卡的蛞蝓头部使用独立 `BossSlugFace`，旧版 `BossDamageTarget.Find` 未返回该头部。四技能像素均接触头部碰撞体时，狂潮经物件分支提交 24 点 `Crush`，鲨鱼向嘴部和上壳各提交 28 点 `Crush`，漩涡／涌泉没有头部请求。探测拦截了网络伤害入口，头部保持原血量，因此这些数字代表捕获到的请求，不代表实际扣血。详细版本与关卡记录见 Aquabro 修复历史。

这说明覆盖 `BossPiece`、`BossBlockPiece`、`BossBlockWeapon` 仍不等于覆盖所有非 `Unit` Boss。接入 `BossSlugFace` 后，以最终头部对象去重、仍向真实碰撞体的原版代理提交伤害；既能修复漩涡／涌泉遗漏，也能统一狂潮数值并消除鲨鱼多代理重复请求。修复测试和当前部署范围见[蛞蝓头部修复记录](../CustomBro/Aquabro/docs/修复与验证历史.md#2026-10-19-蛞蝓头部兼容修复)。

## 10. 最终撒旦的独立头部和手臂

`SatanTestBoss(Clone)` 的当前头部接收者为 `BossSatanFloatingHead`，侧面 `DamageRelay.damageGameObject` 指向同一头部；六条 `BossSatanArm` 各自独立。不能按 Boss 根节点去重，也不能仅因装甲／胸部 `BossBlockPiece` 已被覆盖就认为裸露头部已覆盖。

头部侧面的两个 `ignoreDamageWhile...ColliderActive` 条件必须保留；装甲仍存时，应由装甲接收伤害。手臂在后台时会关闭 3D 伤害碰撞体，无命中属于正常阶段；开放前台碰撞后才参加技能接触。浮动头部和手臂原版均排除 `Unholy`，实际伤害继续交给原版 `Damage`。`BossSatanHead` 是另一个旧类，其受伤还要求开口动画，不能与浮动头部混为一类。

旧 DLL 的暂停现场确认：左侧装甲已破、代理开放，但四技能公共路径对头部均没有请求；胸部和右侧外装甲均各有一次请求。探测拦截了扣血，手臂当时碰撞关闭，不能据此声称已验证前台手臂实际扣血。实现、回归和最新部署见[撒旦部件修复记录](../CustomBro/Aquabro/docs/修复与验证历史.md#2026-10-19-撒旦头部与手臂兼容修复)。

## 11. 为什么其他自定义 Bro 不需要逐个添加 Boss 类型

Aquabro 连续漏伤的主要原因是自定义攻击入口不完整：早期只遍历普通 `Unit`，后续公共部件路径又依赖已知 Boss 接收者类型白名单。遇到不继承 `Unit`、也未进入白名单的接收者，攻击还没有提交给原版 `Damage` 就被漏掉。这属于当前实现的覆盖缺口，不是自定义 Bro 必然要为每个 Boss 单独写补丁。

两份作者源码地图对应的真实源码中，可以看到以下做法：

| 例子 | 具体源码入口 | 伤害处理方式 |
| --- | --- | --- |
| Tony Brotana | `CustomBros/TonyBrotana/TonyBrotana.cs`，初始化与 `SpawnPrimaryProjectile` | 直接取 Brommando 原版弹丸并 `SpawnProjectileLocally`，普通弹交给基类；沿用弹丸已有的碰撞和伤害链。 |
| Santa Braus | `CustomBros/SantaBraus/SantaBraus/SantaBraus.cs`，拳击墙体检测与 `SwingFistEnemies` | 单位走 `Map.HitUnits`，射线击中的场景碰撞对象另交 `MapController.Damage_Local`；没有只接受若干 Boss 类名的前置白名单。 |
| Captain Ameribro | `alexneargarder-BroforceMods/Captain Ameribro/Captain Ameribro/Shield.cs`，`HitWalls` | 有 `BossBlockPiece`、`BossBlockWeapon` 特判，也保留 `else -> ProjectileApplyDamageToBlock` 通用回退，单位另行检测。 |
| Brostbuster 的 Slimer | `alexneargarder-BroforceMods/Brostbuster/Brostbuster/Slimer.cs`，`HitWalls` | 非 Block 对象走 `ProjectileApplyDamageToBlock`，同时保留 `base.HitWalls()`。 |
| Furibrosa 的 WarRig | `alexneargarder-BroforceMods/Furibrosa/Furibrosa/WarRig.cs`，`DamageGround` | 刻意排除 BossBlockPiece、BossBlockWeapon 及部分指向 Boss 单位的代理，说明其他角色也有设计边界，不能据此推定每招都覆盖全部 Boss。 |

上述仓库位于 Broforce-Mods 的父目录，具体索引见 [Gorzontrok 三 Bro 地图](14-Gorzontrok三bro源码地图.md)与 [alexneargarder 六 Bro 地图](09-alexneargarder六bro源码地图.md)。原版 `Broforce_src/Broforce-Source/Projectile.cs` 的 `ProjectileApplyDamageToBlock` 最终直接向碰撞对象调用 `MapController.Damage_Networked`。关键是保留“单位命中”和“场景碰撞对象命中”两条入口，而不是只遍历单位或限定几个 Boss 类型。

Aquabro 的像素接触、水线裁剪和持续伤害预算需要自定义处理，代理的最终接收者、关联部件也需要合理去重；这些要求不能作为封闭目标发现白名单的理由。后续若改造伤害架构，应以通用 Collider／DamageRelay 入口提交原生伤害，针对已知传播关系和特殊预算做适配，并验证单位／对象双路径不会叠伤。本轮后续已实施通用伤害入口改造，见下节；未实机验证上述其他角色的全部 Boss。


## 12. Aquabro 的通用碰撞伤害入口

`src/CollisionDamageTarget.cs` 替代原先的 `BossDamageTarget.cs`。发现流程先解析实际碰撞对象与 DamageRelay 的转发，按原版 unit／doodad／bossWeapon／damageGameObject 分支和免疫回退顺序识别接收者，再检查脚本是否支持 `Damage(DamageObject)`。类型检查结果按脚本 Type 缓存，支持继承的私有方法；未知 Boss 无需先写入类名白名单才能提交原版伤害。

普通单位身体路径继续保留；明确的单位代理与单位身体共用 Unit 身份。已知 Boss 部件继续用对应部件身份，`connectedPieces` 传播集保持既有去重；其他接收者按最终 GameObject 登记，同对象的多碰撞体、多代理和多层代理共用预算。独立接收者不会按 Boss 根节点合并。

伤害仍提交给最初接触的碰撞对象，由原版 DamageRelay／Damage／网络策略处理。每层代理的保护碰撞体和销毁状态均检查，循环代理被拦截。Block、Doodad 使用原有破土／物件规则；已走公共接触路径的接收者从地形／物件伤害中排除，避免直接命中、水花与破土叠伤。像素、水线、视线、所有权和各技能伤害节奏保持原规则。

已有类型适配负责提前检查阶段资格、友伤、死亡和传播预算，不再决定所有目标能否进入发现流程。未知接收者的内部阶段规则交给其原生 Damage 执行，预算按提交次数而非成功扣血次数记录；不通过猜测私有字段或直接改血量判断是否成功。因而不能宣称覆盖了所有未知 Boss 的特殊传播、免疫预算或联机实现。

离线测试新增不在生产适配表中的接收者及其私有 Damage 的派生类，覆盖四技能、普通／蓄力戟尖与水花、多碰撞体／代理链、循环保护、原生阶段免伤、单位身体与代理去重和涌泉 28／漩涡 36 的多帧率预算；旧坦克、蛞蝓及撒旦回归保留。当前构建与实机验证见 [部署与验证索引](../CustomBro/Aquabro/docs/修复与验证历史.md#2026-10-19-通用碰撞伤害入口重构)。

## 13. 先确认技能实例是否生成：涌泉单柱案例

排查“技能打不到 Boss”时，先记录技能实例数量、坐标、延迟和有效阶段，再检查像素接触、遮挡、接收者、伤害请求与实际扣血。实例根本未生成时，修改 Boss 伤害入口无法补回攻击范围。

2026-10-19 的沙虫关卡现场，深海涌泉实际只有中间一柱。旧算法从人物附近向七个预定位置做直线遮挡检测，左右仅高 16px 的地块就使六个外侧位置全部取消；最近阻挡为地形，旁边的沙虫代理不是根因。新版按最多 4px 小步沿地表探测，允许逐格上 16px 台阶，高墙仍取消墙后的水柱，因此七个预定位置不保证每次都生成七柱。

离线回归复现旧版单柱并验证新版低台阶七柱展开；实机重进关卡后地形已变化，两次分别接受 5／4 个位置，后一次实际创建 4 个实例并立即清理，没有推进伤害。完整动画与远程联机未验收。具体地形、部署标识和诊断边界见[涌泉展开修复记录](../CustomBro/Aquabro/docs/修复与验证历史.md#2026-10-19-涌泉在低台阶间只生成一柱修复)。
