# 海王实体破裂碎块 v1

2026-09-18 用户微调同步：以用户保存的正式源稿 `海王_Aseprite关键文件/04_死亡碎块/haiwang_trident_death_gibs-v1.aseprite` 为输入，写入正式关键身体图集 `海王_Aseprite关键文件/03_游戏图集/haiwang_trident_body_atlas.aseprite`，并导出 `_Mod/sprite.png`。头部新增 4 个像素，坐标为图集 (325,10)、(332,10)、(324,11)、(324,12)；对应源稿坐标为 (5,9)、(12,9)、(4,10)、(4,11)。另有 3 个皮肤像素和 3 个血液像素按用户微调源稿改色；没有半透明像素，四个取样区外没有变化。根目录旧图集已归档，BuildBro.ps1 已重新构建并部署 DLL 和贴图。同步前备份位于 before-source-refresh-20260918-040440/。

这组素材不是普通动画帧。原版 `GibHolderBro` 在死亡时创建六个实体碎块，每个碎块通过 `SpriteSM` 从角色身体贴图取样；左右两臂共用一个 8×8 区域，左右两腿共用另一个 8×8 区域。因此源稿使用 1 个诊断帧保存四个取样区，并在图集内按原坐标覆盖。

## 参考与主稿

- 参考角色：Rambro。
- 原版像素底稿：`Broforce_src/GameAssets/hero/RAMBO_anim_1024x512.png`。
- 用户提供的 3 倍参考：`Broforce_src/GameAssets/hero_3x/RAMBO_anim_3072x1536.png`；它与 1024 版本是精确 3 倍最近邻版本，本稿按 1 倍像素制作。
- 海王外观主稿：`CustomBro/Aquabro/tools/海王_Aseprite关键文件/01_动作主稿/haiwang_trident_held-v2.aseprite`。

## 取样映射

`lowerLeftPixel` 是游戏序列化坐标；由于 `SpriteSM` 的 UV 翻转，PNG 顶部坐标如下：

| 碎块 | `lowerLeftPixel` | `pixelDimensions` | PNG 实际区域 |
| --- | --- | --- | --- |
| 头部 | `(320,17)` | `16×16` | `(320,1)` 到 `(335,16)` |
| 躯干 | `(320,30)` | `16×16` | `(320,14)` 到 `(335,29)` |
| 双臂 | `(341,13)` | `8×8` | `(341,5)` 到 `(348,12)` |
| 双腿 | `(341,27)` | `8×8` | `(341,19)` 到 `(348,26)` |

源稿帧号为 `1`，这里的“帧”仅用于 Aseprite 编辑和预览，不对应游戏中的身体动画格号。游戏实体碎块由 `BodyArm1/2`、`BodyHead`、`BodyLeg1/2`、`BodyTorso` 六个对象独立取样。

## 换装约束

- 原版四个矩形仍是唯一写入范围，未改变四个取样区外的像素。
- 初始换装保留原版皮肤和血液像素；本次用户微调额外改动 3 个皮肤像素、3 个血液像素，并在头部新增 4 个像素，坐标见顶部同步记录。
- 头部黑色发丝换为海王金发三色；躯干军绿色换为金色鳞甲；腿部黑色靴体换为深绿色。
- 源稿和图集均无半透明像素，也没有裁切；新增轮廓仅限用户明确微调的头部四个像素。
- 三叉戟层为空；实体破裂由死亡碎块组件显示，不从武器图集取样。

## 交付文件

- 正式源稿：[haiwang_trident_death_gibs-v1.aseprite](../../海王_Aseprite关键文件/04_死亡碎块/haiwang_trident_death_gibs-v1.aseprite)
- 正常停留预览：[normal.gif](normal.gif)
- 慢速停留预览：[slow.gif](slow.gif)
- 原版/换装并排对照：[rambro-aquabro-compare-annotated.png](rambro-aquabro-compare-annotated.png)
- 原版碎块：[rambro-reference.png](rambro-reference.png)
- 海王碎块：[aquabro-gibs.png](aquabro-gibs.png)
- 图集修改前：[atlas-before.png](atlas-before.png)
- 图集修改后：[atlas-after.png](atlas-after.png)
- 验收数据：[checks.json](checks.json)
- Aseprite 重现脚本：[create_gibs.lua](create_gibs.lua)
- 用户微调同步脚本：[sync_gibs_from_source.lua](../../gibs-v2/sync_gibs_from_source.lua)

工作图集、关键图集和游戏 PNG 已同步：

- `CustomBro/Aquabro/tools/archive/root-working-sources-20260918/haiwang_trident_body_atlas.aseprite`
- `CustomBro/Aquabro/tools/海王_Aseprite关键文件/03_游戏图集/haiwang_trident_body_atlas.aseprite`
- `CustomBro/Aquabro/_Mod/sprite.png`

本次按用户明确要求更新了关键文件目录中的图集副本；其它关键源稿未修改。

## 验收结果

`checks.json` 记录了每个取样区的原版非透明像素数、换装后非透明像素数和颜色替换数。同步前后的工作图集变化为 `22` 个像素，区域外变化为 `0`；头部新增 4 个原版透明位置像素，皮肤变化为 `3`，血液变化为 `3`；源稿和图集均无半透明像素。新增轮廓仅为用户在头部源稿中明确做出的四个像素调整，坐标已在本文件顶部列出。

正常与慢速 GIF 都从最终保存的 1 帧源稿导出。由于实体破裂由游戏物理碎块组件驱动，不是循环动画，两份预览只改变单帧停留时长，不改变像素内容。

本次使用 Aseprite 内置 Lua 像素编辑接口读取用户微调源稿并同步图集；当前会话没有可调用的 Aseprite MCP 连接，因此没有使用 Python 生成或改造角色像素。后续复现应使用 [sync_gibs_from_source.lua](../../gibs-v2/sync_gibs_from_source.lua)，不要用会重生成源稿的 [create_gibs.lua](create_gibs.lua) 覆盖用户微调。
