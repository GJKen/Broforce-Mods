# Workshop 加载中重复加入导致换人与入图失败

## 现场与复现

地图为 `3781818421 / Two Random Guys Save The Princess / Test Evan2`。房主选中 Cobro，实际生成 Rambro；加入方停在主菜单，反复出现 `BroBase.Start` 及角色 `Update` 空引用。

原始日志记录：加入方 `22:14:03.533 UTC` 已开始 Workshop 地图下载，`22:14:06.069 UTC` 再次执行同一房间的 `SteamLayer.JoinLobby`，清除了尚在使用的注入与晚加入状态。`22:14:07.194 UTC` 原下载完成回调到达时，其会话状态已失效，因此没有继续加载地图。后续网络角色出现在主菜单，导致空引用。日志只能确认有两次加入请求，不能据此断定用户手动点击了两次。

房主侧 BroMaker 同时记录 `No network-compatible BroMaker character is available; keeping the vanilla spawn.`。用原生 Swap Bros API 复现时，查询得到 `a peer has not published a BroMaker manifest`，随后 Cobro 切换退回 Rambro。加入方加载被打断后缺少有效的角色兼容清单，使房主切换也受影响。

使用同一地图，在第一次地图加载未完成时重复调用原生 `SteamLayer.JoinLobby`，稳定复现加入方留在主菜单以及房主自定义换人回退；只加入一次则双方正常进入，并能生成 Cobro/Aquabro。

## 修改与验证

在现有加入入口的会话清理之前识别“相同 Steam 房间、已有 Workshop 加载正在等待或执行、未超时”的重复请求，保留第一次加入。新房间、完成后的加入以及超时重试仍走现有流程。没有修改 Swap Bros 或 BroMaker 源码，也没有跳过角色兼容检查。

- `.NET Framework 3.5` 编译通过，版本仍为 `0.5.0`；确认双端游戏退出后部署，更新分发 DLL 和 ZIP。
- 使用同一重复加入脚本回归：日志出现 `Ignored repeated join to the current Workshop lobby while its map is loading.`，原下载完成并正常进入地图，约 10 秒完成本地槽位加入。
- 房主通过原生 Swap Bros API 切换 Cobro，加入方切换 Bronobi；两端均看到同样的两个角色、各自正确的本地归属，角色均存活。此次覆盖 API 换人链路，未补做真实菜单和物理快捷键测试。
- 当前双端测试日志未发现 Unity 错误或补丁失败。测试脚本已卸载，双端游戏均已关闭并确认退出。
- 回归重启时发现加入方持久化的多人联机 Mod 启用标记为 `false`，测试先通过 UMM 临时启用。Workshop 联机要求双方启用该 Mod，收尾已把加入方该标记保存为 `true`；地图配置、Swap Bros 键位和选角设置未另行改写。

当前构建：`buildHash=352369deeb227e77255856bc8e82abdd3a884a5fb5cafd1442df63cce8667ffd`；DLL SHA-256：`24FA715356F354575D4BCFF7EA01C6EB7D0546217E3E8126065EF6A9CCA384C7`。

证据位于 [diagnostics/2026-11-09-workshop-swap](diagnostics/2026-11-09-workshop-swap/)，保留 `before-results.json` 和 `after-results.json` 中的修改前复现及修改后双端快照；临时脚本和日志副本已按用户要求删除。当前为双端 MCP 回归通过，等待用户按原操作复测；不据此归因于最近精简，也未覆盖其它地图或长期稳定性。

## 与最近精简的关系

针对用户追问，比较最初精简前基线 `3a32a7b883cb4821b6c63c95b32da21c7dcd2ffb` 与当前已提交源码。重复 `JoinLobby` 后无条件调用 `ResetWorkshopStateForNewSession` 的路径、该方法对注入与晚加入状态的清理，以及 `HarmonyDiagnostics.WorkshopJoin.cs` 在精简前均已存在，相关行为没有被近期精简改动。`SwapBrosCompatibility.cs` 也没有差异；删除的是无状态诊断转发包装。阶段一删除的缓存完成入口只由无调用者的 Queue 置位，不能据此解释本次中断。

代码证据支持这是精简前已有的缺口，没有发现近期精简直接引入该问题的证据。本次未部署最初精简前 DLL 重做同一实机对照，因此不声称已经实测排除所有间接影响。
