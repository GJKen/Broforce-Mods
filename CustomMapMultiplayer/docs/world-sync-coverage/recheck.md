# G1–G7 修复后复核

2026-11-17开始复核，2026-11-20收束G1–G7版本说明；当前发行版本为 **0.5.6**、世界协议 **25**，见 [版本说明](../RELEASE-0.5.6.md)。以下G1–G7证据仍属于各自历史构建，不把它们算作当前构建重新验收。

结论：G1–G7 原报告列出的具体主路径均已有修复与定向证据，但不能据此宣称世界同步全覆盖。复核确认的 **G1/R1 直接 Unit 伤害及关联终态转交**已补齐，TeleFrag、Brobocop、BroDredd、公共多段和 ref/out bool 选点修复的证据见[直接 Unit 伤害与碎尸顺序记录](../../issues/ISSUES-2026-11-17-直接Unit伤害与碎尸顺序转交.md)。**G2/R3/R6** 先修复埋雷继承外层来源的重复伤害，接续又按真实路由和计数确证并修复自然 Mine 键错位的漏路由/错爆及自身坍塌抢占倒计时；最终构建双向踏雷与实际英雄伤害通过。自然替换块、延迟脚手架/桥面链及五指爆心原构建计数未发现重复有效结算，见[地形埋雷与延迟副作用记录](../../issues/ISSUES-2026-11-17-地形埋雷重复伤害与延迟副作用核验.md)。其它辅助链和触发器仍有未验范围，不足以判定它们全部存在重复。G3–G7 保留原记录的覆盖限制和未验收组合。

## 2026-11-20角色验证记录与边界

2026-11-20用户已停止全量角色、独立形态与机关扩测。当时版本保留已有公共修复及各自构建的定向证据，不保证全角色、全机关通过；清单项数不作为完成数，也不据此计算完成百分比。以下观察保留当时构建归属；后续Desperabro/BroveHeart等既有修复以[问题索引](../../issues/README.md)及各自记录为准，不能把旧分歧当作当前仍必现。原始探针和逐批材料不作为发行说明的依赖。

- 最近角色验证主要增加正常输入覆盖，没有新的生产修复或性能优化。McBrover、Ellen、Walker 本人正常近战各有真实目标的双端血量终态；空挥、动作观察和受控底层原生方法不能提升为完整技能通过。
- Desperabro 演奏期间正常 fire 在拥有端实际进入 UseFire→Finish，并在自然 4.2 秒前取得正式双端终态；两原英雄与伙伴存活，但副本伙伴仍演奏且位置分歧。只认拥有端控制因果与分歧观察，双端终止控制未通过，未直接确定 CMM 根因。
- BroveHeart 正常技能输入已观察到拥有端 boost 激活并关闭，副本对应控制状态未激活；只保留该短窗观察，不认双端技能状态结算。
- 部分角色技能、独立形态、方向与实际适用机关未测，精确副本弹体和调用链仍有缺口。Scorpion、Chev 本轮不在换角工具的当前过滤列表中，未执行正常输入；不表示原版永久不可用。死亡、因果或原证丢失样本不补认。
- 自定义适配未纳入本轮；Furibrosa 召车/驾驶观察与 Aquabro 历史实现和验收保留。多加入方、网络劣化、音频一致性、迁移及任意动态字段恢复不属本轮验收。
- 黑屏、切关、加入与死亡残留等历史问题维持各自记录状态，不能宣称当前必现。未测不推翻已有修复，也不自动授权继续补测。

## 基线和证据范围

- 当前0.5.6/0.5.6.0、WorldProtocol25，DLL521216字节，ZIP内DLL/Info与发行目录一致。0.5.5与0.5.6是行为等价精简：前者减少反射调用并缩小512字节，后者减少重复字段查询但文件大小不变；双端实际数组/房间字段差分通过，未重跑全部G1–G7。特殊同步修复及受控验证范围见 [问题记录](../../issues/ISSUES-2026-11-22-特殊敌人与榴弹效果同步.md)，优化范围见 [版本说明](../RELEASE-0.5.6.md)。

