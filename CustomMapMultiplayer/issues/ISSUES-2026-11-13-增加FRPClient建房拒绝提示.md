# 增加 FRP Client 建房拒绝提示

本项为 FRP Client 点击创建房间时增加明确提示，并保留建房菜单。本次建房失败由角色配置错误引起，改为 Host 即可恢复建房；代码改动用于改善误操作反馈，不是恢复建房的必要条件。

## 现场与根因

- 用户授权通过远端 MCP 检查并重试 FRP 建房。现场为 `newJoin`，玩家数为 0，`FrpDirectLayer.Room=null`，连接状态为 `Disconnected`。
- 远端保存的 `FrpDirectRole=client`，传输层已完成协议 4 握手并持续收到心跳。因此本次建房失败不是 UDP 隧道不可达。
- 失败会话 `auto-20261112-212240-734-da15ee2b` 在 `2026-11-12T21:22:40Z` 记录 `FRP_DIRECT client cannot create a room while configured as Client.`；同会话最新普通日志与 trace 均已读取。
- 已查阅原版 `MakeOnlineMenu.DoHostGame`、`ConnectionLayer.CreateMatch/OnCreatedLobby`、`Connect.Layer`，以及 Mod 的 `FrpDirectLayer.CreateMatch` 和连接层工厂。原生 `DoHostGame` 在 `CreateMatch` 返回后无条件调用 `onHostGame` 并关闭菜单；FRP Client 拒绝创建房间后仍触发场景切换，导致无房间的空准备场景。

## 配置处理与提示优化

- 配置处理：远端 FRP 角色改为 Host 并保存，重新应用配置后正常监听 UDP；该操作即可解决本次建房失败。
- 在原生建房按钮入口 `MakeOnlineMenu.DoHostGame` 增加 FRP Client 分支：保留菜单，并按当前语言提示先将角色改为房主。原有底层 Client 禁止建房规则保留。
- FRP 设置的中英文角色说明明确区分建房与加入。
- 该改动不增加传输重试、不调整握手协议，也不自动把加入方转换成房主。

## 验证与边界

- 原构建 `3c0e249a...`：远端改为 Host 后，经原生菜单流程重试，成功得到 `FrpDirectRoomInfo`、`IsOnlineRoomReady=true`、`connectionState=Hosting`、`DebugLobby.state=JoinedMatch`。新会话 `auto-20261112-212751-212-d0811bcd` 记录 `FRP_DIRECT Broforce room created; playerLimit=4; remoteMemberLimit=3.`。
- 快速脚本实际读取当前地图配置；首次默认 ID 不匹配时未执行操作，改用当前配置的 Workshop ID 后完成建房。观测停在 `MissionScreenVietnam`，只据此确认建房成功，不记作地图游玩通过；随后已卸载脚本。
- 正式 Release 构建通过，`buildHash=b0927bf97aaae2bff13475858846508b8717876b9699afcf96e6e833face7d0c`。
- 本机已在确认游戏进程退出后复制新版 DLL。远端已暂存新版并启动隐藏部署脚本，脚本等待原游戏进程退出后复制，不自动启动游戏；需手动启动后核验部署回执与实际加载构建。
- 本机重启后核验实际加载 `b0927bf9...`，`MakeOnlineMenu.DoHostGame` 已安装正式 `DoHostGamePrefix`。临时切换 Client（不保存），打开原生建房菜单并调用实际按钮入口：`openBefore=True`、`openAfter=True`、场景仍为 `MainMenu`、`Room=null`，中文提示正确；随后恢复原 Host 角色。
- 本机恢复 Host 后调用同一原生按钮入口，成功得到 `FrpDirectRoomInfo`、`ready=True`、`Hosting/JoinedMatch`。测试结束已关闭本机游戏并确认进程退出，没有保存临时 Client 配置。
- 新版远端建房和双端加入验收待继续记录；当前不能把编译成功或旧构建建房成功记作新版全部通过。

源码、说明与构建产物按用户要求仅作本地提交，未推送或发布。
