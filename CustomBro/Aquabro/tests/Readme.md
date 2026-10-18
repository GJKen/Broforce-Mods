# Aquabro 测试与诊断工具

本目录用于修改 Aquabro 后检查旧功能是否仍然正常，例如蓄力能否出手、水花碰到敌人是否命中、鲨鱼能否跟随电梯，以及暂停时动画会不会继续推进。

游戏运行和正式构建都不依赖本目录，`BuildAssets.ps1` 不会自动执行这里的测试。只安装角色时无需复制它；公开源码时可以一起保留，方便后续维护。测试文件不应放入游戏的 `_Mod` 目录。

## 常用自动回归测试

优先运行下面的 `Test-*.ps1`。它们在游戏外读取生产源码，用模拟的游戏对象编译并检查逻辑，无需启动 Broforce。

| 入口 | 游戏中的对应表现 |
| --- | --- |
| [Test-Charge.ps1](Test-Charge.ps1) | 普攻按住蓄力、松开投掷、连续攻击、冷却、动作打断，以及非拥有端是否重复造成伤害。 |
| [Test-WaterBlastZones.ps1](Test-WaterBlastZones.ps1) | 三叉戟贴身和高速飞行时能否命中；普通／蓄力水花的可见像素接触、墙体遮挡、内圈伤害与外圈击退、敌人稍后进入水花，以及同一目标是否重复受击。 |
| [Test-Spring.ps1](Test-Spring.ps1) | **四个技能的综合测试**，并非只测涌泉：检查特效像素与判定是否一致、敌人和物件受击、鲨鱼操控与电梯跟随、漩涡控制、涌泉及拦弹、技能图标等。 |
| [Test-SpriteInitialization.ps1](Test-SpriteInitialization.ps1) | 角色刚生成、被直升机运输或尚未开始更新时，身体和武器贴图能否正确初始化，是否出现空引用或取错图集位置。 |
| [Test-FrameStep.ps1](Test-FrameStep.ps1) | 暂停、逐帧调试、移动平台和击掌加速时，角色动画是否使用正确的时间步。 |

以下 C# 文件由 `Test-Spring.ps1` 一起编译，不需要分别运行：

| 文件 | 用途 |
| --- | --- |
| [SkillSpringTests.cs](SkillSpringTests.cs) | 综合测试入口、涌泉和狂潮测试，以及模拟游戏对象。 |
| [SkillSharkTests.cs](SkillSharkTests.cs) | 猎杀潜行的鲨鱼移动、操控、地形、电梯和命中测试。 |
| [SkillMaelstromTests.cs](SkillMaelstromTests.cs) | 漩涡捕获、控制、移动与结束伤害测试。 |
| [SkillDamageTargetTests.cs](SkillDamageTargetTests.cs) | 四技能对不同敌人、Boss 和物件的伤害及过滤测试。 |

## 如何运行

在 Windows PowerShell 中，从 Aquabro 项目根目录执行：

```powershell
./tests/Test-Charge.ps1
./tests/Test-WaterBlastZones.ps1
./tests/Test-SpriteInitialization.ps1
./tests/Test-FrameStep.ps1
```

这四项需要系统提供 .NET Framework 3.5 的 `csc.exe`。水花测试还会读取当前 `_Mod/water-rocket-explosion.png` 和 `src/ChargedWaterPixelMask.cs`，两者应与当前版本配套。

四技能综合测试还需要 Aseprite 和 `0Harmony.dll`。首次配置可复制项目根目录的示例：

```powershell
Copy-Item ./LocalBuild.example.json ./LocalBuild.json
# 编辑 LocalBuild.json，设置 AsepritePath 和 ReferenceDirectory 后运行：
./tests/Test-Spring.ps1
```

也可以不创建本机配置，直接传入参数：

```powershell
# 工具和依赖目录仅为示例，替换为实际位置。
./tests/Test-Spring.ps1 -AsepritePath 'aseprite' -ReferenceDirectory './.aquabro-buildrefs'
```

`ReferenceDirectory` 中需有 `0Harmony.dll`，`aseprite` 需能从 PATH 找到，或改为其可执行文件位置。本机配置、依赖和生成物不随源码推送；完整构建依赖说明见 [角色 README](../README.md#构建关闭游戏与部署)。

测试通过时会打印 `PASS`；断言失败或编译失败时会报错并停止。通过只代表该脚本检查的场景成立，不代表已经完成实机或多人联机验收。

## 运行后会生成什么

- 普通回归脚本在系统临时目录生成测试源码和可执行文件，结束时清理。
- `Test-Spring.ps1` 会导出技能 PNG 和像素蒙版到 `archive/skill-regression/previews/`，该目录被 Git 忽略，结果保留在本地便于查看。即使不推送 `archive/`，脚本也会自行创建这个输出目录，不需要旧归档资料。
- 上述自动回归不会替换或部署正式 `_Mod/Aquabro.dll`，也不会覆盖正式源稿。

## 本地可选的现场诊断文件

下面的 C# 文件仅在本地保留，不随当前发布清单提供。它们使用真实 Unity／Broforce 对象和 `ScriptContext.Logger`，供游戏内脚本执行环境（如 MCP）载入。它们不是普通命令行测试，也不由前面的 PowerShell 回归自动执行。需要按脚本内容准备角色、敌人或暂停状态。

| 文件 | 用途 |
| --- | --- |
| `Inspect-ExplosionDamage.cs` | 读取原版手雷、火箭和 Aquabro 水爆的伤害参考值。 |
| `Inspect-RocketReference.cs` | 读取原版火箭及爆炸特效的参数。 |
| `Inspect-MaelstromControl.cs` | 查看漩涡捕获名单和敌人的移动、控制状态。 |
| `Inspect-MaelstromDamage.cs` | 查看附近敌人的血量、防护和漩涡命中状态。 |
| `Inspect-SkillRenderDepth.cs` | 查看敌人、地形和技能的渲染深度，定位特效遮挡问题。 |
| `Probe-MaelstromHeavyControl.cs` | 在暂停场景中用脚本内复制的控制逻辑检查真实重型敌人的眩晕与速度，再恢复记录的状态；不直接调用当前生产方法。 |
| `Probe-ChargedVisualContact.cs` | 创建临时敌人副本，对比真实水花可见像素和实际受击结果。 |
| `Verify-ProjectileDamage.cs` | 发射实际弹丸并检查木箱是否被破坏。 |
| `Verify-RocketWaterBlast.cs` | 创建碰撞探针，检查水爆对地形的伤害和重复命中。 |

`Probe-*` 和 `Verify-*` 中有调用伤害、生成弹丸或修改对象状态的操作，应在专门的测试场景使用。部分脚本保留了编写时的参数或方法签名，使用前需对照当前源码；不能把它们的旧断言当作当前角色机制说明。
