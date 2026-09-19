# 海王地面秀肌肉 352–375 交接记录

制作日期：2026-09-17；最近同步：2026-09-18

## 交付文件

- [正式源稿](../../海王_Aseprite关键文件/01_动作主稿/haiwang_trident_flex-v2.aseprite)：32×32、24 帧、六个可编辑图层。
- [归档前根目录副本](../root-working-sources-20260918/haiwang_trident_flex-v2.aseprite)：历史工作副本，与正式源稿 SHA-256 一致。
- [带帧号放大预览](flex/frames.png)：从最终保存并重新打开的关键源稿导出，4×6 排列；标题为源稿帧/游戏格。
- [逐帧预览目录](flex)：每帧一个 8 倍 nearest-neighbour PNG。
- [逐帧审计结果](flex-audit.json)：Aseprite 重新打开后执行的只读审计结果。
- [制作脚本](create_ground_flex.lua)：通过 Aseprite Lua 逐帧换装生成源稿。
- [同步脚本](../../gestures-v2/sync_ground_flex.lua)：读取正式关键源稿，将其写入正式身体图集并重新导出 `sprite.png`。
- [审计脚本](../../gestures-v2/audit_ground_flex.lua)：重开源稿、图集和 PNG 后逐像素核对。

## 同步与部署注意

游戏运行时不会读取 `.aseprite` 源稿，只读取 `_Mod/sprite.png`。修改正式关键源稿后，必须先运行 `sync_ground_flex.lua`，再运行 `BuildBro.ps1`，才能让发型等像素修改进入游戏。

2026-09-18 曾发现同步脚本读取了临时目录中的旧源稿，导致正式源稿的发型微调没有进入 `352–375`。脚本现已固定读取 `海王_Aseprite关键文件/01_动作主稿/haiwang_trident_flex-v2.aseprite`；本次已重新同步、构建并部署。

## 帧映射

| 源稿帧 | 内部 frame | 身体图集格 | 原版来源 | 保存后时长 |
| ---: | ---: | ---: | ---: | ---: |
| 1–24 | 0–23 | 352–375 | `RAMBO_anim_1024x512.png` 同格 | 66 ms；停驻帧见下表 |

制作严格使用原版身体图集第 11 行的连续 24 格。未使用空中秀肌肉 402–406。

停驻帧按源码实测保存值记录：

- 内部 frame 7、12、19，即身体格 359、364、371：240 ms。
- 内部 frame 8、18，即身体格 360、370：100 ms，并由游戏代码触发 `PlayFlexSound` 与 `TriggerFlexEvent`。
- 其他帧：Aseprite 重新保存后为 66 ms，对应源码约 66.7 ms。

普通地面手势开始时隐藏三叉戟。第六层“武器或道具（空手留空）”保留为可编辑空层；24 帧武器像素均为 0。普通非阻塞手势在运行时 `frame > 54` 后才允许退出，实际还受 `blockMovementForGesture`、`Alpha1` 和 `buttonGesture` 条件影响。

## 分层

1. 后侧手臂
2. 裤子与靴子
3. 躯干服装与鳞甲
4. 头发与脸部
5. 前侧手臂
6. 武器或道具（空手留空）

动作底稿来自原版对应格，保留原版四肢、头部、身体比例、遮挡、接触点和帧间位移。头带红色像素按海王外观要求移除并换为金发；原版其他非透明轮廓全部保留其位置并映射到海王材质。躯干使用主稿同方向的纵向金色明暗纹理，裤靴使用主稿绿色配色。

## 已更新文件

- `tools/海王_Aseprite关键文件/01_动作主稿/haiwang_trident_flex-v2.aseprite`
- `tools/archive/root-working-sources-20260918/haiwang_trident_flex-v2.aseprite`
- `tools/海王_Aseprite关键文件/03_游戏图集/haiwang_trident_body_atlas.aseprite`
- `tools/archive/root-working-sources-20260918/haiwang_trident_body_atlas.aseprite`
- `_Mod/sprite.png`
- `tools/rebuild_key_sources.lua`：加入 `bodyOnly('flex-v2',24,352)`。
- `tools/海王_Aseprite关键文件/Readme.md`：登记 21 个源稿和本动作。

## 审计结果（2026-09-17 版本）

最终审计从关键源稿重新打开，并核对工作源稿、两个身体图集和 `_Mod/sprite.png`：

- 24 帧、32×32、六层、所有 cels 在画布原点。
- 身体格 352–375 与最终源稿逐像素一致。
- 两份图集彼此一致，且与 `sprite.png` 一致；所有尺寸为 1024×1024。
- 武器像素 0；半透明像素 0；新增原版透明区像素 0。
- 除允许移除的原版红色头带外，移除原版不透明像素 0。
- 图集逐格不一致像素 0。

待游戏内确认：实际手势开始、触发声音和 `TriggerFlexEvent` 的运行时表现，以及非阻塞退出时收招帧与当前角色站姿的衔接。素材本身没有待确认的帧或纹理差异。
