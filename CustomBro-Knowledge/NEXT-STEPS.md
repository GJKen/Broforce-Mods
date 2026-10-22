# 维护入口

本页取代 2026-09 的开发交接记录。旧记录包含已废弃的 Aquabro 技能方案、已删除的角色工程、个人机器路径和过期测试待办，不再作为当前状态依据。

## 当前项目

Aquabro 当前实现与待验证事项以 [角色 README](../CustomBro/Aquabro/README.md)、[当前实际应用状态](../CustomBro/Aquabro/当前角色实际应用状态.md) 和 [测试说明](../CustomBro/Aquabro/tests/Readme.md) 为准。构建资源使用项目内 `BuildAssets.ps1` 与本机 `LocalBuild.json` 配置；不要套用旧的 `BuildBro.ps1` / `MakeSprites.py` 交接说明。

## 开始维护前

1. 查看本知识库 [目录](README.md)，按任务选读开发、资源或排错文档。
2. 查看仓库根目录 `AGENTS.md`、目标角色 README 和测试脚本，确认当前约束。
3. 本地路径从模板复制后自行配置；不要依赖旧文档里的机器路径。
4. 修改后按目标项目已有的回归测试和运行验证步骤检查，并更新对应项目文档。

源码参考是带日期的快照，版本及运行状态见各源码地图中的核对范围。游戏源码反编译材料的边界见 `Broforce_src/Broforce-Source/!SOURCE_ANALYSIS.md`。
