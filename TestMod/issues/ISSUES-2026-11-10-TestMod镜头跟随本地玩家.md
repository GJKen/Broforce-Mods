# TestMod 镜头按本地玩家隔离跟随

日期：2026-11-10

状态：已结案（2026-11-11）。用户实测一端开启、一端关闭镜头跟随，两端各自画面正常，最初“未开启端镜头被拉走”的问题已解决。

## 需求

Broforce 原生 `HeroController.GetGetFollowPosition()` 会把所有参与玩家的位置并入镜头跟随范围。希望联机时每个游戏端的镜头只考虑本机控制的角色；离线本地游玩时允许选择一个角色，让共用镜头跟随该角色。

本记录实现的是镜头目标过滤，不是本地分屏。离线玩家仍然看到同一个画面。

## 实现

- 新增默认关闭的“镜头跟随”设置页。
- 联机时自动选择 `PIDS[i].IsMine` 对应的第一名参与玩家。
- 离线时允许选择 P1～P4，玩家编号保存在 UMM 设置中。
- Harmony 转译器只替换 `HeroController.GetGetFollowPosition()` 中的 `IsPlaying` 玩家筛选调用；原生跟随算法仍处理关卡额外跟随点、模式偏移和其它镜头状态。
- 关卡编辑和关卡完成阶段仍使用原生玩家筛选；关卡脚本直接控制镜头时不受该补丁影响。
- 目标角色死亡时恢复原生多人玩家筛选；角色复活后重新聚焦。角色对象在重生阶段暂时为空时，继续保留目标槽位筛选。
- 新增公开 API：`SetFocusedCameraEnabled(bool)`、`SetCameraFollowPlayer(int)`、`IsFocusedCameraEnabled`。

## 死亡后的镜头恢复

联机测试确认存活期间镜头跟随正常，但目标角色死亡后镜头仍停留在其位置。原生 `Player.HasFollowPosition()` 会在本机角色死亡后保留 1.5 秒跟随位置；角色的 `IsAlive()` 还可能因延迟复活状态返回 `true`。修复后使用角色 `health <= 0` 判断死亡，并恢复所有原生参与玩家的筛选结果；重生对象暂时为空时仍只保留原目标槽位筛选，避免镜头在重生间隙转向其他玩家。

## 修改范围

- `src/CameraFollowPatch.cs`
- `src/Plugin.cs`
- `src/SettingsUI.cs`
- `src/TestModSettings.cs`
- `TestMod.csproj`
- `README.md`
- `Release/UMM/Mods/TestMod/TestMod.dll`

## 构建与部署

- 初次实现通过 `BuildAndDeploy.ps1 -SkipDeploy` 构建，`tests/RunGameSpeedTests.ps1` 的 57 项调速状态机检查通过；测试不覆盖镜头功能。
- 初次实现已部署到本机和 5700G 的 TestMod 目录，发布目录及两端安装 DLL 的 SHA-256 为 `A2DE5853C56DB8732AFCD83E7725A0FC878E8F79902CAC7A09B990056370EF81`。
- 死亡恢复修复通过 `BuildAndDeploy.ps1 -SkipDeploy` 构建，57 项既有调速状态机检查通过；编译仍有一条既有警告：`TestModBehaviour.cs` 使用的 `Application.loadedLevelName` 已过时。
- 本次部署前本机没有运行 Broforce；5700G 的停止接口返回 `Game stopped`。随后 `BuildAndDeploy.ps1` 成功更新本机和 5700G 的 DLL；发布目录及两端安装 DLL 的 SHA-256 均为 `13FB49DC1019B11B518110249C5B207C840592B50DA534277392A878A0DA819C`。

## 此前验收记录（兼容改动前）

兼容改动前，用户已确认联机跟随及角色死亡、复活后的镜头行为通过。以下保留当时的验收记录，未测项目不计入通过范围：

