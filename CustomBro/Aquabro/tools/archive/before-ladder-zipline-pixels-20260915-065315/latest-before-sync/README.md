# 海王贴图工作文件

根目录保留当前编辑与查看需要的文件；旧稿、对照图、重复导出和临时文件保存在 [archive](archive/)。

2026-09-15 最新素材：贴墙、悬挂、攀爬已按 Rambro 对应动作的像素轮廓修正，共 10／18／20 帧，外观采用当前 held-v2。三份独立稿、总稿前 48 帧、身体图集 38 格及本地 `_Mod/sprite.png` 已同步；本轮未重新构建或部署。查看 [逐帧对照](traversal-v2/rambro-refit-preview.png)。

## 可编辑源文件

| 文件 | 用途 |
| --- | --- |
| [haiwang_standing.aseprite](haiwang_standing.aseprite) | 旧版独立站姿与外观稿；当前持戟站姿使用下方 held-v2 主稿 |
| [haiwang_trident_held-v2.aseprite](haiwang_trident_held-v2.aseprite) | 用户最新微调的持戟站姿主稿；同步到站立图集与跳跃 v2 第 18 帧 |
| [haiwang_run.aseprite](haiwang_run.aseprite) | 旧版独立八帧跑步，内含隐藏的 Rambro 参考层；当前游戏使用持戟跑步 v2 |
| [haiwang_trident_run-v2.aseprite](haiwang_trident_run-v2.aseprite) | 按新版持戟站姿重制的八帧跑步，身体、后侧摆臂、持戟手臂与武器分层 |
| [haiwang_trident_run-v3.aseprite](haiwang_trident_run-v3.aseprite) | 磁盘上保留的较新跑步微调稿，本轮地形动作的外观参考之一；文件保持原样 |
| [haiwang_trident_jump-v2.aseprite](haiwang_trident_jump-v2.aseprite) | 前进跳跃、原地跳跃及落地衔接，共 18 帧；仅海王，六个可编辑图层，按上升、下落、落地分标签 |
| [haiwang_trident_crouch-v2.aseprite](haiwang_trident_crouch-v2.aseprite) | 用户微调后的蹲持与八帧蹲走，共 9 帧；仅海王，六个主体图层及新增握点修补层，含“蹲持”“蹲走”标签；已接入图集及代码 |
| [haiwang_trident_traversal-v2.aseprite](haiwang_trident_traversal-v2.aseprite) | 贴墙、悬挂、攀爬、爬梯为空手，滑索持戟；32×32、86 帧、六个可编辑图层、五个中文标签 |
| [haiwang_trident_wall-v2.aseprite](haiwang_trident_wall-v2.aseprite) | 贴墙独立动画，空手，10 帧；对应总稿 1–10 帧；已按 Rambro 76–80、86–90 格修正比例 |
| [haiwang_trident_hanging-v2.aseprite](haiwang_trident_hanging-v2.aseprite) | 悬挂独立动画，空手，18 帧；对应总稿 11–28 帧；已按 Rambro 107–124 格修正比例 |
| [haiwang_trident_climbing-v2.aseprite](haiwang_trident_climbing-v2.aseprite) | 攀爬独立动画，空手，20 帧；对应总稿 29–48 帧；已按 Rambro 76–95 格修正比例 |
| [haiwang_trident_ladder-v2.aseprite](haiwang_trident_ladder-v2.aseprite) | 爬梯独立动画，空手，20 帧；对应总稿 49–68 帧 |
| [haiwang_trident_zipline-v2.aseprite](haiwang_trident_zipline-v2.aseprite) | 滑索独立动画，保留持戟，18 帧；对应总稿 69–86 帧 |
| [haiwang_trident_run-v2_compare_rambro_full.aseprite](haiwang_trident_run-v2_compare_rambro_full.aseprite) | 完整跑步对照：奇数帧为兰博身体、持枪手臂和枪，偶数帧为海王，保留海王分层 |
| [haiwang_trident_run-v2_compare_rambro.aseprite](haiwang_trident_run-v2_compare_rambro.aseprite) | 旧交错对照，仅含兰博身体，使用旧稿的蹲姿移动参考；完整对照使用上一项 |
| [haiwang_trident_actions.aseprite](haiwang_trident_actions.aseprite) | 编辑持戟、蓄力、投掷、突刺的武器与手臂动作 |
| [haiwang_trident_body.aseprite](haiwang_trident_body.aseprite) | 早期站立与跑步身体配合稿，共 9 帧；当前游戏动作取自 v2 源稿 |
| [haiwang_trident_body_atlas.aseprite](haiwang_trident_body_atlas.aseprite) | 当前游戏身体图集，对应 ../_Mod/sprite.png |
| [haiwang_trident_gun_atlas.aseprite](haiwang_trident_gun_atlas.aseprite) | 当前游戏武器图集，对应 ../_Mod/gunSprite.png |
| [haiwang_trident_projectile.aseprite](haiwang_trident_projectile.aseprite) | 普通与蓄力飞行戟，对应 ../_Mod/projectiles/Trident.png |

