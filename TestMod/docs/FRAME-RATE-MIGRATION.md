# FrameRateLimiter 整合至 TestMod

日期：2026-11-11

帧率限制和 VSync 控制已整合到 TestMod 的“帧率设置”页，与 FPS 显示及其快捷键共同维护。源码和文档已归并，旧 ZIP、DLL 和仓库根目录的独立 FrameRateLimiter 文件夹均已移除，后续只构建部署 TestMod。

## 当前入口

- [TestMod 使用与构建说明](../README.md)
- [整合实现和验收记录](../issues/ISSUES-2026-11-11-TestMod整合帧率限制.md)
- [问题索引](../issues/README.md)

帧率控制默认关闭，预设目标为 60 FPS。启用后可设置 1～1000 FPS 或“不限制”，默认关闭 VSync；关闭控制、停用和卸载 TestMod 时恢复接管前的显示设置。FPS 数字与帧率控制分别开关，旧的 FPS 显示绑定继续保存。

## 历史路径映射

下列原路径均相对于仓库根目录。历史文档中的旧提交、路径和实测结论作为历史记录保留，不追溯改写。

| 原路径 | 当前入口或处理方式 |
| --- | --- |
| `FrameRateLimiter/src/Plugin.cs` | `TestMod/src/FrameRateController.cs`、`TestMod/src/Plugin.cs`、`TestMod/src/SettingsUI.cs` |
| `FrameRateLimiter/src/FrameRateLimiterSettings.cs` | `TestMod/src/TestModSettings.cs` |
| `FrameRateLimiter/src/TooltipOverlay.cs` | VSync 中英文说明改为在“帧率设置”页直接显示，独立绘制对象移除 |
| `FrameRateLimiter/FrameRateLimiter.csproj` | `TestMod/TestMod.csproj` |
| `FrameRateLimiter/BuildAndDeploy.ps1`、`LocalBroforcePath.props.example` | 使用 `TestMod/BuildAndDeploy.ps1` 和既有本机路径配置 |
| `FrameRateLimiter/README.md` | [历史中文说明](archive/FrameRateLimiter-README.md)；当前用法见 TestMod README |
| `FrameRateLimiter/README.en.md` | [历史英文说明](archive/FrameRateLimiter-README.en.md) |
| `FrameRateLimiter/docs/BUILD.md` | [历史独立构建说明](archive/FrameRateLimiter-BUILD.md) |
| `FrameRateLimiter/docs/能否写一个给游戏限制帧数的mod-2026-09-29.md` | [原始需求对话](能否写一个给游戏限制帧数的mod-2026-09-29.md) |
| `FrameRateLimiter/issues/ISSUES-2026-09-30-帧率限制Mod实现与VSync悬浮提示修复.md` | [原问题与验收记录](../issues/archive/ISSUES-2026-09-30-帧率限制Mod实现与VSync悬浮提示修复.md) |
| `FrameRateLimiter/Release/` 下的 ZIP、DLL、清单及安装说明 | 独立产物已移除；当前产物统一为 `TestMod/Release/UMM/Mods/TestMod/` |

## 安装目录

本机和内网端均使用既有 `UMM/Mods/TestMod` 目录。整合时检查的两端安装目录均未发现独立 FrameRateLimiter，无需迁移旧实例配置或删除安装目录。

## 清理记录

此前删除旧 ZIP、DLL 的两次操作被自动审批拒绝，仅返回 `blocked by policy`，当时未执行删除。用户随后明确确认“Git 仓库里的 FrameRateLimiter 文件夹也一起删除”。已删除两个旧产物，确认剩余目录中没有文件后移除全部空目录，并确认仓库根目录不再存在 FrameRateLimiter 文件夹。
