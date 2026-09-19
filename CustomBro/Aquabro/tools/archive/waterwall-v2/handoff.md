# 海王水墙特技施放 v2

> 2026-09-16 用户已确认绘画效果；源稿已纳入关键文件并接入身体图集 145–152，DLL 与贴图已构建部署。

2026-09-16 初始交付仅包含独立素材；当前源稿已经用户确认并接入游戏图集，DLL 与贴图已构建部署。技能特效、伤害、弹药和触发逻辑保持不变。

## 源稿与绘制方式

- `../../海王_Aseprite关键文件/01_动作主稿/haiwang_trident_waterwall-v2.aseprite`：32×32，8 帧，对应身体 145–152。
- 姿势原稿：`Broforce_src/GameAssets/hero/RAMBO_anim_1024x512.png` 的 145–152 格（零起算，32 列）。直接使用对应动作的原始像素位置，未复用翻滚或蹬墙翻转。
- 外观主稿：`../../海王_Aseprite关键文件/01_动作主稿/haiwang_trident_held-v2.aseprite`；衔接参考：`../../海王_Aseprite关键文件/01_动作主稿/haiwang_trident_run-v3.aseprite` 第 1 帧、`../../海王_Aseprite关键文件/01_动作主稿/haiwang_trident_jump-v2.aseprite` 第 18 帧。
- 通过 Aseprite MCP 的 `run_lua_script`，按逐帧检查的头发、面部、颈部和双臂区域分层换装。保持原版肩肘、手臂长度、脸部皮肤、身体轮廓、腿脚和重心移动，仅将材质换为金发、金色鳞甲、绿色裤靴，去掉外飘红头带。原版这 8 格没有独立手雷像素；抬臂、压掌、收势均为空手。
- 不使用 Python 生成或修改角色像素。图中文字排版通过 PowerShell/System.Drawing 完成，只拼接 Aseprite 已导出的图像。
- 六个可编辑中文图层：后侧手臂、绿色裤靴、金色鳞甲与颈部、金发与脸部、前侧手臂、金色三叉戟（空手留空）。三叉戟层所有帧均为零像素。
- 标签：水墙特技施放、起手聚势、抬臂引水、施放水墙（触发）、收势、衔接占位（游戏立即切回）。

## 已核对的代码与运行时序

反编译参考为 `C:\Users\5700G\AppData\Local\Temp\haiwang-traversal-v2\TestVanDammeAnim.cs` 和同目录 `BroMakerParameters.cs`。本机对应 DLL 的 SHA-256 与原反编译记录一致：

- Assembly-CSharp.dll：`6059336EE9E93A9BCD46E89D259687AE179CE32E0DDBEE78DD7B0ACF8B0FF3A9`
- BroMakerLib.dll：`9C1F1A2CF6503B1DFB976497C9A8C2AD5EFAC6E6C3746470343272C7ECA6E636`

`_Mod/Aquabro.json` 设置 `BetterAnimation=true`；`Parameters.BetterAnimation()` 明确设置 `useNewThrowingFrames=true`。Aquabro 没有覆盖 `AnimateSpecial()` 或将该开关关闭。因此核对的是新版 145–152 分支，而非旧版 16–20 分支。

`AnimateSpecial()` 先隐藏武器、清除身体偏移，设置 `frameRate=0.0334f`，取列 `17+Clamp(frame,0,7)`、纵向参数 `spritePixelHeight*5`。按当前图集格号换算为 `145+frame`。

| 源稿帧 | 身体格 | 内部 frame | 动作 | 运行行为 |
| --- | --- | --- | --- | --- |
| 1 | 145 | 0 | 起手 | 按键只清零计数，不主动取帧；是否显示取决于递增前是否另有 ChangeFrame |
| 2 | 146 | 1 | 撤臂 | 普通计时路径的首次显示帧 |
| 3 | 147 | 2 | 聚势 | 33.4 ms 标称步长 |
| 4 | 148 | 3 | 抬臂引水 | 33.4 ms 标称步长 |
| 5 | 149 | 4 | 施放水墙 | 先选中身体 149，再调用 Aquabro.UseSpecial() |
| 6 | 150 | 5 | 压掌收势 | 33.4 ms 标称步长 |
| 7 | 151 | 6 | 收臂 | 最后正常停留的施放帧 |
| 8 | 152 | 7 | 衔接占位 | 同一次调用中立即重置状态并 ChangeFrame，无独立完整停留 |

`PressSpecial()` 设置 `usingSpecial=true`、`frame=0` 和面向，没有重置 `counter` 或主动调用 `ChangeFrame()`。常规更新在 `counter>frameRate` 时先 `IncreaseFrame()`，再 `ChangeFrame()`。因此没有其他状态干预时，实际可见序列通常为 **146→147→148→149→150→151→站姿/跑步/跳跃等当前常规动作**。不能把完整素材表 145→152 误当作保证播放八拍的序列。

