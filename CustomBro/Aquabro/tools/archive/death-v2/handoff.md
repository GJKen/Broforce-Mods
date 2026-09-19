# 海王普通死亡姿势 v2

> 2026-09-16 用户已确认绘画效果；源稿已纳入关键文件并接入身体图集 4–5，DLL 与贴图已构建部署。

2026-09-16 初始交付为独立素材；当前源稿已经用户确认并接入游戏图集，DLL 与贴图已构建部署。素材为 32×32、两个状态姿势，不是循环动画，不包含受伤倒地、起身、碎尸、穿刺或寄生死亡；代码和技能资源保持不变。

## 参考与制作

- 姿势：`Broforce_src/GameAssets/hero/RAMBO_anim_1024x512.png` 身体 4、5 格（零起算，32 列）。保持原版格内坐标、皮肤像素、头颈连接、肢体比例和接地位置，不旋转拼贴其他动作。
- 外观：`tools/海王_Aseprite关键文件/01_动作主稿/haiwang_trident_held-v2.aseprite` 最新主稿的金发、面部肤色、金色鳞甲与绿色裤靴配色；沿用本对话确认的原姿势逐帧换装方式。
- 通过 Aseprite MCP `run_lua_script`，按逐帧检查的材质与部位区域绘制分层。未使用 Python 生成或改造角色像素。没有编辑“海王_Aseprite关键文件”只读目录。
- 第 1 帧去掉外飘头带末端 4 个像素，其余原版占用轮廓保留；第 2 帧保持全部原版占用轮廓。头带穿过头部的部分替换为金发，保留颈部及脸部原位置。

## 帧映射与状态切换

已核对 `C:\Users\5700G\AppData\Local\Temp\haiwang-traversal-v2\TestVanDammeAnim.cs`：`ChangeFrame()`、`AnimateDeath()`（5150 行）、`AnimateActualDeath()`（5167 行）、`AnimateFallingDeath()`（5174 行）、`Death()`（9876 行），以及当前 `src/Aquabro.cs` 的 `CanUseTrident()`、`RenderTrident()`、`LateUpdate()`。

| 源稿帧 | 身体格 | 标签 | 取帧条件 | 武器行为 |
| --- | --- | --- | --- | --- |
| 1 | 4 | 空中死亡（状态姿势） | Dead 分支，排除寄生优先分支后，Y > groundHeight + 0.2 且 impaledByTransform == null | AnimateFallingDeath 只取身体格，不调用武器开关 |
| 2 | 5 | 死亡倒地（状态姿势） | 普通死亡落地后，Y ≤ groundHeight + 0.2 | AnimateActualDeath 取身体格后明确 DeactivateGun |

`AnimateDeath()` 的完整顺序：设置 `frameRate=0.0334f`；如果寄生且寄生帧有效，进入寄生动画；否则判断上述高度和穿刺条件，选择 4 或 5。代码的 else 也包含 impaledByTransform 非空的情况，但本次不制作或验收穿刺表现。

这两个方法不按 frame 计数循环。`33.4 ms` 是死亡动画检查步长，不是“4 停留一拍、5 停留一拍”。空中姿势持续到状态条件改变，倒地姿势持续到后续尸体处理或状态变化；若尸体重新腾空，也可能重新选择 4。源稿每帧 1000 ms 仅为编辑器占位，没有游戏时长含义；分别设置单帧中文标签，没有跨两帧的循环标签，不提供循环 GIF。

## 武器显隐与手臂拆分

不能只看到空中方法没有 DeactivateGun 就认定空中必然持武器，也不能认定该方法总会隐藏武器。它本身保留进入时的武器激活状态。

本机普通死亡调用链中，`TestVanDammeAnim.Death()` 在调用 `base.Death(...)`、`ChangeFrame()` 之前已经执行 `DeactivateGun()`（9942 行）。`ChangeFrame()` 的常规激活条件包含 health > 0，死亡后不会由该入口恢复武器。Aquabro 的 `CanUseTrident()` 同样要求 health > 0；其死亡期间 `LateUpdate()` 取消攻击状态，`RenderTrident()` 不会在普通死亡路径重新激活三叉戟。因此本次正常死亡两姿势的素材按无武器显示制作，这是调用链核对结果，而非对两个动画方法的默认假设。

六层均可编辑：

- 后侧手臂：空中姿势保留向上失去支撑的后臂（11 像素）；倒地姿势按原版被躯干遮住，该帧此层为空，不额外画一条暴露的手臂。
- 绿色裤靴：保留原版双腿与靴底轮廓。
- 金色鳞甲与颈部：衣服换装、颈部肤色保留。
- 金发与脸部：按横卧头部方向设置金发明暗，脸部原像素位置不变。
- 前侧手臂：空中弯臂、倒地贴地前臂均包含在身体源稿中，分别为 19、18 像素。
- 金色三叉戟（空手留空）：两帧均为零像素。没有把手臂留给被隐藏的武器图集，也没有在身体层和武器层重复绘制手臂。

若以后通过其他模组或非标准入口直接设置 Dead、手动激活武器，空中方法本身不会强制隐藏，可能发生武器与本稿手臂重叠。这不是已验证的普通 Death 路径；本轮不为此改代码或另制武器格，后续接入时应单独确认。

## 预览与检查

源稿保存后重新从磁盘打开，再由 Aseprite 导出对照图。PowerShell/System.Drawing 仅拼图和添加文字；放大逐帧图的每个角色像素已回读核对，和最终源稿一致。

- `frames.png`：带源稿帧号、身体格号的 8 倍放大逐帧图。
- `rambro-compare.png`：上排海王、下排同格 Rambro。
- `mirror.png`：上排海王、下排水平镜像，供左右方向检查。
- `checks.json`：分层像素数量、轮廓、透明度、配色、预览一致性及既有文件保护检查。

两帧都没有原版轮廓外新增像素，原皮肤像素变化为零；均为单个八邻域连通主体，无封闭透明孔洞、半透明、边缘裁切或画布外像素。所有可见颜色均来自当前 held-v2。坐标边界分别为 (7,16)–(25,30)、(7,22)–(25,30)，没有将横卧姿势强行对齐站姿脚底。

检查了 tools、src、_Mod 下既有 211 个文件的 SHA-256，全部保持原样，包括水墙、翻滚、蹬墙翻转、其他已完成动作及只读关键文件副本。

## 全部新增文件

- `tools/海王_Aseprite关键文件/01_动作主稿/haiwang_trident_death-v2.aseprite`
- `tools/death-v2/frames.png`
- `tools/death-v2/rambro-compare.png`
- `tools/death-v2/mirror.png`
- `tools/death-v2/checks.json`
- `tools/death-v2/handoff.md`

没有修改既有文件。源稿 SHA-256 保存在 checks.json。制作脚本与 MCP 调用日志留在系统临时目录 `C:\Users\5700G\AppData\Local\Temp\haiwang-death-v2-20260916\`。

## 接入后的待实测项目

用户已确认外观与死亡姿势，素材像素检查及图集逐像素检查通过。普通死亡入口到空中、落地及左右朝向的实际表现仍需游戏内验证；非标准死亡入口的武器激活状态需要单独检查。
