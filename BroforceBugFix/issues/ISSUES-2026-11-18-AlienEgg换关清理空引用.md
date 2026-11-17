# AlienEgg 换关清理空引用

2026-11-18。修复 `Map.RemoveAlienEggExplosive → AlienEggExplosive.OnDestroy` 的空集合异常，版本 `0.4.0`，已构建部署双端并完成定向验收。未提交、推送或发布；旧三项修复的验收范围不扩大。

以下原始现场至“部署与清理”保留 `0.4.0` 的验收与残留异常归属。随后 `0.4.1` 补齐同批 Listener 路径，结果在文末另行补记，不追溯改写旧结论。

## 原始现场与根因边界

已有 Steam 双端 Workshop `454967403` / `The brocountry` / `Test Evan2` 原生重载日志，在 2026-11-17 UTC 16:11:57 两端均出现：

```text
NullReferenceException
Map.RemoveAlienEggExplosive (AlienEggExplosive)
MonoMod.Utils.DynamicMethodDefinition.AlienEggExplosive.OnDestroy_Patch1
```

随后场景继续加载。原始相邻日志显示：房主 16:11:57.1440606 发起加载、57.2186044 抛错、58.0776535 加载完成；加入方对应为 57.6180864、57.6971705、58.6438751，随后收到 WORLD_STATE epoch。该现场没有采集实际销毁先后、集合值和发起者，不能把补丁包装名视为 CMM 致因，也不能排除 CMM 对生命周期的影响。

核对原版反编译源码和实际运行的游戏方法：

- `Map.Awake` 建立静态 `alienEggs`；`Map.OnDestroy` 将其置 null。
- `Map.RemoveAlienEggExplosive` 直接调用集合的 `Contains/Remove`，没有 null 判断。
- `AlienEggExplosive.OnDestroy` 先移除登记，再调用继承的 `NetworkObject.OnDestroy`。移除抛错会中断基类清理。

这条原生调用链可解释集合先清空、卵后销毁时的异常。本轮自然重载另行观察到了该顺序，但不能追溯证明原始现场由相同发起者触发。CMM 的销毁记录 Prefix 与可能发起销毁的 Removed 路径仍存在；本次没有禁用 CMM 做因果对照。

## 最小补丁

`src/AlienEggCleanupFix.cs` 使用独立 Harmony ID `GJKen.BroforceBugFix.AlienEggCleanup`，仅给 `Map.RemoveAlienEggExplosive` 安装 Prefix：集合非 null 时执行原方法，为 null 时只跳过该次移除。不重建集合，不捕获其它异常，不跳过卵的 `OnDestroy` 或基类调用。

默认开启的持久化字段为 `EnableAlienEggCleanupFix`，生效条件为 `UMM 已启用 && EnableAllFixes && EnableAlienEggCleanupFix`。面板变更立即协调补丁；关闭和卸载按独立 ID 移除。工程 Compile 清单与脚本实际编译的六个 src 文件一致，构建脚本未修改。

## 受控验证：生产 DLL 与实际 Harmony

双端先在离线状态、后在同一 Steam 房间分别执行受控夹具。夹具只在同一同步调用内替换集合并恢复原引用，用未激活的真实 `AlienEggExplosive` 组件调用游戏原方法；没有用补丁算法的镜像函数代替测试。

| 检查 | 离线双端 | Steam 房主/加入方 |
| --- | --- | --- |
| 非空集合移除有效卵，保留原集合引用 | 通过 | 通过 |
| 再次移除同一卵 | 通过 | 通过 |
| 同一 null 集合，关闭独立补丁 | 原版移除抛 `NullReferenceException` | 同左 |
| null 集合，关闭补丁调用原生卵 OnDestroy | 抛错，未产生基类销毁标记 | 同左 |
| null 集合，开启补丁 | 无异常，集合仍为 null | 同左 |
| 开启补丁调用原生卵 OnDestroy | `Registry.DestroyedMapObjects` 写入测试 NID | 同左 |

正常移除、基类标记均为游戏方法的实际结果。人工置 null 只证明受控边界，不冒充原始现场自然复现。

实际 Harmony 检查还确认：独立关闭时所有其它补丁归属保持一致；主开关关闭、UMM 停用及卸载移除自身所有修复，同时保留同方法上的临时外部观察 Prefix。恢复后旧三个独立 ID 均重新安装。双端将独立开关保存为 false，卸载/重新 Load 并启用后仍为 false 且无该补丁，最后恢复 true 并保存。目标方法最终仅有本独立 ID；卵 OnDestroy 上原有 CMM ID 保留。

## 自然重载：独立于人工空集合夹具

观察补丁只记录 Map/卵销毁先后、移除入口、基类入口及原样返回的异常，不改集合、销毁条件或异常。

| 模式与场景 | Map 销毁 | 卵销毁 / 其中集合为 null | 基类入口 | null 集合移除 | 卵销毁异常 |
| --- | --- | --- | --- | --- | --- |
| 离线 `WM_AlienMission2(mouse)` 第 1 关 | 1 | 32 / 32 | 32 | 32 | 0 |
| Steam Workshop454967403 房主 | 1 | 9 / 9 | 9 | 9 | 0 |
| 同次重载加入方 | 1 | 9 / 9 | 9 | 9 | 0 |

顺序均为 `map_enter list=N → map_exit null=True → egg_enter null=True`。离线重载后仍有本地存活玩家；联机双方均继续进入 The brocountry，`isInGame=true`、两名玩家、速度 1。联机最新普通日志未见 `Map.RemoveAlienEggExplosive` 堆栈。普通及 trace 日志都实际读取，包括文件元数据曾显示 0 字节但仍可读出缓冲内容的日志。