- 2026-11-22第42批历史构建：0.5.4/0.5.4.0、WorldProtocol25、内嵌构建 `7f3748ff36b3ce9a78b3cee8c88057730e842980d7c1a844e95446e3c82be288`，DLL521728字节。仅修复继承MakeEffects身份比较，一次成功构建与双端DLL部署；一房受控原生Mega窗口六枚同NID每枚发送/有效接收各一次，真实Large/Small/Glow及位置一致，之后原生移除。自然阶段入口未验、伤害单枚NID归因未证只作限制；四特殊和其它已接受证据复用，G1–G7未重跑。双端统一恢复并独立确认进程0。详见[第42批结果](../../issues/ISSUES-2026-11-22-特殊敌人与榴弹效果同步.md)。

- 2026-11-22第36批：复用0.5.3/协议25及`4f86cd81…`构建，两端实际加载一致；生产修改/构建/部署0。唯一自然Lost Soul源[91,7,70003,3]串起接触入口、Send/ReceiveHit Melee4和目标owner同源Damage，hp5000→4996一次；最终Melee0另结算且不降血，mirror接触抑制，修正后的探针无异常。详见[第36批结果](../../issues/ISSUES-2026-11-22-特殊敌人与榴弹效果同步.md)。其它已接受四特殊代表证据复用，未扩方向/晚加入矩阵。
- 2026-11-22第35批历史构建：0.5.3/0.5.3.0、WorldProtocol25、内嵌构建 `4f86cd81a36c2b6fc5a0c36aaa2c7d5c9bef1d548e4b3d936edf665c7a7a4fc5`，521,728字节；一次成功构建/双端部署并实际加载。20项受控检查通过；Lost Soul自然接触受观察脚本异常污染，修正探针后未复验；Baneling自然周期取得同源跨端Acid0覆盖，未冒认Acid9或关闭历史异向。见[第35批结果](../../issues/ISSUES-2026-11-22-特殊敌人与榴弹效果同步.md)。
- 2026-11-22第34批历史构建：0.5.2/0.5.2.0、WorldProtocol25，DLL内嵌构建 `506be106bf771a9d3080faa33c0c543524968175cb052d5acfa5c02bf8e9e8b2`，521,216字节；标准脚本成功构建/双端部署一次，另有一次真实编译失败。双端实际加载版本和Build hash一致，实际Warlock配置消息消费142/142字节。Warlock、Screecher代表链通过；当批Lost Soul跨端Melee4、Baneling跨端酸作用未通过，详见[第34批结果](../../issues/ISSUES-2026-11-22-特殊敌人与榴弹效果同步.md)。以下0.5.1和G1–G7核算为历史事实，不改写为新构建结果。

- 2026-11-20 离线核对：分支 `main`，HEAD 为 `75f2869dd612362d0cb113f3689a5d1a3865cde5`；最近 CMM 历史提交为 `85f5abfb054be0b9cdbc028f03d2a6c66b409fef`。初次复核 HEAD `434027ed…` 属历史基线；当前实现含未提交修复，不能由 HEAD 代替。
- 2026-11-20核算时协议24。自然Mine键错位的踏雷路由和自身坍塌倒计时修复新增专用消息；当时仓库DLL内嵌构建为 `fadcc2a40b4a91b69b54798f9c3079bbe7417eb71993f61f2f6bc892a2047f7c`，版本 `0.5.1.0`，540,672字节；SHA-256为 `7680094EB83AF55AF68A4B13324A763736D3C7CB0FB122A3CE09C01CB54D8A35`。当时ZIP内DLL、Info.json和顶层元数据对应一致，包版本0.5.1。
- 历史核算（2026-11-20）：当时按 BuildAndDeploy 清单算法只读核算 56 个源码和 15 个引用，所得 hash 与当时 DLL 内嵌值及已验运行构建 `fadcc2a4…` 一致。该次只读核算未构建、部署或启动游戏；相同构建身份不代表下列旧矩阵已全部重跑。
- G2 接续修复运行 `Test-WorldPickups.ps1` **245 PASS**、`Test-WorldReplayGeneration.ps1` **9 PASS**。含实际 Harmony 分派/作用域、CheckForMine/CreateRPC 及生产 codec 检查，不等于 254 个实机用例；前轮 228+9 和中间倒计时版 237+9 保留原归属。
- 初次复核仅做静态分析；G1、G2 接续修复实际修改实现、构建、部署和定向双端验证。G1 角色矩阵属于 `afe672f2…`，替换块/支撑/五指爆心矩阵属于 `f634681c…`；本轮最终踏雷、重复/负对照、立即坍塌及英雄伤害属于 `6d433bbf…`。其它实测仍引用原记录的具体构建。G7 主要加入样本来自 `a97e7be8…`，`9807cc86…` 仅另验原生 LaserBeam 终态/晚到 Fire，没有把旧矩阵称为最新构建全部重测。

