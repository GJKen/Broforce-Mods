# alexneargarder 仓库六 bro 源码地图

来源：[alexneargarder/BroforceMods](https://github.com/alexneargarder/BroforceMods)。以下行数与重写清单来自 2026-10-14 核对的源码快照。

2026-10-14 补充核对：以下六个角色全部具有本地源码工程、`_ModContent` 角色定义和 `Releases` 发布 ZIP；mod 版本与对应 manifest 一致。本文原有行数与玩法解析保留，新增版本与文件入口如下。

> [Gorzontrok/CustomBros](https://github.com/Gorzontrok/CustomBros) 中的 Bronobi / SantaBraus / TonyBrotana 与本文六个角色不是同一作者作品，源码入口见 [14 文档](14-Gorzontrok三bro源码地图.md)；Vojkan 的两个角色见 [08 文档](08-VojkanSE87源码解析.md)。
> 两边都值得看,但引用时要说清出处 —— 早期版本的本节曾把这两批误写成同一作者。

## 总览

### 本地工程与发布包

路径相对上游 `alexneargarder/BroforceMods` 仓库根目录。该核对快照中工程 / 内容目录常见格式为 `<目录名>/<目录名>/`、`<目录名>/<目录名>/_ModContent/`，每个工程目录有同名 `.csproj`；发布包位于 `Releases/<目录名>/`。

| 发布名称 | 本地目录名 | 主类入口 | mod / manifest 版本 | 发布 ZIP |
|---|---|---|---|---|
| `Furibrosa` | `Furibrosa` | [Furibrosa.cs](../../alexneargarder-BroforceMods/Furibrosa/Furibrosa/Furibrosa.cs) | `1.1.1` | `AlexN-Furibrosa-1.1.1.zip` |
| `RJBrocready` | `RJBrocready` | [RJBrocready.cs](../../alexneargarder-BroforceMods/RJBrocready/RJBrocready/RJBrocready.cs) | `1.1.1` | `AlexN-RJBrocready-1.1.1.zip` |
| `Drunken_Broster` | `Drunken Broster` | [DrunkenBroster.cs](../../alexneargarder-BroforceMods/Drunken%20Broster/Drunken%20Broster/DrunkenBroster.cs) | `1.0.1` | `AlexN-Drunken_Broster-1.0.1.zip` |
| `Brostbuster` | `Brostbuster` | [Brostbuster.cs](../../alexneargarder-BroforceMods/Brostbuster/Brostbuster/Brostbuster.cs) | `1.1.0` | `AlexN-Brostbuster-1.1.0.zip` |
| `Mission_Impossibro` | `Mission Impossibro` | [MissionImpossibro.cs](../../alexneargarder-BroforceMods/Mission%20Impossibro/Mission%20Impossibro/MissionImpossibro.cs) | `1.1.0` | `AlexN-Mission_Impossibro-1.1.0.zip` |
| `Captain_Ameribro` | `Captain Ameribro` | [CaptainAmeribro.cs](../../alexneargarder-BroforceMods/Captain%20Ameribro/Captain%20Ameribro/CaptainAmeribro.cs) | `1.1.0` | `AlexN-Captain_Ameribro-1.1.0.zip` |

注意：`RJBrocready.cs` 的 `[HeroPreset]` 字符串为 **`R.J. Brocready`**，不要用发布包名替代它。其余五个 preset 使用本地目录名中的空格形式。发布文件名与 preset 是不同标识。

结合 Bronobi（CustomBros）和 Cobro / Jack Broton（Vojkan 仓库），这六个组成用户列出的九角色清单，统一入口见 [README](README.md#三个参考仓库的九个已发布角色)。本次仅核对本地材料，未编译或运行游戏。

### 玩法与重写方法

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
- **Captain Ameribro**:本文六个角色中唯一的 **SwordHero** 实例（Gorzontrok 的 Bronobi 也使用它，见 [14](14-Gorzontrok三bro源码地图.md)）；盾牌 = 射出去的弹 + `PockettedShield` 回收后挂回角色跟随；空中冲刺重写 `RunLeftAirDash/RunRightAirDash`。
- **Drunken Broster**:**HeroPreset 可不带 HeroType**;改原版动画(跑步/待机)直接重写 `AnimateActualIdleFrames/AnimateActualNewRunningFrames`(配 useNewFrames);翻滚 = `AnimateRolling/RollOnLand/CanDoRollOnLand`;近战捡到不同武器 = MeleeItems 目录下每个武器一个 Projectile 类。
- **Furibrosa**:**驾驶敌人** = `PilotUnit/CanPilotUnit/PilotUnitRPC`;召唤大型载具 = 独立 Action 类 + `ExecuteAction`;`RecallBro` 召回。
- **Mission Impossibro**:潜行 = 自定义 `IsInStealthMode` 状态 + 敌人 AI 不索敌;`ThrowBackMook` 抓起敌人扔回;`AirJump` 二段跳。
- **RJBrocready**:**双形态变身** = parameters 贴图数组变体 + `SwitchVariant(int)`(06 文档) + 状态字段控制两套动画/武器;近战连段 follow-up 用 `CanStartMeleeFollowUp`。

## 与本工程的关系

BroTemplate 管线与之同构。做新玩法时的推荐路径:先在本表里找有没有 bro 已实现过类似机制 → 读对应 .cs 的相关方法 → 抄结构再改内容。
