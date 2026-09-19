# 海王 Aseprite 贴图原稿

更新日期：2026-09-19。

本目录是唯一正式 Aseprite 源文件集合，共 23 个文件。后续修改、同步和图集重建均以这里的文件为输入；`tools` 根目录不再保留工作副本。历史副本和预览资料位于 `tools/archive`，不能反向覆盖本目录。

## 目录

| 文件夹 | 数量 | 用途 |
| --- | ---: | --- |
| `01_动作主稿` | 15 | 站姿、跑步、跳跃、蹲姿、攻击、飞行戟、地形总稿、绳索蓄力、五组动作及寄生死亡。 |
| `02_地形独立稿` | 5 | 贴墙、悬挂、攀爬、爬梯和滑索的独立编辑稿。 |
| `03_游戏图集` | 2 | 正式身体和武器图集。 |
| `04_死亡碎块` | 1 | 角色死亡后实体碎块的四区取样编辑稿。 |

## 01_动作主稿

| 文件 | 帧数 | 用途 |
| --- | ---: | --- |
| [haiwang_trident_held-v2.aseprite](01_动作主稿/haiwang_trident_held-v2.aseprite) | 1 | 持戟站姿与人物外观主稿；跳跃第 18 帧与它衔接。 |
| [haiwang_trident_run-v3.aseprite](01_动作主稿/haiwang_trident_run-v3.aseprite) | 8 | 正式跑步版本；对应身体 32–39、96–103。 |
| [haiwang_trident_jump-v2.aseprite](01_动作主稿/haiwang_trident_jump-v2.aseprite) | 18 | 前进跳跃、原地跳跃及落地衔接。 |
| [haiwang_trident_crouch-v2.aseprite](01_动作主稿/haiwang_trident_crouch-v2.aseprite) | 9 | 一帧蹲持和八帧蹲走。 |
| [haiwang_trident_actions.aseprite](01_动作主稿/haiwang_trident_actions.aseprite) | 36 | 持戟、蓄力、投掷、突刺的武器与手臂动作。 |
| [haiwang_trident_projectile.aseprite](01_动作主稿/haiwang_trident_projectile.aseprite) | 2 | 普通与蓄力飞行戟。 |
| [haiwang_trident_traversal-v2.aseprite](01_动作主稿/haiwang_trident_traversal-v2.aseprite) | 86 | 贴墙、悬挂、攀爬、爬梯、滑索总稿。 |
| [haiwang_trident_zipline_charge.aseprite](01_动作主稿/haiwang_trident_zipline_charge.aseprite) | 12 | 绳索蓄力独立稿；使用第 2、3、4 帧生成武器格。 |
| [haiwang_trident_death-v2.aseprite](01_动作主稿/haiwang_trident_death-v2.aseprite) | 2 | 普通空中死亡和死亡倒地，对应身体 4–5。 |
| [haiwang_trident_insemination-death-v3.aseprite](01_动作主稿/haiwang_trident_insemination-death-v3.aseprite) | 8 | 寄生死亡（胸口爆裂），对应身体 235–242；空手。 |
| [haiwang_trident_highfive-v2.aseprite](01_动作主稿/haiwang_trident_highfive-v2.aseprite) | 6 | 击掌动作，对应身体 17–22。 |
| [haiwang_trident_flex-v2.aseprite](01_动作主稿/haiwang_trident_flex-v2.aseprite) | 24 | 地面秀肌肉，对应身体 352–375；空手。 |
| [haiwang_trident_roll-v2.aseprite](01_动作主稿/haiwang_trident_roll-v2.aseprite) | 13 | 高处落地翻滚，对应身体 51–63。 |
| [haiwang_trident_waterwall-v2.aseprite](01_动作主稿/haiwang_trident_waterwall-v2.aseprite) | 8 | 水墙特技施放，对应身体 145–152。 |
| [haiwang_trident_chimney-flip-v2.aseprite](01_动作主稿/haiwang_trident_chimney-flip-v2.aseprite) | 12 | 蹬墙翻转，对应身体 203–214。 |

