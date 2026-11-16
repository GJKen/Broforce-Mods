# 当前交接状态

更新：2026-11-16。工作区：`D:\Study\C#\Broforce-Mods`。

## 当前构建

- CustomMapMultiplayer 当前仓库发行包为世界状态同步协议 13，`buildHash=e6d50e9a9869b6c51bd83c3b89999a12c258aa6f2604b9e7c0001222373279c3`，DLL 487,424 字节，SHA-256 为 `A0237264AA08BF28ECD1C4CCB1F97DF6C27E512B4C6ECC7F4379D5B35C99FE08`；仓库 DLL 与 ZIP 中的 DLL 一致。协议 13 补充燃烧时长、燃烧伤害、油污和油桶引爆计时同步，各端须使用同一构建。
- 上述为仓库产物信息；两端当前部署、实际加载及验收范围须按现场和各问题记录核对。此前协议 12 构建的长矛双向定向复测与用户确认见[长矛同步记录](../issues/ISSUES-2026-11-15-长矛60Hz同步整合与结束特效.md)。
- 三类车辆的 10 发敌方弹体在加入方通过爆炸表现复测。村民近战救援和早到快照分支短测通过；动态跟随、完整车辆 AI 战斗及长期联机仍未覆盖。
- 装甲、盾兵和多个 Boss 的实际样本及边界见[命中反馈记录](../issues/ISSUES-2026-11-13-箱内道具释放分歧与命中反馈延迟.md)。离线检查、部署和脚本样本不代表完整 Boss 战已验收。
- 原版蛇脱离空目标异常已由 BroforceBugFix 独立修复；最初目标引用丢失原因仍未确认。CMM 不包含该补丁。

## 当前状态与待办

- 上次实测收尾时，两端游戏已关闭，探针和测试实体已清理。后续操作前仍需重新读取现场状态。
- 村民动态跟随逐帧一致性、完整 Boss 战、异常网络及多加入方仍待验收；具体边界以各[问题记录](../issues/README.md)为准。
- 以下远端临时文件及调试截图的删除曾被自动审批拒绝，原因为 `blocked by policy`。六个文件仍在远端；保留准确路径供后续获授权的清理任务核对，不把清理记为完成。

远端根目录：`\\5700G\Epan\Games\Broforce Mods\Broforce\profiles\Broforce\UMM\Mods\`

- `GJKen-CustomMapMultiplayer\CustomMapMultiplayer\BossWaitForExit.ps1`
- `GJKen-CustomMapMultiplayer\CustomMapMultiplayer\BossExited.txt`（已核对内容为 `9324`）
- `alexneargarder-UnityInspectorMod\Screenshots\screenshot_20261114_070135_ae393f3b.png`
- `alexneargarder-UnityInspectorMod\Screenshots\screenshot_20261114_070225_7c11fafd.png`
- `alexneargarder-UnityInspectorMod\Screenshots\screenshot_20261114_070653_e7d30088.png`
- `alexneargarder-UnityInspectorMod\Screenshots\screenshot_20261114_071405_8ae523bd.png`
