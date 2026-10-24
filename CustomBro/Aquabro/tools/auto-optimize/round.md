# Aquabro DLL 瘦身：单轮执行约束

你正在执行已获用户授权的 Aquabro 自动精简。只完成当前一轮，交接由外层控制器负责；不要自己再开对话或创建定时任务。遵守仓库根 AGENTS.md 和 CustomBro/Aquabro/AGENTS.md。

进度说明只用commentary；不要以“准备读取／已启动游戏／下一步验证”等进度文字结束本轮。必须继续完成、回退或确认无候选，再按schema提交最终JSON；若环境阻碍则清理回退后提交blocked的JSON。已验收的历史改动是本轮基线，不重做；本轮实际体积以交接参数baseline_dll_bytes及快照为准，下面761856字节是切换体积目标时的历史分析。

## 本次目标：最终 DLL 实际字节数最小化

用户已明确最终目的是减小编译后的 Aquabro.dll。本规则替代旧批次仅以计算次数或分配数量认定收益的规则；旧历史的 no_candidate 不代表新目标没有候选。每轮至少减少交接参数 minimum_saved_bytes（当前512）字节，同体积、变大、只减少源码行数或只改善耗时，都不能验收。

优先按预期字节收益排序：无损精简大块内嵌数据、可证明无用的私有代码、重复 IL／数据共享。合并同一功能内紧密相关的调整为一轮，不能把一次加减乘除拆成一轮；可以先做不修改生产文件的候选体积估算。保留运行性能与可维护性，不能换来逐帧解压、每次查询重建数据、额外依赖、外部数据文件或延迟失败。

当前已知基线：正式构建已 /optimize+ /debug-，使用 Framework v3.5 csc；DLL为761856字节，PE元数据约706808字节，其中#US字符串堆676500字节。五份生成蒙版以Base64字符串保存，是优先调查方向。不能把Python压缩估算当成.NET可运行证据；如选无损编码，要求旧Unity Mono/.NET3.5实际解码成功，五种蒙版每帧每字节完全相同，初始化只做一次且稳定调用不新增分配。

禁止改编译器、响应参数、csproj、构建配置和Assembly名称来伪造体积差；不得更改aquabro-build.rsp、BuildAssets.ps1、tools/BuildEnvironment.ps1。禁止混淆、程序集压壳、删除功能/资源、降低像素精度、改变帧顺序或缩小命中区域。压缩后的数据仍内嵌DLL，PNG、JSON、部署目录结构保持不变。

## 最高验收要求

**不得破坏本轮修改前的任何机制。** 优化不等于改玩法，不改数值、时序、判定范围、目标筛选、所有权、去重、冷却、资源生命周期、地形遮挡、原生伤害路由、动画帧／蒙版、输入交接、共享状态或联机语义。原机制不理想也不能借优化顺带改掉。不能仅凭编译通过或看起来等价认定机制不变。

- 只改 CustomBro/Aquabro/src 中与体积候选相关的代码，必要时补该项目回归、诊断和文档。仅为无损蒙版存储，另外允许修改 tools/export_skill_effects.lua、tools/export_water_effects.lua、tools/SpringPixelMask.cs.in、tools/ChargedWaterPixelMask.cs.in；必须保证正式构建重新生成后仍得到新表示，不能只手改生成文件。禁止修改其他工具、其他插件、AGENTS.md、Git配置、自动化控制器及其规则、LocalBuild.json、依赖DLL、源稿、正式PNG、TridentAtlas.cs。保持PixelMask查询接口、size与frames字段以及ChargedWaterPixelMask.Frames字段，便于独立完整导出。
- 现有所有工作区改动都是用户的基线；不得 stash、checkout、reset、clean、提交、推送或抹去它们。本轮开始已有的精简也不得重做。回退只恢复本轮方案触碰的内容到本轮基线。
- 每项候选必须解释为什么会减少最终DLL，而非只减少CPU工作。不能删保护判断或合并独立目标以换体积，不为缩短行数制造抽象，不做无关大重构。

## 失败处理（用户明确要求）

1. 发现测试、实机机制不一致，或实际DLL缩减未达到门槛，记录证据并撤销当前方案。不能因体积无收益就作为成功交接。
2. 分析原因，尝试真正不同、可验证且保持机制的替代实现。每个候选最多尝试三种合理方案；不要重复同样的失败，也不要删断言、改预期、放宽判定或改原机制来通过验证。
3. 相关可用方案都失败／不适用时，恢复本候选原代码及受影响部署，返回 `alternatives_exhausted`。这只是放弃当前候选，外层会新开对话找别处。不能因第一次验证失败就停止整体优化。
4. 游戏工具、构建环境等真实阻碍无法解决时，恢复本轮并返回 `blocked`，不能伪造实机或声称机制保持已证实。

## 每轮步骤

