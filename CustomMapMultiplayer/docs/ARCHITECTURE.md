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
- `src/SettingsUiText.cs`：UMM 设置界面及游戏内提示的中英文文案和系统语言选择，包括主动 AFK 的统一标题提示。
- `src/DiagnosticSettings.cs`：Workshop、会话、当前页面、语言、FRP 和日志类别配置。旧的折叠状态字段仅为迁移兼容保留。
- `src/DiagnosticLog.cs`：普通会话日志、Harmony 追踪、分类过滤和刷新。
- `src/DiagnosticsBehaviour.cs`：场景、Unity 错误和英雄生成状态观察。
- `src/HarmonyDiagnostics.cs`：大厅、关卡切换、Workshop 加载、玩家和英雄流程。
- `src/HarmonyDiagnostics.SteamHost.cs`：同帧 Steam 房主身份查询缓存及回调失效；复用原版结果，不改变敌人更新范围。
- `src/WorkshopMapDirectory.cs`：枚举 Steam 已订阅项目、读取本地地图状态、查询 Workshop 标题并维护选择页目录。
- `src/HarmonyDiagnostics.WorkshopIdentity.cs`：房主地图身份发布、加入方会话配置采用、Steam 订阅检测和缺图加载保护。
- `src/HarmonyDiagnostics.WorkshopPlayer.cs`：Workshop 本地玩家请求、控制器所有权、掉线重入和英雄类型恢复；死亡主动 AFK 返回不补生命或生成角色，并恢复原死亡头像；每次本地 `Player.Start` 成功后释放已完成的加入请求，保留进行中的重复请求保护。
- `src/HarmonyDiagnostics.Lifecycle.cs`：Workshop 会话及场景生命周期清理，包括死亡 AFK 返回标记和掉线头像缓存，避免影响下一回合出生。
- `src/HarmonyDiagnostics.WorkshopSpawn.cs`：Workshop 首次稳定出生点记录、异常 DropInDuringGame 复活位置修复、远端角色初次位置校正和出生点重广播。
- `src/HarmonyDiagnostics.WorkshopPickup.cs`：道具确定性、拾取所有权、幂等和满弹药退避。
- `src/HarmonyDiagnostics.WorkshopLevelEnd.cs`：Workshop 关卡结束动作防重入保护。
- `src/HarmonyDiagnostics.Afk.cs`：原生 AFK 倒计时、存活及死亡主动 AFK、死亡返回控制器记录、本地化标题提示、超时和槽位移除观测。
- `src/HarmonyDiagnostics.Chat.cs`：联机聊天的 Enter/Esc 边界、真实键盘重复编辑、Unity IME 提交、500 UTF-16 字符上限、相对输入框 `RectTransform` 右下角的本地字数显示，以及基于 Unity `TextGenerator` 的多行 viewport、按实际视觉行的 Up/Down 与 preferred X、`MessageController.UpdateText`/`ChatTextBox.Update` 时序同步和聊天 `Text.OnPopulateMesh` 前置校正。
- `src/HarmonyDiagnostics.Acid.cs`：英雄酸液入口、酸池扫描、本地预测、Host 权威校验和死亡链日志。
- `src/HarmonyDiagnostics.WorldState.cs`：非英雄世界对象的持续状态、拥有端校验、版本与基线恢复、生命周期重放，以及单位副本的 LateUpdate 校正；替换原普通 Mook 一次尸体终态通路。
- `src/HarmonyDiagnostics.WorldDoodads.cs`：Doodad 的实际 sprite、已知 material、受损状态和碰撞/子物体阶段；持续结果进入增量/基线，一次性受损表现独立按 life/序号重播。
- `src/HarmonyDiagnostics.WorldRegistration.cs`：将后注册对象绑定到墓碑，等待原生初始化完成后按最终身份消费终态；清理 Map/Registry 引用并抑制销毁 RPC 回声，保护较新生命周期。
- `src/HarmonyDiagnostics.WorldProjectiles.cs`：本地弹体的拥有端生成描述、Fire 前网络登记和副本恢复；保留基础视觉副本，重播实际派生 MakeEffects，并保留外部效果接管。
- `src/HarmonyDiagnostics.WorldGrenades.cs`：共享来源的本地手雷生成描述、Launch 前身份/所有权登记、待生成/基线恢复及副本 Update 隔离；原生网络手雷保留自身生成通路。
- `src/HarmonyDiagnostics.WorldGrenadeEffects.cs`：捕获原版 Grenade 零参数 MakeEffects 的实际覆写，按效果坐标和随机状态重播一次派生表现。
- `src/HarmonyDiagnostics.WorldCarriers.cs`：标记 FlashBangExplosion 派生载体的实际 Owner/取池序号，隔离副本原生玩法与历史效果重播，接入持续载体描述。
- `src/HarmonyDiagnostics.WorldPersistentEffects.cs`：持续气云/火焰波和 LaserBeam 的可靠描述、加入基线、网格/剩余阶段、pending、终态墓碑及原生 NID 合并；只有拥有端执行玩法，粒子沿用原生池生命周期。
- `src/HarmonyDiagnostics.WorldPersistentStatus.cs`：非英雄持续异常的采集/codec/计时恢复，冻结/酸液材质、寄生依赖和副本结算隔离；不重放历史 RPC 或赋予寄生生成权限。
- `src/HarmonyDiagnostics.WorldHitSounds.cs`：捕获受击、燃烧、Fizzle 和手雷 Death 作用域内实际选中的音频及参数；已随效果重播的声音不再独立发送。
- `src/HarmonyDiagnostics.WorldMutations.cs`：按实际签名转交 Doodad 直接伤害、直接点燃和弹体受击，保留来源上下文、目标拥有端原生门禁与延迟参数。
- `src/HarmonyDiagnostics.WorldReplayGeneration.cs`：生命周期回放及原生副本确认中的生成边界；跳过返回值被丢弃或存入未读局部变量的工厂调用，保留已有道具释放和后续清理。
- `src/HarmonyDiagnostics.WorldActors.cs`：状态异常的目标拥有端请求与副本计时恢复，以及普通／亡灵自爆兵的权威爆炸表现；角色更新上下文按运行时类型登记，兼容自定义角色。
- `src/HarmonyDiagnostics.Melee.cs`：近身玩家击掌设置及实际命中视觉/血液同步；保留已有预测位，补充普通受击的喷血和背景血迹原参数。
- `src/HarmonyDiagnostics.LevelOutcome.cs`：`LevelFinish`/`RemoveLife` 前后快照。
- `src/HarmonyDiagnostics.Reflection.cs`：连接层状态读取及反射元数据缓存。
- `src/HarmonyDiagnostics.Trace.cs`：Harmony 方法追踪消息格式化及追踪反射缓存。
- `src/SwapBrosCompatibility.cs`：按 Swap Bros 2.1.5 的真实运行时声明解析 `Swap_Bros_Mod.Main.GetSelectedBroHeroType(Int32)`、`Main.settings`、`Settings.alwaysChosen` 和 `Settings.ignoreForcedBros`，并在失败时记录阶段、异常和返回值。
- `src/ReflectionProbe.cs`：只读扫描 `Assembly-CSharp` 中的相关类型。
- `src/OnlinePlayerListFormatter.cs`：Steam/FRP 在线名单的延迟颜色、房主渐变、Rich Text 转义和秒到毫秒换算。
- `src/FrpDirectTransport.cs`：Lidgren UDP、多连接握手、认证、心跳、重连和可靠字节路由。
- `src/FrpDirectRoomInfo.cs`：FRP 房间信息和 Workshop 阶段元数据。
- `src/FrpDirectLayer.cs`：复用原生 PID、ServerID、RPCBatcher 和 `RecieveBytes`，按机器路由多客户端 RPC。
- `src/FrpDirectNetworkManager.cs`：选择 FRP/Steam 层并管理 `Connect.layer` 生命周期。

