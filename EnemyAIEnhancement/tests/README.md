# 受控回归脚本

本目录公开三个可复用的定向脚本，需通过Unity Inspector的C#脚本执行入口加载。它们依赖正在运行的游戏、已加载的EnemyAIEnhancement与Harmony，以及执行环境提供的ScriptContext；不是可直接由dotnet test运行的单元测试。

| 脚本 | 范围 |
| --- | --- |
| [scope-authority-regression.cs](scope-authority-regression.cs) | 真实支持碰撞与障碍、近身/指挥边界、拥有端和控制条件 |
| [transport-regression.cs](transport-regression.cs) | 初始化后的真实载具预制体、武器拥有权转换与运输阶段 |
| [boss-fallback-regression.cs](boss-fallback-regression.cs) | 实际注入的原生查询签名、关闭/失去增强资格后的回退与Boss动作分支 |

在离线关卡、存活玩家和满足脚本几何要求的场景运行。脚本会临时生成受控对象、修改玩家或拥有权状态、安装测试补丁，并在finally中恢复；执行后卸载脚本，核对现场已恢复，再退出游戏。不要把这些受控调用写成自然联机战斗验收。

这些脚本来自实现阶段的定向验收，发布整理未重新运行它们。近期精简的等价检查和双端验证范围见 [验证说明](../docs/VALIDATION.md)。其它观察脚本、逐批探针、基线DLL和地图副本仅本地保留，不作为本目录的公开依赖。