1. 先读本轮交接文件、当前部署状态、README、机制实现细节、测试说明及上一轮报告。列出按预估字节收益排序的候选，选择一项；同功能可合并紧密相关的调整。记录当前实际DLL字节数及本轮快照哈希，写出具体机制不变量。若没候选，完整读生产源码及蒙版生成入口，说明剩余候选为什么不能减小DLL或无法安全验收；不要沿用旧性能目标的排除结论。
2. **改生产代码之前**启动当前游戏、进入测试关并确保实际角色是海王，记录 DLL SHA256、游戏加载程序集 MVID、场景、关键状态，运行覆盖本候选机制的基线。先保存原始 MCP 输出和 before.json，再改代码。尽量复用现有诊断，覆盖受影响边界（方向、FPS、暂停、所有权、关联部件等按实际路径确定）。
3. 做可回退的聚焦改动，执行相关离线回归。使用tools/auto-optimize/Measure-Build.ps1 -OutputDirectory 本轮evidence下目录，不部署地测量同正式参数编译的候选DLL；体积不足先换方案，避免为零收益反复启动游戏。必要时补风险断言，不放宽既有测试。若改了生成器，先完整生成并构建才能测其输出。
4. 先彻底关闭 Broforce 并确认进程数为 0，再运行 `BuildAssets.ps1` 构建部署，核对项目及两处目标 DLL SHA256。编译仍有既有三处 CS0253 不属于本轮新失败。
5. 新进程启动游戏，确认新 MVID 与新 DLL；执行与基线相同的实机检查，必要时补完整受影响周期。保存原始 MCP 输出和 after.json。只有机制不变量全部一致、测试通过，才能写 `docs/自动精简-<批次>-<轮次>.md` 和更新当前部署状态；如有失败按替代方案流程处理。
6. 清理探针／临时实体／补丁、恢复临时设置并关闭游戏；返回JSON结果。报告必须包含旧/新DLL字节、减少字节与比例、相同构建参数、完整蒙版比较及验证局限。只在实际缩减达到门槛且机制保持后写docs验收报告；失败方案只写archive证据。

## 每轮强制完整蒙版证据

所有成功轮次（即使只改普通代码）都必须在旧、新实际游戏进程分别运行受保护的tools/auto-optimize/MaskSnapshotLive.cs；用ScriptContext.Args["output"]传入本轮evidence/before-masks.bin和after-masks.bin绝对路径。探针通过实际加载的程序集导出五种蒙版的名称、尺寸、帧数、每帧长度及全部字节。保存原始MCP输出，将路径填入mask_before_record、mask_after_record，并把每种蒙版帧数和指纹加入before/after.json的mechanisms。控制器会独立完整字节比较，不能只比较若干接触采样、命中总数或自己填写的哈希。

修改生产代码之前必须先运行旧版完整导出并保存before.json；改生成器后还需证明完整BuildAssets.ps1重新生成的所有PNG与基线哈希相同，动画帧表不变，五组离线入口通过。新的解码路径需覆盖首次初始化、完整逐帧查询、重复调用、暂停/释放清理，禁止把解码移动到逐帧查询。

输出dll_bytes_before/dll_bytes_after/saved_bytes必须来自本轮快照和最终部署DLL实际文件，不能来自估算。未成功保留代码的结果填相等的前后体积、saved_bytes=0，未运行蒙版导出可填空路径；不能伪称新实机验证。

## 实机记录格式

before.json / after.json 必须是 UTF-8 JSON，存到本轮指定的证据目录，至少包含：

```json
{
  "phase": "before",
  "dll_sha256": "实际 DLL 的完整 SHA256",
  "runtime_mvid": "从游戏实际读取的 GUID",
  "scene": "实际场景",
  "character": "AquabroMod.Aquabro",
  "mechanisms": {"具体不变量名称": "实际测到的确定性结果，可用数字／数组／对象"},
  "passed": true,
  "raw_logs": ["证据目录下实际保存的原始 MCP 输出文件"],
  "notes": "明确限制、性能数据及不参与等价比较的非确定性状态"
}
```

after 的 phase 为 after；`mechanisms` 的键和值必须与 before 一致。把性能耗时、出生坐标等非确定性数据放 notes，不要伪造或删去确实改变的机制。原始输出必须能支持机制结论；若既有探针不足，应先补合适探针，无法建立可靠基线的候选不要动。

## 工具与本机已知事项

- Unity Inspector MCP 可执行生产方法、记录游戏状态；`Error: Game process died` 可能误报，检查实际进程／连接再判断。
- MCP 的 launch_game 在 Windows 可能因 bf 命令缺失失败；需要实机启动时，遵循 `CustomBro/Aquabro/AGENTS.md` 使用桌面 `Broforce.url` 快捷方式。不要把本机游戏或 UnityModManager 绝对路径写进源码、报告或 Git，也不要随意改安装配置。
- 暂停用 `TestMod.Plugin.SetGamePaused(true)`；仅设 Time.timeScale 可能被 Test Mod 改回。结束恢复 false 后终止游戏。
- 可用 MCP go_to_level 0/0 进入测试地图；加载后再检查角色，不要假定切图已经完成。
- `tests/LogicOptimizationLive.cs` 覆盖上一轮的毒水/HUD/预算，不是所有新改动都能只用它验收；新路径需对应基线。
- 五个离线入口：Test-Spring、Test-Charge、Test-WaterBlastZones、Test-SpriteInitialization、Test-FrameStep。修改后外层还会独立执行这五个脚本。
- 最新 .log / .trace.log 即使是 0 字节也要读取核对。

## 返回语义

- `optimized`：最终DLL至少减少minimum_saved_bytes字节，机制不变、完整蒙版相同，所有适用验证通过，before/after及docs报告真实存在；至少有一个passed方案。
- `no_candidate`：通读当前 src 后找不到仍有收益且可安全验证的候选，本轮没有改生产代码；summary 写清检查范围。
- `alternatives_exhausted`：写明具体候选、尝试与失败原因，所有失败改动已回退；外层继续寻找其他候选。
- `blocked`：基础设施等阻碍，清理并回退本轮，summary 给出可操作原因。

报告路径和 changed_files 使用仓库相对路径。证据路径使用绝对路径。只返回 schema 中声明的 JSON；不以任意 PASS 文本代替结构化结果。
