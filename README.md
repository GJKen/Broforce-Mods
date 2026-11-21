# Broforce-Mods

> [English](README.en.md)

本仓库收录 Broforce 模组源码及开发工具，用于留档和备份。

本仓库的新增功能与维护主要在 AI 辅助下完成。

各项目的安装、构建和使用方式见下方链接的项目文档。

## 致谢

- [BroforceMods Wiki](https://github.com/alexneargarder/BroforceMods)
- [Gorzontrok](https://github.com/Gorzontrok)

## 项目

- [`CustomMapMultiplayer`](CustomMapMultiplayer/README.md)：第三方 Workshop 地图联机、FRP Direct。
- [`RocketLibMultiplayer`](RocketLibMultiplayer/README.md)：RocketLib 公共联机层源码与构建项目，作者入口见[联机接入说明](RocketLibMultiplayer/docs/networking.md)。
- [`BroMakerMultiplayer`](BroMakerMultiplayer/README.md)：BroMaker 公共框架联机接入；[角色作者说明](BroMakerMultiplayer/docs/networking.md)。消费者专属机制由作者适配。
- [`BroforceBugFix`](BroforceBugFix/readme.md)：独立的原版游戏缺陷修复插件，修复爆炸箱递归、加入房间残留暂停、蛇/抱脸虫脱离空目标、异形卵及 Listener 换关清理和沙尘暴切图初始化异常；各项修复的验收范围见项目文档。
- [`UnityInspectorMod`](UnityInspectorMod/README.md)：为 Broforce 提供 TCP 检查与运行时调试接口，供 `unity-inspector-mcp` 连接。
- [`Broforce_src`](Broforce_src/unity-inspector-mcp/README.md)：Unity Inspector MCP 工具；工具的安装、配置和使用方式见 [`unity-inspector-mcp/README.md`](Broforce_src/unity-inspector-mcp/README.md)。