| 场景 | 验收标准 | 状态 |
| --- | --- | --- |
| 联机双方分别启用镜头跟随 | 两端各自主要跟随本机控制的第一名玩家；远端玩家移动不会把该端镜头拉向远处 | 已通过 |
| 联机目标角色死亡及复活 | 死亡时回到原生多人镜头；角色复活后重新聚焦本机角色 | 已通过 |
| 离线双人选择 P1 | 共用镜头只把 P1 纳入玩家跟随范围；P2 不再拉动镜头 | 待测 |
| 离线双人选择 P2 | 共用镜头只把 P2 纳入玩家跟随范围；P1 不再拉动镜头 | 待测 |
| 默认关闭及关闭设置 | 原生多人镜头行为恢复 | 待测 |
| 带额外跟随点或脚本镜头的关卡 | 原生额外跟随点和脚本镜头控制仍工作 | 待测 |

本表记录此前版本的覆盖范围；本次兼容改动的验收与结案见下文。

## 2026-11-11 原版队友镜头被拉走：源码发现与修改

### 新增问题

用户反馈：只有部分玩家启用角色跟随时，启用者走远后，未安装 Mod 或未启用角色跟随的玩家仍按原版算法计算全部玩家的位置，镜头被拉向前方，可能看不见自己的角色。用户另报告曾遇到原版联机两端镜头分离的 Bug。

### 源码发现

1. 原版各端在 `SortOfFollow.LateUpdate()` 中分别调用 `HeroController.GetGetFollowPosition()`。`Player.HasFollowPosition()` 对远端玩家额外检查角色引用、存活状态及 `charHasBeenInstantiated`；本端与远端状态不一致时，跟随集合可能不同。该路径可以解释镜头分离的可能性，但没有当时的运行现场，不能确认用户曾遇到的 Bug 根因。
2. `Player.GetFollowPosition()` 优先返回 `playerTemporaryTarget.position`。`HeroController.AddTemporaryPlayerTarget(int, Transform)` 和 `RemoveTemporaryPlayerTarget(int)` 均带 `[AllowedRPC]`，原版客户端即可接收；`ProjectileController.SpawnProjectileOverNetwork()` 已用于遥控弹等原生功能。RPC 可以指定接收 PID，无需全房间广播。
3. `CanLookForReposition()` 会在任何存活实例持有临时目标时返回 `false`，进而暂停 `TestVanDammeAnim.ConstrainSpeedToSidesOfScreen()` 中寻找梯子或孔洞的自动回位流程。这是已知机制副作用，不能将本方案描述为所有玩法均零影响。

源码入口：

- [HeroController.cs](../../Broforce_src/Broforce-Source/HeroController.cs)：`AddTemporaryPlayerTarget`、`GetGetFollowPosition`、`CanLookForReposition`。
- [Player.cs](../../Broforce_src/Broforce-Source/Player.cs)：`HasFollowPosition`、`GetFollowPosition`、`AddPlayerTarget`、`RemovePlayerTarget`。
- [ProjectileController.cs](../../Broforce_src/Broforce-Source/ProjectileController.cs)：原版临时目标的网络设置。
- [RPCBatcher.cs](../../Broforce_src/Broforce-Source/RPCBatcher.cs)：按 PID 路由及 `HaveSentSyncStateToPlayer` 发送门禁。

此前“单端只能限制距离”的结论过于绝对：原版协议存在可复用的临时目标入口。但源码可行性和离线模拟通过不等于混装实机验收通过。

### 本次采用的第二点方案

用户明确要求仅实施上述第二点，保留第一点为调查记录。

