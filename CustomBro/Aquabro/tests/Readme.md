# Aquabro 测试与诊断工具

本目录供开发检查，不参与角色运行，也不会由 `BuildAssets.ps1` 自动执行或部署到 `_Mod`。当前通过结果见 [当前状态](../当前角色实际应用状态.md)，旧现场、版本与修复过程见 [修复历史](../docs/修复与验证历史.md)。

## 常用自动回归测试

以下脚本在游戏外读取生产源码，用模拟游戏对象编译检查，无需启动 Broforce；通过会打印 `PASS`，编译或断言失败会停止。离线通过只覆盖相应用例，实机与远程联机另行验证。

| 入口 | 检查内容 |
| --- | --- |
| [Test-Charge.ps1](Test-Charge.ps1) | 蓄力／投掷／冷却／打断与非拥有端过滤；长按近战丢兵、短按前刺、不可丢／无目标、暂停及多帧率交接 |
| [Test-WaterBlastZones.ps1](Test-WaterBlastZones.ps1) | 贴身／高速戟尖、水花可见接触、内外圈、墙后单位／物件／弹丸、迟入目标与 Boss 去重 |
| [Test-Spring.ps1](Test-Spring.ps1) | **四技能综合回归**：地形、操控、伤害、Boss、拦弹、毒水、蒙版、HUD；不只测涌泉 |
| [Test-SpriteInitialization.ps1](Test-SpriteInitialization.ps1) | 生成和直升机运输时身体／武器贴图初始化 |
| [Test-FrameStep.ps1](Test-FrameStep.ps1) | 暂停、逐帧调试、移动平台与击掌加速的时间步 |

## 如何运行

从 Aquabro 项目根目录用 PowerShell 执行；需要系统 .NET Framework 3.5 的 `csc.exe`：

```powershell
./tests/Test-Charge.ps1
./tests/Test-WaterBlastZones.ps1
./tests/Test-SpriteInitialization.ps1
./tests/Test-FrameStep.ps1
```

水花测试读取当前 `_Mod/water-rocket-explosion.png` 和 `src/ChargedWaterPixelMask.cs`，两者必须配套。

四技能综合测试另需 Aseprite 和 `0Harmony.dll`。可在 `LocalBuild.json` 中配置 `AsepritePath`、`ReferenceDirectory` 后运行 `./tests/Test-Spring.ps1`，也可直接传参：

```powershell
./tests/Test-Spring.ps1 -AsepritePath 'aseprite' -ReferenceDirectory './.aquabro-buildrefs'
```

