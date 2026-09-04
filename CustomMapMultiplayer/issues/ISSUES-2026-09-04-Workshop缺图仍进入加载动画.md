# Workshop 缺图仍进入加载动画

## 当前现象

- 加入方没有订阅房主 Workshop 地图时，仍会进入 Workshop 地图加载流程的动画。
- 随后地图加载调用虽然被拦截，但加入方会持续停留在加载流程。
- 缺图状态下 `GameState.LoadLevel` 被重复调用。

## 根因

1. 当前缺图阻止点位于 `GameState.LoadLevel`，晚于地图切换上游。日志先出现 `LevelSelectionController.GotoNextCampaignScene`、`GameModeController.LoadNextScene`，并进入 `LoadingScreen` 和 `MissionScreenVietnam`，之后才进入 `GameState.LoadLevel`。因此当前代码只能阻止最终地图加载，不能阻止此前已经开始的加载动画。
2. `GameState.LoadLevel` 返回 `false` 只阻止当前调用，没有终止触发它的上游切关状态或重试链。缺图期间 trace 持续出现 `GameState.LoadLevel(nextScene="Test Evan2")`，说明加载流程仍在重复请求。

## 日志证据

- Host：`diagnostics-host-auto-20260904-045324-714-4ebf1cbf-20260904-045324-714.log`
- Client：`diagnostics-client-auto-20260904-045331-504-6f763026-20260904-045331-504.log`
- Client trace：`diagnostics-client-auto-20260904-045331-504-6f763026-20260904-045331-504.trace.log`
- Client 在约 `+0.518s` 识别未订阅；约 `+9.353s` 进入 `LoadingScreen`；约 `+9.819s` 进入 `MissionScreenVietnam`；约 `+11.068s` 才开始被 `GameState.LoadLevel` 阻止。

## 修复方案

1. 在 `LevelSelectionController.GotoNextCampaignScene` 或其紧邻的地图切换入口增加加入方缺图前置阻止：当 `_workshopSubscriptionMissing` 已确认、当前为线上 Workshop 加入方且目标是本次房主地图时，直接终止本次地图切换。
2. 前置阻止点应早于 `GameModeController.LoadNextScene`，使加入方不会进入本次 Workshop 地图的加载动画。
3. 保留 `GameState.LoadLevel` 作为后置保护，但将阻止记录改为低频或一次性；前置阻止成功后不应再由上游重复触发 `GameState.LoadLevel`。
4. 正常已订阅地图、房主、官方地图和非 Workshop 会话继续走原生流程。

## 验收重点

- 加入方未订阅房主地图时，在进入 Workshop 地图加载动画前终止本次地图切换。
- 缺图后不再持续重复调用 `GameState.LoadLevel`，也不持续停留在 Workshop 加载流程。
- 已订阅地图的双端 Workshop 加载、晚加入和重入行为不受影响。

## 实现记录（2026-09-04）

- 复用已安装在 `TracePrefix` 中的 `LevelSelectionController.GotoNextCampaignScene` 入口，在确认加入方缺图且目标为本次房主地图时直接返回 `false`，阻止后续 `GameModeController.LoadNextScene` 加载动画。
- 保留 `GameState.LoadLevel` 后置保护；缺图阻止日志按 Workshop 身份一次性记录，避免重复输出。
- 缺图时继续清除晚加入状态；房主、已订阅地图、官方地图和非 Workshop 会话保持原生切换流程。
- 已使用 .NET Framework 3.5 Release 构建脚本编译通过。

## 验收记录（2026-09-04）

- 双端实测确认：加入方未订阅房主 Workshop 地图时，已在进入 Workshop 加载动画前成功拦截地图切换。
- 本次记录确认的是前置拦截结果；其它地图、已订阅路径和长期重复尝试仍按验收重点继续覆盖。
