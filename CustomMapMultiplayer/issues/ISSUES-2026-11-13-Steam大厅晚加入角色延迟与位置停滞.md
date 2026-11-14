# Steam 大厅晚加入角色延迟与位置停滞

日期：2026-11-13。状态：两处根因已修复、构建并部署双端，当前地图的加入与双向移动短测通过；首次完整出角色仍约 42 秒，入场速度及长期稳定性仍待验证。

用户授权通过一键流程操作两端，本机为加入方、远端为房主。本轮使用已运行的游戏，经远端当前 MCP 地址认证连接；游戏联机层实际为 SteamLayer，未使用 FRP Direct。MCP 的穿透连接与游戏传输方式分别核对。

## 基线与操作

- 两端开始均在 MainMenu。远端执行 `quick-online-workshop.cs`，读取该房动态 Lobby ID 后，本机执行 `quick-join-workshop-lobby.cs`。采用房主当前配置的 Workshop `3642518573` / `Contra Predator II` / `Test Evan2`；加入方原保存的 `454967403` 被正常按本次房主地图覆盖，没有改动双方保存设置。
- 双端 CMM `buildHash=b37b18d44ee39160b9cfd3cdd9f48dd03bef20cbbfdd3ce539803ac583c04ac3`，MVID 均为 `beca2922-4a68-435d-a61b-272c3ed010ca`。BroMakerLib、RocketLib、Swap Bros 的加载 MVID 亦相同。
- Aquabro 程序集版本均为 `0.1.0.0`，但加载 MVID 不同：本机 `50d7d3e8-65c3-4bb8-8ec3-4ed37cebd259`，房主 `d1c91664-f38b-4960-b8a0-59eddf4007e6`。这是进程内实际差异，尚不能单凭该差异断定本轮全部异常的原因。
- 房主会话 `auto-20261112-193032-261-62eab7bf`，加入方会话 `auto-20261112-193140-378-fe1b35bc`。下述时间均为日志 UTC；两机系统时间存在约 1—2 秒偏差，不将跨机时间差作为精确网络延迟测量。

## 已复现的行为

1. 加入方 `19:31:40` 开始加入，约 `19:31:53` 入图，却长期没有本地玩家。`19:32:17` 双端都只看到房主 P1；本机画面提示“按开枪键加入游戏”。本机 `GetLocalPlayerCount=0`。正常模拟一次开枪加入亦未立即完成。
2. 房主 `19:32:38` 已将加入方登记到槽位 1，但 `Player` 对象仍为空；加入方此时仍只知道槽位 0。加入方自动请求在约 45 秒间隔重试，并于 `19:33:41` 记录 120 秒超时。
3. 本机直到 `19:34:20` 才记录本地 `Player.Start`；随后双端均出现 P2 Rambro。按加入方自身日志，从开始加入到 Player.Start 约 160 秒。后续在本机拥有的槽位 1 调用正常 `switch_bromaker_bro(Aquabro)` 成功，双端生成相同 NID `146/7/70004/4` 的 Aquabro，脚本、同步、对象及主 Renderer 均启用。双端自定义角色清单均报告 Aquabro 可用，未捕获本轮 BroMaker 初始化异常。
4. 双端各执行一次 350ms 方向输入：本机 P2 左移，由 X=120 到 `88.35966`，房主能看到相同位置；房主 P1 右移，由 X=`92.29143` 到 `124.619484`，本机的房主副本仍停在 `92.29143`。`19:37`、`19:38` 及最后 `19:41` 读数均保留该分叉。当前已确认本次房主位移没有持续更新到本机；一次相反方向位移到达，不能据此证明加入方长期同步完全正常。

| 玩家 | 房主端 X | 加入方 X | 归属 / NID |
| --- | --- | --- | --- |
| 房主 P1 Aquabro | 124.619484 | 92.29143 | PID 6 / `146/6/60004/4` |
| 加入方 P2 Aquabro | 88.35966 | 88.35966 | PID 7 / `146/7/70004/4` |

## 同步链观测与结论边界

