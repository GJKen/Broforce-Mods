# ISSUES-2026-10-26 死亡后主动 AFK 与返回状态

## 状态

已修复并完成本机房主、内网加入方双端回归：死亡后可主动 AFK，同一关返回保持死亡、0 生命和原角色死亡头像；存活与死亡主动 AFK 使用统一的本地化标题提示。成功加入后清理请求缓存，双方各快速连续返回 4 次均无需等待 10 秒超时。新版已构建并部署双端，具体构建、过程和异常边界见下文。当前行为说明见 [AFK 文档](../docs/AFK.md)。

## 问题与根因

- 旧实现把 `Player.idleTimer` 设为超过 35 秒，但原生 `Player.Update` 只在角色存活时检查闲置计时。角色死亡、生命耗尽或 character 消失时，请求返回成功而槽位未退出。
- 首轮修复让死亡角色直接调用原生 `HeroController.Dropout` / `DropoutRPC`，并显示标题 `AFK`；存活角色仍显示原生计时分支的提示，两者文案不一致且死亡提示未按 Mod 语言选择。
- 已有返回流程在 `Player.Start` 前后将生命补为 1，并调用 `RespawnBro(false)`。首轮验收把死亡 AFK 返回后复活错误地视为通过，用户指出可反复利用 AFK 无限复活；该结果不能作为正确行为保留。
- 死亡返回禁止生成角色后，不再经过 `RegisterHeroToPlayer` / `SetHeroType`；新建 Player 的 `heroType=None`、HUD 默认 `Rambro`，只调用 `SetAvatarDead` 会显示错误角色的死亡头像。现场保存的英雄是 `AshBrolliams`，本机和内网却都显示默认 Rambro。

## 修复范围

- 死亡或没有 character 时直接使用原生掉线生命周期，保留目标所有权、多人限制和跳过自动重入的既有逻辑。
- 存活与死亡主动 AFK 共用原生标题 UI 和本地化文案。跟随 Mod 语言设置，中文为“已进入旁观AFK模式”，英文为“Entered AFK spectator mode”。
- 主动 AFK 前记录已经死亡的本地控制器。同一关显式返回时不补生命，不触发角色生成，保持死亡并等待正常救援或下一回合。场景加载或会话清理时移除记录，避免影响下一回合出生。
- 保留存活 AFK 返回、普通掉线恢复、原英雄与控制器绑定和单人房间限制。
- 死亡返回在设置死亡表情前，用已保存英雄类型调用原生 `SetHeroType` 恢复头像；远端新建 Player 同样使用已知英雄类型初始化 HUD。仅恢复类型与头像，不调用角色生成或补生命。
- `BoondockBros` 的第二张头像由原生角色注册后另行开启；死亡返回没有角色实例，因此使用英雄 prefab 的 `avatar1` / `avatar2` 调用原生 `GoToDoubleAvatarMode`，再把两张头像都切到死亡帧。
- `DoubleBroSeven` 的原生 `SetHeroType` 会轮换头像变体，因此双方在掉线前保存各自实际显示的头像纹理，返回时恢复，避免同一英雄返回后换脸；场景加载及会话清理时释放记录。

## 首轮实测记录

- 构建：`b310d1937f9b3fbd98a5f2a52edbaeefd37599b86ec61ac6a826e6c26b757d57`，已部署本机与内网游戏端并从运行进程核对。
- 环境：本机 Steam 房主、内网加入方；Workshop `3715087178` / `Bromandy_Ptr1` / `Test Evan2`。
- 通过：房主死亡后主动 AFK；加入方死亡后通过原生 `Menu.RunInput` 选择 AFK；房主 character 移除后通过同一菜单选择 AFK。双端只移除目标槽位，玩家数同步为 1，另一方存活，关卡未结束，暂停状态恢复 `UnPaused`。
- 通过：主动 AFK 后不自动重入，存活角色主动 AFK、只剩一名参与者时拒绝 AFK。
- 需修正：原生 `AddLocalPlayer` 返回后补为 1 条生命并生成角色，此行为允许死亡玩家无限复活。
- 证据：房主会话 `auto-20261025-193356-065-e2107d86`，加入方会话 `auto-20261025-193514-922-1c563c89`。日志有主动 AFK 跳过自动重入及返回时补生命的记录。时间戳使用 UTC，与本地日期存在时区差异。

