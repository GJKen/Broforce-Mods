# 海王素材归档索引

2026-09-15 按用途与制作阶段归档 22 个文件。原文件名保留；素材、脚本和检查报告内容保持原样，旧版跑步说明的链接已按新位置更新。

当前可编辑源稿见 [tools/README.md](../README.md)。本表的“原位置”均相对于 `tools/`。

| 分组 | 数量 | 内容 |
| --- | ---: | --- |
| [旧版跑步](legacy-run/) | 2 | 旧版独立跑步及配套说明 |
| [角色对照与预览导出](references/) | 6 | 参考素材与已导出的预览 |
| [地形初稿与空手修订](traversal-initial/) | 3 | 历史初稿生成脚本、空手专项检查和当时报告 |
| [Rambro 比例修正](rambro-refit/) | 6 | 比例重画脚本、阶段检查与对照图 |
| [爬梯与滑索像素修订](ladder-zipline-pixels/) | 5 | 像素修订阶段检查与对照图 |

| 文件 | 原位置 |
| --- | --- |
| [haiwang_run.aseprite](legacy-run/haiwang_run.aseprite) | `haiwang_run.aseprite` |
| [haiwang_run_说明.md](legacy-run/haiwang_run_说明.md) | `haiwang_run_说明.md` |
| [haiwang_trident_run-v2_compare_rambro.aseprite](references/haiwang_trident_run-v2_compare_rambro.aseprite) | `haiwang_trident_run-v2_compare_rambro.aseprite` |
| [haiwang_trident_run-v2_compare_rambro_full.aseprite](references/haiwang_trident_run-v2_compare_rambro_full.aseprite) | `haiwang_trident_run-v2_compare_rambro_full.aseprite` |
| [predabro_zipline_charge_reference.aseprite](references/predabro_zipline_charge_reference.aseprite) | `predabro_zipline_charge_reference.aseprite` |
| [haiwang_trident_run-v2_sheet.png](references/haiwang_trident_run-v2_sheet.png) | `haiwang_trident_run-v2_sheet.png` |
| [haiwang_trident_run-v2_sheet.json](references/haiwang_trident_run-v2_sheet.json) | `haiwang_trident_run-v2_sheet.json` |
| [haiwang_trident_main_actions.png](references/haiwang_trident_main_actions.png) | `haiwang_trident_main_actions.png` |
| [create_traversal.lua](traversal-initial/create_traversal.lua) | `traversal-v2/create_traversal.lua` |
| [check_traversal.py](traversal-initial/check_traversal.py) | `traversal-v2/check_traversal.py` |
| [asset-checks.json](traversal-initial/asset-checks.json) | `traversal-v2/asset-checks.json` |
| [refit_rambro.lua](rambro-refit/refit_rambro.lua) | `traversal-v2/refit_rambro.lua` |
| [rambro-refit-checks.json](rambro-refit/rambro-refit-checks.json) | `traversal-v2/rambro-refit-checks.json` |
| [rambro-refit-preview.png](rambro-refit/rambro-refit-preview.png) | `traversal-v2/rambro-refit-preview.png` |
| [wall-rambro-compare.gif](rambro-refit/wall-rambro-compare.gif) | `traversal-v2/wall-rambro-compare.gif` |
| [hanging-rambro-compare.gif](rambro-refit/hanging-rambro-compare.gif) | `traversal-v2/hanging-rambro-compare.gif` |
| [climbing-rambro-compare.gif](rambro-refit/climbing-rambro-compare.gif) | `traversal-v2/climbing-rambro-compare.gif` |
| [ladder-zipline-pixel-checks.json](ladder-zipline-pixels/ladder-zipline-pixel-checks.json) | `traversal-v2/ladder-zipline-pixel-checks.json` |
| [ladder-pixel-compare.png](ladder-zipline-pixels/ladder-pixel-compare.png) | `traversal-v2/ladder-pixel-compare.png` |
| [ladder-pixel-compare.gif](ladder-zipline-pixels/ladder-pixel-compare.gif) | `traversal-v2/ladder-pixel-compare.gif` |
| [zipline-pixel-compare.png](ladder-zipline-pixels/zipline-pixel-compare.png) | `traversal-v2/zipline-pixel-compare.png` |
| [zipline-pixel-compare.gif](ladder-zipline-pixels/zipline-pixel-compare.gif) | `traversal-v2/zipline-pixel-compare.gif` |

历史脚本用于记录当时的生成、重画或专项验收过程。`check_traversal.py` 依赖原目录层级推导项目位置，并含与当时备份一致的断言；若需复现，应先恢复到表中原相对路径，并准备对应阶段的输入及备份。当前同步继续使用 [sync_traversal.lua](../traversal-v2/sync_traversal.lua)、[sync_zipline_charge.lua](../traversal-v2/sync_zipline_charge.lua) 和 [zipline_charge_source.lua](../traversal-v2/zipline_charge_source.lua)。

原有 `before-*` 备份、[旧站姿恢复稿](haiwang_standing-Recovered.aseprite)、静态武器素材与早期预览合成工程继续保存在本目录，沿用原来的位置。旧的 `haiwang_standing.aseprite`、`haiwang_trident_body.aseprite` 及部分 GIF 不在当前工作目录中，已从当前文件索引中撤下；恢复稿和预览源工程按各自名称保留。
