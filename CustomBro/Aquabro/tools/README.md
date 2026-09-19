# 海王贴图工具入口

更新日期：2026-09-19。

## 正式源文件

所有后续 Aseprite 编辑、同步和图集重建都以 [海王 Aseprite 关键文件](海王_Aseprite关键文件/Readme.md) 为准。正式文件按用途分为 `01_动作主稿`、`02_地形独立稿`、`03_游戏图集` 和 `04_死亡碎块`。`tools` 根目录不再放置 Aseprite 文件；历史根目录副本已移到 [root-working-sources-20260918](archive/root-working-sources-20260918)。

角色死亡后实体碎块的正式源稿是 [haiwang_trident_death_gibs-v1.aseprite](海王_Aseprite关键文件/04_死亡碎块/haiwang_trident_death_gibs-v1.aseprite)，正式身体图集是 [haiwang_trident_body_atlas.aseprite](海王_Aseprite关键文件/03_游戏图集/haiwang_trident_body_atlas.aseprite)。旧根目录身体图集哈希为 `B766B3D2096A7CE3503A25C569D551BC252A07D2C4E6278DF2E414AAEF4A8D7F`；2026-09-18 碎块接入后图集哈希为 `80771229D57C6020F07E9D01626C5F90826CF8F49CCD8E1742882B00491D85EB`；2026-09-19 寄生死亡接入后正式图集哈希为 `ACEBB9512D93447F6F39A0046AE4D682A2F92300711B13CCE5BCFB7E49D652C0`，旧文件已归档，不能覆盖正式图集。

2026-09-19 寄生死亡（胸口爆裂）身体 235–242 换装已同步进正式图集和 `_Mod/sprite.png` 并部署：源稿为 `insemination-death-v3`（8 帧×33ms，空手），掩码零差异、皮肤位零缺失、无半透明像素；鳞甲条纹按帧分段走线（帧 1–3 近直、4–6 极点弧、7–8 轮廓平行弧）。换装依据、逐帧对照和机器核对见 `tools/_rambro235242/验收说明.md`。`tools` 根目录的 `_rambro235242` 为本轮工作目录，`archive/insemination-death-v1` 与 tools 下乱码目录中的 v2 均已作废。

## 工作流程

1. 修改并确认关键目录中的正式源稿。
2. 普通动作和图集使用 [rebuild_key_sources.lua](rebuild_key_sources.lua)；它从关键目录读取，并把源稿和图集写回对应的关键目录子文件夹。
3. 地面秀肌肉使用 [sync_ground_flex.lua](gestures-v2/sync_ground_flex.lua)，实体破裂碎块使用 [sync_gibs_from_source.lua](gibs-v2/sync_gibs_from_source.lua)，地形与滑索使用 [sync_traversal.lua](traversal-v2/sync_traversal.lua) 和 [sync_zipline_charge.lua](traversal-v2/sync_zipline_charge.lua)。
4. 同步完成后再运行 `BuildBro.ps1`。构建脚本只编译 `src` 并部署 `_Mod`，不会读取或转换 Aseprite；游戏实际读取的是 `_Mod/sprite.png`、`gunSprite.png` 和飞行戟 PNG。

## 文档分工

- [haiwang_trident_设计.md](haiwang_trident_设计.md)：动作、格号、代码接口和贴图设计约束。
- [haiwang_trident_反馈记录.md](haiwang_trident_反馈记录.md)：按日期记录修改、部署、测试和待实测事项。
- [海王 Aseprite 关键文件/Readme.md](海王_Aseprite关键文件/Readme.md)：当前 23 个正式 Aseprite 的清单和维护规则。
- [archive/README.md](archive/README.md)：早期历史资料索引；本次新增的根目录副本和功能验收资料见其对应归档子目录, 非必要不用读取里面的内容。

## 当前入口

- 全量重建：[rebuild_key_sources.lua](rebuild_key_sources.lua)
- 地面秀肌肉：[gestures-v2/sync_ground_flex.lua](gestures-v2/sync_ground_flex.lua)、[gestures-v2/audit_ground_flex.lua](gestures-v2/audit_ground_flex.lua)
- 实体破裂碎块：[gibs-v2/sync_gibs_from_source.lua](gibs-v2/sync_gibs_from_source.lua)
- 地形与滑索：[traversal-v2/sync_traversal.lua](traversal-v2/sync_traversal.lua)、[traversal-v2/sync_zipline_charge.lua](traversal-v2/sync_zipline_charge.lua)