## 设计边界

- 方法级追踪不记录房间密码、Steam ID、主机名或 Workshop 作者身份。
- 聊天完整输入保留在 `MessageController.message`；viewport 只修改 `Caret.text.text`、显示用光标位置和 Unity UI 文本显示设置，不使用固定字符数裁剪显示内容，也不改写网络协议。
- 聊天 viewport 使用输入框 `RectTransform` 的实际宽度、字体设置和 Unity `TextGenerator` 行表按光标行推进；可视行数使用相同生成设置下 `VerticalOverflow.Truncate` 的实际行数。完整消息更新后同步 `Caret.message`、主文本和显示光标位置，并在聊天文本进入 `OnPopulateMesh` 前再次校正，避免把 Canvas 网格刷新误认为输入状态同步。多行 Up/Down 使用 `UILineInfo.startCharIdx` 找视觉行、使用 `UICharInfo.cursorPos/charWidth` 保留 preferred X，并保存导航行号处理短行和换行边界；单行 Up 仍放行原生上一条消息编辑行为。
- Workshop 专用补丁不得因遗留场景名、Lobby phase 或本地旧配置误作用于普通官方联机。
- 不因为可选 Mod 指纹不同拒绝会话。
- 世界状态采用拥有端权威、带版本的增量快照；加入、重入或权威代次变更时请求完整基线，静态对象只在变化时发送。瞬时弹体同步生成描述、运动和终态，销毁对象退出活动候选扫描。
- Doodad 受损阶段直接恢复结果，基线不重播 Hurt/受损特效；先到 Removed 的墓碑保留至代次清理，后注册副本等原生初始化结束再退休。材料身份、子类私有状态及实际时序覆盖见[G5/G6 记录](../issues/ISSUES-2026-11-17-场景物件受损阶段与后注册清理.md)。
- 持续状态按拥有端时钟和接收后经过时间扣除年龄；寄生源未登记时暂存，Removed 不等待依赖。持续载体用 Owner/serial/revision 隔离池生命周期，终态保留原生 NID 以拦截晚到 Fire；Flash 副本由描述推进，Laser 副本只运行表现。真实加入/重入、非房主恢复和视觉精度边界见[G7 记录](../issues/ISSUES-2026-11-17-中途加入持续状态与载体恢复.md)。
- 登记的非英雄 Unit、Doodad、Block、Projectile 和 Grenade 共用状态通路；尸体、近战命中、自爆兵、盾牌、装甲和 Boss 接收器按对象语义补充状态或本地表现。副本不重复执行拥有端伤害、奖励、派生爆炸或完整死亡结算。
- 协议字段和对象覆盖随版本演进；当前构建与逐项验证范围以[README 当前状态](../README.md#当前状态)及对应 [issue 记录](../issues/README.md) 为准。编译、部署或少量样本通过不代表所有 Boss、特殊弹体和地形伤害闭环均已验收。
- 原生网络 Grenade 沿用原生生成；有共享来源的本地手雷才提升为共享描述，无 NID 来源保留原语义。派生效果与载体修复不代表完整持续状态或晚加入恢复，覆盖样本和限制见[G3/G4 记录](../issues/ISSUES-2026-11-17-派生手雷与命中音画同步.md)。特殊 Boss、复合单位、钱币和金色奖励等特殊语义及完整地形伤害闭环尚未逐项验收。