## G1–G7 对照

“已修复”指原报告列出的具体缺陷已有实现及对应定向证据；“实现边界”表示当前确实未提供完整能力，不等于已证实每个边界都会造成故障；“仅未验收”不能据此要求立即改代码。

| 项目 | 已修复及已有证据 | 仍有缺口、实现边界或风险 | 仅未验收的代表范围 |
| --- | --- | --- | --- |
| G1 伤害被拦未转交 | 原四类直接入口保留；八参数 Unit.Damage、GibNow/虚拟 Gib 已复用可靠 Owner 请求，保留原参数及 Damage→Gib 顺序、life/epoch/序号规则。TeleFrag 普通/特殊目标、Brobocop、BroDredd 和公共多段均双向计数通过；ref/out bool 回写破坏选点也已修复。[新记录](../../issues/ISSUES-2026-11-17-直接Unit伤害与碎尸顺序转交.md) | 无 NID/未登记或失效来源不排队补发，第三方新声明与其它处决入口未通用化；`DamageOptional` 同步 bool/ref 不跨网返回，其穿透影响仍待验。 | 完整技能按键/动画、人工视听、各类 Damage_Local、完整 Boss/延迟战斗、其它执行/处决与恶劣网络。 |
| G2 生命周期及地形延迟副作用 | 保留生命周期工厂/钱币边界及 APC、奖励箱、机枪座原证据。Mine 继承外层接收来源的重复伤害已修复；接续又确证并修复自然 Mine 键错位漏路由/错爆和自身坍塌抢占玩家倒计时。最终双向各一次、重复/负对照、立即坍塌及实际英雄伤害通过；11-18 又完成双向原生运动/CheckForTraps 真实射线踏雷，以及双方 primed 后独立坍塌的原生 Explode(5) 边界。替换块/支撑/五指爆心沿用原构建计数。[新记录](../../issues/ISSUES-2026-11-17-地形埋雷重复伤害与延迟副作用核验.md) | 不是任意辅助函数、会使用返回值的召唤、第三方程序集或地图事件的一次性系统。按方块解析附着 Mine，不重编号或全场扫描；保留各端必需的本地替换/爆炸，未全局禁止生成。派生 Collapse 可重复安排回调，但已测样本没有重复有效破坏。 | 任意陷阱/连续跑跳按键和人工音画、全部 supportedBy 树/链条/屋顶组合、APC 摩托分支、特殊 Boss 死亡、地图触发器、真实晚加入奖励/倒计时、多加入方、燃烧传播和长期运行。 |
| G3 派生手雷生成 | `SpawnGrenadeLocally` 对有身份来源发布共享描述并在 Launch 前登记；Cluster 子雷、原生 Molotov 火雷顺序、零参数派生效果及 FlashBangExplosion 载体有处理。Cluster、Molotov、火焰波/催泪双向样本成立。[记录](../../issues/ISSUES-2026-11-17-派生手雷与命中音画同步.md) | 无来源/NID 的第三方本地工厂保留原语义；生成描述只传规定字段，任意运行时 prefab 配置未全部序列化。原版派生声明已统一扫描，不能把“某派生类未单测”写成“未安装钩子”。 | 全部 prefab、源销毁/反弹/归属变化、每种动态配置及长时间联机。持续载体的正常加入/重入已由 G7 补测，不再沿用 G3 记录中的旧“G7 尚未实现”。 |
| G4 命中音画与派生表现 | 实际派生 MakeEffects 重播、Grenade 零参数覆写、SachelPack/Turkey 随机视觉、枪伤喷血/背景血迹及选定 Hurt/Burn/Fizzle/Death 声音已补；保留 Aquabro 外部接管。相应双端调用/参数有实测。[记录](../../issues/ISSUES-2026-11-17-派生手雷与命中音画同步.md) | 不是全局音画事件总线。记录仍明确普通 Rocket 的随机音频选片可能不同；AudioClip 按名解析存在资源重名/未加载限制。任意外部声明的效果方法不自动获得原版程序集扫描钩子。 | 完整人工听感、每种原版派生类的 Owner 专属视觉分支、全部英雄/Boss/外部角色音画。缺少人工试听不能再记成“音效未实现”。 |
| G5 Doodad 受损阶段 | sprite/pixel、已知 damage material、isDamaged、三组碰撞、根 Collider 和 pristine children 已进入快照；瞬时受损效果独立事件，基线不补播。Hatch/Bush、双向阶段恢复、重复基线有实测。[记录](../../issues/ISSUES-2026-11-17-场景物件受损阶段与后注册清理.md) | 材质只编码已知字段身份，任意自定义/未标识原材质不在覆盖内；门、机关计时、奖励/触发器等私有状态未通用化。没有发现原报告的通用受损阶段仍然缺失。 | greater 阶段、全部 prefab 的根碰撞/子物体组合、各类私有状态和长期时序。 |
| G6 Removed 先到后注册 | 已有空 entry 会绑定迟到对象，等待原生初始化后消费墓碑，清理 Map/Registry/Sync；保持类型、Owner、life、epoch 规则。真实 AlienBrute 双向倒序、普通生成销毁及负对照有实测；G7 又补 LaserBeam NativeId 终态拒绝晚到 Fire。[记录](../../issues/ISSUES-2026-11-17-场景物件受损阶段与后注册清理.md) | 保证范围要求可识别身份及已处理生命周期。无 NID、只 SetActive(false)/返池不自动等于 Removed；这不是新的已确认注册错误。 | 其它全部类型的同一倒序、OnlyDestroyScriptOnSync 全矩阵、自然复活、真实切图/新 epoch、Host migration。 |
| G7 持续状态/载体加入恢复 | Stun/Blind/Freeze、Shock/Plasma、Acid、TearGas、寄生关系及燃烧年龄纳入状态；FlashBangExplosion 活动网格/剩余时间和 LaserBeam 原点/角度/启动/life 纳入 Owner/serial 描述，副本不做 tick/RunDamage。真实 Steam 加入/重入、非房主→房主受控恢复、依赖 pending、终态有证据。[记录](../../issues/ISSUES-2026-11-17-中途加入持续状态与载体恢复.md) | 云团只重建活动格代表 Puff；历史粒子精确年龄、全部 spawner/随机外观/声音及任意动态字段没有完整恢复。任意 timeScale 变化历史未重建。**本次排除 TearGas 与 stunTime 关联条件为缺陷。** | 全部 Freeze 技能入口、寄生自然结束后的胸腔实体生成、完整 Boss/英雄战、多加入方、Host migration、真实切图/复活与恶劣网络。非房主→房主夹具不能当成第三端真实晚加入。 |

