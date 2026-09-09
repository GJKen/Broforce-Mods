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
| `ISSUES-2026-09-08-增加Workshop地图选择与UMM面板布局优化.md` | Workshop directory, title lookup, and map selection page implemented; latest runtime screen still needs a fresh MCP retest | The current page switches the UMM content area, uses a two-column grid, search/filter/favorite/recent-online-map controls, and manual Workshop ID input; the Starred tab can clear all starred maps at once. It preserves not-installed, incomplete-download, and unreadable-campaign states and the existing Workshop/multiplayer behavior. |
| `ISSUES-2026-09-05-Workshop房主退出后加入方返回黑屏.md` | Steam single-client Host-exit fix accepted by the user; FRP Direct comparison returns normally to MainMenu | Records the false Host-migration decision after a Steam Host leaves, the Workshop load loop and black screen, and the exit cleanup fix; FRP Direct does not support Host migration, but its joining player returns directly to MainMenu and is not affected by this issue. |
| `ISSUES-2026-09-05-联机聊天框Esc后无法再次呼出.md` | Fixed and passed an in-room multiplayer regression; separate Host/Client records remain to be added | The failure after `Enter -> Esc -> Esc -> Enter`, including the inability to send with Enter after reopening, is fixed by synchronizing the chat boundary and stale pause-menu state. See the issue's verification steps. |
| `ISSUES-2026-08-30-Assembly-CSharp反编译与重建方案.md` | Plan documented; temporary replacement experiment pending | Covers `Assembly-CSharp.dll` decompilation, IL patching, C# rebuilding, isolated testing, and rollback. An isolated Chinese-input switch Mod now exists for local testing; the official assembly has not been rebuilt. |
| `ISSUES-2026-08-31-Test_Evan2原生地图对象与投掷物清理空引用.md` | Target exceptions passed two-sided regression; crate guard branch not directly hit | The `5582c...` regression produced no `TorturedVillager`, `Map.RemoveProjectile`, or `DoodadCrate` errors. Villager and projectile guards each triggered once; the crate error branch was not directly exercised. |
| `ISSUES-2026-08-30-加入方箱子坍塌特效持续重复.md` | Reproduced historically; not reproduced in latest short test, direct acceptance pending | The latest `5582c...` two-sided test had no crate error loop or repeated collapse, but did not directly trigger either crate guard branch. Its relationship to `BroforceBugFix` remains tracked separately. |
| `ISSUES-2026-08-30-联机房主低帧率与Host专属扫描性能问题.md` | Sustained low FPS not reproduced in latest short test; formal A/B no longer scheduled for this round | After excluding reload and pre-exit host-migration windows, `5582c...` measured Host/Client weighted frame times of `11.284/9.149ms`; Host acid-pool refresh totaled `2264.130ms`. These numbers remain observational only; unified settings, reverse Host, and repeated p50/p95/p99 comparison rounds are no longer required for this round. |
| `ISSUES-2026-09-07-投掷酸液命中角色未正常死亡.md` | Fixed and verified by user in-game testing | `DamageType.Acid` hits now enter the Workshop hero acid-death path even when the thrown projectile does not create a `DoodadAcidPool`; the user confirmed the hit character dies normally. |
| `ISSUES-2026-08-28-全联机死亡实体与尸体终态同步.md` | Partially fixed; divergence probability significantly reduced | The authoritative death event and corpse final-state submission for ordinary Mooks are implemented. User testing still finds occasional low-probability inconsistencies; continue collecting residual samples by NID. Late-join snapshot replay is not implemented. |
| `ISSUES-2026-08-28-McBrover火鸡主动引爆后残留实体.md` | Still reproducible; probability significantly reduced | Records residual entities after McBrover's turkey self-detonation in the official Steam `Test Evan2` map. The narrow NID idempotence fix has been deployed and the user confirmed a significantly lower frequency, but the root cause and remote lifecycle chain are not closed. |
| `ISSUES-2026-09-07-Swap Bros Always spawn as chosen bro与联机角色生成冲突.md` | Verified in offline and Steam multiplayer testing | The user confirmed that both offline and Steam rooms spawn the locked hero. The implementation uses the confirmed `Swap_Bros_Mod.Main.GetSelectedBroHeroType(Int32)` and real `Main.settings`/`Settings.alwaysChosen` declarations; map-forced heroes still follow `ignoreForcedBros`. |
| `ISSUES-2026-08-28-Swap-Bros切换后加入方镜头跳转.md` | In-game retest failed; fix reverted | Records field evidence and failed directions for the joining player's camera jump. The camera/character binding fix remains reverted; a separate hero-selection compatibility fix is tracked independently. |
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
| `archive/ISSUES-2026-08-28-DemolitionBro炸弹主动引爆联机同步.md` | Archived | `DemolitionBro.currentBomb` self-detonation synchronization was implemented and verified in two-sided multiplayer testing; the user confirmed no issue. |
| `archive/ISSUES-2026-08-28-投掷物主动引爆与重复地形伤害.md` | Archived | Preserves the historical self-detonation and duplicate terrain-damage analysis; the document explicitly retains the current source/build re-verification boundary. |
| `archive/ISSUES-2026-08-30-Workshop联机酸液池导致双方一起死亡.md` | Archived | Two-sided acid-pool death isolation was verified in-game; thrown-acid hits have a separate record. |
| `archive/ISSUES-2026-08-31-FRP断线后主菜单动画变慢与残留对象.md` | Archived | The root cause and formal fix are recorded; the low-probability disconnect reproduction boundary remains in the document. |
| `archive/ISSUES-2026-09-01-新增ESC菜单主动AFK按钮.md` | Archived | Manual AFK, the single-player notice, re-entry recovery, and Esc-menu language switching completed their corresponding in-game acceptance. |
| `archive/ISSUES-2026-09-04-Workshop缺图仍进入加载动画.md` | Archived | The pre-load interception and post-load protection for missing Workshop maps are recorded. |
| `archive/ISSUES-2026-09-04-Workshop缺图运行时提示未按语言切换.md` | Archived | Bilingual missing-map prompts and the host Workshop ID display passed two-sided testing. |
| `archive/ISSUES-2026-09-05-联机聊天输入框长按删除与光标移动.md` | Archived | Long-press deletion, caret movement, and the real multiplayer input regression are recorded. |
| `archive/ISSUES-2026-09-05-联机聊天输入框长消息可视滚动效果不理想.md` | Archived | The core viewport fix passed real-keyboard validation; full input-matrix boundaries remain in the document. |
| `archive/ISSUES-2026-09-05-联机聊天中文输入法支持与关键改动.md` | Archived | Chinese IME, candidate submission, numeric input, and reopening chat passed real-keyboard validation. |
| `archive/ISSUES-2026-09-06-联机聊天输入框上下方向键跨视觉行移动.md` | Archived | The key viewport and visual-line Up/Down regression passed; full input-matrix boundaries remain in the document. |
| `archive/ISSUES-2026-09-06-联机聊天输入框右下角字数显示.md` | Archived | The lower-right character counter passed real-keyboard acceptance. |
| `archive/ISSUES-2026-09-06-联机聊天长消息发送后历史显示被隐藏.md` | Archived | Input, sending, and clearing were verified; the remaining chat-history boundary is retained in the document. |
| `archive/ISSUES-2026-09-07-联机聊天输入框激活时角色仍可操作.md` | Archived | Control-input blocking while the chat box is active passed real-keyboard acceptance. |

## Reading Rules

1. To determine whether both DLLs match, compare `BUILD_INFO buildHash` in the runtime logs first. Do not rely only on file names, sizes, or modification times.
2. A successful compilation does not mean multiplayer testing passed. Only rounds with explicitly recorded test results count as acceptance evidence.
3. For network root-cause analysis, collect logs from every participant in the actual test. If a remote side can provide only logs, clearly state that MCP, UMM, or `error.log` evidence is unavailable.
4. Content marked as "restored", "reverted", or "unresolved" in an issue must not be copied back into the current implementation without re-verification.
