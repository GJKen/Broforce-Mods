# Enemy AI Enhancement

Broforce敌人AI增强插件，版本 **0.19.4**。改善敌人的目标记忆、安全追击、站位、兵种战术，以及载具、蠕虫和首领的决策机会。武器、伤害、剧情及已提交的攻击阶段继续使用原生流程。

## 安装

需要Steam版Broforce和Unity Mod Manager（UMM）。

在r2modman中选择Broforce，为所用配置安装UMM，然后通过 Settings → Profile → Import local mod 导入本ZIP，使用 Start modded 启动。

手动安装时，先退出游戏，将ZIP内的 `UMM/Mods/EnemyAIEnhancement` 整个目录复制到所用配置的 `UMM/Mods/` 下。DLL和Info.json应位于同一层，不要把ZIP外层目录一并放入Mods。

更新时保留已有 `Settings.xml`。从旧名称EnemyAIMultiplayer升级时移除旧DLL，只启用一份EnemyAIEnhancement，无需替换游戏程序集。

## 设置与联机

UMM面板提供目标记忆、追击和手雷避险三个独立开关。默认记忆最后可见位置6秒，追击边界为出生锚点左右16格。

联机各端安装相同版本并使用一致设置。增强决策只由实际拥有端执行；Workshop世界中的部分特殊召唤、接触伤害和效果回放需要配套CustomMapMultiplayer修复。当前组合验证为EAI0.19.4、CMM0.5.6/世界协议25；CMM不包含在本ZIP中。

## 范围与限制

当前实现覆盖已检查的66项敌人菜单中的59项，其余7项不适用。实现覆盖不等于每项都通过全部方向和自然阶段的联机验证；Mega榴弹阶段采用受控准备，自然阶段入口、部分晚加入、混装及长会话仍有未验范围。

完整使用说明与验证边界见 [GitHub项目说明](https://github.com/GJKen/Broforce-Mods/blob/main/EnemyAIEnhancement/README.md)，反馈问题请使用 [仓库Issues](https://github.com/GJKen/Broforce-Mods/issues)。
