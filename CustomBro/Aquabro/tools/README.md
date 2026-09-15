# 海王贴图工作文件

2026-09-16 直升机挂载贴图修复：MCP 逐层隐藏确认场景中只有一个海王实例，异常来自身体与武器 `SpriteSM` 在恢复 1024×1024 自定义贴图后仍沿用 Rambro 512／64 高图集的网格 UV，分别一次采样 64／512 像素，因而压入多行海王素材。仅把 `pixelDimensions` 与 `tempUV` 改成 32×32／`0.03125` 不会更新已经提交给网格的四个 UV 顶点；当前 `Helicopter.SetBrosPositions()` Harmony 后置补丁在恢复材质、纹理、尺寸和网格大小后，再调用 `CalcUVs()` 与 `UpdateUVs()`。关键源文件检查确认 `held-v2` 的身体层与武器层拆分正确，武器格只含持戟手臂和三叉戟，没有第二个身体。此前的 `Helicopter.Update()` 后置补丁与原版 `Update -> SetPosition -> SetBrosPositions` 常规调用链重复，已经删除。清理版 DLL 已重新构建部署，攻击 390 项、水墙 16 项、移动动画 1856 项检查通过；游戏启动正常，直升机画面等待当前 DLL 的独立复测。误判和过时方案见 [反馈记录](haiwang_trident_反馈记录.md#本轮误判与过时修复复盘)，部署详情见 [helicopter-texture-fix-deployment.json](traversal-v2/helicopter-texture-fix-deployment.json)。

2026-09-16 关键文件目录整理：正式跑步主稿现位于 `01_动作主稿/haiwang_trident_run-v3.aseprite`，原先仅包含该文件的 `04_跑步微调备用` 已删除。迁移没有修改源稿像素，身体、武器和飞行戟贴图不需要重新生成。v2 仍停用并保存在 `../backups/before-key-sources-run-v3-20260915-2230/`。此前重建已将跳跃第 9 帧同步到 v3 首帧，并纳入滑索第 13–18 帧完整长度三叉戟修订。最新检查见 [key-source-checks.json](traversal-v2/key-source-checks.json)，跑步预览见 [run-v3.gif](traversal-v2/run-v3.gif)。下方带日期的 v2 记录仅保留历史制作过程。

2026-09-15 绳索蓄力手臂修订已接入游戏武器图集：滑行身体 524–529 的回拉、蓄力与蓄满共 18 格，取自独立蓄力稿第 2／3／4 帧。12 个预览帧与游戏身体、武器合成一致；其余武器格及身体 PNG 保留。检查见 [zipline-charge-integration.json](traversal-v2/zipline-charge-integration.json)，本次 DLL 与武器贴图的部署结果见 [zipline-charge-deployment.json](traversal-v2/zipline-charge-deployment.json)。

2026-09-15 按用户要求关闭海王专用爬梯动画及进出梯过渡，恢复原版站姿爬梯逻辑。站姿替换为 171–173 格的停驻逻辑也受爬梯开关控制。爬梯独立稿的 20 帧、总稿与图集素材保留备用。

2026-09-15 用户微调后重新部署：采用总稿在 08:01:34 保存的最新内容，重新生成贴墙／悬挂／攀爬／爬梯／滑索五份独立 Aseprite（10／18／20／20／18 帧），保留六层、中文标签与帧时长。86 组身体与武器图集合成匹配总稿，DLL 和两张 PNG 已重新构建部署，部署文件校验一致；总稿原文件保留。检查见 [user-sync-checks.json](traversal-v2/user-sync-checks.json)，部署见 [deployment.json](traversal-v2/deployment.json)。重启游戏后查看本次微调。

2026-09-15 爬梯、滑索像素修订：爬梯 20 帧按 Rambro 双臂轮廓重画，滑索 18 帧采用 Rambro 换手及 Predabro 实际单手握持像素。两份独立稿、总稿 49–86 帧、38 个身体格、162 个配对武器格和本地 PNG 已同步；当时未构建部署，本次部署见上方记录。对照：[爬梯](archive/ladder-zipline-pixels/ladder-pixel-compare.png)、[滑索](archive/ladder-zipline-pixels/zipline-pixel-compare.png)。

根目录保留 16 份当前编辑源文件及三份说明。`traversal-v2/` 保留当前同步脚本、帧映射、预览与最新检查及部署记录。旧稿、对照导出和历史制作记录按阶段归档，文件位置见 [归档索引](archive/README.md)。

2026-09-15 像素修订：贴墙、悬挂、攀爬已按 Rambro 对应动作的像素轮廓修正，共 10／18／20 帧，外观采用当前 held-v2。三份独立稿、总稿前 48 帧、身体图集 38 格及本地 `_Mod/sprite.png` 已同步；当时未重新构建或部署，本次部署见上方记录。查看 [逐帧对照](archive/rambro-refit/rambro-refit-preview.png)。

## 可编辑源文件

| 文件 | 用途 |
| --- | --- |
| [haiwang_trident_held-v2.aseprite](haiwang_trident_held-v2.aseprite) | 用户最新微调的持戟站姿主稿；同步到站立图集与跳跃 v2 第 18 帧 |
| [haiwang_trident_run-v3.aseprite](haiwang_trident_run-v3.aseprite) | 当前八帧跑步主稿，对应关键文件目录 `01_动作主稿`；普通跑步、冲刺和落地接跑步使用此版本，保留原始分层与像素 |
| [haiwang_trident_jump-v2.aseprite](haiwang_trident_jump-v2.aseprite) | 前进跳跃、原地跳跃及落地衔接，共 18 帧；仅海王，六个可编辑图层，按上升、下落、落地分标签 |
| [haiwang_trident_crouch-v2.aseprite](haiwang_trident_crouch-v2.aseprite) | 用户微调后的蹲持与八帧蹲走，共 9 帧；仅海王，六个主体图层及新增握点修补层，含“蹲持”“蹲走”标签；已接入图集及代码 |
| [haiwang_trident_traversal-v2.aseprite](haiwang_trident_traversal-v2.aseprite) | 贴墙、悬挂、攀爬、爬梯为空手，滑索持戟；32×32、86 帧、六个可编辑图层、五个中文标签 |
| [haiwang_trident_wall-v2.aseprite](haiwang_trident_wall-v2.aseprite) | 贴墙独立动画，空手，10 帧；对应总稿 1–10 帧；已按 Rambro 76–80、86–90 格修正比例 |
| [haiwang_trident_hanging-v2.aseprite](haiwang_trident_hanging-v2.aseprite) | 悬挂独立动画，空手，18 帧；对应总稿 11–28 帧；已按 Rambro 107–124 格修正比例 |
| [haiwang_trident_climbing-v2.aseprite](haiwang_trident_climbing-v2.aseprite) | 攀爬独立动画，空手，20 帧；对应总稿 29–48 帧；已按 Rambro 76–95 格修正比例 |
| [haiwang_trident_ladder-v2.aseprite](haiwang_trident_ladder-v2.aseprite) | 爬梯独立动画，空手，20 帧；对应总稿 49–68 帧；当前停用，素材保留备用 |
| [haiwang_trident_zipline-v2.aseprite](haiwang_trident_zipline-v2.aseprite) | 滑索独立动画，保留持戟，18 帧；对应总稿 69–86 帧；换手参考 Rambro，收势使用 Predabro 单手握持像素 |
| [haiwang_trident_zipline_charge.aseprite](haiwang_trident_zipline_charge.aseprite) | 绳索长按蓄力独立修订稿，32×32、12 帧、六层；前臂和手指采用 Predabro 原像素重配色，握点收至肩旁，蓄满保持同一手臂；第 2／3／4 帧已接入对应的 18 个武器格 |
| [haiwang_trident_actions.aseprite](haiwang_trident_actions.aseprite) | 编辑持戟、蓄力、投掷、突刺的武器与手臂动作 |
| [haiwang_trident_body_atlas.aseprite](haiwang_trident_body_atlas.aseprite) | 当前游戏身体图集，对应 ../_Mod/sprite.png |
| [haiwang_trident_gun_atlas.aseprite](haiwang_trident_gun_atlas.aseprite) | 当前游戏武器图集，对应 ../_Mod/gunSprite.png |
| [haiwang_trident_projectile.aseprite](haiwang_trident_projectile.aseprite) | 普通与蓄力飞行戟，对应 ../_Mod/projectiles/Trident.png |

修改分帧动作后，需要同步到对应图集并导出 PNG；图集不会自动跟随源文件更新。持戟站姿、跑步、跳跃、落地、蹲持、蹲走，以及五类地形动作 v2 的游戏取帧接入已完成。最新用户微调已同步素材与图集并重新构建部署，记录见 [反馈记录](haiwang_trident_反馈记录.md)。`RenderTrident()` 按实际显示的身体格和攻击姿态选择武器帧，`TridentMovementAnimation` 记录配对格号、滑索重定向和落地时序。

蹲姿与蹲走 v2 继续使用身体格 6、40–47，配对武器格 64–72，普通持戟取消原版附加位移，蓄力、投掷和近战继续使用原攻击姿态。当前身体、武器图集均为 1024×1024，新增区域用于本轮地形动作。蹲姿源稿帧号、握点与同步要求见 [蹲姿与蹲走说明](haiwang_trident_设计.md#新版持戟蹲姿与蹲走-v2动画稿)。

2026-09-14 用户微调同步：站姿使用本目录最新的 `haiwang_trident_held-v2.aseprite`，同时更新跳跃第 18 帧、身体格 0/106 和武器格 9/62；`archive` 中的同名文件保留为旧参考。蹲姿按用户调整后的图层顺序合成，新增握点修补层一并进入武器图集。两个用户源文件的像素、图层与文件内容均保留。

2026-09-14 蹲姿再次微调同步：已导入 `haiwang_trident_crouch-v2.aseprite` 在 21:53:46 保存的九帧，更新身体格 6、40–47 和武器格 64–72。九组身体与武器合成后逐像素匹配最新源稿；DLL 与两张贴图已重新构建部署，部署文件校验一致。完成部署后更新本次文档记录，游戏内表现待重启实测。

五份地形独立动画于 2026-09-15 从 08:01:34 保存的用户微调总稿重新拆出，每份保留 32×32 画布、六个可编辑图层、原帧时长、cel 数据和对应中文标签，帧号各自从 1 开始。修改独立文件后，需要将对应帧同步回总稿及游戏图集；当前同步脚本仍读取总稿。贴墙第 1–5 帧与攀爬独立稿第 1–5 帧、贴墙第 6–10 帧与攀爬独立稿第 11–15 帧共用游戏格，微调时保持对应帧一致。

## 当前预览

- 绳索蓄力手臂修订：[动态预览](traversal-v2/zipline-charge-fixed.gif)、[Predabro／旧稿／修订对照](traversal-v2/zipline-charge-predabro-compare.png)。原独立稿已更新，旧稿保存在 `archive/before-zipline-charge-arm-20260915-091310/`。

- [五类地形动作总览](traversal-v2/preview.png)，循环预览：[贴墙](traversal-v2/wall.gif)、[悬挂](traversal-v2/hanging.gif)、[攀爬](traversal-v2/climbing.gif)、[爬梯](traversal-v2/ladder.gif)、[滑索](traversal-v2/zipline.gif)

跑步 v2 初稿曾以 [archive/haiwang_trident_held-v2.aseprite](archive/haiwang_trident_held-v2.aseprite) 为参考；当前站姿和新增动作使用本目录最新的 held-v2 主稿。已有跑步文件保留用户微调，沿用八帧脚步，每帧 32×32、80 毫秒，循环 640 毫秒。后手贴身小动，持戟手和戟杆同步；原跑步工程中的两个隐藏参考图层继续保留。

2026-09-14 摆臂修正：缩小后手动作，补齐移动手臂下方的鳞甲、发尾接缝和交叉脚部的漏像素。八帧已检查无封闭透明空洞、无半透明像素；八帧原稿、完整十六帧对照和预览同步更新。修正前文件保存在 `archive/before-run-small-arm-*` 中。

2026-09-14 持戟节奏调整：以用户微调的身体和后侧手臂为基础，将持戟手收至身前，前倾角依次为 25°、27°、29°、30°、29°、27°、25°、24°。手腕沿用兰博跑步参考的 1 像素位移节奏：第 3–6 帧回收，第 7–8 帧抬起，戟杆配合跨步回摆。肩部保留原有起伏，前臂连接随握点调整，手掌始终包住戟杆。

三个戟尖完整落在 32×32 画布内；戟尾收短 1 像素，适应降低后的握点并保持脚底基准以内。八帧原稿、完整十六帧对照及预览已同步，另附与兰博同时播放的八帧 GIF。修改前文件保存在 `archive/before-run-dynamic-trident-*` 中。

逐帧倾角、握点、兰博参考格号与后续同步流程见 [三叉戟设计文档](haiwang_trident_设计.md#新版持戟跑步-v2动画稿)。上述持戟节奏调整仅涉及 `金色三叉戟` 和 `持戟手臂` 图层，保留用户微调的头部、身体、双腿和后侧手臂。

[归档的原尺寸帧条](archive/references/haiwang_trident_run-v2_sheet.png) 为 256×32，配套 [帧数据](archive/references/haiwang_trident_run-v2_sheet.json) 包含时长与 `run` 标签。游戏身体图集的 32–39 格和冲刺 96–103 格使用这套八帧，手臂与三叉戟由武器图集逐帧配合。

完整交错对照工程采用官方 `RAMBO_anim_1024x512.png` 的普通跑步 32–39 格，叠加 `RAMBRO_gun_anim_1024x64.png` 对应的 32–39 格。兰博的持枪手臂和枪位于武器贴图中，必须与身体一起显示。两个兰博图层统一上移 1 像素，脚底基准为 `y=29`，保留原版腾空帧的离地高度。

时间轴顺序为“兰博 1、海王 1、兰博 2、海王 2……”共十六帧，每帧 80 毫秒。兰博身体及持枪层已锁定，海王保留原稿分层与像素，可选择偶数帧微调。旧对照曾采用 40–47 格，这一段在当前新版动画逻辑中用于蹲姿移动，不是普通跑步。

## 说明

- 2026-09-15 比例修正：双臂、躯干和裤靴按原版格逐像素取形，统一上移 1 像素以沿用当前基准；头发、面部、鳞甲和皮肤使用当前 held-v2 的配色。原版刀具及红头带移除，换手时保留手臂经过脸前的遮挡。补齐 3 处金发接缝，脸与手臂之间原版已有的透明留白保留。
- 本轮 48 帧和 38 个身体格已核对，六份 GIF 与源稿一致，图集其余 986 格及总稿 49–86 帧保留。站跑跳蹲、爬梯、滑索源稿与武器图集未改动。检查见 [rambro-refit-checks.json](archive/rambro-refit/rambro-refit-checks.json)；修改前备份为 `../backups/before-rambro-proportions-20260915-064442/`。

- 地形动作 v2：贴墙 1–10、悬挂 11–28、攀爬 29–48、爬梯 49–68 均为空手，完整双臂合入身体图集；滑索 69–86 保留手持三叉戟。源稿第六层现名为“活动手臂”。游戏仍按原版状态、帧号和高度选帧。
- 已核对本机实际 `Assembly-CSharp.dll` 和 `BroMakerLib.dll`：新版刀攀开启，专用爬梯及进出梯过渡已按用户要求关闭；悬挂保留原版 `y=-2` 身体偏移；滑索独立使用 512–529 格。详见 [设计文档](haiwang_trident_设计.md#地形动作-v2贴墙悬挂攀爬爬梯滑索)。
- 2026-09-15 空手修正：前四类动作共 68 帧去除三叉戟，58 个身体格包含完整双臂，旧武器 73–594 格清空；滑索 18 帧及武器 595–756 格保持一致。进梯首帧收拢原来包住戟杆的指尖。86 帧素材核对与移动动画 1856 次检查通过；构建部署结果见 [deployment.json](traversal-v2/deployment.json)，重启游戏后确认显示。
- 地形动作的 [逐帧映射](traversal-v2/traversal-manifest.csv)、[原版方法记录](traversal-v2/native-animation-map.json)、[用户微调同步检查](traversal-v2/user-sync-checks.json) 和 [部署结果](traversal-v2/deployment.json) 保存在 `traversal-v2`；早期空手修订的 [素材检查结果](archive/traversal-initial/asset-checks.json) 已归档。修改前备份为 `../backups/before-traversal-v2-20260914-222759/`。同步脚本及共用帧的微调要求见设计文档。
- 持戟蹲姿 v2 为 32×32，第 1 帧为蹲持，第 2–9 帧为八帧蹲走；选择“蹲走”标签循环预览。源稿每帧 80 毫秒，蹲走一轮 640 毫秒；游戏沿用原版新蹲走的 25 毫秒帧间隔，源稿时长不代表游戏播放速度。头发和脸部沿用跳跃 v2 第 18 帧的像素，蹲走手臂与握点只作 1 像素幅度的变化，参考身体格为 6、40–47。
- 持戟跳跃 v2 的第 1–9 帧为前进跳跃及落地接跑步，第 10–18 帧为原地跳跃及落地接站立。源稿空中每帧 67 毫秒，每套落地三帧分别为 60、80、100 毫秒。第 9 帧与当前跑步首帧一致，第 18 帧与本目录最新持戟站姿主稿一致；后续微调时保持这两个衔接关系，并同步身体与武器图集。各帧的阶段、参考格号和握点记录在工程 cel 数据中。
- [旧版跑步造型、配色与参考约束（归档）](archive/legacy-run/haiwang_run_说明.md)
- [三叉戟操作、参数与游戏图集对应](haiwang_trident_设计.md)
- [三叉戟反馈记录与实测结果](haiwang_trident_反馈记录.md)

## 归档

本次将 22 个文件整理到 [archive](archive/) 下的五个分组：旧版跑步、角色对照与预览导出、地形初稿与空手修订、Rambro 比例修正、爬梯与滑索像素修订。[归档索引](archive/README.md) 列出每个文件的新位置、原位置与用途。

原有恢复稿、静态武器稿、预览合成工程及 `before-*` 备份继续保存在归档目录。历史脚本保留原实现；当前同步入口为 [sync_traversal.lua](traversal-v2/sync_traversal.lua) 和 [sync_zipline_charge.lua](traversal-v2/sync_zipline_charge.lua)，二者共用 [zipline_charge_source.lua](traversal-v2/zipline_charge_source.lua)。

游戏使用 `../_Mod` 中的导出资源；`BuildBro.ps1` 构建 DLL 并部署 `_Mod`，不会读取或转换 Aseprite 源稿。
