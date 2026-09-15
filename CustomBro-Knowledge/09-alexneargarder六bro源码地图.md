# alexneargarder 仓库六 bro 源码地图

来源:`E:\Study\C#\alexneargarder-BroforceMods\`。以下行数与重写清单来自全仓 grep 扫描。

> 本机另有一份 `E:\Study\C#\CustomBros\`(**Gorzontrok** 的作品:Bronobi / SantaBraus / TonyBrotana,以及 BroTemplate),**与本文这 6 个 bro 不是同一作者**,别混。
> 两边都值得看,但引用时要说清出处 —— 早期版本的本节曾把这两批误写成同一作者。

## 总览

| Bro | 主类行数 | 基类/原型 | 特色玩法 | 值得抄的手法 |
|---|---|---|---|---|
| Brostbuster(捉鬼) | 1079 | CustomHero/Rambro | 质子流持续光束、幽灵陷阱 | 弹射物回调全家桶、Harmony 补丁、持续音效 |
| Captain Ameribro(美队) | 1184 | **SwordHero**/Nebro | 盾牌投掷+反弹、空中冲刺 | SwordHero 基类、盾作为跟随物体(PockettedShield) |
| Drunken Broster(醉拳) | **3792** | CustomHero(无 HeroType) | 醉酒镜头、翻滚、近战捡武器 | 最大最全:翻滚动画、LedgeGrapple、镜头管理 |
| Furibrosa(疯麦) | 1189 | CustomHero/Rambro | 召唤战车、驾驶敌人单位 | PilotUnit/CanPilotUnit、RecallBro、召唤 Action |
| Mission Impossibro(碟中谍) | 1236 | CustomHero/Rambro | 潜行模式、飞踢、抛回敌人 | IsInStealthMode、AirJump、ThrowBackMook、拳近战 |
| RJBrocready(变身博士) | **2423** | CustomHero/Rambro | 人/怪物双形态变身 | SwitchVariant/GetVariant 变身、近战连段 follow-up |

另有 `ExampleMod`(纯入门示例)和 Utility Mod 里的 TestDummy(测试假人,重写了 Damage/Death/Land/AnimateIdle 等几乎所有钩子,当"钩子速查表"用)。

## 重写面覆盖统计(每个 bro 用过的钩子)

- **全梯队都会碰**:`Awake/Start/Update/OnDestroy`、`UseFire/PressSpecial/UseSpecial/AnimateSpecial`、`PreloadAssets`、`BeforePrefabSetup/AfterPrefabSetup/PrefabSetup`、`OnDeath`
- **武器**:`RunFiring/StartFiring/StopFiring/FireWeapon/RunGun/SetGunSprite/SetGunPosition/Fire/ActivateGun/ReleaseFire`
- **移动**: `RunMovement/Jump/Land/CheckFacingDirection/ApplyFallingGravity/AddSpeedLeft/AddSpeedRight/AirJump/AirDash 系列/AttachToZipline/AnimateZipline/LedgeGrapple/RollOnLand`
- **近战**:`StartCustomMelee/StartMeleeCommon/RunCustomMeleeMovement/AnimateCustomMelee/CancelMelee/CanStartNewMelee/CanStartMeleeFollowUp/SetMeleeType/TryMeleeTerrain/PerformPunchAttack`
- **受击/死亡**:`Damage/Knock/Gib/Death/OnDeath/OnRevived/HitUnits/HitWalls/HitProjectiles/Bounce`
- **输入/杂项**:`RegisterCustomTriggers/ExecuteAction/UIOptions/ShowGUI/ChangeFrame/IncreaseFrame/GetVariant`

结论:CustomHero/SwordHero 的可重写面 ≈ 60+ 个钩子,想改什么行为都有入口。写新 bro 时按"玩法 → 钩子"反查本表即可。

## 每个 bro 的独门技巧(速查)

- **Brostbuster**:持续光束 = 每帧 SpawnProjectileLocally 短寿命弹 + 持续循环音效(04 文档);`GhostTrap.cs` 独立交互物;`HarmonyPatches.cs` 补原版行为缺口。
- **Captain Ameribro**:唯一的 **SwordHero** 实例(近战为主就继承它);盾牌 = 射出去的弹 + `PockettedShield` 回收后挂回角色跟随;空中冲刺重写 `RunLeftAirDash/RunRightAirDash`。
- **Drunken Broster**:**HeroPreset 可不带 HeroType**;改原版动画(跑步/待机)直接重写 `AnimateActualIdleFrames/AnimateActualNewRunningFrames`(配 useNewFrames);翻滚 = `AnimateRolling/RollOnLand/CanDoRollOnLand`;近战捡到不同武器 = MeleeItems 目录下每个武器一个 Projectile 类。
- **Furibrosa**:**驾驶敌人** = `PilotUnit/CanPilotUnit/PilotUnitRPC`;召唤大型载具 = 独立 Action 类 + `ExecuteAction`;`RecallBro` 召回。
- **Mission Impossibro**:潜行 = 自定义 `IsInStealthMode` 状态 + 敌人 AI 不索敌;`ThrowBackMook` 抓起敌人扔回;`AirJump` 二段跳。
- **RJBrocready**:**双形态变身** = parameters 贴图数组变体 + `SwitchVariant(int)`(06 文档) + 状态字段控制两套动画/武器;近战连段 follow-up 用 `CanStartMeleeFollowUp`。

## 与本工程的关系

BroTemplate 管线与之同构。做新玩法时的推荐路径:先在本表里找有没有 bro 已实现过类似机制 → 读对应 .cs 的相关方法 → 抄结构再改内容。