从首次显示 146 起，149 的标称触发偏移为 **100.2 ms**，在 **200.4 ms** 后退出施放；触发后到退出为 **100.2 ms**。这些数值按 33.4 ms 步长计算，实际受游戏更新采样、既有 counter、动作切换和额外 ChangeFrame 调用影响，不能宣称按下按键后固定 133.6 ms 触发。若递增前实际显示了 145，则多一次起始取帧；本轮未做运行时采样。

`Aquabro.UseSpecial()` 先 `CancelTrident()`；有弹药时减 1，再调用 `RisingWave.CallMethod(this, risingWaveTexture)`。无弹药时仅提示弹药并调用 `ActivateGun()`，不生成水墙。以上代码全部保持原样。`CanUseTrident()` 排除了 usingSpecial；主体含完整双臂，避免隐藏武器后缺臂。

内部 frame≥7 时先写 152，随后清零 frame、清除 usingSpecial/usingPockettedSpecial，恢复武器，再调用 `ChangeFrame()`。收招验收应重点看 **151 接当前动作**，不能只检查 152 接站姿。

## 预览说明

所有本动作预览均在最终源稿保存并从磁盘重新打开之后导出，使用最近邻放大。未从临时未保存画面导出。

- `normal.gif`：完整 8 帧素材的正常速度审阅，192×192；每帧源稿 33 ms（Aseprite 整毫秒精度，游戏为 33.4 ms）。GIF 10 ms 精度，时长为 30/40/30/30/40/30/30/30 ms，总长 260 ms。**包含供检查的 145、152，不代表运行时保证显示它们。**
- `slow.gif`：完整 8 帧的四倍慢速审阅，130/130/140/130/130/130/130/140 ms，总长 1060 ms。
- `runtime-segment.gif`：通常计时路径的 146–151 六帧片段，30/40/30/30/40/30 ms，总长 200 ms。为便于查看而循环，不包含按键前等待、152 停留或技能结束后的站跑动作。
- `frames.png`：带源稿帧号、身体格号、动作阶段的逐帧图。
- `rambro-compare.png`：逐帧海王/Rambro 对照。
- `mirror.png`：左右镜像检查图，镜像映射 x→31−x。
- `transition.png`：从左到右为本稿第 7、8 帧，held-v2 第 1 帧，run-v3 第 1 帧，jump-v2 第 18 帧。后三格取自既有最终源稿，仅作衔接参考。
- `sheet.png` / `sheet.json`：原尺寸横排素材及帧信息，供独立检查，未写入游戏图集。

## 检查结果与接入后待实测事项

- 8 帧均为单个八邻域连通主体，无内部封闭透明孔洞，无画布边缘像素，无裁切、半透明或画布外像素。
- 对比 Rambro：轮廓外新增像素为 0，原皮肤像素变化为 0；仅删去外飘红头带像素。头颈、肩肘、双臂遮挡沿用原版像素连接，不拉长或放大部位。
- 全部角色颜色都能在当前 held-v2 可见像素中找到。正常/慢速 GIF、原尺寸横排图、镜像图逐像素回读核对通过。
- 起始基线覆盖 tools、src、_Mod 下既有 199 个文件，SHA-256 全部未变；包括已完成翻滚、蹬墙翻转及用户只读关键文件副本。
- 用户已确认召唤动作与外观，素材技术检查及图集逐像素检查通过。运行时起始取帧、技能效果出现和 151 接站跑的表现尚未进行游戏内验证。

## 本轮全部新增文件

没有修改既有文件。新增文件为：

- `tools/海王_Aseprite关键文件/01_动作主稿/haiwang_trident_waterwall-v2.aseprite`
- `tools/waterwall-v2/handoff.md`
- `tools/waterwall-v2/normal.gif`
- `tools/waterwall-v2/slow.gif`
- `tools/waterwall-v2/runtime-segment.gif`
- `tools/waterwall-v2/frames.png`
- `tools/waterwall-v2/rambro-compare.png`
- `tools/waterwall-v2/mirror.png`
- `tools/waterwall-v2/transition.png`
- `tools/waterwall-v2/sheet.png`
- `tools/waterwall-v2/sheet.json`
- `tools/waterwall-v2/checks.json`

最终源稿 SHA-256：`5F02FE211D168907E9502A52D3D56336EC75EA0BE8EA898FF942A099E820743F`。

制作脚本、MCP 调用日志和详细检查中间记录保存在系统临时目录 `C:\Users\5700G\AppData\Local\Temp\haiwang-waterwall-v2-20260916\`，没有写入只读关键文件目录。
