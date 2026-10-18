# 普攻水花源稿与导出

这里只维护正式输入和选帧。伤害分区、持续检测、击退、画面大小和播放时序统一见 [README](../../README.md#普通水花与蓄力水爆)。

| 用途 | 正式源文件 | 导出范围 | 运行时 PNG |
| --- | --- | --- | --- |
| 普通命中 | [haiwang_water_hit-small-v2.aseprite](haiwang_water_hit-small-v2.aseprite)，48×48、12 帧 | 全部 12 帧 | `water-hit-small.png`，576×48 |
| 蓄力水爆 | [haiwang_water_rocket-explosion-v2.aseprite](haiwang_water_rocket-explosion-v2.aseprite)，64×64、22 帧 | 仅第 17–22 帧 | `water-rocket-explosion.png`，384×64 |

[export_water_effects.lua](../../tools/export_water_effects.lua) 按此表导出，并从同一批第 17–22 帧合成图的非透明像素生成 `ChargedWaterPixelMask.cs`。蒙版坐标从左上角开始、每帧 64×64，运行时按 72×72 显示大小换算；不要单独手改生成的蒙版。`BuildAssets.ps1` 同时生成、编译和替换蒙版／图集，源码项目与 `aquabro-build.rsp` 已接入。水爆源稿保留完整 22 帧和每帧 50ms；当前导出选择不应通过删除前 16 帧来实现。实际播放时长由 `src/Aquabro.cs` 控制，不能只改源帧时长就认定游戏时序也会改变。

水爆 v2 派生自本地水特效拆分参考中的 `02_水球爆裂.aseprite`；原始参考保留在本地 `01_动作主稿/cankao/`，不随仓库推送。正式源稿已包含构建所需像素，不依赖该参考目录。水体配色约束统一见 [特殊技能源稿](../03_特殊技能/Readme.md#图层与配色约束)。旧 `water-hit-charged.png` 及其活动源稿、导出条目已移除，不是当前依赖。

运行时效果和时序以角色 README 与源码为准。