## 02_地形独立稿

| 文件 | 帧数 | 用途 |
| --- | ---: | --- |
| [haiwang_trident_wall-v2.aseprite](02_地形独立稿/haiwang_trident_wall-v2.aseprite) | 10 | 贴墙独立动画，总稿第 1–10 帧。 |
| [haiwang_trident_hanging-v2.aseprite](02_地形独立稿/haiwang_trident_hanging-v2.aseprite) | 18 | 悬挂独立动画，总稿第 11–28 帧。 |
| [haiwang_trident_climbing-v2.aseprite](02_地形独立稿/haiwang_trident_climbing-v2.aseprite) | 20 | 攀爬独立动画，总稿第 29–48 帧。 |
| [haiwang_trident_ladder-v2.aseprite](02_地形独立稿/haiwang_trident_ladder-v2.aseprite) | 20 | 爬梯独立动画，总稿第 49–68 帧；当前停用，保留备用。 |
| [haiwang_trident_zipline-v2.aseprite](02_地形独立稿/haiwang_trident_zipline-v2.aseprite) | 18 | 持戟滑索独立动画，总稿第 69–86 帧。 |

## 03_游戏图集

| 文件 | 用途 |
| --- | --- |
| [haiwang_trident_body_atlas.aseprite](03_游戏图集/haiwang_trident_body_atlas.aseprite) | 1024×1024 正式身体图集，对应 `_Mod/sprite.png`。2026-09-19 正式哈希：`ACEBB9512D93447F6F39A0046AE4D682A2F92300711B13CCE5BCFB7E49D652C0`。 |
| [haiwang_trident_gun_atlas.aseprite](03_游戏图集/haiwang_trident_gun_atlas.aseprite) | 1024×1024 正式武器图集，对应 `_Mod/gunSprite.png`。 |

## 04_死亡碎块

| 文件 | 用途 |
| --- | --- |
| [haiwang_trident_death_gibs-v1.aseprite](04_死亡碎块/haiwang_trident_death_gibs-v1.aseprite) | 角色死亡后实体碎块的 30×30 诊断源稿，保存头部、躯干、双臂、双腿四个取样区；不是普通动画格。 |

## 维护规则

修改独立稿后，需要同步回总稿和游戏图集；现有脚本只会从本目录读取正式源稿。普通动作使用 `tools/rebuild_key_sources.lua`，地面秀肌肉使用 `tools/gestures-v2/sync_ground_flex.lua`，实体破裂碎块使用 `tools/gibs-v2/sync_gibs_from_source.lua`，地形与滑索使用 `tools/traversal-v2` 中的同步脚本。

重建输出必须回写本目录的对应子文件夹：动作稿在 `01_动作主稿`，地形独立稿在 `02_地形独立稿`，图集在 `03_游戏图集`。游戏运行时不读取 Aseprite 源稿；同步后再运行 `BuildBro.ps1` 部署 `_Mod` 资源。

实体破裂的正式版本是 2026-09-18 用户微调后的版本：头部新增 4 个像素，皮肤变化 3 个，血液变化 3 个，四个取样区外变化 0，无半透明像素。旧的根目录身体图集已归档到 `tools/archive/root-working-sources-20260918/haiwang_trident_body_atlas.aseprite`，其哈希不同，不能覆盖 `03_游戏图集` 中的正式版本。

寄生死亡的正式版本是 2026-09-19 的 `insemination-death-v3`：对 Rambro 235–242 逐像素换装（掩码零差异），鳞甲条纹在帧 7–8 用轮廓平行弧线、帧 4–6 用极点弧、帧 1–3 近直条；换装对照与机器核对见 `tools/_rambro235242/验收说明.md`。之前 archive/insemination-death-v1 与 tools 下乱码目录中的 v2 均已作废。
