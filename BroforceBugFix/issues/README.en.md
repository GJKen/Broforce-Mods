# Broforce Bug Fix Issue Records

> [Chinese](README.md)

This directory records evidence, patch boundaries, and acceptance status for each original-game defect. An implemented fix is not necessarily accepted in-game; status must be determined from the corresponding document.

| Issue | Status | Fix version |
| --- | --- | --- |
| [AlienEgg null-list cleanup during scene teardown](ISSUES-2026-11-18-AlienEgg换关清理空引用.md) | `0.4.1` handles the Listener exception remaining in `0.4.0`; deployed on both peers; controlled checks and offline/Steam native reloads passed | `0.4.1` |
| [Null-target snake detach causes repeated bleeding](ISSUES-2026-11-14-蛇脱离空目标导致反复出血.md) | Deployed; offline A/B and controlled FRP checks passed on both peers; initial reference-loss trigger remains unconfirmed | Local update to `0.3.0` |
| [DoodadCrate recursive explosion stack overflow](ISSUES-2026-08-27-DoodadCrate爆炸递归栈溢出.md) | Implemented; offline and two-sided retesting pending | `0.2.0` |
| [Official lobby join blocked by a previous unfinished map load](ISSUES-2026-11-09-未完成地图加载后加入官方房卡在主菜单.md) | Deployed; two-machine runtime retest passed with joining-side CMM disabled | `0.3.0` |

Historical records are archived in `archive/`. Runtime evidence is kept under `docs/evidence/`; issue records retain conclusions, boundaries, and entry links.
