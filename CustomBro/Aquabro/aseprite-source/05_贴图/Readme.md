# 头像与技能 HUD 源稿

本文件维护源帧、图层和导出对应关系；图标外形、HUD 间距、次数显示与玩家行为统一见 [README](../../docs/机制实现细节.md#头像与技能图标)。所有源稿及 PNG 预览直接放在本目录根部，不建立图标子目录。

## 头像编辑

[avatar.aseprite](avatar.aseprite) 为四帧 32×64，依次是正常、微笑、怒吼和死亡表情；[avatar.png](avatar.png) 为 128×64 横向导出图。源稿从已有 PNG 无损导入，保留“原始头像像素”层和空白“修订”层；原始分层工程未找到，不能把这两层当作恢复出的旧图层。

在修订层编辑后，由 [export_avatar.lua](../../tools/export_avatar.lua) 导出。构建读取 Aseprite，PNG 用于预览和运行时输出，不应只修改 PNG 后再期待源稿同步改变。

## 技能图标编辑

[skill-hud-icons-v3.aseprite](skill-hud-icons-v3.aseprite) 为 16×16、四帧、三层：轮廓与阴影／水体与鲨鱼／浪冠与高光。

| 源帧 | 技能 | 同目录 PNG |
| --- | --- | --- |
| 1 | 怒海狂潮 | [special-tidal-wave.png](special-tidal-wave.png) |
| 2 | 深海猎杀 | [special-shark-hunt.png](special-shark-hunt.png) |
| 3 | 深海漩涡 | [special-maelstrom.png](special-maelstrom.png) |
| 4 | 深海涌泉 | [special-abyssal-spring.png](special-abyssal-spring.png) |

[export_skill_icons.lua](../../tools/export_skill_icons.lua) 将四帧分别导出，并用第 1 帧生成 [special.png](special.png) 作为默认图标；同时更新本目录 PNG 和构建输出目录。导出只读取现有源稿，不重新绘制或覆盖它。[BuildAssets.ps1](../../BuildAssets.ps1) 会等待头像／图标导出进程完成。

后续外形调整以正式稿为准，机制文档中的图标说明同时更新。
