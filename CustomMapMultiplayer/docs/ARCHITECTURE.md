# Custom Map Multiplayer：架构与代码职责

[返回开发文档索引](DEVELOPMENT.md)

## 项目基线

- 项目面向 Steam 版 Broforce，是 Unity Mod Manager + Harmony Mod，目标框架为 .NET Framework 3.5。
- 默认网络路径是官方 Steam Lobby/P2P；`FRP Direct` 默认关闭，启用后接管房间、PID 和游戏 RPC，Steam 仍负责 Workshop 内容下载。
- 使用 Workshop 地图注入或 FRP Direct 时，所有参与端必须使用相同构建并下载相同 Workshop 地图；版本只以日志中的 `BUILD_INFO buildHash` 判断，不能依赖文件名、时间或大小。
- 只使用官方 Steam 大厅彩色名单时，显示补丁只需安装在查看方。

## 设置与运行时边界

UMM 设置页采用左侧导航和右侧动态内容布局，包含 `Multiplayer Options`、`FRP Direct`、语言和 `Diagnostic Logs` 页面。Host/Client 配置、角色选择、连接文本和总开关会自动应用，不依赖 Apply 按钮。

Workshop 专用行为必须同时满足有效的 Workshop 注入配置、线上会话和配置场景等条件。关闭注入、停用或卸载 Mod、离开 Steam 房间时，应清理 Mod 写入的 Workshop 状态；普通官方联机保留原生战役状态。

## 源码职责

- `src/Plugin.cs`：UMM 加载、设置界面、保存和启用/禁用入口。
- `src/SettingsUiText.cs`：UMM 设置界面的中英文文案和系统语言选择。
- `src/DiagnosticSettings.cs`：Workshop、会话、当前页面、语言、FRP 和日志类别配置。旧的折叠状态字段仅为迁移兼容保留。
- `src/DiagnosticLog.cs`：普通会话日志、Harmony 追踪、分类过滤和刷新。
- `src/DiagnosticsBehaviour.cs`：场景、Unity 错误和英雄生成状态观察。
- `src/HarmonyDiagnostics.cs`：大厅、关卡切换、Workshop 加载、玩家和英雄流程。
- `src/HarmonyDiagnostics.WorkshopIdentity.cs`：房主地图身份发布、加入方会话配置采用、Steam 订阅检测和缺图加载保护。
- `src/HarmonyDiagnostics.WorkshopPlayer.cs`：Workshop 本地玩家请求、控制器所有权、掉线重入和英雄类型恢复。
- `src/HarmonyDiagnostics.WorkshopPickup.cs`：道具确定性、拾取所有权、幂等和满弹药退避。
- `src/HarmonyDiagnostics.WorkshopLevelEnd.cs`：Workshop 关卡结束动作防重入保护。
- `src/HarmonyDiagnostics.Afk.cs`：原生 AFK 倒计时、主动 AFK、超时和槽位移除观测。
- `src/HarmonyDiagnostics.Acid.cs`：英雄酸液入口、酸池扫描、本地预测、Host 权威校验和死亡链日志。
- `src/HarmonyDiagnostics.EntityFinalState.cs`：普通网络 Mook 的死亡事件、尸体终态同步、待提交候选和生命周期清理。
- `src/HarmonyDiagnostics.LevelOutcome.cs`：`LevelFinish`/`RemoveLife` 前后快照。
- `src/HarmonyDiagnostics.Reflection.cs`：连接层状态读取及反射元数据缓存。
- `src/HarmonyDiagnostics.Trace.cs`：Harmony 方法追踪消息格式化及追踪反射缓存。
- `src/OptionalBroModDiagnostics.cs`：Swap Bros 公开 API、版本和角色指纹的只读弱依赖诊断。
- `src/ReflectionProbe.cs`：只读扫描 `Assembly-CSharp` 中的相关类型。
- `src/OnlinePlayerListFormatter.cs`：Steam/FRP 在线名单的延迟颜色、房主渐变、Rich Text 转义和秒到毫秒换算。
- `src/FrpDirectTransport.cs`：Lidgren UDP、多连接握手、认证、心跳、重连和可靠字节路由。
- `src/FrpDirectRoomInfo.cs`：FRP 房间信息和 Workshop 阶段元数据。
- `src/FrpDirectLayer.cs`：复用原生 PID、ServerID、RPCBatcher 和 `RecieveBytes`，按机器路由多客户端 RPC。
- `src/FrpDirectNetworkManager.cs`：选择 FRP/Steam 层并管理 `Connect.layer` 生命周期。

## 设计边界

- 方法级追踪不记录房间密码、Steam ID、主机名或 Workshop 作者身份。
- Workshop 专用补丁不得因遗留场景名、Lobby phase 或本地旧配置误作用于普通官方联机。
- 本项目不引用 RocketLib，不调用换人 API，也不因为可选 Mod 指纹不同拒绝会话。
- 活动 AI、敌方弹体、钱币、金色奖励、载具和普通 `Grenade` 地形伤害不在持续同步范围内。