## 已完成修复的历史定位与验收边界

### 直接 Unit 伤害与关联终态：已完成接续修复

以下为初次复核的定位过程；其中“尚无转交/尚未复现”描述已由协议 22 修复及受控实测取代。当前实现、221+9 检查、最终构建双向矩阵、失败样本和清理见[直接 Unit 伤害与碎尸顺序记录](../../issues/ISSUES-2026-11-17-直接Unit伤害与碎尸顺序转交.md)。没有重开全量 G1–G7 审计。

初次复核时，[WorldDirectMutationKind](../../src/HarmonyDiagnostics.WorldMutations.cs#L13) 只给四类直接入口分配请求 kind，未包含八参数 `Unit.Damage`。当时的安装点 [PatchEntityFinalStateSynchronization](../../src/HarmonyDiagnostics.WorldState.cs#L167) 对其它 Damage/Gib 仍使用 [WorldMutationPrefix](../../src/HarmonyDiagnostics.WorldState.cs#L319)，非拥有副本返回 false，不会从这里发送新请求。

本次追到的明确成立条件：世界会话有效、epoch 有效、目标是已登记且由另一端拥有的非英雄单位、该目标能被 TeleFrag 选中。

1. `BrondleFly.UseSpecial` 在 `IsMine` 条件内才调用 TeleFrag；目标 Owner 上的远端英雄不执行该函数。
2. `TeleFrag` 对普通敌人直接 `unit.Damage(unit.health, …)`，随后 `unit.GibNow(…)`；SatanMiniboss 分支直接 `Damage(50, Knock, …)`，均没有 Map 公共命中请求。
3. 本端目标副本的 Damage 被 mutation 门禁拒绝；`Unit.GibNow` 只调用虚拟 Gib，也被相同门禁拒绝。不能寄望 Gib 内部的原生 RPC，因为原函数体尚未执行。TeleFrag 后续位置/动画变化不传递这次目标伤害。

因此这是**源码确认的跨 Owner 伤害/终态转交空档，尚未实机复现**，比单纯“没有专用补丁”更强。即使双方最终血量都没变、状态一致，这次攻击仍可能未在目标 Owner 结算。

当时提出为公共直接伤害定义来源、已有命中上下文和目标 Owner 结算规则，并连同 Gib/处决等复合结果设计；仅把 TeleFrag 的 Damage 改成普通非致死请求，仍可能漏掉紧随其后的碎尸语义。可复用现有请求编码及接收作用域，但不能在全部 Unit.Damage 上无条件转发，否则已有 Map 请求、基类/覆写嵌套和目标端英雄动画会引入重复来源。

`Brobocop.PerformBroboCopPunchAttack` 和 `BroDredd.PerformTazerMeleeAttack` 继续作为计数对照，不能像 TeleFrag 一样直接判为失效：它们可能在目标 Owner 上运行远端英雄动画。BroDredd 的 HitClosestUnit 公共命中和后续 Plasma 直接伤害是两条不同语义，合法追加伤害不能被按帧去重吞掉。

当时的最小验收要求：交换施术者/目标 Owner，分别测 TeleFrag 普通敌人的一次伤害及碎尸、其特殊目标分支、Brobocop 拳、BroDredd 达到追加电击阈值；记录目标 Owner 真正执行次数、参数、终态和双方反馈。另对普通公共命中、连续多段及嵌套其它 Owner 做负对照。不能只比较最终血量。

### G2：已完成地形和延迟副作用的定向核验及埋雷修复

[CanSkipWorldReplayFactory](../../src/HarmonyDiagnostics.WorldReplayGeneration.cs#L20) 和 [PatchWorldReplayGeneration](../../src/HarmonyDiagnostics.WorldReplayGeneration.cs#L58) 保留原边界。本轮按原报告 R3/R6 统计具体链路，而没有扩大工厂拦截；结果、失败样本及最终构建计数见[新记录](../../issues/ISSUES-2026-11-17-地形埋雷重复伤害与延迟副作用核验.md)。

- `Block.DestroyBlockInternal` 会调用 supportedBy.Collapse、ReplaceBlockWith 和 mine.Detonate。加入方请求接收期间，Mine 原生爆炸误继承 `_worldReceivingTarget` 并额外转交；现在显式 Mine 保持自身来源，公共命中不借用外层弹体上下文，仍由各端原生 Owner 门禁结算。
- `ReplaceBlockWith` 的 PlaceGround/SetupBlock/Registry 每端各执行一次，替换 NID 一致；重复确认和基线未新增实例。自然脚手架延迟链中四块桥面每块每端实际破坏及统计各一次。不能把重复 Collapse 入口或每端本地恢复直接算作重复网络召唤。
- `ExplodingHeartCountDown` 的原生 Detonate RPC 双向均每组件每端一次；两个合法组件保留两次爆炸。手动移除待触发组件后无爆炸；不等于原生死亡自动取消。跨 Owner 点燃、真实晚加入和全部延迟组件未验收。
- 地图统计/触发器可能有各端必需的本地状态；本轮没有发现已测替换块、桥面或延迟爆炸再次产生有效破坏/伤害，未给这些路径另加禁止规则。
- 接续自然踏雷核验：首个协议 22 布局键一致、原生路由正确；但真实英雄显示自身坍塌将副本倒计时 -10 抢成 5，造成漏伤。中间协议 23 重载后捕获真实 Mine 键错位，实际接收为 null 或另一颗雷，才增加基于 Block NID 的 prime RPC。最终协议 24 在自然错位键现场双向正确引爆，保留 0.3 秒倒计时、原生立即坍塌和幂等。身份/交叉解析表及实际次数见[地形埋雷记录的协议 24 接续段](../../issues/ISSUES-2026-11-17-地形埋雷重复伤害与延迟副作用核验.md)。没有把受控 CheckForMine 写成完整物理踩踏。
- 2026-11-18 集成补验沿用同一协议 24 构建，无业务改动：正常 Rambro 自然落地双向通过 Update/RunMovement → CheckForTraps 的真实射线引爆，每颗被踩 Mine 每端有效 Explode(-10) 恰一次，踩雷英雄各一次伤害；双方先 primed 后的独立坍塌仍以原生 ActuallyCollapse 执行 Explode(5)，重复确认不追加伤害。见[地形埋雷记录的自然踏雷集成段](../../issues/ISSUES-2026-11-17-地形埋雷重复伤害与延迟副作用核验.md)。首次探针错误样本排除；换关 AlienEgg 清理空引用可由原生集合卸载顺序解释，现有证据未建立其与 Mine 修复的关联，按独立清理线索保留，因果限制见当时的独立重载复核。不把任意机关、按键音画或旧矩阵写成本轮全验。

本轮已确认的埋雷重复点已修复并复测。未把其它边界一并判坏，不增加全局 Instantiate/Spawn 拦截或通用事件账本。

## 本次排除及优先级

TearGas 的恢复条件 `s.TearGas && s.StunTime > age` 位于 [ApplyWorldPersistentStatus](../../src/HarmonyDiagnostics.WorldPersistentStatus.cs#L86)。原版 `Mook.TearGas` 同时写 `stunTime=time` 和 `tearGasChoking=true`；`TestVanDammeAnim.Update` 在 stunTime 耗尽时调用 StopBeingStunned；`Mook.StopBeingStunned` 清除 choking。当前原版公共生命周期没有另一个独立 choking 计时，因此不能为该条件增加新字段或直接删除年龄判定。任意外部脚本直接改字段另属兼容范围。

后续顺序：直接 Unit 伤害及复合终态转交、G2 地形/延迟和埋雷来源重复修复、踏雷身份/自身坍塌参数修复均已完成；双向正常原生运动/真实射线踏雷、双方 primed 后独立坍塌的集成验证也已通过。当前没有其它有证据且未处理的 G2 必需故障；相关修复按现有证据保留，当前不再安排扩测。连续跑跳按键、完整人工音画、其它陷阱及 supportedBy/触发器组合、燃烧传播、G4 随机声音及 G3/G5/G7 限制保留为可选边界，按后续实际需求选择；不为“尚未测过全部角色和资源”继续扩框架。

初次复核新增本文件并在原报告加入入口；接续修复更新本文件、实现、发行产物和当前文档。保留全部既有工作区修改及原报告历史基线，未提交或推送。
