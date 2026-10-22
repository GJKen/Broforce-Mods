# VojkanSE87 源码解析：Cobro 与 Jack Broton

来源：[VojkanSE87/BroforceMods](https://github.com/VojkanSE87/BroforceMods)。核对日期：2026-10-14，依据源码、JSON 与发布 README；本次未验证游戏安装或运行状态。

## 角色总览与源码入口

以下路径相对上述仓库；版本取自本地 mod JSON 和发布 manifest，不代表线上最新版本。

| 角色 | 原型与玩法 | 本地版本 | 源码 / 内容 / 发布目录 |
|---|---|---|---|
| Cobro | 《Cobra》中的 Cobretti；自动冲锋枪、可切换的六发手枪、喝酒扔罐、移动拳击或枪托近战 | `1.0.4`，BroMaker `2.6.2` | [Bro.cs](../../VojkanSE87-BroforceMods/Cobro/Cobro/Bro.cs)、[CoorsCanProjectile.cs](../../VojkanSE87-BroforceMods/Cobro/Cobro/CoorsCanProjectile.cs)；`Cobro/Cobro/_ModContent/`；`Releases/Cobro/` |
| Jack Broton | 《Big Trouble in Little China》中的 Jack Burton；TEC 9 半自动枪、两次飞刀后一次无敌药水、站立提问与移动拳击 | `1.0.3`，BroMaker `2.6.2` | [Bro.cs](../../VojkanSE87-BroforceMods/Jack%20Broton/Jack%20Broton/Bro.cs)、[BootKnife.cs](../../VojkanSE87-BroforceMods/Jack%20Broton/Jack%20Broton/BootKnife.cs)；`Jack Broton/Jack Broton/_ModContent/`；`Releases/Jack Broton/` |

两个角色都继承 `CustomHero`，preset 分别为 `Cobro` / `Jack Broton`，官方原型均为 `HeroType.Rambro`。两边 JSON 都开启 `BetterAnimation`，并设置救援 5 次解锁。

`Releases` 下分别有 `Vojkan-Cobro-1.0.4.zip`、`Vojkan-Jack_Broton-1.0.3.zip`，以及玩法 README、Changelog、manifest、icon。`CorrectRambro` 与 `BroMaxForeheadFix` 只有官方角色贴图修正文件，不属于自定义角色工程。

Gorzontrok 的三个角色和两个仓库的五角色清单见 [14 文档](14-Gorzontrok三bro源码地图.md)。

核心结构：角色类重写 `CustomHero` 行为，配套类管理独立物体，代码加载额外贴图/音效；JSON 负责角色注册、基础参数、素材路径与解锁。它们沿用 [02 文档](02-最小可运行角色与文件结构.md) 的角色管线。

## Cobro 层 1：JSON 接线

- `parameters` 放 Sprite/GunSprite/SpecialIcons/Avatar、GunSpriteOffset 与 `BetterAnimation`。
- `cutscene` 可换过场动画类型(`anim: "Intro_Bro_Bronan"` 不只 Rambro)、加 `barkPath` 语音。
- `beforeAwake` 调 speed/fireRate/originalSpecialAmmo(6 发)。

## Cobro 层 2：冲锋枪 / 手枪模式切换材质

关键手法：**JSON 挂冲锋枪模式，手枪模式的身体与枪图在 `InitializeResources` 中用 ResourcesController 加载，运行时换 Renderer.material**。源码变量虽然叫 `stealthMaterial`，此处实际用于拔出手枪的特技模式，不能据变量名认定角色具有潜行能力。

```csharp
// Awake 调用 InitializeResources，dir 是 dll 所在文件夹
this.normalMaterial     = base.material;                                  // JSON 已挂的 sprite.png
this.stealthMaterial    = ResourcesController.GetMaterial(dir, "spriteSpecial.png");
this.normalGunMaterial  = this.gunSprite.meshRender.material;
this.stealthGunMaterial = ResourcesController.GetMaterial(dir, "gunSpriteSpecial.png");
// 切形态: GetComponent<Renderer>().material = this.stealthMaterial;
```

- 同一 bro 可有多套 1024×512 身体图,布局一致,运行时整体切换。
- 换材质必须用 `.material` 实例,不能动 sharedMaterial。

`PressSpecial` 开始拔枪或在手枪模式发射；`AnimateSpecial` 根据 `specialAnimationTimer / frameRate` 计算动画帧。`FireSpecialWeapon` 消耗六发弹药中的一发；主攻击可触发 `ReverseSpecialMode` 收枪，恢复正常材质后延迟恢复主武器。`usingSpecialFrame` 字段也存在，但当前动画的帧索引实际由独立计时器推导。

## Cobro 层 3：方法重写（本地主类 837 行）

| 重写 | 干什么 | 备注 |
|---|---|---|
| `Awake` | 加载材质/音效、**用代码 new 弹射物 prefab**(`MeshFilter+MeshRenderer+SpriteSM+自定义类`)、设 `gunSpriteHangingFrame = 9` | 挂吊帧号按自家 spritesheet 调 |
| `UseFire` | 自定义主武器:换弹射物、改弹速 `480`、寿命 `life=0.19f`、随机散布、音效 | 核心是 `ProjectileController.SpawnProjectileLocally(...)` |
| `PressSpecial` | 检查弹药/状态 → 拔枪，或在手枪模式发射 | 拔枪动画完成后设置 `specialActive` |
| `AnimateSpecial` | **独立 `specialAnimationTimer`**推导帧号 + 拔枪/收枪阶段状态机 | 不直接依赖普通动作的 `base.frame` |
| `SetGunPosition` | 枪口位置微调,甚至单独处理滑索上左/右的偏移 | 细节打磨层 |
| `StartCustomMelee` / `AnimateMelee` / `RunKnifeMeleeMovement` / `PerformKnifeMeleeAttack` | 整套自定义近战连段、判定、位移 | 见 03 文档近战流程 |
| `Update` | 特技动画计时、模式/主武器延迟状态管理 | 处理死亡/上直升机分支 |

## Cobro 层 4：独立罐子与内容资产

[CoorsCanProjectile.cs](../../VojkanSE87-BroforceMods/Cobro/Cobro/CoorsCanProjectile.cs) 中的类名是 `CoorsCan`，继承 `Grenade`（本地 442 行）。`Awake` 中构造带 MeshFilter、MeshRenderer、SpriteSM 和 CoorsCan 的对象作为 prefab。站立近战喝酒并扔罐；罐子碰撞、滚动时播放音效，碰撞后调用 `Map.AttractMooks` / `Map.AttractAliens` 吸引敌人。这是独立物体逻辑，不是只改角色动画。

- `sounds\` 文件夹 wav,代码里静态 AudioClip 数组加载(见 04 文档)。
- **`rambroLabeled.xcf`(GIMP 源文件)= 带标签的 Rambro 1024×512 spritesheet**——作者标注了每一帧是什么动作。想知道每帧含义,直接用 GIMP 打开这个文件看标注,比在游戏里猜快得多。
- `spriteSpecial.png`/`gunSpriteSpecial.png` = 手枪模式整版图；`CoorsCan.png` = 罐子贴图。

## Jack Broton：飞刀、药水与语音近战

源码目录为 `Jack Broton\Jack Broton\`，本地 `Bro.cs` 1152 行 + `BootKnife.cs` 404 行。`JackBroton.json` 的初始特技为 3，图标数组为 `special.png`、`special2.png`、`special2.png`。这些是 UI 图标，不能把 `special2.png` 写成第二套身体变身图。

`PressSpecial` 保存扣弹前的 `preDecrementAmmo`：大于 1 时投飞刀，等于 1 时喝药水。`AnimateSpecial` 分别选择地面/墙上动画，在动作帧触发投刀或 7.5 秒无敌与慢时间效果，再结束状态；`hasThrownProjectile` 防止重复触发。投刀动画期间会受伤，不能把三次特技都描述为无敌。

站立近战选择 `question1.wav` 至 `question7.wav` 播放提问；移动近战改为拳击。`questionPool` / `lastQuestionIndex` 管理语音选择，`Update` 另有流汗计时与特技取消逻辑。

比 Cobro 多展示的模式:

- **自定义 Projectile 子类**(不只是改参数):`class BootKnife : Projectile`，在 `Awake` 里自设 SpriteSM 帧属性（`pixelDimensions = 34×16`、`width/height` 显式设）、伤害（`damage = 8`）、寿命（`life = 9f`）和连续碰撞检测（`rb.collisionDetectionMode = Continuous`）；类字段维护单位穿透上限 `maxPenetrations = 19` 和墙体穿透上限 `maxWallPenetrations = 14`。静态字段缓存 Material 防重复加载。
- **更多重写点**:`Damage`(自定义受击反应)、`CancelMelee`、`TryMeleeTerrain`、`OnDestroy`、`AfterPrefabSetup`、`invulnerable` 属性重写。
- `BootKnife` 的 `Update` 自行推进位置、减寿命、命中敌人并反弹敌方弹射物；`Bounce` 在开启穿墙且未达到上限时累加计数，否则注销并销毁。不能只复制 `damage/life` 就视为保留了完整飞刀行为。
- `Damage`、特技状态重置与 `OnDestroy` 处理取消、无敌效果和清理；做类似玩法时要连同这些分支阅读。

## 开发新角色时如何参考

两套工程使用 `.mod.json` + 角色 JSON + DLL + `_ModContent` 的标准管线，构建入口为仓库 `Scripts/BroforceModBuild.targets`。可以按需参考：独立物体/弹射物（CoorsCan、BootKnife）→ 特技状态机（03）→ 近战（03）→ 音效（04）→ 多套材质（本文层 2）。同为 .NET Framework 3.5 工程也需匹配实际 BroMaker/游戏 API 与编译器；本次整理没有编译、部署或核对发布 ZIP 与源码二进制是否对应。
