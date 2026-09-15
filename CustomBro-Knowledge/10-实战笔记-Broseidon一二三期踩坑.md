# 实战笔记:Broseidon 一/二期(贴图 + 主武器 + 特技)

2026-09-12,`E:\Study\C#\Broforce-Mods\CustomBro\BroTemplate\` 全程踩坑记录。所有结论都已实测(编译通过/游戏现象验证)。

## 一、JSON 变量节留空 ≠ 继承原版 bro 默认值

最小可运行 JSON 里 `beforeAwake` 留空,游戏能跑但 **`fireRate` 落到 0 → 每帧开枪**(攻速爆表)。
修复:显式写 `"beforeAwake": { "speed": 130, "fireRate": 0.1, ... }`。
**规则:凡在乎的数值字段必须显式写,别指望"空 = 原版默认"。**

## 二、gunSprite 各帧含"手臂",不能整帧重画

现象:整帧擦掉画新武器后,站立时手臂消失、移动时手臂上下抽搐。
原因:gunSprite.png 的每一格不是"纯武器"——大部分格带着**持枪手臂的不同姿势**(举枪/垂手/挂吊),格与格之间手臂位置不同。擦掉整格 = 抹掉手臂;贴一个静止的新武器 = 摆臂动画没了 → 抽搐。
修复:不重画,只做**调色板替换**(枪身灰金属系 (114/80/76/63/61/46/30 灰) → 金色系),手臂像素原样保留。
**规则:改 gunSprite 只换色或只改"枪身"像素,手臂帧是动画的一部分。**

## 三、csc 3.5 = C# 3(最重要的一条)

.NET Framework 3.5 自带的 csc 只支持 C# 3 语言级,以下全部编译错误:

| 写法 | 报错 | 替代 |
|---|---|---|
| 字符串插值 `$"text {var}"` | CS1056 意外的字符"$" | `"text " + var` |
| 可选参数默认值 `Log(msg)`(被调方有默认值也没用,编译期就拒) | CS1501 重载无 1 参版本 | 显式传全参 `Log(msg, LogType.Exception, true)` |
| 自动属性初始化器、表达式体成员、null 条件 `?.` 等 C#6 语法 | 各类语法错 | 全部回退 C#3 写法 |

BroMakerLib 源码(VS 工程,新语法)里的调用示例不能照抄,要看方法签名自己补全参数。
`var`、Lambda、LINQ、对象/集合初始化器这些 C#3 特性**可用**。

## 四、游戏/库 API 签名要用反射核实,不能凭文档猜

知识库文档里的代码片段常省略参数。本次用 PowerShell 反射核实(命令模式):

```powershell
$dir = 'E:\SteamLibrary\steamapps\common\Broforce\Broforce_beta_Data\Managed'
Get-ChildItem $dir -Filter 'UnityEngine*.dll' | ForEach-Object { [System.Reflection.Assembly]::LoadFrom($_.FullName) | Out-Null }
$asm = [System.Reflection.Assembly]::LoadFrom((Join-Path $dir 'Assembly-CSharp.dll'))
try { $types = $asm.GetTypes() } catch { $types = $_.Exception.Types | Where-Object { $_ } }
$t = $types | Where-Object { $_.Name -eq 'EffectsController' }
$t.GetMethods() | Where-Object { $_.Name -match 'MuzzleFlash' } |
  ForEach-Object { $_.Name + '(' + (($_.GetParameters() | ForEach-Object { $_.ParameterType.Name + ' ' + $_.Name }) -join ', ') + ')' }
