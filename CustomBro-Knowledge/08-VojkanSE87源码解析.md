# VojkanSE87 源码解析(Cobro)——高度自定义的三层结构

来源:`E:\Study\C#\VojkanSE87-BroforceMods\`(Cobro.nexusmods#47,已装在本机 BroMaker_Storage)

核心结论:**作者的自定义程度全部来自 C# 代码里对 CustomHero 方法的重写 + 额外贴图/音效的代码加载。JSON 只负责基础接线,和我们 BroTemplate 工程的结构完全同构**(`[HeroPreset("Cobro", HeroType.Rambro)] : CustomHero`,Cobro.json 字段与我们的 bro json 一致)。

## 层 1:JSON 接线(与我们现有写法相同)

- `parameters` 只放 Sprite/GunSprite/SpecialIcons/Avatar + GunSpriteOffset。
- `cutscene` 可换过场动画类型(`anim: "Intro_Bro_Bronan"` 不只 Rambro)、加 `barkPath` 语音。
- `beforeAwake` 调 speed/fireRate/originalSpecialAmmo(6 发)。

## 层 2:多套贴图 + 代码切换材质(Cobro 的变身/潜行形态)

关键手法:**JSON 只挂正常形态,潜行形态贴图在代码里用 ResourcesController 加载,运行时换 Renderer.material**:

```csharp
// Awake 里(文件路径 = dll 所在文件夹)
this.normalMaterial     = base.material;                                  // JSON 已挂的 sprite.png
this.stealthMaterial    = ResourcesController.GetMaterial(dir, "spriteSpecial.png");
this.normalGunMaterial  = this.gunSprite.meshRender.material;
this.stealthGunMaterial = ResourcesController.GetMaterial(dir, "gunSpriteSpecial.png");
// 切形态: GetComponent<Renderer>().material = this.stealthMaterial;
```

- 同一 bro 可有多套 1024×512 身体图,布局一致,运行时整体切换。
- 换材质必须用 `.material` 实例,不能动 sharedMaterial。

## 层 3:方法重写(836 行 Bro.cs 的重写清单)

| 重写 | 干什么 | 备注 |
|---|---|---|
| `Awake` | 加载材质/音效、**用代码 new 弹射物 prefab**(`MeshFilter+MeshRenderer+SpriteSM+自定义类`)、设 `gunSpriteHangingFrame = 9` | 挂吊帧号按自家 spritesheet 调 |
| `UseFire` | 自定义主武器:换弹射物、改弹速 `480`、寿命 `life=0.19f`、随机散布、音效 | 核心是 `ProjectileController.SpawnProjectileLocally(...)` |
| `PressSpecial` | 检查弹药/冷却 → `specialActive = true` | |
| `AnimateSpecial` | **独立帧计数器 `usingSpecialFrame`**(防墙跳重置)+ 计时器 + 阶段状态机 | 07 FAQ 同款模式 |
| `SetGunPosition` | 枪口位置微调,甚至单独处理滑索上左/右的偏移 | 细节打磨层 |
| `StartCustomMelee` / `AnimateMelee` / `RunKnifeMeleeMovement` / `PerformKnifeMeleeAttack` | 整套自定义近战连段、判定、位移 | 见 03 文档近战流程 |
| `Update` | 状态管理(潜行计时等) | 记得处理死亡/上直升机分支 |

## 层 4:内容资产

- `sounds\` 文件夹 wav,代码里静态 AudioClip 数组加载(见 04 文档)。
- **`rambroLabeled.xcf`(GIMP 源文件)= 带标签的 Rambro 1024×512 spritesheet**——作者标注了每一帧是什么动作。想知道每帧含义,直接用 GIMP 打开这个文件看标注,比在游戏里猜快得多。
- `spriteSpecial.png`/`gunSpriteSpecial.png` = 变身形态整版图。

## 另一个成品:Jack Broton(1554 行,更重度)

同仓库还有第二个 bro:`Jack Broton\Jack Broton\`(Bro.cs 1151 行 + 自定义弹射物 BootKnife.cs 403 行)。仓库其余两个工程(BroMaxForeheadFix、CorrectRambro)是小型修复 mod,不是 bro。作者在 Nexus 还有更多作品未入库。

比 Cobro 多展示的模式:

- **自定义 Projectile 子类**(不只是改参数):`class BootKnife : Projectile`,在 `Awake` 里自设 SpriteSM 帧属性(`pixelDimensions = 34×16`、`width/height` 显式设)、伤害(`damage = 8`)、寿命(`life = 9f`)、穿墙计数(`penetrateWalls`、`maxPenetrations`)、连续碰撞检测(`rb.collisionDetectionMode = Continuous`)。静态字段缓存 Material 防重复加载。
- **更多重写点**:`Damage`(自定义受击反应)、`CancelMelee`、`TryMeleeTerrain`、`OnDestroy`、`AfterPrefabSetup`、`invulnerable` 属性重写。
- 特技用了第二套贴图 `special2.png`,以及题目池/流汗计时器等独立玩法逻辑(全部写在 bro 类里)。

## 对 BroTemplate 的意义

管线(编译/部署/加载/贴图)与 Cobro 完全同源,不存在额外门槛。高度自定义 = 按需逐层加:自定义弹射物(05)→ 特技状态机(03)→ 近战(03)→ 音效(04)→ 多形态材质(本文层 2)。