- 新增 `src/CameraCompatibility.cs`，沿用已有角色跟随开关，不新增设置或 Mod 协议。
- A 启用角色跟随、B 未安装或未开启时，只向 B 发送 `AddTemporaryPlayerTarget(A.playerNum, B.character.transform)`。B 端的跟随范围由 A、B 两个真实位置改为两个 B 位置，A 走远不会再扩大 B 的玩家跟随范围；A 本机继续使用现有本地玩家筛选。
- 每个远端 PID 单独绑定第一名存活本地角色；同端多名本地玩家只绑定一次，保留其原生多人构图。远端持有原生投射物临时目标时，锚点跟随该投射物；另一启用端指向本机英雄的兼容锚点不会被拿来互相套用。
- 仅在目标、角色或连接变更时发 RPC；等待原版状态同步发送门禁通过，且目标 Transform 已注册网络 ID 后才绑定。
- 本机角色死亡、重生空窗、关闭功能、禁用或卸载 Mod、编辑状态、关卡完成、过场或脚本镜头接管时撤销远端覆盖。队友死亡时恢复原生旁观，复活或换人后更新目标。
- 本机有原生临时目标时暂停兼容覆盖，并把原生目标恢复给接收端，避免清理覆盖时误删刚启动的遥控技能目标。
- 游戏会话 ID 保存数值快照；会话或 `HeroController` 实例改变后丢弃旧绑定，不向新关卡发送旧场景的清理调用。断线后不向缺失连接发送调用。
- 不伪造角色坐标、生命或参与状态，也不实施第一点中的状态异常路径。原版自动回位副作用保留并在设置界面、README 中说明。

### 验证与验收边界

- 使用实际游戏依赖完成 .NET 3.5 编译；仅有既有 `Application.loadedLevelName` 弃用警告。
- `tests/RunCameraCompatibilityTests.ps1` 的 42 项检查通过。测试使用真实兼容控制器和离线端点模拟，覆盖原版双人镜头、多人构图、定向目标、遥控技能、关闭与死亡清理、重生换人、脚本镜头、同步门禁、断线重连、可变会话 ID 和换关清理。测试运行时生成的 EXE 已自动删除。
- 部署前本机及 5700G 的 Unity Inspector 停止接口均返回 `success: true, Game stopped`，本机另确认 Broforce 进程已退出。`BuildAndDeploy.ps1` 成功编译并更新发布目录、本机及 5700G 的 DLL；两端安装 DLL 均为 49,152 字节。没有启动游戏，保持两端关闭。
- 本次没有启动游戏或运行联机验收。原有“双方开启跟随、死亡恢复通过”的记录属于此前版本，不能作为本次原版队友兼容改动的实机结果。

| 本次兼容改动场景 | 标准 | 状态 |
| --- | --- | --- |
| A 开启、B 完全未加载 TestMod，分别作为房主与客户端 | A 走远时 B 仍能看到自己的角色，角色位置和攻击同步正常 | 待实机 |
| A 开启、B 安装但关闭角色跟随 | 两端各自镜头正常，未开启端的画面不会被开启端拉走 | 用户实测通过（2026-11-11） |
| 双方开启、任一方死亡复活或换人 | 两端各自跟随，旁观和重新绑定正常 | 待实机 |
| 任一方使用遥控弹等原生技能 | 技能镜头正常，结束后兼容覆盖恢复 | 待实机 |
| 脚本镜头、关卡完成、重开、切关、断线重连 | 脚本接管正常，无上一场景目标残留 | 待实机 |
| 角色跌出画面且原版尝试寻找回位位置 | 确认已知自动回位副作用对实际玩法的影响 | 待实机 |

### 2026-11-11 用户实测与结案

用户反馈：一边关闭镜头跟随、一边开启镜头跟随，都不会影响玩家自己的镜头，最初报告的镜头被拉走、看不见自身角色的问题没有再出现。

据此将本次“开启端移动影响未开启端镜头”的问题标记为已解决并结案，不再为此继续修改代码。该结果来自用户实机测试；上文“本次没有启动游戏”指代理此前的构建部署阶段。

本次反馈未单独确认完全未加载 TestMod、房主与客户端互换、遥控技能、特殊关卡及其他表中待测场景；保留其未测状态和已知自动回位副作用，不作为本次结案的阻断项。后续若出现具体异常，再按对应场景补充记录。
