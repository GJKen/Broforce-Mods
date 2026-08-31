# Custom Map Multiplayer：网络与房间

[返回开发文档索引](DEVELOPMENT.md) · [架构与代码职责](ARCHITECTURE.md)

## FRP Direct 网络层

`FrpDirectTransport` 复用 Lidgren，应用标识为 `CustomMapMultiplayer.FrpDirect.v1`。`EnableFrpDirect` 同时控制传输和游戏连接层；Host/Client 配置隔离，角色、总开关和连接文本自动应用，无 Apply 按钮。

- Host 监听配置的 UDP 端口（默认 27045），可在地图内设置 `1` 至 `4` 人总上限；Client 以临时端口连接 `host:port`，普通断线后每 5 秒重试。
- Host 以挑战、密码、协议版本、双方 `buildHash` 和机器 ID 完成 HMAC-SHA256 握手。协议 v4 提供房间、加入/离开、机器路由 `GameData` 和 RTT 快照；协议不匹配或认证失败会拒绝连接。
- Host 使用原生 PID 分配与定向映射同步；客户端数据经 Host 中继，目标非 Host 的 RPC 不在 Host 重复执行。房间层按 `capacity - 1` 拒绝新加入，降额不移除既有成员或 PID。
- Client 离开或断线只清理该机器的 PID；Host 离开会结束房间，不支持主机迁移。RTT 为各机器至 Host 的往返时间。
- Workshop 内容仍从 Steam 下载，房间和 RPC 走 FRP；密码只保护握手，不加密后续 UDP。Client 每 5 秒心跳，正常情况下 60 秒无有效心跳才断开。

三机基础联机与静态 `1` 人房满员提示已实测；四机、`2` 至 `4` 人边界、动态容量和主机迁移仍待验收。完整历史证据见 [FRP Direct 实施与验收记录](../issues/archive/ISSUES-2026-08-24-FRP内网穿透联机方案.md)。

## 在线玩家延迟名单

- 官方 Steam 大厅和 FRP Direct 都复用原生 `Interface.OnlinePlayerList`，显示 `xxxms | 玩家名`。首个样本尚未产生时显示灰色 `--ms`；`0-80ms` 为绿色、`81-150ms` 为黄色、`151ms` 以上为红色。
- 房主由 `PID.ServerID` 或 FRP Host 身份识别，显示 `HOST | 房主名`；房主名使用 4 秒一轮的动态暖色到青色渐变。名字中的尖括号会转义，避免注入 Unity Rich Text。
- Steam 使用游戏原生 `PingController` 暴露的 `PID.Ping`，按秒换算为毫秒。名单按 PID 映射而不是玩家名关联；核心 PID 尚未建立时回退原生 Steam 大厅名单，远端 PID 同步期间持续格式化已有成员并在后续刷新补齐。
- Steam 名单格式结果缓存 0.1 秒，原生 `SteamLayer.Update` 仍每帧请求名单，因此渐变以 10 FPS 更新而不会每帧重新分配富文本。Steam 显示是本地 UI 改动，不需要额外协议，也不要求其他玩家安装 Mod。
- FRP RTT 表示每台机器到房主的往返时间；Steam Ping 是当前机器通过游戏原生采样得到的对应 PID 延迟，不同玩家视角的数值可能不同。

## Esc 返回大厅

原生 Workshop 返回链为：

```text
GameModeController.LoadNextScene(VictoryCustomCampaignSteam)
VictoryCustomCampaignSteam
CustomLevelRatingMenuSteam
MainMenu
```

有效 Workshop 线上会话从暂停菜单返回时，Mod 将目标直接改为 `MainMenu`，清除 `loadCustomCampaign`/`immediatelyGoToCustomCampaign`，跳过通关时间和评分界面。`MainMenu.InitializeMenu` 完成后清理陈旧暂停状态，再调用原生 `TryToGoToLobby(Online)`。

返回主菜单的 Logo 动画复用原生 `Lobby.GoBackToMainMenu -> MainMenu.Show -> ShowRoutine`；菜单文字、高亮和 Renderer 在动画完成后恢复。打开大厅失败时恢复完整主菜单，最长等待 30 秒，避免隐藏或不可操作状态。

## 相关实现文件

- `src/FrpDirectTransport.cs`、`src/FrpDirectLayer.cs`、`src/FrpDirectNetworkManager.cs`：传输、原生连接层和生命周期。
- `src/FrpDirectRoomInfo.cs`：FRP 房间信息和 Workshop 阶段元数据。
- `src/OnlinePlayerListFormatter.cs`：Steam/FRP 在线名单格式化。
