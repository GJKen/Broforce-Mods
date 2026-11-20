# 直接 Unit 伤害与碎尸顺序转交

2026-11-17。承接[世界同步复核](../docs/world-sync-coverage/recheck.md)的 G1/R1。已修复、完成标准 Release 双端部署及受控 Steam 双向验证；未提交或推送。原始审计继续保留协议 17 的历史基线，本轮协议为 **22**。

## 原因和实现

BrondleFly 的 TeleFrag 只在施术者拥有端执行。普通敌人分支直接调用八参数 `Unit.Damage`，随后调用 `GibNow(Crush, 0, 100)`；SatanMiniboss 分支直接调用 `Damage(50, Knock, 0, 360, …)`。原副本 mutation 门禁只拒绝这些调用，没有向目标 Owner 转交，因此目标端远端英雄动画不能补偿 TeleFrag。

`WorldMutations.cs` 按完整签名增加 kind 7（直接 Damage）、8（GibNow）、9（受保护的虚拟 Gib），复用现有可靠命中请求和结果通道。目标 Owner 执行原虚方法及覆写，不绕过原生护盾、伤害变换、死亡和碎尸门禁。直接方法本来没有 Map 的 DamagePolicy 检查，不额外改变这一契约；已有 Map 公共命中入口保留其原路径。

来源必须是本地拥有、有 NID 的英雄或世界对象；非空 damageSender 也必须可经 Registry 识别，否则保留既有门禁。原 actor（含 null）、伤害、类型、力、方向和命中位置原值传输。英雄更新上下文同时记录远端英雄，使目标 Owner 上的远端动画不再另结算一次；状态请求仍要求本地拥有英雄。快照/表现重播、原生 RPC 保留原边界。

kind 7–9 追加来源 NID、目标 life 和每目标发送序号。接收验证 epoch、会话、目标 Owner、来源 Registry/Owner、life、Removed/destroyed 和每发送者已接收序号；可靠流保持 Damage→GibNow 的发送顺序，重复或更早序号不再次执行。序号存在现有 WorldEntry 中，不新增全局攻击账本或逐帧扫描。目标复活已发生而周期快照尚未发布时，先发布当前 life，再比较请求。未知/未登记来源和登记前请求不排队重试。

Owner 接收作用域允许当前目标的覆写/base 及 GibNow→Gib 在本端执行；嵌套影响另一 Owner 时，以当前结算目标作为转交来源，保留原 damageSender，继续转交。finally 恢复接收目标、命中请求和请求方；同帧多段和 BroDredd 的独立 Plasma 不按帧吞掉。

本轮实机还确认一个相关根因：范围上下文前缀使用 `object[] __args` 读取 `DamageDoodads(out bool)` 后，Brobocop 近战选点 x 从应为 445 变成 128，目标未选中。临时只替换该前缀为具名参数后，另一位置的实际选点从 32 恢复为应为 117，原生拳击选中目标。正式代码改用四个具名 sender/firedBy/playerNum/newPlayerNum 入口；`DamageOptional(ref bool)` 也用具名 DamageObject 包装入口，避免 Harmony 对 ref/out bool 的自动参数数组回写。保留原方法和 ref 契约，不以重新选目标补偿。

## 构建与离线验证

- `buildHash=afe672f2a4f3b28b34962945e003d9cbbbab5897f0a724040e390da261839929`。
- DLL 536,576 字节，版本 `0.5.1.0`，SHA-256 `FE6B52EF37E45E3A04CA4E9A89B7D50411006D6651439FF809C1D610469D60BB`。
- 双端加载 MVID `025d56f4-b78d-4626-b073-5692800b1d63`，协议 22，最终验收 epoch `639305102827174667`。
- `Test-WorldPickups.ps1`：**221 PASS**。复用生产方法、codec 和实际 Harmony 补丁，覆盖参数/actor/null、Owner、虚拟覆写/base、GibNow/Gib、原生 RPC、不递归、另一 Owner 嵌套、多段、护盾、异常恢复、重复/旧序号、独立的旧 epoch/旧 life、来源不符、Removed、无 NID、具名范围入口及 ref/out 行为。
- `Test-WorldReplayGeneration.ps1`：**9 PASS**，保留已有生命周期生成边界。
- `BuildAndDeploy.ps1 -Configuration Release` 成功；脚本终止并确认双端退出后部署 DLL 和 Info.json。当前 DLL/ZIP 与 README 的构建信息已更新。

上述离线检查不是 230 个实机用例。引擎和 RPC 边界有 stub；虚方法分派和前缀执行使用实际 Harmony，未把字符串匹配或镜像算法当作行为验收。

## 最终构建实机范围

