# Broforce Bug Fix Issue Records

> [Chinese](README.md)

This directory records evidence, patch boundaries, and acceptance status for each original-game defect. An implemented fix is not necessarily accepted in-game; status must be determined from the corresponding document.

The fix version identifies the corresponding historical milestone. See the [project README](../README.en.md) for the current installation version and the [development guide](../docs/DEVELOPMENT.md#验证范围与复测要求) for each build's validation scope.

| Issue | Status | Fix version |
| --- | --- | --- |
| [Sandstorm null reference during scene transitions](ISSUES-2026-11-21-沙尘暴切图初始化空引用.md) | Deployed on both peers; targeted checks, native join/reloads, storm synchronization across both official levels, native level transitions, and the ending passed assisted tests in segments | `0.5.0` |
| [AlienEgg null-list cleanup during scene teardown](ISSUES-2026-11-18-AlienEgg换关清理空引用.md) | `0.4.1` handles the Listener exception remaining in `0.4.0`; deployed on both peers; controlled checks and offline/Steam native reloads passed | `0.4.1` |
| [Null-target snake detach causes repeated bleeding](ISSUES-2026-11-14-蛇脱离空目标导致反复出血.md) | Deployed; offline A/B and controlled FRP checks passed on both peers; initial reference-loss trigger remains unconfirmed | `0.3.0` |
| [Official lobby join blocked by a previous unfinished map load](ISSUES-2026-11-09-未完成地图加载后加入官方房卡在主菜单.md) | Deployed; two-machine runtime retest passed with joining-side CMM disabled | `0.3.0` |
| [DoodadCrate recursive explosion stack overflow](ISSUES-2026-08-27-DoodadCrate爆炸递归栈溢出.md) | Controlled re-entry, exception cleanup, and ordinary-crate checks on both peers passed; full acceptance of the original failing level and explosive-crate combinations remains pending | `0.2.0` |

Historical issue records are archived in `archive/`. Routine changes report the necessary validation without saving log or screenshot attachments by default. See the [materials guide](../docs/MATERIALS.md) for historical moves, deletions, and local-only materials.
