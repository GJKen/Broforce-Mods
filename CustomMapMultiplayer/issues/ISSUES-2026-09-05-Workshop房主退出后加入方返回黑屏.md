# Workshop 房主退出后加入方返回黑屏

## 状态

历史根因已定位，但此前把“只剩一个加入方”处理为退出房间的方案不符合官方 Steam Lobby 行为。2026-09-10 的最新双端日志确认该分支会错误触发 `SteamLayer.LeaveMatch`；当前修正恢复官方 Host migration：只要加入方仍在房间，就继续接管 Host 和 Workshop 状态。本文件中的单 Client 退出结论已被当前修正覆盖，用户已确认修复版本符合预期。

此前保留的 Steam 修复构建和 FRP 对照记录仍作为历史证据，不代表当前 Steam 单 Client 的验收预期。

用户此前确认 Steam 测试返回正常：房主退出后，加入方能够正常返回，不再出现黑屏。本次 FRP 实验使用了明确排除本轮新增退出保护的构建 `buildHash=69ad1b5fdbbe22d74d5ed166bea8b50ad2e24c6c028b5daba9eb4e51ef4e6f3b`，用户确认 FRP 房主退出后加入方会直接返回主菜单，没有复现本 issue 的黑屏。因此 FRP Direct 不属于本 issue 的受影响路径；真正的多人 Host migration 和其它退出路径仍需分别回归。

## 复现现象

1. 加入方进入房间后地图状态不正确，停留在 `P1-P4` 角色界面。
2. 房主随后进入 Workshop 地图。
3. 房主退出房间。
4. 加入方收到 `ConnectionLayer.RemovePlayer` 后被错误判断为新的房主。
5. 加入方返回时进入黑屏，无法正常回到原生主菜单。

## 根因

旧逻辑只根据 `IsOnlineHost()` 的结果变化判断 Host migration：

```text
client -> host
```

当房主离开、房间内只剩一个加入方时，网络层也会把这个加入方暂时标记为 Host。旧逻辑将这个“房主离开后的单客户端状态”当成真正的多人 Host migration，执行 `HandleWorkshopHostPromotion`，重新设置：

```text
GameState.loadCustomCampaign = true
```

随后旧的 Workshop 加载请求和回调仍然生效，反复触发：

```text
GameState.LoadLevel(MainMenu)
SteamController.LoadLevel
Workshop UGC 回调
```

最终没有稳定的 `MainMenu` 场景加载，也没有正常的 `SESSION_END`，表现为黑屏和加载循环。

## 修复方案

### 1. 识别官方 Host migration

- 在客户端仍能读取 `PID.ServerID` 时保存当前 Host PID。
- 在 `ConnectionLayer.RemovePlayer` 的清理前观察被移除的 PID，确认它是否为当前或此前记录的 Host PID。
- Host 角色变化时确认旧 Host PID 已离开；当前本地加入方仍是房间成员。
- 当前加入方仍是房间成员时，无论是否还有其它远程玩家，都将自身计入有效成员并继续执行原有 Host migration 流程。
- 不再使用“其它远程成员数量为零”作为退出房间条件，避免把唯一剩余的加入方错误清理掉。
- 只有旧 Host 排除后连接表中没有任何有效成员时，才执行退出清理。

### 2. 历史上的单客户端退出清理

此前的单客户端退出路径曾调用 `ClearInjectedWorkshopRuntimeState`，清理网络会话、Workshop 身份、关卡切换状态、暂停状态和延迟加入状态，并将游戏状态恢复为原生主菜单。

该路径已被当前官方 Host migration 要求覆盖，不再用于 Steam 房主离开后的单 Client 场景。

### 3. 阻止退出后的 Workshop 加载循环

退出清理期间设置原生主菜单退出保护：

- `GameState.LoadLevel(MainMenu)` 被允许继续执行。
- 过期的 `SteamController.LoadLevel` 请求被拦截。
- 过期的 Workshop UGC 详情回调被拦截。
- Workshop 加载请求和重复加载抑制状态被清理。
- 原生 `MainMenu` 加载后释放退出保护。

### 4. FRP Direct 对照结果

