# 问题记录与验证材料

`issues/` 只保存问题与修复记录、索引及这些记录的 `archive/`。截图、原始 MCP 结果等附件放在 `docs/evidence/<任务>/`；问题正文保留现象、原因、修复和验收边界，并链接附件。

2026-11-09 整理的路径映射（相对本项目）：

| 原路径 | 当前路径 |
| --- | --- |
| `issues/diagnostics/2026-11-09-online-join-prompt/` | `docs/evidence/2026-11-09-online-join-prompt/` |
| `issues/diagnostics/2026-11-09-workshop-swap/` | `docs/evidence/2026-11-09-workshop-swap/` |
| `issues/diagnostics/2026-11-09-official-join/client-lobby.png` | `docs/evidence/2026-11-09-official-join/client-lobby.png` |
| `issues/diagnostics/2026-11-09-official-join/README.md` | `issues/ISSUES-2026-11-09-禁用多人联机Mod后无法加入官方地图.md` |

原先已跟踪的七份附件保持原内容迁移；其余材料的提交范围不因迁移改变。旧 `issues/diagnostics/` 已搬空并移除。

问题入口见 [问题索引](../issues/README.md)。

2026-11-11 联机性能记录从 BroforceBugFix 迁入（路径相对仓库根）：

| 原路径 | 当前路径 |
| --- | --- |
| `BroforceBugFix/issues/ISSUES-2026-11-11-Steam房主身份重复查询.md` | `CustomMapMultiplayer/issues/ISSUES-2026-11-11-Steam房主身份重复查询.md` |
| `BroforceBugFix/issues/ISSUES-2026-11-11-联机房主敌人逐帧更新负载.md` | `CustomMapMultiplayer/issues/ISSUES-2026-11-11-联机房主敌人逐帧更新负载.md` |

原始定位和失败方案保留；当前实现仅整合同帧 Steam 身份查询缓存，用户已确认稳定高帧。