`ReferenceDirectory` 须含 `0Harmony.dll`；`aseprite` 可改为实际程序路径。首次配置及完整依赖见 [构建说明](../docs/机制实现细节.md#构建关闭游戏与部署)。

普通脚本的临时源码和可执行文件结束后清理。综合测试将预览 PNG／蒙版保留在被 Git 忽略的临时预览目录；不会替换正式 DLL、部署资源或覆盖源稿。

## 综合测试文件与覆盖范围

下列用例由 `Test-Spring.ps1` 统一编译执行；水花另编译 `BossWaterDamageTests.cs`，两者共用 `BossDamageTestStubs.cs`。

| 文件 | 主要覆盖 |
| --- | --- |
| [SkillSpringTests.cs](SkillSpringTests.cs) | 综合入口与模拟对象；七柱展开、低台阶／牢笼／高墙、共享七击和整组污染、狂潮、拦弹、切换与次数、HUD |
| [SkillSharkTests.cs](SkillSharkTests.cs) | 潜行操控、梯子／电梯／Boss 顶面支撑；多帧率重力飞行、深坑／无底洞、阻墙、镜头与暂停；固定水花、59 格本体与蒙版、鼻尖触水和入水停破坏 |
| [SkillMaelstromTests.cs](SkillMaelstromTests.cs) | 普通兵／载具／露头虫类落点、分散吸附、普通／重装尸体吸力及死亡状态、重装控制、拆墙扩路线、收尾显示帧与伤害一致 |
| [SkillDamageTargetTests.cs](SkillDamageTargetTests.cs) | 单位、Boss 和物件的伤害资格；毒水两阶段、原生腐蚀、免疫与普通伤害回退 |
| [SkillOcclusionTests.cs](SkillOcclusionTests.cs) | 墙后狂潮／猎杀物件、三种屏障拦弹与酸池污染，含贴身薄墙、弹丸先撞墙和拆墙恢复 |
| [BossSkillDamageTests.cs](BossSkillDamageTests.cs) | 部件、代理链与未知接收者；阶段保护、关联去重、伤害预算；狂潮载具挡停／击杀恢复和锯齿打落 |
| [BossWaterDamageTests.cs](BossWaterDamageTests.cs) | 普通／蓄力完整扫掠、直击优先、地形与水花去重、可见像素、遮挡和大型部件边缘 |
| [BossDamageTestStubs.cs](BossDamageTestStubs.cs) | 非 Unit 的 Boss 模型，不提供帧内防重复，以暴露重复请求 |
| [PixelMaskTests.cs](PixelMaskTests.cs) | 公共像素查询的边缘／斜角、反向矩形、空帧及世界坐标平移、镜像、缩放、水线裁剪 |

维护时保留这些关键边界：

- **Boss**：坦克、蛞蝓头部、撒旦头部／六臂及关联部件分别检查；装甲、关闭碰撞、免疫阶段不能被身体兜底绕过。持续伤害覆盖 30／60／120 FPS 和涌泉 28、漩涡 36、狂潮 26 的基础总额。
- **虫类落点**：`CheckWormDestination` 覆盖主体未登记／远离头部、已登记仍按露出部位、头部下缘埋土、最近目标和护甲／关闭／无效／墙／无地面／距离过滤，不能只用手动登记的虫代替。
- **漩涡路线与收尾**：`CheckWallExpansion` 覆盖双向、15～120 FPS、不同时刻拆墙、无跳位和原定寿命；`CheckFinishVisualFrame` 按显示帧 24／跳帧 26 检查正式 PNG 接触及单次 9 点收尾。
- **鲨鱼画面**：飞行末格与入水本体对齐，鼻尖到水线；接触首格仅本体、前后水层透明，下一格有水花。鼻尖间隙为零不能替代首帧水线检查，不恢复旧版按地形裁切水花的断言。
- **毒水**：清水→两级渐变、50%／100% 概率、来源去重、暂停、原生覆酸与停击飞／吸附；同次涌泉先吸毒再伤害，延迟柱继承状态。渐变从原 PNG 解码，不能依赖渲染缓存的 CPU 像素。
- **死亡酸液残留**：`CheckSlimeSources` 覆盖三种水体、双向／两种缩放、地板／墙／天花板四个附着面、隔墙后拆墙、血色／伤害／有效性筛选、来源去重和两级升级，不消耗残留。`Probe-SlimeWaterLive.cs` 默认只读现场；暂停时传 `verify=true`，用临时原生酸液检查 Setup 登记和实际 PNG 变绿，结束清理全部临时对象。

## 本地可选的现场诊断文件

这些脚本供 MCP 等游戏内脚本环境加载，使用真实 Unity／Broforce 对象，不是命令行回归。除 [Preview-SharkBallistics.cs](Preview-SharkBallistics.cs) 外，下列现场文件可能仅本地保留、不随发布包提供；先检查文件和当前方法签名。

| 文件 | 用途／限制 |
| --- | --- |
| `Inspect-ExplosionDamage.cs`、`Inspect-RocketReference.cs` | 读取原版爆炸伤害和资源参数 |
| `Inspect-MaelstromControl.cs`、`Inspect-MaelstromDamage.cs`、`Inspect-SkillRenderDepth.cs` | 读取吸附、受伤或渲染状态；旧时序须核对 |
| `Probe-MaelstromPlacement.cs` | 只读敌人落点与遮挡 |
| `Verify-MeleeThrowLive.cs` | 不暂停、正常速度，通过模拟近战按键检查左右丢兵、短按、重装兵和空放；临时平地、真实敌人，关闭目标 AI，结束还原设置与清理对象 |
| `Verify-MaelstromCorpseLive.cs` | 正常速度，以原版伤害杀死普通／重装兵，双向调用实际吸力检查离地和死亡状态；不推进完整技能伤害周期，结束清理并还原 |
| `Verify-MaelstromVehiclePlacement.cs` | 暂停，用原生巨型坦克检查双向落点／高墙；临时移动实体并创建技能 |
| `Verify-UtilityMaelstromPlacement.cs` | 暂停，通过 Utility Mod 生成敌人并比较索敌和实际释放位置，不结算伤害；完整流程与结果见 [验证指南](../docs/UtilityMod验证指南.md#mcp-辅助核对与本次证据) |
| `Probe-MaelstromHeavyControl.cs` | 使用脚本复制的逻辑检查真实重装兵眩晕／速度，非当前生产方法 |
| `Probe-ChargedVisualContact.cs` | 原生敌人副本的可见水花与受击对照 |
| `Probe-BossPieceDamage.cs`、`Probe-SatanPartRouting.cs` | 拦截并记录伤害请求，不能当成实际扣血验证 |
| `Probe-BossPieceHealth.cs` | 临时原生部件的普攻／四技能扣血与无敌阶段 |
| `Probe-SlugHeadHealth.cs` | 真实蛞蝓头部扣血；历史执行超时，未取得有效结果 |
| `Probe-SatanDamageSmoke.cs` | 临时开放真实头部代理后核对扣血并还原；历史手臂计数为 0，无手臂覆盖 |
| `Probe-SharkBossSupport.cs` | Boss 顶面找地／移动的坐标副本，不创建技能或伤害 |
| `Probe-SpringPlacement.cs`、`Probe-SpringCagePlacement.cs` | 查看七柱规划和阻挡；前者 `checkSpawn` 可临时生成后立即清理 |
| `Probe-GenericCollisionDamage.cs` | 临时未知接收者和多层代理的实际伤害请求／扣血 |
| `Probe-WaterMechanics.cs` | 原生士兵副本、偏离身体代理与漩涡收尾显示帧／扣血 |
| `Probe-MaelstromExpansion.cs` | 真实墙体拆除后路线扩展、连续性、寿命和暂停 |
| `Probe-WaterOcclusionLive.cs` | 真实墙、原生弹丸／酸池／物件的有墙与移墙对照 |
| `Probe-WaterBlastSceneOcclusion.cs` | 普通／蓄力物件与火箭遮挡；`verify=true` 断言墙后零伤、移墙恢复 |
| `Probe-SharkBreachTerrain.cs` | 只读鲨鱼状态和附近地面，不推进鲨鱼 |
| `Probe-SkillAdjustments.cs` | 旧共享次数与分散吸附探针；鲨鱼部分已过期 |
| `Probe-SharkBallistics.cs`、`Probe-SharkEntry.cs` | 暂停，真实地面的连续飞行／固定水花／入水；后者 `verify=true` 检查鼻尖、材质和入水停破坏，首帧水线另作画面对照 |
| `Preview-SharkBallistics.cs` | 暂停，以 `x`、`y`、`facing`、`age` 定格生产鲨鱼供截图；见下方参数 |
| `Verify-ProjectileDamage.cs`、`Verify-RocketWaterBlast.cs` | 实际弹丸、木箱、水爆地形与重复命中检查 |

预览的 `entry=true` 从指定落点上方 1px 开始，向下速度 240px/s、横速 0、跃出计时 0.7 秒；此时 `age` 表示从该预置状态继续推进的时间。结束调用 `PreviewSharkBallistics.Unload()`；重新编译可能保留不同程序集的静态实例，须逐个调用已加载类型的 `Unload()` 再卸载。

现场脚本可能生成弹丸、提交伤害或改对象状态，应在测试关按各脚本要求准备并还原。结束清理临时对象和补丁后默认关闭游戏；部署前同样须确认游戏已退出。逐种敌人的观察与记录方法见 [Utility Mod 验证指南](../docs/UtilityMod验证指南.md)。
