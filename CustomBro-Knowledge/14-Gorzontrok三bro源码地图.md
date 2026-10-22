# Gorzontrok 三 bro 源码地图与本地角色清单

核对日期：2026-10-14。依据两个本地仓库的角色类、工程、`.mod.json`、角色 JSON 和发布目录整理；版本指本地文件声明，未查询线上最新版本，也未运行游戏验证。

本文范围为 `CustomBros` 与 `VojkanSE87-BroforceMods` 的源码快照。`alexneargarder-BroforceMods` 另有六个角色案例，见 [09](09-alexneargarder六bro源码地图.md)。此前统计了三个参考仓库的源码与发布包数量；该数量不代表上游当前状态。

## 更新前已有的相关描述

| 文档 | 已有内容 | 本次整理方式 |
|---|---|---|
| [README](README.md) | CustomBros 路径与三个角色名称 | 补全两个仓库的五角色总览和阅读入口 |
| [02 最小可运行角色](02-最小可运行角色与文件结构.md) | Bronobi 文件结构、SantaBraus/TonyBrotana 路径 | 修正作者链接、能力定义形式，补充内联角色 JSON |
| [08 VojkanSE87 源码解析](08-VojkanSE87源码解析.md) | Cobro 详细解析、Jack Broton 简介 | 沿用原文档，补齐玩法、配套类、版本与源码入口，修正“潜行”误读 |
| [09 alexneargarder 六 bro](09-alexneargarder六bro源码地图.md) | 区分两位作者的说明 | 去掉当前不存在的 BroTemplate，链接本文 |
| [12 用 Aseprite 画角色贴图](12-用Aseprite画角色贴图.md) | TonyBrotana 透明动作格对照 | 保留原审计，说明角色 JSON 内联于 mod 文件，并链接源码地图 |
| [NEXT-STEPS](NEXT-STEPS.md) | 两个仓库的参考源码列表 | 更新该列表；其他开发进度仍是原日期的记录 |

## 两个仓库共五个角色

| 仓库 / 作者 | 角色 / preset | 基类 / 官方原型 | 本地版本与发布材料 | 玩法与主要参考点 |
|---|---|---|---|---|
| `CustomBros` / Gorzontrok | Bronobi / `Bronobi` | `SwordHero` / `Blade` | mod 与 `Release/Bronobi` 均为 `1.0.1`，含 ZIP | 光剑、反弹子弹、抓取敌人、精神控制、死亡幽灵 |
| `CustomBros` / Gorzontrok | Santa Braus / `SantaBraus` | `CustomHero` / `Rambro` | mod 为 `0.0.0`，当前仓库无对应发布目录 | 徒手主攻击、击杀生成礼物、拾取换枪、礼物特技库存 |
| `CustomBros` / Gorzontrok | Tony Brotana / `TonyBrotana` | `CustomHero` / `Rambro` | mod 为 `1.0.0`；旧 Changelog/Repository 记录到 `0.8.3`，当前仓库无对应 ZIP | 三发普通弹后追加火箭、逐实例换弹射物贴图、金钱补弹补丁源码 |
| `VojkanSE87-BroforceMods` / Vojkan | Cobro / `Cobro` | `CustomHero` / `Rambro` | mod 与 `Releases/Cobro` 均为 `1.0.4`，含 ZIP | 冲锋枪 / 六发手枪切换、喝酒扔罐吸引敌人、多套材质与近战 |
| `VojkanSE87-BroforceMods` / Vojkan | Jack Broton / `Jack Broton` | `CustomHero` / `Rambro` | mod 与 `Releases/Jack Broton` 均为 `1.0.3`，含 ZIP | TEC 9、两次飞刀后一次无敌药水、提问语音、移动拳击 |

`BroMaxForeheadFix` 和 `CorrectRambro` 是官方角色贴图修正，各自只有 `.fa.json` 与 PNG，不计入自定义角色。`_SharedTextures` 是共享素材，`Scripts` 是构建工具，也不算角色。当前 `CustomBros` 中没有 BroTemplate 工程。

