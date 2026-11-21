# Broforce Bug Fix 开发与测试

## 维护原则

- 只修复已有运行日志、稳定复现或原版源码调用链支持的缺陷。
- 每个修复使用独立补丁类和独立问题记录，避免把无关行为合并到一个 Harmony 补丁。
- 补丁默认保留原版行为，只拦截能够明确判定为异常的状态。
- Harmony Prefix 或 Finalizer 发生内部异常时，不应吞掉原版异常或让保护状态永久残留。
- 新修复必须在离线和联机环境分别评估；本机状态修复不自动等于网络同步修复。

## 架构与代码职责

- `src/Plugin.cs`：UMM 生命周期、启用和卸载。
- `src/BugFixSettings.cs`：全部修复主开关和各修复的持久化独立开关。
- `BuildAndDeploy.ps1`：使用 .NET Framework 3.5 编译器构建，更新项目安装包、本机及已配置的额外测试部署目录。
- `issues/`：保存问题记录、结论、修复边界和复测入口；材料管理见 [MATERIALS.md](MATERIALS.md)。

各修复的源码、独立开关及问题入口如下。Harmony ID 均使用 `GJKen.BroforceBugFix.` 前缀：

| 源码（相对 `src/`） | Harmony ID 后缀 | 设置字段 | 问题与验收 |
| --- | --- | --- | --- |
| `DoodadCrateReentryFix.cs` | `DoodadCrateReentry` | `EnableDoodadCrateReentryFix` | [爆炸箱递归](../issues/ISSUES-2026-08-27-DoodadCrate爆炸递归栈溢出.md) |
| `LobbyJoinStreamFix.cs` | `LobbyJoinStream` | `EnableLobbyJoinStreamFix` | [加入房间残留暂停](../issues/ISSUES-2026-11-09-未完成地图加载后加入官方房卡在主菜单.md) |
| `FaceHuggerDetachFix.cs` | `FaceHuggerDetach` | `EnableFaceHuggerDetachFix` | [蛇脱离空目标](../issues/ISSUES-2026-11-14-蛇脱离空目标导致反复出血.md) |
| `AlienEggCleanupFix.cs` | `AlienEggCleanup` | `EnableAlienEggCleanupFix` | [卵与 Listener 换关清理](../issues/ISSUES-2026-11-18-AlienEgg换关清理空引用.md) |
| `SandstormSetupFix.cs` | `SandstormSetup` | `EnableSandstormSetupFix` | [沙尘暴切图初始化](../issues/ISSUES-2026-11-21-沙尘暴切图初始化空引用.md) |

各修复使用独立补丁类和 Harmony ID；卸载只移除本插件自己的补丁，保留其它 Mod。调用链、具体修复机制和验收边界统一维护在表中的问题记录。

## 开关模型

每个修复的生效条件统一为：

```text
UMM Mod 已启用 && EnableAllFixes && 对应修复开关
```

`EnableAllFixes` 关闭时卸载所有修复，但不修改独立开关值。重新打开主开关后，只恢复仍被独立开关选中的修复。设置面板中的变化立即调用补丁协调逻辑，插件卸载也复用该逻辑，UMM 保存设置时写入持久化配置。新增修复必须接入 `ApplyConfiguredFixes`，并拥有自己的设置字段。

箱体修复必须保留同步锁、按引用比较的集合和 Finalizer：只拦截同一实例尚未退出时的嵌套重入，正常返回或抛异常时均清理登记，并原样返回异常。不提前更改 `destroyed`，不改变原版伤害、掉落物和 RPC 顺序。

## 构建与部署

路径配置使用未提交的 `LocalBroforcePath.props`：

```xml
<Project>
  <PropertyGroup>
    <BroforceManagedPath>...\Broforce_beta_Data\Managed</BroforceManagedPath>
    <UnityModManagerPath>...\UMM\Core</UnityModManagerPath>
    <TestDeployModPath></TestDeployModPath>
  </PropertyGroup>
</Project>
```

`TestDeployModPath` 是可选的额外测试部署目录，留空时只部署本机 UMM。该文件包含机器或内网专用路径，已由仓库根 `.gitignore` 忽略，不能提交。

