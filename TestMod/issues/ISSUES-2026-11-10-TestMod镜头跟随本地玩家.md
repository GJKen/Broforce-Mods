# TestMod 镜头按本地玩家隔离跟随

日期：2026-11-10

状态：镜头跟随已实现并部署；联机跟随及死亡恢复验收通过，离线场景待测。

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

## 待验收

本机未启动游戏；用户已确认联机跟随及角色死亡、复活后的镜头行为通过。以下离线及特殊关卡场景仍待实机确认：

| 场景 | 验收标准 | 状态 |
| --- | --- | --- |
| 联机双方分别启用镜头跟随 | 两端各自主要跟随本机控制的第一名玩家；远端玩家移动不会把该端镜头拉向远处 | 已通过 |
| 联机目标角色死亡及复活 | 死亡时回到原生多人镜头；角色复活后重新聚焦本机角色 | 已通过 |
| 离线双人选择 P1 | 共用镜头只把 P1 纳入玩家跟随范围；P2 不再拉动镜头 | 待测 |
| 离线双人选择 P2 | 共用镜头只把 P2 纳入玩家跟随范围；P1 不再拉动镜头 | 待测 |
| 默认关闭及关闭设置 | 原生多人镜头行为恢复 | 待测 |
| 带额外跟随点或脚本镜头的关卡 | 原生额外跟随点和脚本镜头控制仍工作 | 待测 |

本地双人和特殊关卡等场景实机验收通过前，本功能状态维持“已实现并部署，联机验收通过”。