## 本轮验收要求

- 房主和加入方分别覆盖死亡、AFK、返回；同一关保持死亡、无角色、无新增生命，重复操作不能复活。
- 存活与死亡 AFK 的中英文提示分别一致；检查原生标题文本和显示状态。
- 返回后的死亡玩家仍参与游戏，可由正常救援恢复；重开或下一关不残留死亡 AFK 标记。
- 回归存活 AFK 返回和单人限制；结束时检查双端场景与玩家状态。

测试使用 Unity Inspector 调用原生死亡、菜单和加入接口，正式验收必须使用构建部署后的 DLL，不能以临时运行时补丁代替。

## 本轮双端验收结果

- 构建：`084a406f6d1e1ce852f54c8d1f345b01f3ae368bba3900b535c904b1a694d85d`，Release 构建成功并部署本机、5700G；运行进程均已核对为该构建。
- 环境：本机 Steam 房主、5700G 加入方；Workshop `3715087178` / `Bromandy_Ptr1` / `Test Evan2`。
- 房主死亡后 AFK 再加入，双端均为 `IsAlive=false`、`Lives=0`、`character=null`、`firstDeployment=false`，槽位仍参与游戏。重复一次后仍保持死亡，未增加生命。
- 加入方经原生 `Menu.RunInput` 选择 AFK，再通过原生 `AddLocalPlayer` 返回；双端同样保持死亡和 0 生命。重复一次后仍未复活，暂停状态正常恢复。
- 存活和死亡主动 AFK 的中文标题均为“已进入旁观AFK模式”，英文均为“Entered AFK spectator mode”；标题显示状态已核对。中文截图确认字形完整，使用原生底部标题框，没有乱码或缺字。
- 存活 AFK 返回正常出生；只剩一名参与者时请求被拒绝，不移除该玩家。
- 房主死亡返回后，经原生 `RescueBro.Free` / `HeroController.CheckRescueBros` 正常获救，恢复 1 条生命和存活角色，未被死亡记录阻止。
- 加入方再次以死亡状态 AFK 后，房主重开关卡；加入方死亡返回记录已清空，再加入正常出生。最终双端均在 `Test Evan2`、玩家数为 2、两名玩家存活且各 1 条生命、`UnPaused`。
- 为排除 Utility Mod 无限生命干扰，仅在运行时临时关闭加入方 `infiniteLives`；验收后恢复原值 `true`。语言恢复本机 `zh`、加入方 `system`，未保存测试覆盖值。
- 日志：房主 `auto-20261025-195654-233-1fe0fea8`、加入方 `auto-20261025-195749-110-27585174`，双方各有两条 `Preserved dead Workshop player after manual AFK return`，本轮会话日志无 `[ERROR]` / `[EXCEPTION]`。最新 `.log` 和 `.trace.log` 均已读取。日志时间为 UTC，本地验收日期为 2026-10-26。
- 中文显示证据：本机 Unity Inspector 截图 `screenshot_20261026_040909_917a13fd.png`。该图单独调用同一提示方法验证字形；真实存活、死亡 AFK 入口的文本另外逐项核对。

## 死亡返回头像补充验收