- 分叉时双端 `PauseStream=false`，`Registry.IsExiting=false`；人物 `enabled=true`、`Syncronize=true`、`active=true`，主 Renderer 可见，均已登记到 `Registry.ComponentsToSync` 且 `ReadyTobeSynced=true`。SyncController 启用且其 NID 均为 `146/0/0/4`，发送间隔约 0.08333 秒。不是仅凭角色模型是否可见作判断。
- 加入方在 `19:38:35` 的房主角色状态缓冲只剩一个旧样本，`stateTime=1468.551`，当时插值时间约 `1727.840`；RPC 等待、待处理和待发送队列在两次快照中均为 0。没有通过强制复活、重开、改 PID、清缓冲或改时间来制造正常结果。
- 原版已查阅 `HeroController.AddLocalPlayer / RequestJoinGame / AddPlayer`、`RPCController.ProcessRPCQueue`、`PingController`、`SyncController.SerializeAllOwnedObjects / RecieveStatesRPC / DispatchStates` 与 `NetworkObject.RunSync`；并对照 CMM 的 `WorkshopJoin / WorkshopPlayer / WorkshopSpawn` 和 BroMaker `CustomHero.Awake / Start`。出生请求的登记、RPC 到达/执行以及人物状态传输仍需进一步抓取，不能从现有快照直接判为 Steam 丢包、时钟问题或 Aquabro 版本问题。
- 本轮角色不存在先前的 `enabled=false` 现象；`heroType=Rambro` 与 `className=Aquabro` 并存也不能单独解释为自定义角色加载失败。未执行攻击、伤害、技能、重开或晚加入中的蓄力测试。
- 远端截图被已打开的 UMM 面板遮挡；远端位置结论使用其实际角色状态，不将遮挡截图当作人物画面一致的证据。

## 后续定位与正式修复

第二轮和重入轮使用临时 Harmony 观察脚本，记录普通 RPC 入发送队列前的门状态、RPC 接收次数、原生状态分发、实际送交 Steam 的字节数，以及 Steam 会话排队字段。未通过改时间、改所有权或强制生成角色完成验收。

1. **自动加入请求过早，被原生层丢弃。** 重入轮直接捕获 `RequestJoinGame` 的发送门为 `sent=False requested=False`，房主没有收到该首次请求。45 秒后重试时才为 `True/True`。原版 `HeroController.AddLocalPlayer` 发送普通 `RequestJoinGame` RPC，`RPCBatcher.QueueSend` 在 `HaveSentSyncStateToPlayer=false` 时直接返回；CMM 此前只等待场景与 `SpawnJoinedPlayers`，误把已调用加入函数当成有效请求。正式修复在 `TryRequestLateJoinPlayer` 等待本端发送门打开且当前 `Registry.playersThatIHaveSyncedWith` 含房主 PID，避免旧槽位和初始状态尚未到达时抢先请求。
2. **持续世界快照与角色消息共用 Steam 可靠通道，形成传输积压。** 原版 `RPCController.RouteRPCObject` 未使用 `useReliableStream`，`SteamLayer.SendData` 将全部 RPC 分片送入可靠 channel 0。因此 CMM 的 `UnreliableRPC` 运动快照仍占用相同通道。现场房主实际发送约 70 KB/s；加入方一度只有约 20 秒前的人物状态，原始半程 Ping 超过 10 秒，RPC 执行队列却为空，Steam `queuedBytes/queuedPackets` 亦为 0。后两者不足以排除可靠传输链路中的积压。

受控对照只在房主临时抑制 `SendWorldStates(..., reliable=false)`：实际流量降至约 15 KB/s，随后角色状态恢复到当前时间附近，原始半程 Ping 回到约 0.4 秒，房主位移重新到达加入方。该补丁仅用于确定流量影响，已在部署前卸载；正式源码保留运动快照。

正式修复在每个完整世界状态批次上使用 Deflate 无损压缩，接收端解压后继续原有解析与状态应用，协议升为 8，双方必须使用同一构建。现场 241 个已登记 Unit 的样本：完整批次原始 33,167 字节，压缩后 5,381 字节，减少约 84%。发送频率、状态字段和可靠性选择未调整。

改动文件为 `src/HarmonyDiagnostics.WorkshopJoin.cs` 与 `src/HarmonyDiagnostics.WorldState.cs`；标准 Release 构建通过。构建 `3c0e249aa638b3ac6e75fd43239370800444ed9c3f94279c9e4ec8a695ca5364` 已部署，重启后双端实际加载 MVID 均为 `0382b65a-6309-4e85-b3b5-d7e78980eb4e`、协议均为 8。远端后台复制回执确认等待原游戏进程退出后才替换 DLL；本机停止接口初次返回时进程尚未退出，首次退出检查失败后曾继续复制，随后已确认进程完全退出并重新复制，再启动检查实际加载构建。用户要求后续远端游戏由其手动启动，不再进行远端自动启动尝试。

