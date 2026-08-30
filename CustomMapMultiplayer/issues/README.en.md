# Issue Index

> [Chinese](README.md)

This directory contains current issues, log evidence, experimental changes, and acceptance results. Historical records that have completed their corresponding test scope are moved to `archive/`. Archived documents may contain reverted code, old parameters, or plans that were incomplete at the time. Current usage and valid implementation are defined by the project root `README.en.md` and `docs/DEVELOPMENT.md`.

## Current Status Summary

- The current transport is official Steam Lobby/Steam P2P. `FRP Direct` is disabled by default; three-player basic multiplayer and the static full-room notice for a `1`-player room are verified, while four-player play, capacity boundaries, dynamic adjustment, and host migration still require testing.
- The current distributed `buildHash` and DLL SHA-256 are defined by the [Current Status section of the root README](../README.en.md#current-status). Build, experiment, and deployment statements in archived documents do not represent the current implementation.
- Current focus areas are low-probability Mook final-state divergence, McBrover self-detonation residuals, level-end re-entry, late-join death snapshots, and AFK/long-session stability. Other verified scopes and evidence boundaries are defined by the table below.

## Current Records

| Document | Status | Reading notes |
| --- | --- | --- |
| `ISSUES-2026-08-30-Assembly-CSharp反编译与重建方案.md` | Plan documented; temporary replacement experiment pending | Covers `Assembly-CSharp.dll` decompilation, IL patching, C# rebuilding, isolated testing, and rollback. An isolated Chinese-input switch Mod now exists for local testing; the official assembly has not been rebuilt. |
| `ISSUES-2026-08-31-Test_Evan2原生地图对象与投掷物清理空引用.md` | Target exceptions passed two-sided regression; crate guard branch not directly hit | The `5582c...` regression produced no `TorturedVillager`, `Map.RemoveProjectile`, or `DoodadCrate` errors. Villager and projectile guards each triggered once; the crate error branch was not directly exercised. |
| `ISSUES-2026-08-30-加入方箱子坍塌特效持续重复.md` | Reproduced historically; not reproduced in latest short test, direct acceptance pending | The latest `5582c...` two-sided test had no crate error loop or repeated collapse, but did not directly trigger either crate guard branch. Its relationship to `BroforceBugFix` remains tracked separately. |
| `ISSUES-2026-08-30-联机房主低帧率与Host专属扫描性能问题.md` | Sustained low FPS not reproduced in latest short test; formal A/B no longer scheduled for this round | After excluding reload and pre-exit host-migration windows, `5582c...` measured Host/Client weighted frame times of `11.284/9.149ms`; Host acid-pool refresh totaled `2264.130ms`. These numbers remain observational only; unified settings, reverse Host, and repeated p50/p95/p99 comparison rounds are no longer required for this round. |
| `ISSUES-2026-08-30-Workshop联机酸液池导致双方一起死亡.md` | Fixed and verified in two-sided in-game testing | The old transpiler covered only `CheckForTraps`; direct `CalculateMovement`/`Damage` calls bypassed it. The implementation now keeps a scene-level `DoodadAcidPool` list, predicts local joining-player death, and enforces Host authority at the common `CoverInAcid` entry without killing the player left at spawn. |
| `ISSUES-2026-08-28-全联机死亡实体与尸体终态同步.md` | Partially fixed; divergence probability significantly reduced | The authoritative death event and corpse final-state submission for ordinary Mooks are implemented. User testing still finds occasional low-probability inconsistencies; continue collecting residual samples by NID. Late-join snapshot replay is not implemented. |
| `ISSUES-2026-08-28-投掷物主动引爆与重复地形伤害.md` | Historical fix pending re-verification | Independently records `DemolitionBro` and `McBrover` self-detonation synchronization and duplicate network `Grenade` terrain damage. Earlier tests confirmed self-detonation recovery, but the current source and build need to be rechecked. |
| `ISSUES-2026-08-28-McBrover火鸡主动引爆后残留实体.md` | Still reproducible; probability significantly reduced | Records residual entities after McBrover's turkey self-detonation in the official Steam `Test Evan2` map. The narrow NID idempotence fix has been deployed and the user confirmed a significantly lower frequency, but the root cause and remote lifecycle chain are not closed. |
| `ISSUES-2026-08-28-Swap-Bros切换后加入方镜头跳转.md` | In-game retest failed; fix reverted | Records field evidence and failed fix directions for the joining player's camera jump. Only read-only Swap Bros compatibility diagnostics remain. |
| `ISSUES-2026-08-27-第三方地图动态世界同步.md` | Deprecated; historical reference only | Preserves experiments and observations from the early third-party Workshop dynamic-world synchronization work. It no longer represents the current implementation, distributed state, or fix strategy; death entities and corpse final states are covered by the later independent issue. |
| `ISSUES-2026-08-26-3715087178联机通关黑屏与关卡结束重入.md` | Root cause identified, implemented, and built; in-game verification pending | Both sides connect normally. The map's successful end action repeatedly cleared the native completion guard during level switching, causing the level number to increase every frame. The document records the narrow re-entry protection patch and retest criteria. |
| `ISSUES-2026-08-22-重复退出重入加入方失败与3781818421进入第4关黑屏.md` | Partially localized and verified; unresolved scope remains | The current-map issue where the host's P1 was lost after a normal exit and re-entry is fixed and verified in two-sided testing. Join failures after about four rounds on older builds, long multi-round stability, and the `3781818421` fourth-level black screen still require separate investigation. |
| `ISSUES-2026-08-25-Utility-Mod代码借鉴方案与AFK诊断改进.md` | Main approach implemented; acceptance and retained items pending | The UMM left navigation, right-side dynamic content, language buttons, selectable log categories, and targeted review process are implemented. New diagnostics, the actual UMM interface, and save behavior still require acceptance testing; pre-serialization and other candidate improvements remain under consideration. |

## Archived Records

The following records completed their respective user-testing scope. Their original logs, approaches, and build information are preserved, but they are no longer current tasks:

| Document | Archive status | Verified scope |
| --- | --- | --- |
| `archive/ISSUES-2026-08-19 联机问题记录与修复.md` | Archived | Early multiplayer fixes, remote joining, late joining, and host-migration-related flows were tested in later work. |
| `archive/ISSUES-2026-08-20-角色退出重入后无法操作.md` | Archived | Character exit, re-entry, and input recovery were tested in later work. |
| `archive/ISSUES-2026-08-20-返回大厅和主菜单动画问题.md` | Archived | Workshop Esc return-to-lobby and lobby-to-main-menu animation were tested. |
| `archive/ISSUES-2026-08-24-联机加入方重复角色与AFK开关编译测试记录.md` | Archived | Duplicate-character fix, remote joining, and the AFK toggle were tested. |
| `archive/ISSUES-2026-08-24-FRP内网穿透联机方案.md` | Archived | FRP Direct public-network two-sided basic play, Workshop loading, and the player list were tested; extended capabilities described in the document are not automatically complete. |
| `archive/ISSUES-2026-08-26-FRP多客户端与地图内动态房间人数上限.md` | Archived | Three-player basic play and the static full-room notice for a `1`-player room were verified; four-player play, `2` to `4` capacity boundaries, dynamic capacity adjustment, and other extended scenarios still require testing. |
| `archive/ISSUES-2026-08-28-关闭Workshop注入后恢复官方地图.md` | Archived | Clearing runtime state after disabling Workshop injection and restoring the official map flow were verified by the user. |

## Reading Rules

1. To determine whether both DLLs match, compare `BUILD_INFO buildHash` in the runtime logs first. Do not rely only on file names, sizes, or modification times.
2. A successful compilation does not mean multiplayer testing passed. Only rounds with explicitly recorded test results count as acceptance evidence.
3. For network root-cause analysis, collect logs from every participant in the actual test. If a remote side can provide only logs, clearly state that MCP, UMM, or `error.log` evidence is unavailable.
4. Content marked as "restored", "reverted", or "unresolved" in an issue must not be copied back into the current implementation without re-verification.