```

本次核实出的签名(与直觉不同的地方加粗):

- `EffectsController.CreateMuzzleFlashRoundEffectBlue(float x, float y, **float z**, float xI, float yI, **Transform parent**)` — 6 参,不是 4 参
- `Map.HitLivingUnits(sender, playerNum, damage, damageType, **range**, **xRange, yRange**, x, y, xI, yI, penetrates, knock, canHeadshot, onlyGroundUnits)` — 有两个重载,一个带 xRange/yRange
- `Map.DamageDoodads(damage, damageType, x, y, xI, yI, range, playerNum, **out hitImpenetrable**, sender)` — sender 在最后
- `Map.HitGrenades(playerNum, range, x, y, xI, yI, **ref grenadeX, ref grenadeY**)` — ref 两个出参
- `Projectile` 基类**没有** `sprite` 字段(BootKnife 也是自己 `GetComponent<SpriteSM>()` 缓存);`X/Y/t/xI/yI/life/projectileSize/damage/damageType/firedBy` 都有
- `Bounce(RaycastHit)` / `HitUnits()` / `TryHitUnitsAtSpawn()` 都是 virtual,撞墙走 Bounce 回调,不用自己写 CheckWalls(那两个 CS1061 就是这么来的)
- `CustomProjectile.SpawnProjectileLocally(FiredBy, x, y, xI, yI, playerNum, **_zOffset = 0f**)` — 7 参(C#3 调用要补第 7 个)
- BroMakerLib 部署版 2.6.2 的 `BMLogger.Log` 有 `(string,LogType,bool)/(object,LogType,bool)/(Exception,bool)`;`LogType` 是 **UnityEngine.LogType**(不是 BroMakerLib.Loggers.LogType)

另外:对 `Assembly-CSharp.dll` 直接 `GetTypes()` 会抛 ReflectionTypeLoadException(依赖没加载),先 `LoadFrom` 全部 `UnityEngine*.dll` 再加载本体,catch 后用 `$_.Exception.Types | Where-Object { $_ }` 拿部分类型。

## 五、贴图规格速查(Bro Template 实测)

> 本节只讲**规格**(尺寸/栅格/帧号)。**贴图怎么画**请看 [12 文档](12-用Aseprite画角色贴图.md) ——
> 结论是**用 Aseprite MCP 手画**,不要沿用本文接下来提到的 `MakeSprites.py` 那套脚本生成管线(已废弃)。

| 文件 | 尺寸 | 说明 |
|---|---|---|
| sprite.png | 1024×512 | **32 列 × 16 行,每帧 32×32,共 512 帧**(实测!按 64×64 切会把 2×2 个角色塞进一格,见下方勘误),主体动画 |
| gunSprite.png | 1024×64 | **32 列 × 2 行,每格 32×32,共 64 格**(实测!不是 16 帧 64×64),**含手臂姿势**,第 9 格是挂吊帧(gunSpriteHangingFrame 默认 6,Cobro 设 9) |
| special.png | 16×16 | 特技弹药图标 |
| avatar.png | 128×64 | **4 帧 32×64**(实测!不是 2 帧 64×64),内容落在每格 row 27~63,手绘 359 色带抗锯齿;4 帧分别是 正常 / 咬牙 / 张嘴(受伤) / 骷髅(死亡) |
| cutscene.png | 256×256 | 入场过场 |
| BronobiForceWave.png | 512×64 | 水墙/波类 FlameWallExplosion 贴图参考尺寸 |
| 自定义弹射物 | 16×16×4 | 放 `projectiles\` 子目录,CustomProjectile 默认约定 |

> **勘误(2026-09-13)**:sprite.png 早期记录成"16×8 帧 / 每帧 64×64"是**错的**。
> 实测列/行像素间距都是 32:1024/32 = 32 列,512/32 = 16 行,共 512 帧、每帧 32×32。
> 判定方法:按 64×64 切第一帧,里面会出现**两个一模一样的角色**(2×2 个 32px 帧)。
> 按 32×32 切,每格才是干净的单角色。写逐帧处理(补发须等)必须按 32px 栅格。
>
> **勘误 2(2026-09-13)**:同一批记录里 **gunSprite.png 与 avatar.png 的规格也写错了**,一并订正:
>
> | 文件 | 错误记录 | 实测 |
> |---|---|---|
> | gunSprite.png | 16 帧 64×64 | **32 列 × 2 行 × 32×32 = 64 格** |
> | avatar.png | 2 帧 64×64 | **4 帧 32×64**(内容在 row 27~63) |
>
> 判定方法与 sprite.png 同源 —— **看内容带是否落在 32 的倍数上**:
> - gunSprite 的行内容带是 `(19,27)` 和 `(52,60)` **两条**,说明有 2 行、行高 32
>   (若真是 64px 帧,64/64=1 行,不可能出现两条 y 带);
>   列内容起点 9, 40, 71, 102, 137… 正好每 32 一组。
> - avatar 的列内容带是 `(0,30) (32,62) (64,94) (96,119)` **四组**,按 64px 切每格会横跨两组;
>   放大看是 4 张不同的脸。
>
> ⚠️ 这两个错误长期没暴露,是因为旧管线只对整张图**换色、从不重新切片** —— 切错了也看不出来。
> 一旦要做逐帧处理(重画 / 补帧 / 精确导出),必须按 32px 栅格。

## 六、HSV 色相重映射处理手绘画

avatar/cutscene 是手绘风(抗锯齿过渡色多),精确 RGB 匹配只能命中少数像素。方案:转 HSV,**只对色相环上的区间重映射**(红 345°~20°→金 43°;绿 60°~180°→青 190°)。
坑:**肤色的色相也在红区**。第二次踩坑=全脸染金。加饱和度阈值(s > 0.75 才转金;肤色饱和度低被排除;头带/绸带高饱和命中)。
实现见 `BroTemplate\MakeSprites.py` 的 `remap_hues()`;body 级精确匹配调色板见 `recolor()`。

## 七、工程基建增量(BuildBro.ps1 本次改了两处)

1. **引用补 `UnityEngine.PhysicsModule.dll`** — `RaycastHit` 在这;只引 CoreModule 会 CS0246。
2. **部署改递归复制** — `_Mod\projectiles\` 子目录(CustomProjectile 弹射物贴图默认约定位置)原来会被漏掉,部署必须保目录结构。
3. Python 贴图管线 `MakeSprites.py`:源图永远从 reference 读、输出到 `_Mod\`,可反复重生成;gunSprite 贴合类操作若按帧画,**paste 别忘了 + i*64 帧偏移**(本次 bug:16 帧全叠在第 0 帧)。
   > ⚠️ **这套管线只适合"占位/换配色"级别**。要画真正属于自己的角色贴图,**别扩展它** —— 2026-09-13 实测三轮被否、工程删除,原因与替代方案见 [12 文档](12-用Aseprite画角色贴图.md)。

## 八、特性效果核对清单(验证时逐项看)

- 换主武器:重写 `UseFire()`,内部自己 Spawn 弹射物 + `SetGunSprite(gunFrame, 0)`
- 换特技行为:重写 `UseSpecial()`(弹药检查/扣减/`FlashSpecialAmmo` 要自己做);动画流程沿用基类 `AnimateSpecial` → 投掷帧自动调 UseSpecial
- 特技弹药数:json `beforeAwake.originalSpecialAmmo`(不是代码字段)
- 波类(FlameWallExplosion 子类):Awake 设 assasinateUnits/maxCollumns/totalExplosions 等,静态 CallMethod 创建 GameObject + Setup(playerNum, owner, DirectionEnum.Any),重写 TryAssassinateUnits 做推飞