[观测脚本的双端输出及流量对照](../docs/evidence/2026-11-13-steam-character/diagnosis.json)。第二轮曾出现短暂第三个 PID，未将该成员操作作为通过依据；双端不同 Aquabro 构建的事实保留，未将其误写为上述两处根因。

## 正式构建双端回归

- 双端重启后核对 `BuildMetadata.BuildHash` 相同，协议均为 8；没有安装停发世界运动的临时补丁。继续使用同一 Workshop 地图，本机为 Client。
- Client 会话 `auto-20261112-210617-002-22a2d24c`：开始加入 `21:06:17.002`，场景加载记录 `+14.696s` 与 `+17.614s`；首次自动请求 `+20.656s`，观察脚本捕获发送门为 `True/True`，房主收到一次请求。`+32.127s` 本地 `Player.Start` 确认槽位，约 `+42.108s` 完整英雄赋值。没有 45 秒超时重试或 120 秒加入失败。首次入场仍明显偏慢，不能把本轮记录写成 8—12 秒快速完成，也未覆盖长期重入稳定性。
- 完整世界快照持续发送时，房主实际交给 Steam 的数据由约 70 KB/s 降到约 30 KB/s；入场积压消退后双方平均半程 Ping 约 0.3 秒，原生角色状态继续更新，未再出现本轮旧样本停留约 20 秒的状态。此为当前网络和地图下的短测，不保证任意外网带宽下都不会积压。
- Client 通过 BroMaker 正常路径切为 Aquabro，随后双方各执行一次 350ms 方向输入。房主 P1 从 X=`96.24627` 到 `129.170685`；Client P2 从 X=`120` 到 `87.95544`。双方最终读数分别完全一致，人物 `enabled/sync/active` 正常。正式构建短测观察约 1 分多钟，未验证攻击、技能、伤害、重开或长时间战斗。
- 对正式 DLL 的压缩函数执行 64 个已登记世界状态的往返验证：8,965 字节压为 833 字节，解压逐字节及重新解析后全部字段一致。标准构建和针对性 `git diff --check` 通过；原有地图 `CheckPoint.Update` 空引用仍有记录，不将其记作本次修复。

[正式构建双端原始读数](../docs/evidence/2026-11-13-steam-character/validation.json)。测试结束时尚未提交、推送或发布；后续按用户要求将源码、说明与构建产物仅作本地提交。README 中历史分发构建未改写为本次本地实验产物。

正式回归结束后，双端观察和一键脚本均已卸载；远端确认观察对象不存在，任务部署脚本、回执与旧 DLL 备份已移除，暂存 DLL 在复制成功时已删除。本地临时 C# 文件已删除，仅保留必要读数和首轮截图。按仓库规则关闭测试游戏：本机停止后再次查询确认进程退出；远端 `Application.Quit()` 返回成功，随后同一端点连接关闭。未额外查询远端操作系统进程表，不将端口关闭写成独立进程验证；后续远端由用户手动启动。

## 首轮记录与清理

- [双端状态及相关日志节选](../docs/evidence/2026-11-13-steam-character/observations.json)
- [加入后尚未生成本地玩家的截图](../docs/evidence/2026-11-13-steam-character/client-before-spawn.png)
- [切角与短距离移动后的加入方截图](../docs/evidence/2026-11-13-steam-character/client-after-movement.png)

一键脚本已在各自端点卸载；未安装额外诊断补丁。截图副本已归入本项目材料目录，游戏截图目录中本轮生成的临时图片已删除。问题记录随本次改动本地提交；原始读数与截图继续只保留在本地工作区，不纳入提交或推送。

按仓库规则结束测试：本机停止工具返回成功，随后进程查询确认已无 Broforce / Broforce_beta；远端执行 `Application.Quit()` 返回成功，随后同一 MCP 端点报告连接关闭。未读取远端操作系统进程表，因此远端保留退出请求和连接关闭的实际回执，不补写独立进程检查结果。
