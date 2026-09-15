# 自定义 Bro 开发 — 当前进度与下一步(供新对话接续)

更新于 2026-09-13。知识库索引见 README.md(01~07 wiki 整理,08/09 参考源码解析,10 实战踩坑,11 官方贴图导出,12 画贴图的正确姿势)。

## 当前状态一览

| 事项 | 状态 |
|---|---|
| **Aquabro(海王)** 工程 | ✅ 在 `E:\Study\C#\Broforce-Mods\CustomBro\Aquabro\`,部署到 `BroMaker_Storage\Aquabro` |
| **Haiwang(海王)** 工程 | ❌ **已删除**。脚本生成贴图的路线走不通,工程与部署副本都已清掉。教训见 [12 文档](12-用Aseprite画角色贴图.md) |
| **官方贴图解包** | ✅ `Broforce_src\GameAssets\`(598 张),详见 [11 文档](11-导出官方贴图与资源包.md) |
| **Aseprite MCP** | ⚙️ 已配好 `ASEPRITE_PATH`,`~/.claude.json` 里已加。**待重启 Claude Code 生效**;重启后调 `create_canvas`(不是 `list_palette_presets`)验证 |
| 画新角色贴图的路线 | ➡️ **改用 Aseprite MCP 手画**([12 文档](12-用Aseprite画角色贴图.md)),不再写脚本生成 |

---

## 一、Aquabro 工程

**角色概念**:主武器 = 三叉戟射潮汐弹(穿透 4 敌 + 击退);特技 = 奔涌波水墙推飞敌人(4 发);近战 = 默认三叉戟突刺。

```
Aquabro\
  BuildBro.ps1            ← 编译+部署(已增强:PhysicsModule 引用、递归部署子目录)
  MakeSprites.py          ← 旧贴图生成管线(Python+Pillow)。⚠️ 这套做法已废弃,见 12 文档
  LocalBroforcePath.props (BroStorageFolderName = "Aquabro")
  src\Aquabro.cs          ← 主类:UseFire 射 TidalBolt / UseSpecial 放 RisingWave
  src\TidalBolt.cs        ← 潮汐弹(: CustomProjectile,穿透/击退/Bounce 销毁)
  src\RisingWave.cs       ← 奔涌波(: FlameWallExplosion,照抄 BronobiForceWave)
  _Mod\                   ← dll + json + 贴图 + projectiles\TidalBolt.png
```

编译部署:`powershell -ExecutionPolicy Bypass -File "E:\Study\C#\Broforce-Mods\CustomBro\Aquabro\BuildBro.ps1"`

**待游戏验证清单**(重启 r2modman 后):
1. 手臂动作正常(站立不消失、移动不抽搐)
2. 金色三叉戟 + 水球弹穿透敌人
3. 特技 4 发水墙推飞敌人
4. 贴图观感——**如果要重做,现在应该用 Aseprite MCP 手画,不要再去改 `MakeSprites.py`**

**已验证历史**:最小工程可生成;攻速异常已修(JSON 留空 ≠ 原版默认,`beforeAwake` 必须显式写 fireRate 等);csc 3.5 = C#3 的语法限制已摸清(见 10 文档)。

---

## 二、关键教训(写码/画图前必读)

**代码侧**(详见 [10 文档](10-实战笔记-Broseidon一二三期踩坑.md)):

1. **csc 3.5 只支持 C#3** —— `$"插值"`(CS1056)、可选参数省略(CS1501)全不可用,API 调用显式传全参
2. **API 签名用 PowerShell 反射 Assembly-CSharp.dll 核实**(命令模板在 10 文档第四节),文档代码常省参数
3. JSON 变量节留空 ≠ 原版默认值,`fireRate` 等会落 0
4. Projectile 基类无 `sprite` 字段,自己 `GetComponent<SpriteSM>()` 缓存;撞墙走 virtual `Bounce(RaycastHit)`
5. 部署要递归(弹射物贴图在 `_Mod\projectiles\` 子目录)

**贴图侧**(详见 [12 文档](12-用Aseprite画角色贴图.md)):

6. **不要用脚本"生成/改造"贴图冒充原创绘制** —— 区域换色、贴头、平移拼姿势三条路都被实测否掉,判定标准是**骨架是不是自己的**
7. **画角色先定"规格文件"**(长相 + 固定配色 + 帧内位置规则),否则每个动作都会飘
8. **一个动作一张对照图确认**,一次只做一个;形状类需求**给候选图让用户挑**,不要用形容词反复猜

---

## 三、机器真实路径

```
游戏本体:        E:\SteamLibrary\steamapps\common\Broforce
r2modman 环境:   E:\Games\Broforce Mods\Broforce\profiles\Broforce\UMM\
  Core\           UnityModManager.dll / 0Harmony.dll / Log.txt
  Mods\           BroMaker-BroMaker\BroMakerLib\BroMakerLib.dll (2.6.2)
                  RocketLib-RocketLib\RocketLib\RocketLib.dll + Newtonsoft.Json.dll
  BroMaker_Storage\   部署目标(现有 Aquabro + VojkanSE87-Cobro)
游戏 Managed:    E:\SteamLibrary\steamapps\common\Broforce\Broforce_beta_Data\Managed
游戏资产包:      E:\SteamLibrary\steamapps\common\Broforce\Broforce_beta_Data\StreamingAssets\
```

工具:

```
官方贴图(已解包):  Broforce_src\GameAssets\            ← 598 张,index.csv 清单
导出脚本:           CustomBro-Knowledge\templates\ExportGameSprites.py
画贴图:             Aseprite 1.3.18.1 @ E:\Program\Aseprite\Aseprite.exe
                    Aseprite MCP @ E:\Study\Python\aseprite-mcp(需 ASEPRITE_PATH)
帧号→动作标号图:    E:\Study\C#\Bro-Maker\images\Example Spritesheets\rambroAnimLabeled.png
```

游戏经 r2modman 启动才加载 profile;改 mod 文件后需重启游戏。BroMaker 加载验证:看 `...\UMM\Core\Log.txt` 的 `[BroMaker] Version '2.6.2'. Loading.`。

---

## 四、参考源码(本机全有)

- `E:\Study\C#\CustomBros\` — Gorzontrok 三 bro(Bronobi 最完整,含 ForceWave/MindControl 波类)
- `E:\Study\C#\VojkanSE87-BroforceMods\` — Cobro(双形态切材质)/ Jack Broton(自定义弹射物)
- `E:\Study\C#\alexneargarder-BroforceMods\` — 6 bro,解析见 [09 文档](09-alexneargarder六bro源码地图.md)
- `Broforce_src\GameAssets\hero\` — **官方 95 张角色动作图**,画新角色前先看这个

---

## 五、下一步

**短期(画新角色 / 重做 Aquabro 贴图)**:

1. 重启 Claude Code,用 `create_canvas` 确认 Aseprite MCP 真的通了
2. 按 [12 文档](12-用Aseprite画角色贴图.md) 第四节,开新对话先把**角色规格**定死、画站立帧
3. 规格确认后一个动作一个动作推,每张对照图确认
4. 全套装好后按 [11 文档](11-导出官方贴图与资源包.md) 的帧布局导出、部署进 `BroMaker_Storage`

**中期(玩法)**:

- 三期:音效(主枪/特技/命中,`ResourcesController.GetAudioClip` + `SoundHolder`,见 [04 文档](04-精灵与音频加载.md))
- 可选彩蛋:进水回弹药 / 加速
- bro 正式名称已统一为 `Aquabro`，包括 `HeroPreset`、JSON 清单、程序集和部署文件夹。