Gorzontrok 的上游是 [Gorzontrok/CustomBros](https://github.com/Gorzontrok/CustomBros)，Vojkan 的上游是 [VojkanSE87/BroforceMods](https://github.com/VojkanSE87/BroforceMods)。[09 文档](09-alexneargarder六bro源码地图.md) 的六个角色属于另一个作者与仓库。

## Gorzontrok 工程与内容入口

以下路径相对各上游仓库根目录；主类行数仅用于估计阅读量，按 2026-10-14 核对的源码快照统计，包含注释和空行。

| 角色 | 主类 | 工程 | 内容目录 / 注册文件 |
|---|---|---|---|
| Bronobi | [Bronobi.cs](../../CustomBros/Bronobi/Bronobi/Bronobi.cs)，488 行 | `Bronobi/Bronobi/Bronobi.csproj` | `Bronobi/Bronobi/_Mod/`，`gz.bronobi.mod.json` + `Bronobi.json` |
| Santa Braus | [SantaBraus.cs](../../CustomBros/SantaBraus/SantaBraus/SantaBraus.cs)，401 行 | `SantaBraus/SantaBraus/SantaBraus.csproj` | `SantaBraus/SantaBraus/_Mod/`，`gz.santabraus.mod.json` + `SantaBraus.json` |
| Tony Brotana | [TonyBrotana.cs](../../CustomBros/TonyBrotana/TonyBrotana.cs)，84 行 | `TonyBrotana/TonyBrotana.csproj` | `TonyBrotana/_MOD/`，`gz.tonybrotana.mod.json` 内联角色定义 |

Bronobi 导入 `Scripts/BroforceModBuild.targets`；SantaBraus / TonyBrotana 仍用工程末尾的 `PostBuildEvent` / XCOPY 复制 DLL 和内容。不能把三个工程都当作使用同一份构建 targets 的模板。

## Bronobi：光剑、抓取与精神控制

注册为 `[HeroPreset("Bronobi", HeroType.Blade)]`，继承 `SwordHero`。主攻击是光剑，不是以 Rambro 枪械为基础；JSON 设置 `fireRate = 0.2`、初始特技 2 发、控制时间 10 秒。

| 行为 | 实现入口 | 配套文件 / 要点 |
|---|---|---|
| 光剑与反弹子弹 | `UseFire`、`FireWeapon`、`SwingSword`、`RunGun` | 直接做范围伤害与 `DeflectProjectiles`；枪图层承载挥剑动画，还处理盾兵 |
| 抓起敌人 | `StartMelee`、`SetupGrabedUnit`、`Update`、`UngrabMook` | 临时改变 `playerNum`、跟随角色位置、换抓取枪图；释放时恢复阵营 |
| 对被抓敌人的操作 | `PressHighFiveMelee`、`UseFire`、`UseSpecial` | 分别投掷、击杀、精神控制；Boss 等对象有额外限制 |
| 范围精神控制 | `UseSpecial`、`AnimateSpecial` | 第 6 帧触发 `MindControlWave.CreateMindControleWave`，第 8 帧结束 |
| 敌人控制组件 | [MindControlEffect.cs](../../CustomBros/Bronobi/Bronobi/MindControlEffect.cs) | 修改阵营和 AI、搜索敌人、播放星星效果，时间到恢复原阵营 |
| 控制波 | [MindControlWave.cs](../../CustomBros/Bronobi/Bronobi/MindControlWave.cs) | 继承 `MatildaTargettingWave`；借 `TryAssassinateUnits` 挂控制组件，Boss 控制时间缩为四分之一 |
| 死亡幽灵 | `Death` → [BronobiGhost.cs](../../CustomBros/Bronobi/Bronobi/BronobiGhost.cs) | 独立 `MonoBehaviour` 与 Starting/Idle/Gift/Leaving 状态，寻找附近玩家并给予奖励 |

另有 [BronobiForceWave.cs](../../CustomBros/Bronobi/Bronobi/BronobiForceWave.cs)（`FlameWallExplosion` 子类）和 `Abilities/AutoReflect.cs`、`ForceWave.cs`、`MindControlWaveAbility.cs`。这些类展示能力扩展，但要区分“类存在”与“当前角色启用”：

- `gz.bronobi.mod.json` 的 `Abilities` 内联注册 ForceWave、MindControlWave 两个能力定义。
- `Bronobi.json` 的 `abilities` 当前为空；主类的 `UseSpecial` 直接创建控制波，不能据此说默认特技绑定了两个能力。
- AutoReflect 类带 `[AbilityPreset]`，但上述 mod JSON 没有列出它；主类自身已有光剑反弹逻辑。

核心素材都在 `_Mod`：身体、光剑、抓取手臂、幽灵图集、控制波、星星特效、光剑音效，以及 `Bronobi_Cutscene.png` / `Bronobi.wav`。动画过场配置也可作为独立参考。

## Santa Braus：随机礼物与拾取换枪

初始特技为 0；未拿武器时，主攻击通过 `UseFire` / `RunGun` / `StartFiring` / `SwingFistEnemies` 实现徒手拳击。拿到武器后改走 `IPresentGun.FireWeapon`，并显示剩余弹药。

| 机制 | 源码入口 | 当前实现 |
|---|---|---|
| 击杀生成礼物 | `ReceiveHeroKillReport` → [PresentsBag.cs](../../CustomBros/SantaBraus/SantaBraus/PresentsBag.cs) | 每次击杀计数 +1，碎尸额外 +1；计数**等于**阈值 2 时生成随机礼物并清零 |
| 可交互礼物 | [WrappedPresent.cs](../../CustomBros/SantaBraus/SantaBraus/WrappedPresent.cs) | `Shrapnel` 子类，创建 `MobileSwitch`；`UnwrapPresent` 调用内容对象的 `Unwrap` |
| 武器礼物 | `Presents/Presents.cs`、[PresentGuns.cs](../../CustomBros/SantaBraus/SantaBraus/Presents/PresentGuns.cs) | 突击步枪 30 发 / 0.1 秒，火箭发射器 8 发 / 0.6 秒；弹尽移除武器，普通弹药箱补充当前枪弹药 |
| 特技礼物 | `AddSpecialPresent`、`UseSpecial` | 保存 `Stack<IPresentSpecial>` 与图标队列，运行时更新特技 UI |
| 全息礼物 | [PresentSpecials.cs](../../CustomBros/SantaBraus/SantaBraus/Presents/PresentSpecials.cs) | 第一次扔全息诱饵，保留对象；再次使用或受到伤害时传送到诱饵并销毁它 |
| 口袋弹药整合 | `PickupPockettableAmmo` | 空袭、外星信息素、机甲投放转为特技礼物；Standard 用于当前武器补弹 |
| 弹药 UI 生命周期 | `Start`、`LateUpdate`、`Death`、`RecallBro`、`OnDestroy` | 创建、跟随玩家 UI、隐藏与销毁弹药文字 |

`PresentsBag.Initialize` 当前随机池仅启用 **TankSummoner、Martini、Hologram、AssaultRifle、RocketLauncher** 五项。代码里其他手雷与口袋特技定义很多，但随机池里的对应行已注释，不能全写成当前会掉落的礼物。

`HarmonyPatches.cs` 当前只给 `Map.GetNearbyGrenade` 加日志，mod JSON 设置 `HasHarmonyPatch = true`。这不是礼物掉落的核心逻辑，也不宜照搬进新角色。

## Tony Brotana：主攻击混合弹射物

`FireWeapon` 维护 `fireCount` / `willFireRocket`；每三发普通弹之后，下次 `SpawnPrimaryProjectile` 发出一枚 Brommando 火箭，然后恢复普通弹。普通弹实例伤害设为 3，火箭前后使用 `rocketDelay = 0.25f`；JSON 的基础 `fireRate` 为 0.08。

火箭使用 `HeroController.GetHeroPrefab(HeroType.Brommando).projectile`，生成后对该实例调用 `SetRendererTexture(RocketTexture)`，贴图为 `TonyBrotana_Special.png`。虽然仓库有 `TonyBrotanaRocket : Rocket`，它是空类，当前生成流程没有使用该类。也不能因为贴图名带 Special，就把这枚火箭描述为重写后的特技：主类没有重写 `UseSpecial` / `AnimateSpecial`。

[MoneyRain.cs](../../CustomBros/TonyBrotana/MoneyRain.cs) 包含两个主要 Harmony 补丁：Tony 伤害敌人时添加 `MookDropMoneyEffect`；拾取 Dollars 时调用 `AddMoney(2)`。`bankAccount >= 5` 时补充特技弹药并清零。

但当前 `gz.tonybrotana.mod.json` 没有 `HasHarmonyPatch = true`，源码中也未见主动 `PatchAll`。本地 BroMaker 的 `Storages/PresetManager.cs` 通过这个开关启动自定义程序集补丁，所以这里应记录为**金钱补弹实现源码，当前配置未启用补丁**，不能保证安装后自动生效。

动作图集的空白帧与风险已有单独核对，见 [12 文档的 TonyBrotana 对照结论](12-用Aseprite画角色贴图.md#tonybrotana-对照结论)。

## 本地材料完整性与阅读顺序

Bronobi 有 `Release/Bronobi/Gorzontrok-Bronobi-1.0.1.zip` 与 manifest；更早版本还分布在 `Bronobi/Release` 和 `Release/Bronobi/Previous Versions`。Vojkan 两个角色的 ZIP、manifest 和玩法 README 见 [08 文档](08-VojkanSE87源码解析.md)。这里的发布包版本不代表当前源码与 ZIP 二进制逐字对应。

Santa 的 JSON 引用 `SantaBraus_cutscene.png`，Tony 的内联 JSON 引用 `cutscene.png`，但这两个文件在各自当前内容目录中缺失。它们有源码和主要贴图，不能仅凭工程存在就认定为完整可部署包。本次只整理文档，没有补资源或编译部署。

按需要选择阅读入口：

| 要实现的功能 | 优先参考 |
|---|---|
| 少量重写、普通弹与火箭混用 | TonyBrotana 主类 |
| 近战主武器、抓取、改变敌人行为 | Bronobi 主类 + MindControlEffect |
| 组合能力定义与 `[AbilityPreset]` | Bronobi 的 mod JSON + Abilities 目录 |
| 随机掉落、交互拾取、换枪与独立弹药 | SantaBraus + PresentsBag + WrappedPresent + PresentGuns |
| 武器模式切换、多套身体/枪材质、独立特技计时 | Cobro，见 [08](08-VojkanSE87源码解析.md) |
| 完整飞刀碰撞、自定义特技流程、受击与清理 | Jack Broton + BootKnife，见 [08](08-VojkanSE87源码解析.md) |

这些工程都以 .NET Framework 3.5 为目标，但源码含 `nameof`、插值字符串、表达式体等现代 C# 语法。目标框架版本与编译器语法版本是两件事，不能直接交给 Windows 自带的 C# 3 编译器照搬编译。
