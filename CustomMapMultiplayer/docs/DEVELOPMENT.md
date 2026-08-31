# Custom Map Multiplayer：开发文档索引

安装、设置和日常使用见[根目录 README](../README.md)。本文只作为开发文档入口和项目级约定；功能实现、测试方法、诊断日志和构建细节按专题拆分。单轮测试、旧构建和失败方案统一保留在 [issues 索引](../issues/README.md)。

## AI 阅读约束
按任务需求读取不同的文档入口处理任务

## 阅读入口

| 需要了解的内容 | 文档 |
| --- | --- |
| 项目基线、整体架构、源码职责和设计边界 | [架构与代码职责](ARCHITECTURE.md) |
| Workshop 地图、晚加入/重入、角色恢复、道具和实体同步 | [Workshop 与游戏状态](WORKSHOP.md) |
| FRP Direct、Steam/FRP 在线名单和返回大厅流程 | [网络与房间](NETWORKING.md) |
| 自动 AFK、主动 AFK 和相关生命周期 | [AFK 行为](AFK.md) |
| 测试基线、证据要求、MCP 观测、专项验收和当前限制 | [测试与验收](TESTING.md) |
| 日志目录、分类、预设、性能和日志约束 | [诊断日志](DIAGNOSTICS.md) |
| 构建、部署、安装包和逆向参考 | [构建与部署](BUILD.md) |

## 当前基线

- 项目面向 Steam 版 Broforce，是 Unity Mod Manager + Harmony Mod，目标框架为 .NET Framework 3.5。
- 默认网络路径是官方 Steam Lobby/P2P；`FRP Direct` 默认关闭，启用后接管房间、PID 和游戏 RPC，Steam 仍负责 Workshop 内容下载。
- Workshop 双端进入、过场晚加入、FRP 公网 UDP 双端游玩、在线玩家名、正常退出后重入、Workshop 道具防重复和酸液池基础回归已有当前地图证据；官方 Steam 大厅、更多地图和更广泛的长期场景仍需单独覆盖。
- UMM 设置页的左侧导航、`Multiplayer Options`、`FRP Direct`、语言和 `Diagnostic Logs` 页面已实现；“立即进入 AFK”按钮已完成基础双端验收。
- 当前版本、分发 `buildHash`、DLL SHA-256 和用户侧限制以 [README 当前状态](../README.md#当前状态) 为唯一来源，避免多处维护。

## 按问题查找

- Workshop 地图没有加载、加入方使用了错误地图或关闭注入后仍残留状态：先看 [Workshop 与游戏状态](WORKSHOP.md)。
- 加入方没有角色、控制器错位、退出后重入失败：看 [Workshop 与游戏状态](WORKSHOP.md) 的晚加入、重入和角色恢复章节，再结合 [测试与验收](TESTING.md)。
- FRP 握手、容量、PID、心跳或在线名单问题：看 [网络与房间](NETWORKING.md)。
- AFK、掉线、自动重入或“立即进入 AFK”问题：看 [AFK 行为](AFK.md)，日志字段见 [诊断日志](DIAGNOSTICS.md)。
- 需要复现或判断“已修复/未修复”：看 [测试与验收](TESTING.md)，不要只依据单端画面或单端日志下结论。
- 需要收集日志：看 [诊断日志](DIAGNOSTICS.md)；需要重新生成 DLL：看 [构建与部署](BUILD.md)。

## 公开文档约定

- 方法级追踪不记录房间密码、Steam ID、主机名或 Workshop 作者身份。
- 构建方式、联机行为、安装方式、日志格式或兼容性变化时，同步更新对应专题文档，并在 README 仍受影响时更新 README。
- 当前版本、发布哈希等唯一来源仍是 README；专题文档只描述机制、证据和限制，不复制易过期的分发元数据。
- 提交或同步前检查上级仓库的 `git status` 和 `git diff`，不要加入 `LocalBroforcePath.props`、日志、缓存或无关文件。
- `LocalBroforcePath.props` 包含机器专用路径，不应提交。
- 未经明确要求，不运行上级仓库的自动提交、推送或更新脚本。
