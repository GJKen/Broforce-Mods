# 海王跑步动画（旧版归档）

- 可编辑工程：`haiwang_run.aseprite`。
- 动画：`run`，8 帧，每帧 32×32；预览每帧 80 ms，一轮 640 ms。
- 绘制时的外观基准为旧版 `haiwang_standing.aseprite`；当前归档保留 [站姿恢复稿](../haiwang_standing-Recovered.aseprite) 供追溯。
- 动作基准：`Broforce-Mods/Broforce_src/GameAssets/hero/RAMBO_anim_1024x512.png` 的第 40–47 格（原版帧号从 0 开始）。
- 本次按明确要求沿用 Rambro 的身体和四肢姿态，在 Aseprite MCP 中逐帧绘制海王的头发和服装。

帧号勘误：本旧稿引用的 40–47 格在当前新版动画逻辑中用于蹲姿移动，普通跑步使用 32–39 格。完整原版跑步还需叠加武器贴图中的持枪手臂与枪。需要微调对比时，使用 [haiwang_trident_run-v2_compare_rambro_full.aseprite](../references/haiwang_trident_run-v2_compare_rambro_full.aseprite)，其中已按普通跑步的身体和武器帧合成兰博，海王帧保留原稿。

## 外观与动作约束

- 金发与脸部：保留站立稿的金色长发、额前轮廓、肤色及脸部明暗；脸部随原版跑步帧起伏，发尾有轻微摆动。
- 裸露手臂：按原版每帧的肩、手臂和手部位置对照，保留原版的小幅动作及遮挡关系。
- 金色鳞甲：保持站立稿的橙金色明暗纹理，随原版身体前倾和腰部轮廓绘制。
- 绿色裤靴：沿用原版双腿交替、屈膝和鞋尖形状，使用站立稿的绿色与深绿色。
- 原版参考统一上移 1 像素，对齐现稿脚底 `y=29`；工程坐标从 0 开始。
- 以现有站立稿的深色收边方式为准；像素只有完全透明或完全不透明，不使用抗锯齿。

## 固定配色

| 部位 | 颜色 |
| --- | --- |
| 金发 | `#FFDD5E`、`#E8AF33`、`#865712` |
| 脸、手臂、颈部 | `#E9A68C`、`#C3866D`、`#AA735D`、`#D79479`、`#CF8C71`、`#B57C65` |
| 鳞甲 | `#F79A3C`、`#E07A18`、`#A8560F` |
| 裤靴 | `#2F8A2A`、`#1C5A18`、`#123F14` |

调色板沿用站立稿的 15 个不透明色；跑步实际使用其中 14 色。

## 图层与预览

工程有四个可编辑图层：`绿色裤靴`、`金色鳞甲`、`裸露手臂`、`金发与脸部`。底部另有锁定并隐藏的 Rambro 跑步参考和海王站立参考；需要对照时可切换可见性。

- [haiwang_run_vs_rambro.png](../haiwang_run_vs_rambro.png)：上排原版，下排海王；列标题为“工程帧号 / 原版帧号”。
- [haiwang_run_frame1_preview.png](../haiwang_run_frame1_preview.png)：旧版跑步首帧的静态预览；循环动画可打开同目录的 `haiwang_run.aseprite` 查看。
- [haiwang_run_sheet.png](../haiwang_run_sheet.png)：独立跑步稿原尺寸横向帧条，256×32。
- [haiwang_run_sheet.json](../haiwang_run_sheet.json)：8 帧的位置、尺寸、时长及 `run` 标签。

早期持戟跑步合成工程见 [haiwang_trident_run_preview.aseprite](../haiwang_trident_run_preview.aseprite)。当前持戟跑步 v2 使用用户优化的站姿与跑姿，包含随步伐变化的三叉戟前倾和握点，可打开 [跑步 v2 主稿](../../haiwang_trident_run-v2.aseprite) 查看动画，或查看归档的 [帧条](../references/haiwang_trident_run-v2_sheet.png) 与 [兰博完整对照](../references/haiwang_trident_run-v2_compare_rambro_full.aseprite)；v2 已同步到游戏图集及武器取帧代码。

最新逐帧参数、游戏实际取帧位置与接入情况以 [haiwang_trident_设计.md](../../haiwang_trident_设计.md) 为准；本文件的配色、图层和姿势约束仅记录 `haiwang_run.aseprite` 独立旧稿。
