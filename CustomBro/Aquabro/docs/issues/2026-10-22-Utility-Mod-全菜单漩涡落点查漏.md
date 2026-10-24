# 2026-10-22 Utility Mod 全菜单漩涡落点查漏

在同一加载版本（标识已脱敏）中，`测试场景` 中，通过 MCP 调用 Utility Mod 右键 Spawn Enemy 的实际生成入口，遍历八组 66 项。人物临时位于 `(已脱敏坐标)`，左右目标分别生成于 `(已脱敏坐标)`／`(已脱敏坐标)`，下方布置临时平地；等待四个 Unity 帧使原生 Start 完成，游戏仍暂停。仅禁止临时对象的屏外禁用，不调整血量、无敌或敌我。逐项核对生产找目标和实际 UseSpecial 创建的漩涡位置，共 132 条。

59 项双向选中；SandWorm、Boneworm、BonewormBehind、AlienWorm、LargeAlienWorm 五项未在 Map.units 登记，实际释放退至人物前方 70px。运行时方法归属与原版源码进一步确认：SandWorm 本体直接沿用 Unit.Damage 空实现，单独记录为特殊敌人；其余四项走 AlienMinibossSandWorm.Damage，属于可受伤虫类的自动定位遗漏。另一次 18 条边界核对中，仅给临时虫对象登记到名单，五项均立即可选中，随后撤销登记；两种抱脸虫发射虫正常登记并选中。DrillCarrier 原生 Start 使其先下潜至 Y=9972，本次被地面挡住，人工抬回地表后双向可选中；Villager 为 playerNum=5 且无敌，按规则排除。Terrorbot 较近的炮臂也可参与自动选取，落点不一定在机体中心。

这是暂停下的生成准备、落点与实际释放核对，没有结算伤害，没有验收自然战斗、Boss 露头／缩回、钻地车出土或联机。正式代码未在本轮查漏中修改、构建或部署；四项可受伤虫类的定位遗漏保持待修。脚本为 tests/Verify-UtilityMaelstromPlacement.cs，完整证据已归档于 本地验证归档。新增 [Utility Mod 验证指南](../UtilityMod验证指南.md)，补充逐种生成、左右、台阶／墙后、四技能与毒水对照、原版限制及结果记录方法；同步更新玩法、机制说明、测试说明与当前状态。临时对象清理后卸载现场脚本并关闭游戏。
