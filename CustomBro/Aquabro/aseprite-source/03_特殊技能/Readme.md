# 四技能正式源稿与导出

本目录维护技能的可编辑文件、源帧、图层和蒙版导出。操作、伤害、移动、消散及生效条件统一见 [角色 README](../../README.md#技能选择与施法通则)；部署和验收状态见 [部署与验证索引](../../当前角色实际应用状态.md)。

## 正式输入与输出

| 技能 | 施法源稿（32×32） | 特效源稿（128×128） | 特效输出及尺寸 |
| --- | --- | --- | --- |
| 怒海狂潮 | [skill-tidal-wave-cast-v3.aseprite](skill-tidal-wave-cast-v3.aseprite)，6 帧 | [skill-tidal-wave-fx-v3.aseprite](skill-tidal-wave-fx-v3.aseprite)，16 帧 | `skill-tidal-wave.png`，2048×128 |
| 深海猎杀 | [skill-shark-hunt-cast-v2.aseprite](skill-shark-hunt-cast-v2.aseprite)，8 帧 | [skill-shark-hunt-fx-v7.aseprite](skill-shark-hunt-fx-v7.aseprite)，28 帧 | `skill-shark-hunt.png`，3584×128 |
| 深海漩涡 | [skill-maelstrom-cast-v3.aseprite](skill-maelstrom-cast-v3.aseprite)，8 帧 | [skill-maelstrom-fx-v3.aseprite](skill-maelstrom-fx-v3.aseprite)，32 帧 | `skill-maelstrom.png`，4096×128 |
| 深海涌泉 | [skill-abyssal-spring-cast-v3.aseprite](skill-abyssal-spring-cast-v3.aseprite)，8 帧 | [skill-abyssal-spring-fx-v3.aseprite](skill-abyssal-spring-fx-v3.aseprite)，28 帧 | `skill-abyssal-spring.png`，3584×128 |

帧号从 1 开始，图集槽号从 0 开始。施法稿每帧 65ms，身体图集按上表顺序预留 `384–391、392–399、400–407、408–415`，狂潮末两格透明。施法是完整合成身体，不应在素材中依赖另一张独立手臂贴图。

## 图层与配色约束

水体色阶为 `#1961A5、#1E76C8、#1F7BD1、#35BFF3、#71CEF0、#BDEDFF、#FFFFFF`，保留透明度。配色修改应保持鲨鱼本体和角色／武器图层的独立性。

猎杀特效保留八层结构：第 2–6 层贡献身体蒙版，第 7–8 层为前景水；完全不透明的前景水遮住身体时，对应身体像素不计入蒙版。不能把全部水层并入鲨鱼身体伤害区域。其它三技能使用相应可见水体合成生成蒙版。

持戟拳头采用狂潮第 1 帧两格像素：同色 `RGBA(233,166,140)`，上格 alpha 137、下格 255，戟柄内侧留一列。当前 cast-v3 的狂潮第 2–5 帧、漩涡第 2–5 帧、涌泉第 3–6 帧已统一；猎杀 cast-v2 已符合此形状，继续使用该稿。

狂潮源帧前六帧保留起浪轮廓，末十帧为水片下落和水滴消散；漩涡 v3 保留六图层和水体自身的 S 型扭动；涌泉 v3 保留六图层，已补齐第 6–17 帧下段轮廓及底沿断点。猎杀 v7 的前六帧补有深色背鳍描边，编辑时不要丢掉其它图层贡献的轮廓。

## 导出入口

[build_runtime_atlases.lua](../../tools/build_runtime_atlases.lua) 的 `skills` 参数指定本目录，负责施法身体格；[export_skill_effects.lua](../../tools/export_skill_effects.lua) 读取特效稿，生成四张横向 PNG 与四份 `*PixelMask.cs`。它们由 [BuildAssets.ps1](../../BuildAssets.ps1) 统一调用，再把蒙版编入 DLL。

PNG、蒙版和 DLL 必须配套更新；生成的 C#／PNG 不能替代 Aseprite 编辑稿。运行时播放和生效帧由代码决定，修改源稿时长不等于修改代码时序。构建命令统一见 [README](../../README.md#构建关闭游戏与部署)。本目录保留正式八份源稿和本文。
