# McBrover 火鸡主动引爆后残留实体

## 状态

**仍可复现，概率显著降低，根因未闭环。**

本 issue 只记录 McBrover（MacBrover）投掷火鸡后再次使用技能主动引爆的残留实体问题，不恢复或扩展历史废弃实验。

## 已观测事实

- 观测端点：房主端与加入方端 Unity Inspector 均能连接。
- 会话：双方处于同一 Test Evan2 场景、Bromandy_Ptr1 官方战役地图；本次观测为官方 Steam 联机，非 FRP Direct。
- 基线：房主端 PID 0、加入方 PID 1；切换到 McBrover 后，currentTurkey 初始为空。
- 初始实测中，用户手动投掷并主动引爆后确认出现二次遗留火鸡。
- 受限 Inspector 采样在双方都看到根路径 Sachel Pack Turkey 对象；该对象组件已停用、destroyed=false、NID 显示 NoID，拥有端/加入方分别为 IsMine=true/false。场景预制体同名对象在引爆前已存在，因此该采样不能单独证明它就是新生成副本。
- 远端玩家活动列表在该次采样时已降为一人；这属于同时发生的玩家会话状态，不能直接归因于火鸡残留。
- 运行时日志游标已按要求重置；本地 Inspector 报告 UMM 日志不可用，远端 UMM 日志只记录了 Inspector 查询异常，没有可用于证明 McBrover Death 时序的原生诊断字段。

## 根因状态与证据

反编译源码确认：

1. McBrover.UseSpecial() 在 currentTurkey != null 时直接调用 currentTurkey.Death()。
2. McBrover 使用 ProjectileController.SpawnProjectileOverNetwork(..., synced: false) 创建火鸡。
3. Projectile.Death() 先销毁自身 GameObject，再执行 MakeEffects()；SachelPackTurkey.MakeEffects() 的地形伤害和完整爆炸逻辑只在拥有端执行。
4. 本次补丁让拥有端发送 NID 并保留本地即时 Death；拥有端诊断日志已多次记录 `owner send`、`owner local Death executed` 和同一 NID 的最终 `OnDestroy`。但异常仍可复现，且未取得可与该异常一一对应的双方 NID、远端处理和地形结果记录。

因此，“远端副本未接收或未完整执行主动引爆生命周期”仍是主要假设，不是已确认根因；也不能排除同一次输入或状态时序生成第二个火鸡 NID。地形伤害是否重复仍未证明。

## 样本概况

| 样本 | 传输/地图 | 结果 | 证据等级 |
| --- | --- | --- | --- |
| 1 | Steam / 官方 Test Evan2 | 用户确认主动引爆后出现二次遗留火鸡；双方受限采样看到同名停用对象 | 已复现；NID/Death 时序字段不足 |
| 2 | Steam / 官方 Test Evan2 / 本次构建 | 用户确认仍出现二次遗留火鸡；主端多次主动引爆 NID 均记录本地 `OnDestroy` | 已复现；远端 NID 与残留对象身份未能在同一时窗对齐 |

修复后用户确认复现概率显著降低，但未记录完整尝试总数，不能给出复现率。后续复测应补充多个正常与异常样本，并记录每次 NID、拥有权、`Death()` 次数、爆炸次数、最终销毁状态和地形变化。

## 本次修复范围

- 新增仅针对 McBrover.UseSpecial() 中唯一 Projectile.Death() 调用点的 Harmony 转译。
- 拥有端主动引爆时，保留原版立即执行的 SachelPackTurkey.Death()，并向 PID.TargetOthers 发送火鸡 NID 与权威坐标。
- 加入方按 NID 查找并执行一次相同的 Death()；发送端和接收端分别使用会话内 NID 幂等集合，重复通知会被忽略。
- 加入方主动 Death 触发的爆炸效果与拥有端原版视觉 RPC 共享同一 NID 幂等保护；无论两类 RPC 的到达顺序如何，同一主动引爆在远端只播放一次效果。
- 新增克制诊断日志：火鸡创建、拥有端发送/本地 Death、远端 NID 命中/未注册、重复事件忽略及最终销毁状态。
- 在会话、场景和生命周期重置时清理 McBrover 火鸡幂等状态。
- SpawnProjectileOverNetwork 的创建日志只记录由 McBrover 发射且类型为 SachelPackTurkey 的对象。

## 明确排除

- 不修改 DemolitionBro、普通 Grenade、通用 Projectile、敌人死亡、尸体、钱币、拾取物或地图同步。
- 不处理地形伤害重复；本次观测没有足够双方地形前后结果证明它是独立或直接相关问题，保留为后续专项验证。
- 不恢复 ISSUES-2026-08-27-第三方地图动态世界同步.md 中的历史实验代码；该文档仅作背景参考。

## 尚未验证

- 双方完全退出、重启并加载同一新构建后的实机复测已完成一轮，但问题仍可复现；当前修复未达到验收标准。
- Steam Workshop、FRP Direct 官方地图、FRP Direct Workshop 地图尚未验证。
- 自然超时、碰撞爆炸与主动引爆交错时序，以及高延迟/迟到 RPC 的最终一致性尚待复测。
- 地形伤害是否在同一生命周期内只发生一次尚待用地图前后状态和双方日志确认。

## 验收标准

双方重启并确认同一 buildHash 后，在本次 Steam 官方 Test Evan2 场景：投掷火鸡并第二次使用技能主动引爆，拥有端立即执行一次原版爆炸；加入方只看到对应的一次爆炸，不出现遗留火鸡或迟到第二次爆炸；同一 NID 的重复通知有日志且被忽略；自然超时或碰撞不会与主动引爆叠加第二个生命周期。地形伤害只有在实测证明属于同一问题时，才要求双方各发生一次且结果一致。当前尚未满足。

## 构建与部署证据

- Release 构建成功。
- buildHash：4f6c722566e8a265880b9bf92c8ddd33c148ce8ec6a2c4590e0ed1e200ee4360
- DLL SHA-256：08C4999E96976DAF631742028B3117B8917253F555786EDF72606959F1D9C189。
- 项目分发、本机 UMM Mod 目录和网络加入方 UMM Mod 目录的 DLL 大小与 SHA-256 完全一致。
- 已部署到本机 UMM Mod 目录和网络加入方 UMM Mod 目录。
- 本节只证明实现、构建和部署完成；联机结果以状态和样本概况为准。
