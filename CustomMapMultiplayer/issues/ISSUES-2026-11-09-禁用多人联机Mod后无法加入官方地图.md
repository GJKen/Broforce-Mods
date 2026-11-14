# 加入方禁用多人联机 Mod 后无法加入官方地图：现场检查

检查日期：2026-11-09（北京时间）。以下保留首次现场排查记录；当时没有修改插件源码、构建或部署。后续复现与修复结果见文末。

## 初始现场

- 本机 JKChan 为房主，Steam Lobby `109775243943865360`，官方街机战役 `VIETNAM`，`loadCustomCampaign=False`、`customLevelID` 为空；地图注入和 FRP 配置均为关闭。
- `Test Evan2` 同时是原生 `LevelSelectionController.CampaignScene`，不能据此认定为三方图。房主使用 Aquabro。
- 内网 5700G 的加入方为 moumentai。UMM 明确记录 `[CustomMapMultiplayer] To skip (disabled).`，运行时也没有加载该程序集。
- Steam 大厅包含双方，但加入方停在 MainMenu，`Connect.Layer.PlayerIDPairs.Count=0`，房主为 2；加入方仍保留 `loadCustomCampaign=True`、旧 Workshop ID `3781818421`，且 `Networking.Networking.PauseStream=True`。
- 双端网络版本 3148、类型数 4904 一致。双方实际游戏 `Assembly-CSharp.dll` SHA-256 均为 `6059336EE9E93A9BCD46E89D259687AE179CE32E0DDBEE78DD7B0ACF8B0FF3A9`。
- 本机最新诊断 `.log` / `.trace.log` 最初显示为 0 字节，也已实际读取；随后缓冲数据写出，确认该房间走官方地图流程。

## 观察和操作

1. 在双端安装临时 `trace-native-join.cs`，记录加入回调、身份注册、收包、P2P 状态和异常；使用原生 `SteamLayer.JoinLobby` 重新加入同一个房间。
2. 双端曾报告 P2P `active=1`，但加入方未执行身份注册。后续加入方明确收到 `P2PSessionConnectFail_Callback`，`m_eP2PSessionError=4`（超时）。
3. 在房主通过原生 `Connect.DeregisterPlayer` 清理未入场的远端 PID 7，并在加入方调用原生 `SpamFirstContact`，未恢复入场。房主随后重新分配 PID 8。
4. 双端显式调用 `SteamNetworking.AcceptP2PSessionWithUser` 均返回 true，再发送 FirstContact，加入方开始收到 RPC 字节。此操作仅属于诊断，不能据此认定原生接受回调是根因。
5. 检查加入方待执行 RPC，发现 `RegisterPlayerID`、`SetServerID`、同步及 BroMaker 实例化消息均积压；将加入方 `Networking.Networking.PauseStream` 从 true 改为 false 后，日志立即出现 `RegisterNewPlayer` 和 `Connect.IDsAreSetup`。证实网络流暂停会阻断现场的身份处理。
6. 处理积压消息时，BroMaker 尝试解析房主的 `BroMaker:hero:Aquabro`。`LoadHero.CreateOriginal` 抛出 `NullReferenceException`，调用链为 `GetOrCreateNetworkPrefab -> ResolveHeroPrefab -> NetworkPrefabs.ResolvePrefab`，随后 BroMaker 主动返回主菜单、离开房间，玩家身份表清空。

## 结论及限制

已确认禁用生效，房间是官方地图，游戏版本一致。现场存在 Steam P2P 超时和加入方网络消息流暂停；解除暂停后还触发自定义角色网络 prefab 在 MainMenu 解析失败。最终加入方仍未入场，不能报告联机成功。

Aquabro 异常是在处理积压消息时观察到的第二个阻断点，尚未证明它也是最初那次失败的触发原因。旧 Workshop 字段属于已观察到的残留，尚未证明其来源或与暂停状态的因果关系。本次未实施永久修复，也未进行重启后的干净重试。

临时追踪脚本已从双端卸载，移除了追踪补丁、日志事件和 runner 对象；本次未启动或重启游戏，原有游戏进程保留。加入方网络流最终为 false，多人联机 Mod 保持禁用。

## 证据

- [加入方大厅截图](../docs/evidence/2026-11-09-official-join/client-lobby.png)。

按用户要求，已删除本轮临时追踪脚本及日志副本；现场观察保留于上述记录。

## 后续复现与修复

继续排查时重启双端，加入方保持 CMM 禁用，干净状态可以正常加入官方 VIETNAM，双端均为 2 名玩家。通过原生线上地图加载流程主动请求旧 Workshop ID 后，复现 MainMenu、PauseStream=true；接着原生加入官方房仍暂停，身份表为 0。

原版 GameState.LoadLevel 在线上先暂停 RPC，再异步下载 Workshop；下载流程未到达 OnStart 时没有恢复暂停。新 JoinLobby 的 Disconnect 也不恢复该静态状态，导致下一房间身份消息无法执行。最初现场的那一次 Workshop 请求由什么操作触发，旧日志仍不足以还原；本轮受控复现没有直接写入 PauseStream。

在 BroforceBugFix 0.3.0 新增独立修复：主菜单加入新房间、清理完旧房后，若仍暂停则清理旧 RPC 队列并解除暂停。已构建部署双端；同一路径复测后正常进入官方房，双端 2 名 Aquabro，加入方有本地角色且 CMM 仍未加载。临时脚本已卸载，测试启动的游戏验证后关闭。

完整修复及保留的验证结果见 [BroforceBugFix 问题记录](../../BroforceBugFix/issues/ISSUES-2026-11-09-未完成地图加载后加入官方房卡在主菜单.md)。首次干净复现及修复前重试的日志副本已按用户要求清理。
