# 旧引爆 RPC 晚于世界删除导致未注册提示

2026-11-21。Steam 双端、Workshop `454967403` / `The brocountry` / `Test Evan2`。本机为加入方 slot 1，远端为房主 slot 0。本轮先在未修改 DLL 上记录实际调用顺序，再修复并部署双端验证。

## 原因和修复前的实际处理顺序

旧构建 `790e10236a01c90ff564aeb5383bfd81019f108e1b4695e99626017237e66fb2`，DLL 515,584 字节。临时 Harmony 探针只观察方法入口、效果执行、删除和 Registry 状态，没有改变生产 RPC 的队列或执行顺序。下面均为接收端自身的 UTC 时间及帧号，未用两台机器的时钟推算先后。

加入方引爆 `154/7/70004/2`，房主接收：

| 接收端 UTC | 帧 | 实际处理 | Registry |
| --- | --- | --- | --- |
| 19:49:50.5919815Z | 12020 | `ReceiveWorldProjectileEffectsRPC` 进入 | 存在 |
| 19:49:50.5929819Z | 12020 | `DemolitionBroBomb.MakeEffects` 执行一次；`_worldApplying=1`、`IsMine=false` | 存在 |
| 19:49:50.6069857Z | 12021 | Removed 快照进入 `TryApplyWorldState`，调用 `RemoveLateWorldObject` | 存在 |
| 19:49:50.6079850Z | 12021 | `RemoveLateWorldObject` 返回；移除注册并停用对象 | 不存在 |
| 19:49:50.6099857Z | 12021 | `NetworkObject.OnDestroy` | 不存在 |
| 19:49:50.8066814Z | 12032 | `ApplyDemolitionBombDetonationRPC` 进入 | 不存在 |
| 19:49:50.8076818Z | 12032 | 输出 `remote NID unregistered` 并返回，没有再调用 `Death` | 不存在 |

房主引爆 `154/6/60005/2`，加入方接收：世界效果在 `19:50:24.7838280Z` / 帧 27638 进入，MakeEffects 在 `.7843543Z` 执行一次；同帧 `.7854173Z` 完成 Registry 注销；旧 RPC 到 `19:50:24.8576868Z` / 帧 27647 才进入，在 `.8582057Z` 输出同样提示并返回。双向结果一致。

`DetonateDemolitionBomb` 先排入 `immediate=false` 的旧 RPC，再原栈调用 `Projectile.Death`；Death 的世界效果及 OnDestroy 的 Removed 快照都是 `immediate=true`。原生 `RPCController.ProcessRPCQueue` 会跳过时间戳尚未达到 `Connect.GetInterpTime(sender)` 的普通 RPC，继续执行后面的 immediate RPC。因此可靠传输也不意味着这三条消息按发送先后执行。

警告表示旧 RPC 查找时对象已经被世界删除路径注销，不能单凭这条提示判断漏爆。在本轮实际样本中，世界效果已先执行一次，旧 RPC 没有再次执行 Death，也没有通过旧路径追加爆炸或伤害。

随后核对最初精简前保存的源码基线：旧引爆 RPC 已经是 `immediate=false`，世界效果和可靠删除状态已经是 `immediate=true`，删除路径也已移除 Registry 键。两条路径及执行时序差异在精简前就已存在，本问题不是此次代码精简引入。

## 修复

`HarmonyDiagnostics.DemolitionBro.cs` 的 `DetonateDemolitionBomb` 在 `_worldEpoch != 0 && IsWorldSession()` 时使用既有世界同步：仅 Owner 在原调用栈执行虚方法 Death，效果及删除由既有公共路径广播，不再额外发送旧引爆 RPC。做法与已有 McBrover 火鸡分流一致。

非世界同步会话的旧 RPC、去重集合、异常处理和本地 Death 路径保留；未修改网络协议、RPC 签名、伤害、爆炸参数、世界删除或表现实现，也没有通过降低日志等级隐藏提示。

## 构建与验证