Steam 双端，Workshop `454967403`、`Test Evan2`。原版 prefab 由正式 `Networking.Instantiate` 创建，双方收到相同 NID，并经正式注册和世界快照同步；本轮没有人工配对 NID。夹具在双方冻结英雄及选定敌人的自主更新/移动，调用原生技能方法，并在其外维护与正式英雄更新相同的来源上下文。计数探针在原方法 IL 函数体入口记录，只有函数体真正执行才计数，跳过前缀不计数；不修改伤害结果。

| 项目 | 最终构建双向结果 |
| --- | --- |
| Brobocop 拳 | `PerformBroboCopPunchAttack` 正确选中目标，meleeHasHit=true。各 Owner 的 Mook.Damage 一次，参数 4/SilencedBullet；原覆写转为 Bullet 后进入 TestVanDammeAnim.Damage 一次，健康 200→196。随后在目标 Owner 调用远端英雄同一原生方法，未增加 Damage，双方保持 196。 |
| BroDredd 连续电击 | 双方先确认角色已登记后，各连续调用 `PerformTazerMeleeAttack` 14 次。首次选中新目标置 tasedCount=0，之后 13 次达到 13（`>12`），因此各 Owner 恰好 14 次 1/Shock 和 1 次 1/Plasma。原 Mook 覆写将这些调用传入基类的 damage 变为 0；shockDamage=14、plasmaDamage=1，健康 196，双方一致。目标端再运行远端英雄 14 次，没有新增原生 Damage。 |
| 公共多段负对照 | 每方向调用 `Map.KnockAndDamageUnit` 三次，每次 2/Normal；各 Owner 的 Mook.Damage 和基类各三次，196→194→192→190，两端一致。目标端远端英雄重复调用三次，不新增伤害。 |
| TeleFrag 特殊目标 | 对各自拥有的原版 SatanMiniboss，另一端 TeleFrag。各 Owner 的 SatanMiniboss.Damage 一次，原参数 50/Knock/0/360；覆写后基类收到 vy=36。240→190，双方一致，未触发 Gib。夹具 GoID 为 60009/70007。 |
| TeleFrag 普通敌人 | 使用前述健康 190 的普通敌人（GoID 60005/70004）。每方向各一次原生 TeleFrag；Owner 的 Mook.Damage 和基类各一次，190/Normal/500/500，随后 Unit.GibNow、Mook.Gib、TestVanDammeAnim.Gib 各一次，Crush/0/100。拥有端 health=0、destroyed=true、action=Dead；另一端最新快照为 Removed/Dead/Destroyed/SpritePose/Gibbed、life=1。 |

双方 `_worldReceivingHit` 最后为 0。先前 `7e8385fe…` 构建也有 TeleFrag 普通/特殊分支证据，但以上矩阵均已在最终 `afe672f2…` 单独重测。

## 失败样本与未验收边界

- 首轮不冻结 AI 的临时 Boss 夹具曾伴随远端进程退出；Windows Application Error 1000 报 `0xc0000005`、模块 unknown。另一轮未冻结的 Boss 样本出现主线程超时，ping 仍为 pong；未判定为通过，也没有证明由转交代码引起。冻结自主更新后最终矩阵完成。完整 Satan AI/阶段转换战斗仍未验收。
- 最终构建初次刚 swap 便发起的一次拳击未观察到 Owner 伤害；等待双方角色实际登记后重复一次，Owner 计数成立。该早期样本不计通过，不宣称来源尚未登记时能排队补发。另有一次游戏启动卡住且最新 `.log`/`.trace.log` 均为 0；两者已读取，确认终止进程后重新启动成功。
- 完整技能按键/动画时序、人工视听体验、长期战斗、全部执行/处决入口、第三方程序集新声明、失效/已消失来源、不同 life 的恶劣网络顺序、多加入方和 Host migration 未验收。现有 G4 命中音画通道保留；本轮记录原生结算、状态反馈和 meleeHasHit，不冒充完整人工试听/视觉验收。
- 没有扩展 G2 地形/延迟副作用审计，没有修改已排除的 TearGas 年龄逻辑，没有重跑全部 G1–G7 矩阵。

## 清理

两端临时计数/冻结探针和选择观测探针已卸载，临时源文件已删除。自动清敌恢复 true，TestMod GameSpeed=1，退出前两端 game_state 的 timeScale=1。全部临时对象随进程退出清除；WMI 最终确认本机和测试端 Broforce 进程数均为 0。清理时一次远端 WMI 查询返回拒绝访问，下一次原生 WMI 终止及退出确认成功，未将失败查询当成已关闭。

已读取两端最新普通 `.log` 与 `.trace.log`，包括远端元数据仍显示 0 字节但实际可读的尾部；保留既有日志和用户材料，未新增截图或证据目录。
