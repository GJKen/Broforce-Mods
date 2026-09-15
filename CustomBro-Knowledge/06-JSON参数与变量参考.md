# JSON 参数与变量参考

来源: [Parameters](https://github.com/Gorzontrok/Bro-Maker/wiki/Parameters) / [Variables](https://github.com/Gorzontrok/Bro-Maker/wiki/Variables)

---

## ⚠️ 使用前必读:本文是 wiki 转录,未经本机核实

**本文内容是从 Bro-Maker wiki 抄录的,没有逐条对着本机的 BroMaker 2.6.2 验证过。** 两处已知风险:

### 1. 下面的"默认值"表与实测结论冲突 —— **以 10 文档为准**

本文列的默认值(如 `"fireRate": 0.0334`、`"speed": 110.0` 等)是 wiki 上的说法。
但 [10 文档](10-实战笔记-Broseidon一二三期踩坑.md) 第一节的**实测**结论是:

> **JSON 变量节留空 ≠ 继承原版 bro 默认值** —— `beforeAwake` 留空时 `fireRate` 落到 **0**,表现为**每帧开枪**(攻速爆表)。

**规则:凡是在乎的数值字段,一律显式写,不要依赖"空 = 默认"。** 表格只当字段清单用,数值不要当真。

### 2. `specialGrenade` / `projectile` 名单未核实

文末那两个名单在本机的 `Assembly-CSharp.dll` 和 `BroMakerLib.dll` 里**用字符串检索对不上**(projectile 51 个里约 18 个、specialGrenade 20 个里约 6 个搜不到)。
⚠️ **这不能断定名单是错的** —— 检索方式本身有假阴性(方法自检时 `CustomHero` 也搜不到,因为它在 BroMakerLib 而不是 Assembly-CSharp)。
准确说法是**未能核实**。要用某个名字时,先在小工程里试通再写进正式 bro。

## parameters 节(贴图/UI 类)

| 变量 | 类型 | 用途 |
|---|---|---|
| `Sprite` | string | 主角色贴图路径 |
| `GunSprite` | string | 枪贴图路径 |
| `SpecialIcons` | string | 特技图标 spritesheet |
| `SpecialIconOffset` | Vector2 | 特技图标偏移 |
| `SpecialIconSpacing` | float | 特技图标水平间距 |
| `Avatar` | string | 头像贴图 |
| `GunSpriteOffset` | Vector2 | 枪精灵偏移(死亡画面枪位置同样用它) |
| `BetterAnimation` | bool | true 启用大量动画变量 |
| `Halo` | bool | 加 Broffy 的光环 |
| `JetPackSprite` | bool | 补上 bro 使用喷气包时缺失的贴图 |

注意:`sprite`、`gunSprite`、`Avatar`、`SpecialIcons` 等**贴图类字段必须放 parameters**;
`disarmedGunMaterial` 等其他贴图是 bro 类的普通字段,放 `before/afterAwake/Start` 节。

### 变体(Variants)

parameters 里多个字段支持**数组**实现多变体,spawn 时经 `GetVariant()` 随机选一个:

- 支持变体的:`Sprite`、`GunSprite`、`Avatar`、`SpecialIcons`、`GunSpriteOffset`、`SpecialIconOffset`、`SpecialIconSpacing`
- 变体数量 = 数组中最大长度;单值字段所有变体共用。
- **数组长度必须一致**(或为 1):4 个 sprite 就不能配 2 个 gun sprite(可重复补齐到 4)。
- `SwitchVariant(int)` 运行时切换(变身类角色用,如 R.J. Brocready 人/怪物形态)。

```json
"Sprite":  ["sprite.png", "variants/sprite_2.png", "variants/sprite_3.png", "variants/sprite_4.png"],
"Avatar":  ["avatar.png", "variants/avatar_2.png", "variants/avatar_3.png", "variants/avatar_4.png"],
"GunSprite": "gunSprite.png"
```

### SpecialIcons 多弹药图标(特有格式)

- 单图标:`"SpecialIcons": "special.png"`
- 多弹药槽各自图标(单变体):`["ammo1.png", "ammo2.png", "ammo3.png"]`
- 多变体 × 多弹药槽:**必须嵌套数组**,外层每项 = 一个变体,内层 = 该变体各弹药槽图标:
  ```json
  "SpecialIcons": [["ammo1.png", "ammo2.png"], ["alt_ammo1.png", "alt_ammo2.png"]]
  ```
  只有单图标的多变体也要嵌套:`[["special.png"], ["thingSpecial.png"]]`(扁平数组会被解释为单变体的两个弹药槽)。

过场也支持变体(wiki 的 Cutscenes 页)。

## before/afterAwake / before/afterStart 节(变量类)

按 wiki Variables 页,默认值如下(部分只对敌人有效)。

> ⚠️ **下面这些数值是 wiki 转录,未经核实,且与 [10 文档](10-实战笔记-Broseidon一二三期踩坑.md) 的实测冲突**
> —— 实测结论是"留空 → 落 0"(`fireRate` 落 0 = 每帧开枪)。**只当字段清单看,数值不要当真,在乎的字段一律显式写。**

### 全局

```json
{
  "canBeCoveredInAcid": true, "meltDuration": 0.7,
  "canAirdash": false, "airdashMaxTime": 0.5, "defaultAirdashDelay": 0.15,
  "canDoIndependentMeleeAnimation": false, "doRollOnLand": false,
  "useDashFrames": false, "useNewFrames": false, "useNewKnifingFrames": false,
  "useNewLedgeGrappleFrames": false, "useNewThrowingFrames": false,
  "useNewPushingFrames": false, "useNewHighFivingFrames": false,
  "hasNewAirFlexFrames": false, "useNewKnifeClimbingFrames": false,
  "useNewLadderClimbingFrames": false, "useLadderClimbingTransition": false,
  "useDuckingFrames": true, "useNewDuckingFrames": false,
  "highFiveBoostTime": 3.7, "highFiveBoostM": 1.4,
  "immuneToOutOfBounds": false,
  "canDash": true, "dashSpeedM": 1.0,
  "canGib": true, "bloodColor": "Red", "bloodCountAmount": 80,
  "deathSoundVolume": 0.4, "willComeBackToLife": false, "reviveZombieTime": 2.0, "canDisembowel": false,
  "JUMP_TIME": 0.123, "speed": 110.0, "maxFallSpeed": -400.0, "_jumpForce": 260.0, "quicksandChokeCounter": 2.0,
  "bypassNewVoicesOnThisBro": false, "pitchShiftAmount": 1.0,
  "specialGrenade": "Grenade", "originalSpecialAmmo": 3,
  "turnAroundWhileUsingSpecials": true, "specialAttackYIBoost": 0.0, "specialAttackXIBoost": 0.0,
  "projectile": "Rambro", "rumbleAmountPerShot": 0.3,
  "fireRate": 0.0334, "fireDelay": 0.0, "breakDoorsOpen": false,
  "canWallClimb": true, "canPushBlocks": true, "canLedgeGrapple": false, "canUnFreeze": false,
  "canBeStrungUp": false, "maxHealth": 1,
  "canCeilingHang": true, "hangGraceTime": 0.3,
  "cancelMeleeOnChangeDirection": false, "meleeType": "Knife", "knifeClimbStabHeight": 18.0,
  "maxWallClimbYI": 100.0, "usePrimaryAvatar": true
}
```

### 贴图类(放 awake/start 节的)

```json
{ "disarmedGunMaterial": "filename.png" }
```

各 bro 专属贴图示例(Brominator 的 `metalBrominator/humanBrominator/metalGunBrominator/humanGunBrominator/brominatorHumanAvatar/brominatorRobotAvatar`,Ash Brolliams 的 `bloodyAvatar`,Seth Brondle 的 `teleportInAnimation/teleportOutAnimation/coveredInBloodMaterial/openMouthMaterial/openMouthBloodyMaterial`)。

### 原版 bro 专属调参字段(示例)

| Bro | 字段 |
|---|---|
| Bro Ceasar / Brominator | `miniGunFireDelay: 1.25`, `pushBackForceM: 1.0` |
| Brochete | `knifeSpeed: 270`, `knifeSpraySpeed: 320`, `knifeSprayCount: 12` |
| Broden | `sliceVolume: 0.3`, `maxElectricShocks: 4`, `lightningRange: 50`, `chainLightningRange: 40`, `zapperXOffset: -16`, `zapperYOffset: -12` |
| Bro Dredd | `remoteProjectileSpeed: 90` |
| Bro Gummer | `remoteProjectileSpeed: 800`, `extraFireDelay: 0.3`, `scanningRange: 10`, `specialCooldownDelay: 0.13` |
| Bromando | `barageCount: 4` |
| Bronnar Jensen | `shootGrenadeSpeedY: 60`, `shootGrenadeSpeedX: 300` |
| Brove Heart | `groundSwordDamage: 10`, `enemySwordDamage: 8`, `sliceVolume: 0.7`, `wallHitVolume: 0.6` |
| Robocop | `scanningRange: 10`, `chargeTimePerBulletFired: 0.075`, `specialCooldownDelay: 0.13` |
| Seth Brondle | `checkCeilingForHangRadius: 6` |

### specialGrenade 可用名称

> ⚠️ 名单转录自 wiki,**未能核实**(本机 DLL 字符串检索对不上约 6/20)。用哪个先在测试工程里试通再用。

`Grenade, Default, Martini, TearGas, FlameWave, AirStrike, FlashBang, Hologram, Cluster, Sticky, GrenadeTollBroad, SummonTank, Molotove, HolyWater, Freeze, MechDrop, AlienPheromones, EvilSmall, EvilBig, EvilBigShortLife`

### projectile 可用名称

> ⚠️ 名单转录自 wiki,**未能核实**(本机 DLL 字符串检索对不上约 18/51)。用哪个先在测试工程里试通再用。

`Default, Bullet, Rambro, Rocket, DrunkRocket, ShotgunAdjusted, ThrowingKnife, ShockWave, Turkey, SachelPack, NoisyCricket, BroboCop, RocketRemote, BulletSeeking, IndianaBrones, Shotgun, Machete, MacheteSpray, Plasma, TimeBro, BroniversalSoldier, KnifeSpray, GrenadeSticky, Sniper, ShotgunFlame, Boomerang, Silence, PredabroCanon, SpearCharged, Spear, Vomit, Sword, RocketBig, Arrow, Stake, SachelPackSmall, DemolitionBomb, Huge, MookMiniGun, RocketHuge, ShellHeavy, BabyDog, SlimeVomit, Airstrike, RocketSeeking, RocketSeekingBig, FireBall, FireBallBombardment, WarlockPortalGuided, ShellMedium, ShellHeavyEvil`
