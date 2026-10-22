# 四技能正式源稿与导出

本目录维护技能的可编辑文件、源帧、图层和蒙版导出。操作、伤害、移动、消散及生效条件统一见 [角色 README](../../README.md#技能选择与施法通则)；部署和验收状态见 [部署与验证索引](../../当前角色实际应用状态.md)。

## 酸蚀水体源稿

新增 [酸蚀水体](酸蚀水体/Readme.md) 子目录：狂潮、漩涡、涌泉 × 两阶段，共六份保留图层／帧的可编辑源稿，以及清水／一阶段／二阶段配色预览。`export_acid_skills.lua` 读取这些正式源稿，导出六张 `skill-<技能>-acid-<阶段>.png`；构建逐帧验证 alpha 完全匹配原稿；猎杀不参与污染，不再保留其酸蚀源稿或输出。污染不改变判定蒙版。常规构建不会重新生成或覆盖编辑稿。

## 正式输入与输出

| 技能 | 施法源稿（32×32） | 特效源稿（128×128） | 特效输出及尺寸 |
| --- | --- | --- | --- |
| 怒海狂潮 | [skill-tidal-wave-cast-v3.aseprite](skill-tidal-wave-cast-v3.aseprite)，6 帧 | [skill-tidal-wave-fx-v3.aseprite](skill-tidal-wave-fx-v3.aseprite)，16 帧 | `skill-tidal-wave.png`，2048×128 |
| 深海猎杀 | [skill-shark-hunt-cast-v2.aseprite](skill-shark-hunt-cast-v2.aseprite)，8 帧 | [skill-shark-hunt-fx-v7.aseprite](skill-shark-hunt-fx-v7.aseprite)，28 帧 | 完整图及前后景水花各 3584×128；`skill-shark-hunt-body.png` 为 7552×128（28 格本体，含入水位置校准，另加 31 格空中补间） |
| 深海漩涡 | [skill-maelstrom-cast-v3.aseprite](skill-maelstrom-cast-v3.aseprite)，8 帧 | [skill-maelstrom-fx-v3.aseprite](skill-maelstrom-fx-v3.aseprite)，32 帧 | `skill-maelstrom.png`，4096×128 |
| 深海涌泉 | [skill-abyssal-spring-cast-v3.aseprite](skill-abyssal-spring-cast-v3.aseprite)，8 帧 | [skill-abyssal-spring-fx-v3.aseprite](skill-abyssal-spring-fx-v3.aseprite)，28 帧 | `skill-abyssal-spring.png`，3584×128 |

帧号从 1 开始，图集槽号从 0 开始。施法稿每帧 65ms，身体图集按上表顺序预留 `384–391、392–399、400–407、408–415`，狂潮末两格透明。施法是完整合成身体，不应在素材中依赖另一张独立手臂贴图。

## 图层与配色约束

水体色阶为 `#1961A5、#1E76C8、#1F7BD1、#35BFF3、#71CEF0、#BDEDFF、#FFFFFF`，保留透明度。配色修改应保持鲨鱼本体和角色／武器图层的独立性。

猎杀特效保留八层结构，v7 实际层序为：1 尾部、2 身体与背鳍、3 后景水片、4 头部与上颌、5 下颌与牙齿、6 近侧胸鳍、7 前景卷浪、8 飞散水珠。实际鲨鱼身体为第 1、2、4、5、6 层，水为第 3、7、8 层。

`skill-shark-hunt-body.png` 只合成第 1、2、4、5、6 层。前 28 格中，19–27 格的身体在导出时下移 3px：偏移量由第 19 格可见鼻尖下沿 109 与水线 112 自动计算，避免逻辑落地时身体仍悬空。额外 31 格以 60Hz 对原姿态的主轴角度和中心插值，完整本体的竖向重心统一采用校准后的入水重心，移除素材弧线。最后一格补间与校准后的第 19 格本体逐像素一致。源稿不保存这些导出调整。

起跳水花后景取第 3 层，前景取第 7、8 层，分别导出独立 PNG，运行时固定在起跳点且完整显示。入水使用 `skill-shark-hunt.png` 的完整合成帧并固定在实际落点；导出器只校准身体，水花保持原像素坐标，仍由 Aseprite 按尾部／身体、第 3 层水、头部／下颌／胸鳍、前景水的原顺序合成，不裁切水花。伤害蒙版由 59 格本体图生成，水花不参与攻击。其它三技能仍使用可见水体合成生成蒙版。

入水首格（槽 19）是鼻尖刚接触水面的姿态，源稿此格第 7、8 层含两段此前留下的水线。运行时入水不应重放这些残留，导出完整图及水花分层图时省略此格水层；身体保持完整，槽 20–27 水花和八层合成顺序不变，首个水花出现在身体开始没入水面的槽 20。此规则不按地形裁剪像素。

持戟拳头采用狂潮第 1 帧两格像素：同色 `RGBA(233,166,140)`，上格 alpha 137、下格 255，戟柄内侧留一列。当前 cast-v3 的狂潮第 2–5 帧、漩涡第 2–5 帧、涌泉第 3–6 帧已统一；猎杀 cast-v2 已符合此形状，继续使用该稿。

狂潮源帧前六帧保留起浪轮廓，末十帧为水片下落和水滴消散；漩涡 v3 保留六图层和水体自身的 S 型扭动；涌泉 v3 保留六图层，已补齐第 6–17 帧下段轮廓及底沿断点。猎杀 v7 的前六帧补有深色背鳍描边，编辑时不要丢掉其它图层贡献的轮廓。

## 导出入口

[build_runtime_atlases.lua](../../tools/build_runtime_atlases.lua) 的 `skills` 参数指定本目录，负责施法身体格；[export_skill_effects.lua](../../tools/export_skill_effects.lua) 读取特效稿，生成四张完整特效 PNG、猎杀本体／前后景水花三张分层 PNG 与四份 `*PixelMask.cs`。它们由 [BuildAssets.ps1](../../BuildAssets.ps1) 统一调用，再把蒙版编入 DLL。

PNG、蒙版和 DLL 必须配套更新；生成的 C#／PNG 不能替代 Aseprite 编辑稿。运行时播放和生效帧由代码决定，修改源稿时长不等于修改代码时序。构建命令统一见 [README](../../README.md#构建关闭游戏与部署)。本目录保留原正式八份源稿、酸蚀水体子目录和本文。