FRP Direct 的协议不执行 Host migration，房主退出时结束房间。2026-09-05 的实验构建明确绕过本轮新增的 FRP 退出保护后，用户确认加入方仍会直接返回主菜单，没有复现本 issue 的黑屏。这说明本 issue 的黑屏根因属于 Steam Lobby 房主离开后的 Host 角色变化和 Workshop 加载循环，不应扩展为 FRP 问题。

## 修改文件

- `src/HarmonyDiagnostics.cs`：保存在线 Host PID 和原生主菜单退出保护状态。
- `src/HarmonyDiagnostics.Lifecycle.cs`：Host 角色判断、旧 Host 移除观察、单客户端退出清理和主菜单恢复。
- `src/HarmonyDiagnostics.Patches.cs`：允许退出路径的原生 `MainMenu` 加载。
- `src/HarmonyDiagnostics.WorkshopCache.cs`：阻止退出后的过期 Workshop 加载和 UGC 回调。

## 后续回归修正（2026-09-10）

最新 Client 日志 `diagnostics-client-auto-20260909-235119-239-efc3c742-20260909-235119-239.log` 记录了错误路径：

```text
Online session role changed from client to host after the old host left, but no remote member remains ...; treating this as room exit instead of Host migration.
SESSION_END reason=SteamLayer_LeaveMatch
```

这里的“no remote member”只表示没有其它远端 PID，不表示房间为空；当前 Client 自己仍在房间内，应该计为有效成员并继续成为 Host。当前源码已改为统计包含本地成员的有效连接；单 Client 和多人 Client 都继续执行 `HandleWorkshopHostPromotion`，只有确实没有有效成员时才执行退出清理。

## 历史运行日志证据

此前修复分支的一次双端日志记录了该修复路径：

- 证据：Host 和 Client 的双端诊断日志。
- 日志构建：`a031ae78b859fd2e43513ea08208a0599931b45758bda431ca1993fbb38c0189`

Client 退出时记录了：

```text
Online session role changed from client to host after the old host left, but no remote member remains ...; treating this as room exit instead of Host migration.
Suppressed Workshop Host promotion because the room has no remaining remote member; clearing network and Workshop runtime state for the native MainMenu.
Allowed native MainMenu load after online Host departure; Workshop injection is disabled.
SESSION_END reason=SteamLayer_LeaveMatch
```

该日志用于说明修复行为和时序。它不是当前指定的有效验收构建，也不是本次用户验收的日志证据。

## 验收重点

- 加入方停留在 `P1-P4` 或任务失败状态时，房主退出后仍应完成 Host migration，不应主动回到原生主菜单。
- 单 Client 和多 Client 都应继续发布 Workshop 身份、地图和大厅 ready 状态。
- Steam 日志不应出现由该路径触发的 `SESSION_END reason=SteamLayer_LeaveMatch`。
- FRP Direct 对照测试仍确认房主退出后加入方直接返回主菜单；FRP 不支持 Host migration。
- 正常主动退出、突然断线、多次重试以及真正的多人 Host migration 仍需分别回归。

## 实施记录（2026-09-05）

- 将单客户端退出与真正的多人 Host migration 分开处理。
- 将退出后的 Workshop 请求拦截和原生主菜单放行逻辑移植到 `main` 基线。
- 在 `main` 上完成 Release 构建并部署到本机及内网 UMM 路径。
- 用户已确认 Steam 房主退出后的加入方返回测试正常，Steam 黑屏问题本轮验收通过。
- 本轮未提交黑屏修复源码；用户未提供本次测试的独立日志或新的 `buildHash`。

## FRP Direct 对照测试记录（2026-09-05）

- 为确认 FRP 是否依赖本轮退出保护，构建了排除 FRP 新增 Host migration 判定和原生主菜单保护的实验版本：`buildHash=69ad1b5fdbbe22d74d5ed166bea8b50ad2e24c6c028b5daba9eb4e51ef4e6f3b`。
- 用户测试确认：FRP 房主退出后，加入方直接返回主菜单，没有复现本 issue 描述的黑屏。FRP 不发生 Host migration，仍保持现有结束房间行为。
- 本次只记录用户对照测试结论，没有新的双端日志或截图；FRP 不纳入本 issue 的修复范围。