- Release 构建成功；新 buildHash `7812014cae58a1283d89dea488886d12eece0678fcae66728b38565297269f4f`。
- DLL 仍为 **515,584 字节**，相对本轮基线 **0 字节变化**。
- `Test-WorldPickups.ps1` 245 PASS，`Test-WorldReplayGeneration.ps1` 9 PASS，共 254 项相关检查。
- 部署前确认两端游戏进程数均为 0，再复制 DLL/Info.json。重启后两端实际加载上述 buildHash，MVID 均为 `efdad6f7-2665-42cd-a4e1-bd87e202a8c2`。
- 修复前另做固定位置伤害对照：炸弹 `(80,220)`，500 HP 原生网络生成 Mook 位于 `(92,220)`，临时冻结目标移动并关闭自动清敌。双向各一次引爆，目标均为 `500→495→490`，即两次 5/Explosion；0/None 击退不计伤害。旧 RPC 到达时没有增加 Death 或爆炸伤害。初期未冻结目标时出现的额外网格命中及 Fall 伤害不作为固定位置对照结果。

修复后双端实机 epoch `639308013895507560`，两端均为 `Test Evan2`、2 玩家。每次均等接收端确认炸弹已注册再通过原生 UseFire 主动引爆：

| 方向 / 炸弹 NID | Owner | 接收端实际顺序 | 目标伤害及副本 |
| --- | --- | --- | --- |
| 加入方 → 房主；`72/7/70004/2` | 帧 9112，Death、MakeEffects 各一次 | `19:57:38.4573565Z` / 帧 7854 世界效果进入；`.4583562Z` 重播 MakeEffects 一次；`.4623564Z` 完成 Registry 注销；`.4673583Z` OnDestroy。旧 RPC 0 次。 | Owner 目标 `500→495→490`；两端最终均 490。 |
| 房主 → 加入方；`72/6/60005/2` | 帧 9120，Death、MakeEffects 各一次 | `19:57:59.5047092Z` / 帧 11363 世界效果进入；`.5057076Z` 重播 MakeEffects 一次；`.5117093Z` 完成 Registry 注销；`.5147107Z` OnDestroy。旧 RPC 0 次。 | 重置同一目标为 500 后，Owner 再次 `500→495→490`；两端最终均 490。 |

目标为真实网络生成的 `72/7/70005/3`，由房主持有权威。第二方向记录了同一个 Bomberman 持续爆炸实例在网格 `(6,14)`、`(5,15)` 各造成一次 5/Explosion，因此两次扣血是两个原生网格命中，未将合法多段合并。两个方向的总伤害均与修复前固定位置对照一致。接收端没有通过旧 RPC 再执行 Death；临时探针在每端记录的炸弹 MakeEffects 总计两次，分别对应自身和对端各一颗炸弹。

接收端 MakeEffects 内还会调用一次 Bomberman.Setup；既有 `WorldCarrierSetupPostfix` 在表现重播中会停用这个临时持续效果，正式持续载体由世界描述恢复。该 Setup 调用不单独计为第二次有效爆炸。没有修改这部分机制。

退房后读取本次客户端/房主会话的 `.log` 和 `.trace.log`（包括列目录时长度为 0 的文件）。四个文件实际读到 35、26、232、325 行；旧引爆发送、`remote NID unregistered` 和 `[ERROR]` 计数均为 0。会话分别为 `auto-20261120-195700-474-f4ac622b`、`auto-20261120-195615-881-0dc2c705`。

## 验证边界与清理

本轮使用原生 UseFire 生成及主动引爆真实注册投射物；为稳定观察消息顺序，临时停止测试炸弹 Update，保持对象活动并等待另一端确认同 NID 已注册后再引爆。固定位置目标用于比较伤害，不代表完整人工输入、视听、任意网格布局、FRP、多加入方、迁移或混合旧版本已验收。

探针不编入生产 DLL，已双端卸载。退房后两端均为 `MainMenu`、`_networkSessionActive=false`、`_injectedForSession=false`、`_worldEpoch=0`、`loadCustomCampaign=false`、自动清敌 `true`。随后真正终止两端游戏，各自进程数均为 0。实机验收完成时尚未提交或推送；之后的提交整理保留其它用户尚未提交的修改。

本轮临时探针源码已移除。自动审批以 `blocked by policy` 拒绝删除临时文件，未重试或绕过；保留 `tests/optimization-work/detonation-start-desktop.png`，以及远端日志目录的 `detonation-order-predeploy-exit.tmp`、`detonation-order-final-exit.tmp`。两份退出文件已分别读到 0，不影响本轮双端实测与退出结论。
