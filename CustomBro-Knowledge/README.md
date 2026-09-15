# Broforce 自定义角色(Custom Bro)编程开发知识库

来源: [Bro-Maker Wiki - Creating a Bro Via Programming](https://github.com/Gorzontrok/Bro-Maker/wiki/Creating-a-Bro-Via-Programming) 及其全部子页面(2026-09抓取)。
结合本地资源交叉验证:`Bro-Maker`(BroMakerLib 源码)、`CustomBros`(成品示例)、`RocketLib`。

## 本地资源对照表(重要)

| 本地路径 | 内容 |
|---|---|
| `E:\Study\C#\Bro-Maker\BroMakerLib\` | BroMakerLib **完整源码**(直接看 `CustomHero.cs` 比查文档更快) |
| `E:\Study\C#\Bro-Maker\BroMakerLib\CustomObjects\Bros\CustomHero.cs` | 自定义角色基类(lifecycle flags、变体去重、tint 管理都在这) |
| `E:\Study\C#\Bro-Maker\BroMakerLib\CustomObjects\Projectiles\` | `CustomProjectile / CustomGrenade / CustomSachelPack / CustomPockettedSpecial` |
| `E:\Study\C#\Bro-Maker\BroMakerLib\ResourcesController.cs` | 加载 sprite / AudioClip 的核心工具类 |
| `E:\Study\C#\CustomBros\Bronobi\` | 成品示例 mod(源码 + `_Mod` 内容文件夹:JSON/贴图/音频) |
| `E:\Study\C#\CustomBros\Scripts\BroforceModBuild.targets` | 自动编译→复制 dll→安装到 BroMaker_Storage 的 MSBuild targets |
| `E:\Study\C#\CustomBros\SantaBraus\`、`TonyBrotana\` | 另两个成品示例 |
| `E:\Study\C#\RocketLib\` | RocketLib 源码(DrawDebug、自定义按键绑定等) |
| `Broforce_src\GameAssets\` | **从游戏 assetbundle 导出的 598 张官方贴图**(95 张角色身体图 / 94 张枪图 / 头像 / 图标),画贴图时的一手参考。清单 `index.csv`,说明见该目录 README 与 [11 文档](11-导出官方贴图与资源包.md) |
| `CustomBro-Knowledge\templates\ExportGameSprites.py` | 上表的导出脚本(UnityPy),`--all` / `--bundle` 可换包 |
| `E:\Study\C#\Bro-Maker\images\Example Spritesheets\rambroAnimLabeled.png` | **帧号→动作**的权威标号图(白色数字标在每个动作块起始帧) |
| `E:\Program\Aseprite\Aseprite.exe` | Aseprite 1.3.18.1(**画贴图用这个**,不要写脚本生成) |
| `E:\Study\Python\aseprite-mcp` | Aseprite MCP server(配置在 `~/.claude.json`,需 `ASEPRITE_PATH` 指向上面那个 exe) |

## 文档目录

1. [开发环境与项目搭建](01-开发环境与项目搭建.md) — 必装工具、VS工程、引用DLL、构建系统
2. [最小可运行角色与文件结构](02-最小可运行角色与文件结构.md) — CustomHero 类 + bro JSON + mod.json
3. [类结构与核心方法重写](03-类结构与核心方法重写.md) — 类继承体系、Primary/Melee/Special 调用链、lifecycle
4. [精灵与音频加载](04-精灵与音频加载.md) — ResourcesController、透明像素、换材质
5. [自定义弹射物](05-自定义弹射物.md) — 三大弹射物类、prefab 创建与生成
6. [JSON参数与变量参考](06-JSON参数与变量参考.md) — parameters 表、before/afterAwake/Start 变量、内置 grenade/projectile 名称
7. [调试与常见问题](07-调试与常见问题.md) — 日志、RuntimeUnityEditor、DrawDebug、FAQ 精选
8. [VojkanSE87 源码解析](08-VojkanSE87源码解析.md) — Cobro 高度自定义的三层结构、多形态材质切换、带标签 Rambro spritesheet
9. [alexneargarder 六 bro 源码地图](09-alexneargarder六bro源码地图.md) — 6 个成品 bro 的玩法与钩子对照表、60+ 可重写点速查
10. [实战笔记 Broseidon 一二期踩坑](10-实战笔记-Broseidon一二三期踩坑.md) — JSON 留空坑、gunSprite 手臂、csc3.5=C#3、反射核签名、HSV 换色、部署子目录
11. [导出官方贴图与资源包](11-导出官方贴图与资源包.md) — assetbundle 在哪、UnityPy 解包两个坑、帧布局规格、帧号→动作对照
12. [用 Aseprite 画角色贴图](12-用Aseprite画角色贴图.md) — **画贴图的唯一正确路线**;Aseprite MCP 配置与验证陷阱、定规格→一次一个动作→每张确认的工作流、脚本路线为什么被放弃

## 一句话总结

最小可用的自定义角色 = 一个继承 `CustomHero` 的类(带 `[HeroPreset("名字", HeroType.Rambro)]` 特性)
+ 一个 bro JSON(指定 `characterPreset` 与贴图参数)
+ 一个 `.mod.json`(注册 dll 与 bro JSON)
,然后把三者放进游戏的 `BroMaker_Storage` 文件夹。默认行为 = Rambro 全套动作,之后靠重写方法定制。

---

## ⚠️ 动手画贴图前先读这条

**不要写脚本去"生成"或"改造"贴图。** 区域换色、贴零件、参数化拼姿势这几条路 2026-09-13 全部实测否掉(整个工程被删),判定标准是**骨架是不是自己画的**——换了颜色不算。

**正确路线**:用 Aseprite MCP 画,先定死角色规格,再一个动作一张对照图确认。完整流程与踩坑见 [12 文档](12-用Aseprite画角色贴图.md)。