修改分帧动作后，需要同步到对应图集并导出 PNG；图集不会自动跟随源文件更新。持戟站姿、跑步、跳跃、落地、蹲持、蹲走，以及五类地形动作 v2 的游戏取帧接入已完成。最新比例修正只更新素材和本地图集，前次构建部署结果见 [反馈记录](haiwang_trident_反馈记录.md)。`RenderTrident()` 按实际显示的身体格和攻击姿态选择武器帧，`TridentMovementAnimation` 记录配对格号、滑索重定向和落地时序。

蹲姿与蹲走 v2 继续使用身体格 6、40–47，配对武器格 64–72，普通持戟取消原版附加位移，蓄力、投掷和近战继续使用原攻击姿态。当前身体、武器图集均为 1024×1024，新增区域用于本轮地形动作。蹲姿源稿帧号、握点与同步要求见 [蹲姿与蹲走说明](haiwang_trident_设计.md#新版持戟蹲姿与蹲走-v2动画稿)。

2026-09-14 用户微调同步：站姿使用本目录最新的 `haiwang_trident_held-v2.aseprite`，同时更新跳跃第 18 帧、身体格 0/106 和武器格 9/62；`archive` 中的同名文件保留为旧参考。蹲姿按用户调整后的图层顺序合成，新增握点修补层一并进入武器图集。两个用户源文件的像素、图层与文件内容均保留。

2026-09-14 蹲姿再次微调同步：已导入 `haiwang_trident_crouch-v2.aseprite` 在 21:53:46 保存的九帧，更新身体格 6、40–47 和武器格 64–72。九组身体与武器合成后逐像素匹配最新源稿；DLL 与两张贴图已重新构建部署，部署文件校验一致。完成部署后更新本次文档记录，游戏内表现待重启实测。

五份地形独立动画于 2026-09-15 从当前空手版总稿拆出，每份保留 32×32 画布、六个可编辑图层、原帧时长、cel 数据和对应中文标签，帧号各自从 1 开始。修改独立文件后，需要将对应帧同步回总稿及游戏图集；当前同步脚本仍读取总稿。贴墙第 1–5 帧与攀爬独立稿第 1–5 帧、贴墙第 6–10 帧与攀爬独立稿第 11–15 帧共用游戏格，微调时保持对应帧一致。

## 当前预览

- [贴墙、悬挂、攀爬与 Rambro 的逐帧对照](traversal-v2/rambro-refit-preview.png)，并排循环：[贴墙](traversal-v2/wall-rambro-compare.gif)、[悬挂](traversal-v2/hanging-rambro-compare.gif)、[攀爬](traversal-v2/climbing-rambro-compare.gif)；左侧原版，右侧海王

- [五类地形动作总览](traversal-v2/preview.png)，循环预览：[贴墙](traversal-v2/wall.gif)、[悬挂](traversal-v2/hanging.gif)、[攀爬](traversal-v2/climbing.gif)、[爬梯](traversal-v2/ladder.gif)、[滑索](traversal-v2/zipline.gif)
- [新版持戟跑步 v2](haiwang_trident_run-v2_preview.gif)
- [与兰博并排播放的跑步对照](haiwang_trident_run-v2_compare_preview.gif)
- [新版站姿与八帧对照](haiwang_trident_run-v2_frames.png)
- [武器姿态对照](haiwang_trident_main_actions.png)
- [持戟跑步](haiwang_trident_run_preview.gif)
- [蓄力投掷](haiwang_trident_throw_preview.gif)
- [近战突刺](haiwang_trident_thrust_preview.gif)

跑步 v2 初稿曾以 [archive/haiwang_trident_held-v2.aseprite](archive/haiwang_trident_held-v2.aseprite) 为参考；当前站姿和新增动作使用本目录最新的 held-v2 主稿。已有跑步文件保留用户微调，沿用八帧脚步，每帧 32×32、80 毫秒，循环 640 毫秒。后手贴身小动，持戟手和戟杆同步；原跑步工程中的两个隐藏参考图层继续保留。

2026-09-14 摆臂修正：缩小后手动作，补齐移动手臂下方的鳞甲、发尾接缝和交叉脚部的漏像素。八帧已检查无封闭透明空洞、无半透明像素；八帧原稿、完整十六帧对照和预览同步更新。修正前文件保存在 `archive/before-run-small-arm-*` 中。

2026-09-14 持戟节奏调整：以用户微调的身体和后侧手臂为基础，将持戟手收至身前，前倾角依次为 25°、27°、29°、30°、29°、27°、25°、24°。手腕沿用兰博跑步参考的 1 像素位移节奏：第 3–6 帧回收，第 7–8 帧抬起，戟杆配合跨步回摆。肩部保留原有起伏，前臂连接随握点调整，手掌始终包住戟杆。

三个戟尖完整落在 32×32 画布内；戟尾收短 1 像素，适应降低后的握点并保持脚底基准以内。八帧原稿、完整十六帧对照及预览已同步，另附与兰博同时播放的八帧 GIF。修改前文件保存在 `archive/before-run-dynamic-trident-*` 中。

逐帧倾角、握点、兰博参考格号与后续同步流程见 [三叉戟设计文档](haiwang_trident_设计.md#新版持戟跑步-v2动画稿)。上述持戟节奏调整仅涉及 `金色三叉戟` 和 `持戟手臂` 图层，保留用户微调的头部、身体、双腿和后侧手臂。

[原尺寸帧条](haiwang_trident_run-v2_sheet.png) 为 256×32，配套 [帧数据](haiwang_trident_run-v2_sheet.json) 包含时长与 `run` 标签。游戏身体图集的 32–39 格和冲刺 96–103 格使用这套八帧，手臂与三叉戟由武器图集逐帧配合。

完整交错对照工程采用官方 `RAMBO_anim_1024x512.png` 的普通跑步 32–39 格，叠加 `RAMBRO_gun_anim_1024x64.png` 对应的 32–39 格。兰博的持枪手臂和枪位于武器贴图中，必须与身体一起显示。两个兰博图层统一上移 1 像素，脚底基准为 `y=29`，保留原版腾空帧的离地高度。

时间轴顺序为“兰博 1、海王 1、兰博 2、海王 2……”共十六帧，每帧 80 毫秒。兰博身体及持枪层已锁定，海王保留原稿分层与像素，可选择偶数帧微调。旧对照曾采用 40–47 格，这一段在当前新版动画逻辑中用于蹲姿移动，不是普通跑步。

## 说明

- 2026-09-15 比例修正：双臂、躯干和裤靴按原版格逐像素取形，统一上移 1 像素以沿用当前基准；头发、面部、鳞甲和皮肤使用当前 held-v2 的配色。原版刀具及红头带移除，换手时保留手臂经过脸前的遮挡。补齐 3 处金发接缝，脸与手臂之间原版已有的透明留白保留。
- 本轮 48 帧和 38 个身体格已核对，六份 GIF 与源稿一致，图集其余 986 格及总稿 49–86 帧保留。站跑跳蹲、爬梯、滑索源稿与武器图集未改动。检查见 [rambro-refit-checks.json](traversal-v2/rambro-refit-checks.json)；修改前备份为 `../backups/before-rambro-proportions-20260915-064442/`。

- 地形动作 v2：贴墙 1–10、悬挂 11–28、攀爬 29–48、爬梯 49–68 均为空手，完整双臂合入身体图集；滑索 69–86 保留手持三叉戟。源稿第六层现名为“活动手臂”。游戏仍按原版状态、帧号和高度选帧。
- 本轮已核对本机实际 `Assembly-CSharp.dll` 和 `BroMakerLib.dll`：显式开启新版刀攀、新版爬梯及进出梯过渡；悬挂保留原版 `y=-2` 身体偏移；滑索独立使用 512–529 格。详见 [设计文档](haiwang_trident_设计.md#地形动作-v2贴墙悬挂攀爬爬梯滑索)。
- 2026-09-15 空手修正：前四类动作共 68 帧去除三叉戟，58 个身体格包含完整双臂，旧武器 73–594 格清空；滑索 18 帧及武器 595–756 格保持一致。进梯首帧收拢原来包住戟杆的指尖。86 帧素材核对与移动动画 1856 次检查通过；构建部署结果见 [deployment.json](traversal-v2/deployment.json)，重启游戏后确认显示。
- 地形动作的 [逐帧映射](traversal-v2/traversal-manifest.csv)、[原版方法记录](traversal-v2/native-animation-map.json)、[素材检查结果](traversal-v2/asset-checks.json) 和 [部署结果](traversal-v2/deployment.json) 保存在 `traversal-v2`。修改前备份为 `../backups/before-traversal-v2-20260914-222759/`。同步脚本及共用帧的微调要求见设计文档。
- 持戟蹲姿 v2 为 32×32，第 1 帧为蹲持，第 2–9 帧为八帧蹲走；选择“蹲走”标签循环预览。源稿每帧 80 毫秒，蹲走一轮 640 毫秒；游戏沿用原版新蹲走的 25 毫秒帧间隔，源稿时长不代表游戏播放速度。头发和脸部沿用跳跃 v2 第 18 帧的像素，蹲走手臂与握点只作 1 像素幅度的变化，参考身体格为 6、40–47。
- 持戟跳跃 v2 的第 1–9 帧为前进跳跃及落地接跑步，第 10–18 帧为原地跳跃及落地接站立。源稿空中每帧 67 毫秒，每套落地三帧分别为 60、80、100 毫秒。第 9 帧与当前跑步首帧一致，第 18 帧与本目录最新持戟站姿主稿一致；后续微调时保持这两个衔接关系，并同步身体与武器图集。各帧的阶段、参考格号和握点记录在工程 cel 数据中。
- [跑步造型、配色与参考约束](haiwang_run_说明.md)
- [三叉戟操作、参数与游戏图集对应](haiwang_trident_设计.md)
- [三叉戟反馈记录与实测结果](haiwang_trident_反馈记录.md)

## 归档

[archive](archive/) 内保留恢复稿、早期静态武器稿、Rambro/Predabro 对照资源、独立跑步导出、重复放大图、预览合成工程和临时检查图。文件沿用原名，需要追溯时可直接打开。

游戏使用 `../_Mod` 中的导出资源；`BuildBro.ps1` 构建 DLL 并部署 `_Mod`，不会读取或转换 Aseprite 源稿。