- 最终构建：`9a715912231ccebd29575872b944802f22fb31ad451587f7a62f2af3299b4902`，已构建、部署并重新启动双端加载。
- 房主 `TheBrocketeer` 死亡后 AFK 返回，两端 `Player.heroType` 和 HUD 类型均恢复为 `TheBrocketeer`，纹理仍为 `avatar_brocketeer`，死亡帧为 96，生命为 0，没有生成角色。
- 房主 `DoubleBroSeven` 返回前后，本机均保留 `avatar_jamesbrondconnery`，内网均保留 `avatar_jamesbrondmoore`，两端死亡帧为 96、生命为 0。原生两端选择的变体本来不同，此修复保持各端原有显示，不扩大为跨端随机变体同步。
- 房主 `BoondockBros` 死亡后返回，两端均保留原英雄类型，`secondAvatar.activeSelf=true`，两张头像的死亡帧均为 96，生命为 0。正常救援仍可恢复角色。
- 内网 `TankBro` 死亡后返回，两端类型均为 `TankBro`、纹理为 `avatar_tankbro`、死亡帧 96、生命 0、无 character；原生救援后正常复活。截图 `screenshot_20261026_043520_11e041b2.png` 已确认显示原角色死亡头像。
- 日志会话：房主 `auto-20261025-203059-709-e55a9d68`，加入方 `auto-20261025-203124-318-49519b90`。最新 `.log` / `.trace.log` 即使目录长度显示为 0 也已实际读取。
- 测试异常如实保留：定向生成、死亡及救援双胞胎角色的测试过程中，加入方出现 `BroBase.Start` / `BoondockBro.Start` 和 `BroBase.TrySpawnDrone` 空引用，不能把本轮记为无错误会话。堆栈不是头像恢复方法；重开关卡后该残留对象消失，后续观察未继续报该错误。此项未扩展修改或宣称根因已修复。
- 验收结束恢复加入方原有 Utility 无限生命设置，重开关卡后双方均为 2 人房、同一地图且角色存活。未提交或推送 Git。

## 连续死亡 AFK 返回延迟

- 用户反馈首次返回立即生效，随后快速重复则需等待一段时间。
- 已在旧版双端会话中快速复现：第一次返回 403ms 后角色已创建，但 `WorkshopLocalJoinRequests` 仍有一条控制器请求；第二次返回后角色为空、槽位未参与，请求年龄约 1031ms。
- 根因是成功加入只在首次 late-join 初始化分支清理请求；后续 `Player.Start` 不清理。主动 AFK 掉线又有意跳过该缓存的退出清理，因此下一次显式返回被重复加入保护挡到原有 10 秒超时。
- 修复为每次本地 Workshop `Player.Start` 成功后释放已完成的加入请求，而非缩短或删除尚在进行中的请求保护；保持死亡、原头像和不自动重入规则不变。
- 构建 `cbb700946327d5123def50f2cad33ab05ad793e970b50662641937427b2114f0` 已部署双端，重新启动后从两个运行进程核对构建一致。
- 本机房主 `DirtyHarry` 原生死亡后，连续执行 4 次 AFK 返回，总耗时约 2.5 秒；5700G 加入方 `TankBro` 原生死亡后，同样连续执行 4 次，总耗时约 3.2 秒。每次 AFK 后约 200ms 请求加入，约 350ms 后检查，均已恢复参与槽位且 `WorkshopLocalJoinRequests.Count=0`，不再等待 10 秒过期。
- 每次返回均为 `IsAlive=false`、`Lives=0`、`character=null`，分别保留 `DirtyHarry` / `TankBro` 类型和原头像纹理；另一端也核对到相同死亡状态与原头像，没有借 AFK 增加生命或复活。
- 本轮仅调整成功加入后的请求清理，尚未创建 Player 时的原有 10 秒重复请求保护保持不变。
- 会话日志：房主 `auto-20261025-204612-771-0557473b`，加入方 `auto-20261025-204640-766-890a1e1a`。最新 `.log` 和 `.trace.log` 均实际读取，包括内网目录长度显示为 0 的文件；各端各有 4 条死亡返回保留记录，本轮会话未发现 `[ERROR]` / `[EXCEPTION]`。
- 测试结束重开当前关卡，双端均在 `Test Evan2`、玩家数为 2、角色存活且 `UnPaused`；恢复加入方原有 Utility `infiniteLives=true`，未保存测试覆盖设置。详细记录仅放本 issue，不追加到 AFK 行为文档；未提交或推送 Git。