构建部署前关闭本机及额外部署目标的 Broforce，并确认进程退出；暂停游戏不能代替退出。脚本会直接覆盖部署副本，不会自动结束游戏进程。在本项目目录运行标准构建命令：

```powershell
powershell -ExecutionPolicy Bypass -File .\BuildAndDeploy.ps1
```

成功构建必须同时得到：

- `BroforceBugFix/BroforceBugFix.dll`；
- 本机 `UMM\Mods\GJKen-BroforceBugFix` 部署副本；
- 配置了 `TestDeployModPath` 时，额外测试目录中的同名部署副本；
- 构建脚本输出的 DLL SHA-256。

需要实机验证时重新启动游戏，确认实际加载版本与修复开关；验证及临时对象清理后关闭游戏。仅修改文档无需构建或部署。

## 验证范围与复测要求

验证结论以对应问题记录中的构建、模式和场景为准。受控状态检查、离线自然流程和真实双端流程应分别记录，不能互相替代，也不能把旧构建的结果自动算作当前安装包的完整回归。

| 修复 | 已有验证 | 待补场景与边界 |
| --- | --- | --- |
| [爆炸箱递归](../issues/ISSUES-2026-08-27-DoodadCrate爆炸递归栈溢出.md) | 历史开关矩阵；2026-11-21 首轮精简构建的受控重入、异常清理及普通箱双端销毁 | 原故障关卡、相邻爆炸箱及全部箱体组合的完整离线/双端验收仍待补齐 |
| [加入房间残留暂停](../issues/ISSUES-2026-11-09-未完成地图加载后加入官方房卡在主菜单.md) | 加入端禁用 CMM 的原生未完成加载、残留暂停及真实加房；首轮精简构建再次检查真实加房 | 补丁只处理主菜单加入新房间的残留流，不修复 Workshop 下载本身；离线不进入该路径 |
| [蛇脱离空目标](../issues/ISSUES-2026-11-14-蛇脱离空目标导致反复出血.md) | 离线 A/B、FRP 双端受控脱离；首轮精简构建的空目标 A/B | 初始引用丢失原因仍未知；自然完整喷血现场、正常目标的自毁/英雄 Flex 分支及长期战斗未扩测 |
| [卵与 Listener 换关清理](../issues/ISSUES-2026-11-18-AlienEgg换关清理空引用.md) | `0.4.0`/`0.4.1` 受控空集合对照及离线/Steam 自然重载；首轮精简构建的原生重载清理 | 不追溯证明原始现场的具体发起者，也不扩展为其它野生物或全部地图的回归结论 |
| [沙尘暴切图初始化](../issues/ISSUES-2026-11-21-沙尘暴切图初始化空引用.md) | `0.5.0` 离线及 Steam 两端各 20 项定向检查、真实加入/重开，官方 `MuscleTemple_3` 两关的风暴同步、原版关间切换及结尾 | 官方战役使用辅助移动、短暂无敌及防坠落，分段验证；不是无辅助连续通关，也未覆盖全部敌人、桥梁及触发器 |

2026-11-21 首轮精简构建还通过了开关与配置重载检查。该轮旧修复回归与随后 `0.5.0` 沙尘暴构建的定向验收分别记录；后续源码调整不自动继承首轮结果，不据此声称 `0.5.0` 已完整重验全部旧修复。

### 爆炸箱待补复测

1. 先在离线模式触发两个相邻爆炸弹药箱，确认只发生有限的正常连锁爆炸。
2. 在 Workshop `3660163376` 的第 2 关复现原故障区域，确认不再出现爆炸特效无限增长或严重掉帧。
3. 检查 UMM `Log.txt` 是否出现 `Suppressed recursive DoodadCrate.ActuallyCollapse call`，且没有 `StackOverflowException`。
4. 联机时房主和加入方都安装相同版本，确认双方仍能看到正常的首次爆炸和后续关卡重开。
5. 分别测试普通木箱、普通弹药箱、单个爆炸弹药箱和多个相邻爆炸弹药箱。

沙尘暴的受控夹具、官方战役分段结果、无效试次、过场期间 Inspector 调度超时的核实与收尾统一见[对应问题记录](../issues/ISSUES-2026-11-21-沙尘暴切图初始化空引用.md)。