**相邻但独立的异常仍在：** 本轮离线观察器捕获 32 条、联机双方各 9 条来自 `Map.RemoveDisturbedWildLife → WildLife.RemoveFromMap → AlienEggListener.OnDestroy` 的空引用。这些没有经过本次目标移除方法，不计作本次修复失败或成功消除的数量，也不能宣称全部异形卵相关清理错误已修复。场景仍能加载；本次按授权的窄范围记录线索，未扩展修复。

## 部署与清理

在双端游戏进程退出后运行标准 Release 构建，.NET Framework 3.5 编译成功，生产 DLL 为 14,848 字节，安装包及本机 UMM 由原脚本更新。当前本机 props 未配置额外测试路径，核对既有双端路径后另复制同一 DLL 和 Info.json 到测试端。双端重启后从实际 ModEntry/程序集确认 `0.4.0` 已加载。

所有临时脚本和观察 Harmony 均卸载，活动脚本数为 0；测试组件和 NID 标记删除、Map 集合原引用恢复。双端确认自动清敌开启、速度 1、输入未阻塞且 DelayInput=false，独立开关恢复开启并持久化。测试结束关闭双端游戏；本轮临时源码用后删除，没有复制原日志、保存截图或新增证据目录。

## 0.4.1 补记：同批 AlienEggListener 清理

后续继续处理上文实际记录的 `AlienEggListener.OnDestroy → WildLife.RemoveFromMap → Map.RemoveDisturbedWildLife` 失败。原版 Listener 判断 `addedToMap && Map.Instance != null` 后调用 RemoveFromMap；后者先清除 `addedToMap`，再调用 Map 的移除方法。Map 实例存在不能保证静态集合未被清空；`Map.OnDestroy` 会将 disturbedWildLife 置 null，而移除方法直接访问该集合。

在现有 AlienEggCleanupFix、同一独立 ID 和开关下增补该 Map 方法的 Prefix：仅参数为 AlienEggListener 且 disturbedWildLife 为 null 时跳过移除。保留 Listener.OnDestroy 和 WildLife.RemoveFromMap，使原版登记标志清理仍执行；非空集合、重复移除和其它野生物路径保持原行为。未改设置序列化、CMM、构建脚本或原有卵门禁，也没有增加全局集合保护。

生产 `0.4.1` DLL 在本机离线和测试端 Steam 房主分别完成定向受控检查：

- 非空列表移除真实 Listener 组件，保留列表原引用和其它条目；重复移除正常。
- 同一 null 集合关闭本项时原版移除抛空引用；原生 Listener.OnDestroy 清除 addedToMap 后仍抛错。开启后移除和原生 Listener.OnDestroy 均不抛，标志确实清除且集合保持 null。
- 普通 WildLife 的 null 集合调用仍抛原版异常，证明门禁限定在 Listener；原有卵 null 集合门禁继续生效。
- 独立开关、主开关及卸载同时移除两个本项目标，保留外部观察 Prefix 和其它 Mod；恢复后两个目标及旧三个修复均正确安装。沿用 `0.4.0` 持久化矩阵，没有重跑全部旧验收。

只观察、不改状态的探针在离线和 Steam 双端各做一次原生重载，记录如下：

| 场景 | 卵 / null 集合卵 / 基类入口 | Listener / 已登记 / null 集合 / 标志已清除 | 卵异常 / Listener 异常 / 目标日志异常 |
| --- | --- | --- | --- |
| 离线 WM_AlienMission2 第 1 关 | 32 / 32 / 32 | 32 / 32 / 32 / 32 | 0 / 0 / 0 |
| Workshop454967403 房主 | 9 / 9 / 9 | 9 / 9 / 9 / 9 | 0 / 0 / 0 |
| 同次重载加入方 | 9 / 9 / 9 | 9 / 9 / 9 / 9 | 0 / 0 / 0 |

三组都记录 Map 先退出、两个集合均为 null，然后卵和 Listener 进入销毁。离线仍有存活的本地玩家，Steam 双方继续加载 The brocountry 且两名玩家均存活；该观察窗口没有其它 Error/Exception。初始 `FindObjectsOfType` 仅统计活动对象、Listener 数为 0，不是卸载数量；表中实际销毁数来自 OnDestroy 观察，不据此漏计非活动组件。人工 null A/B 与自然观察分开，不追溯证明原始现场的具体发起者。

标准 Release 构建成功，`0.4.1` DLL 仍为 14,848 字节，内容已更新；构建脚本报告 SHA-256 `1BF2B5CD60684572ED31B610A905CD1BFFFCC43507668E4360AB2EC29BCB0C75`，不复用 `0.4.0` 构建原证。先确认双端进程退出再部署，原脚本更新安装包/本机，额外测试端仍按既有路径复制，双端实际加载版本验证通过。

探针与临时组件、集合替换已清理，最终两个 Map 方法归属均仅为本修复 ID；CMM 的原生卵销毁观察补丁保留。双端活动脚本为 0，恢复自动清敌开启、速度 1、输入未阻塞和 DelayInput=false，开关开启并保存。普通及 trace 最新日志均实际读取，包括元数据 0 字节文件；本轮联机普通日志两条目标堆栈匹配均为 0。测试结束关闭双端游戏；临时源码用后删除，不复制日志/截图。
