# FRP 断线后主菜单动画变慢与残留对象

## 状态

**根因已定位，正式修复已构建并部署；问题复现概率较低，暂不宣称完成实机验收，后续按正常 FRP 会话持续观察。**

本记录描述 FRP 房间联机过程中加入方突然退出房间后，回到 `MainMenu` 时动画明显变慢的问题。现有证据来自一次内网 Client 会话；没有在新构建上强制制造断线，也没有把当前运行进程重启后观察结果误记为回归通过。

## 现场证据

- 日期：2026-08-31（日志时间 UTC `00:15:04` 至 `00:16:06`）。
- 传输：`FRP Direct`，角色 `client`。
- 触发链：`SESSION_BEGIN trigger=FrpDirectLayer_JoinLobby`，随后 `FRP_DIRECT transport disconnected; role=client; handshakeCompleted=True`，最终 `SESSION_END reason=FRP_Direct_host_transport_disconnected`。
- 旧问题会话 `buildHash`：`7f50860ebec2ea353eb7f087b48ced192b2e246ef50cf4dfd05a9f7a780f03ce`。
- 日志文件：`diagnostics-client-auto-20260831-001504-132-1b8cd01c-20260831-001504-132.log`。

MCP 在断线后的主菜单状态中观察到：

- `scene=MainMenu`、`gameMode=Campaign`、`pauseState=UnPaused`、`timeScale=1`，排除全局暂停或时间缩放导致的慢动画。
- 仍有 `11` 个激活的 `TestVanDammeAnim`，全部为旧 Workshop 地图坐标上的 `ZMook(Clone)`，并保留 `MookTrooperAI`、`PathAgent`、`Rigidbody` 等更新组件。
- `/Connect(Clone)` 仍激活，网络同步相关组件继续运行；当前菜单没有正常的 `Map`、`Block`、`Doodad` 或 `GameModeController` 内容。
- `PERF_SUMMARY` 的 `frameAvgMs` 约 `149--155 ms`，`frameP50Ms` 约 `149--156 ms`，实际约 `6--7 FPS`。
- Unity 日志持续出现 `TestVanDammeAnim.Update -> BroBase.Update` 和 `PlayerHUD.LateUpdate` 的 `NullReferenceException`。

这些对象与错误只在离房后的 `MainMenu` 残留状态中出现；菜单自身的 `transitioning`、`showHideRoutine` 和 `menuActive` 状态正常，动画协程并未卡住，慢速观感是主线程被残留对象和异常拖慢的结果。

## 根因

`FrpDirectLayer.OnRemoteDisconnected` 在 Client 侧调用 `LeaveRemoteRoom("host transport disconnected")`。该方法原先执行了：

1. `HarmonyDiagnostics.PrepareFrpDirectRoomExit`；
2. `Connect.OnConnectionDown()`；
3. `ResetRoomState()`；
4. `base.LeaveMatch(-1)`；
5. `CompleteFrpDirectRemoteRoomExit`。

但它漏掉了本地正常退出和配置变更路径都会调用的原生 `OnGameDestroyed()`。因此 PID/房间状态虽然被清空，旧关卡实例和 `Connect(Clone)` 没有走完整销毁流程，继续在主菜单逐帧更新并抛出空引用。

## 正式修复

在 `src/FrpDirectLayer.cs` 的 `LeaveRemoteRoom` 中，紧跟 `base.LeaveMatch(-1)` 补调用 `OnGameDestroyed()`，使远端异常离房与本地退出使用等价的原生对象销毁顺序。没有修改 FRP 协议、重试策略或主菜单动画实现。

修复通过标准脚本构建并部署到项目包、本机 UMM 和测试机 UMM：

- `buildHash=b8a929e505e65f20ce3afc37bc57fb8b0d52b450c00082ca348932473c5d83c4`。
- DLL SHA-256：`E9ED9E2A4E131051E03D2E7FCB22C28C08157207C6FFCC997B9DF6F35BF68B9C`。
- 三处部署文件哈希一致；README 和开发文档已同步当前构建信息及清理行为。

## 当前验证边界

部署后 MCP 连接到的游戏进程仍是旧运行态，尚未重启加载新 DLL；因此仍能观察到上述 `11` 个 Mook 和 `Connect(Clone)`，这不代表正式修复失败。为遵守 MCP 受控观测规则，本轮没有调用重启、模拟输入或运行时代码，也没有人工制造新的断线样本。

短期恢复方式是完全退出并重新启动 Broforce，清除旧进程中的残留对象。后续遇到自然发生的 FRP 房主断开时，按同一会话记录双方 `BUILD_INFO`、`SESSION_END`、MCP 场景对象和 `PERF_SUMMARY`。

## 后续观察判据

满足以下条件后，才可把本 issue 标记为已验证：

- 新构建 Client 在 FRP 房主自然断开后回到 `MainMenu`，激活的 `TestVanDammeAnim` 数量为 `0`。
- 不再存在继续运行的 `Connect(Clone)` 网络对象，或其已明确停止并完成销毁。
- 新增日志中不再出现 `TestVanDammeAnim.Update`、`PlayerHUD.LateUpdate` 相关空引用。
- `PERF_SUMMARY` 恢复到正常菜单帧率，且主菜单 Logo/文字动画时序正常。
- 至少观察一轮正常退出和一轮自然异常断线；若长期没有再次复现，保留“已修复、低概率待观察”状态，不通过人为断网替代实机证据。

该问题与已归档的普通大厅返回主菜单动画时序问题不同：前者是菜单对象显示顺序，本文记录的是 FRP 异常离房后的旧关卡对象生命周期泄漏。
