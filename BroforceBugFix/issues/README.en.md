# Broforce Bug Fix Issue Records

> [Chinese](README.md)

This directory records evidence, patch boundaries, and acceptance status for each original-game defect. An implemented fix is not necessarily accepted in-game; status must be determined from the corresponding document.

| Issue | Status | Fix version |
| --- | --- | --- |
| [DoodadCrate recursive explosion stack overflow](ISSUES-2026-08-27-DoodadCrate爆炸递归栈溢出.md) | Implemented; offline and two-sided retesting pending | `0.2.0` |
| [Official lobby join blocked by a previous unfinished map load](ISSUES-2026-11-09-未完成地图加载后加入官方房卡在主菜单.md) | Deployed; two-machine runtime retest passed with joining-side CMM disabled | `0.3.0` |

Historical records are archived in `archive/`. Do not delete evidence referenced by the development documentation.
