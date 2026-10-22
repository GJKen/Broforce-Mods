# Broforce 自定义角色开发知识库

这里整理 BroMaker / RocketLib 开发参考、角色源码案例和本仓库的操作记录。Wiki 转录用于查字段与概念；遇到版本差异时，以正在使用的 DLL 源码、游戏反编译源码和目标项目配置为准。源码分析环境说明见 [Broforce 源码分析指南](../Broforce_src/Broforce-Source/!SOURCE_ANALYSIS.md)。

## 按任务阅读

| 主题 | 文档 |
|---|---|
| 环境、项目和构建 | [01 开发环境与项目搭建](01-开发环境与项目搭建.md) |
| 最小角色、JSON 与 mod 清单 | [02 最小可运行角色与文件结构](02-最小可运行角色与文件结构.md)、[06 JSON 参数与变量参考](06-JSON参数与变量参考.md) |
| 生命周期、动画、精灵和音频 | [03 类结构与核心方法重写](03-类结构与核心方法重写.md)、[04 精灵与音频加载](04-精灵与音频加载.md) |
| 弹射物、Boss 伤害 | [05 自定义弹射物](05-自定义弹射物.md)、[15 Boss 伤害判定与攻击覆盖](15-Boss伤害判定与攻击覆盖.md) |
| 构建、部署和验证边界 | [10 构建、部署与运行验证](10-构建部署与运行验证.md) |
| 排错与运行时验证 | [07 调试与常见问题](07-调试与常见问题.md) |
| 贴图导出与编辑 | [11 导出官方贴图与资源包](11-导出官方贴图与资源包.md)、[12 用 Aseprite 画角色贴图](12-用Aseprite画角色贴图.md) |
| 参考角色源码 | [08 VojkanSE87](08-VojkanSE87源码解析.md)、[09 alexneargarder 六 bro](09-alexneargarder六bro源码地图.md)、[14 Gorzontrok 三 bro](14-Gorzontrok三bro源码地图.md) |
| Unity Inspector MCP 运行时切换角色 | [13 用 Unity Inspector MCP 切换游戏角色](13-用Unity Inspector MCP切换游戏角色.md) |

## 当前项目入口

Aquabro 的行为、构建方式和验证状态以 [角色 README](../CustomBro/Aquabro/README.md)、[机制实现细节](../CustomBro/Aquabro/docs/机制实现细节.md)、[当前实际应用状态](../CustomBro/Aquabro/当前角色实际应用状态.md) 及 [测试说明](../CustomBro/Aquabro/tests/Readme.md) 为准。本知识库只记录可复用的通用做法，不复制会频繁变化的角色规格。

模板位于 `templates/`。`BuildBro.ps1` 适用于简单脚本构建示例；具体角色仓库若有自己的构建脚本和资源流水线，应以项目内说明为准。`Broforce_src/GameAssets/` 的导出数量会随游戏版本和筛选方式变化，`index.csv` 才是当前导出清单。

## 参考源码地图的范围

08、09、14 是特定本地源码快照的阅读地图，不代表作者仓库或线上 mod 的最新状态。阅读时以文档标注的核对日期为界；上游仓库更新后，行号、版本和文件内容都可能变化。不要把快照里的绝对路径当成通用安装路径。
