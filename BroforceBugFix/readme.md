# Broforce Bug Fix

> [English](README.en.md)

这是一个面向 Steam 版 Broforce 的 Unity Mod Manager + Harmony 修复插件。项目只收纳已经通过日志和原版源码确认的游戏缺陷；每个修复都应保持独立、可验证，并尽量不改变正常玩法。

## 当前修复

当前版本为 `0.3.0`，包含两个修复：

- 阻止同一个 `DoodadCrate` 在尚未完成 `ActuallyCollapse` 时递归进入该方法，修复相邻爆炸弹药箱互相触发后无限递归、爆炸特效暴增、严重掉帧及 `StackOverflowException`。
- 从主菜单加入新房间时，清除上次未完成地图加载遗留的暂停 RPC 流和旧消息，避免加入官方房后卡在主菜单、身份消息无法执行。此修复不依赖 CustomMapMultiplayer。

| 项目 | 当前值 |
| --- | --- |
| 版本 | `0.3.0` |
| 静态构建 | 已通过 |
| 游戏内复测 | 加入房间修复已通过官方 Steam 双端复测；箱体修复仍待验收 |

第一次坍塌仍执行完整的原版逻辑，包括爆炸、周围伤害、正常连锁反应、掉落物和网络 RPC。补丁只跳过同一实例尚未退出时发生的嵌套重入。

## 修复开关

UMM 的 Mod 启用状态是最外层开关。插件面板内还有两级持久化设置：

- `Enable all bug fixes`：插件内主开关。关闭时立即卸载所有修复，但保留各独立开关的选择。
- `Prevent recursive explosive-ammo crate collapse`：爆炸弹药箱递归修复的独立开关。
- `Clear a stalled previous room's network stream when joining`：加入新房间时清理残留暂停消息流的独立开关。

插件内开关首次安装时均默认开启。UMM、主开关及对应独立开关全部开启时，该项修复才会生效。设置变化会立即应用，不要求重启游戏。

## 安装

将项目内安装包 `BroforceBugFix` 目录中的 `BroforceBugFix.dll` 和 `Info.json` 放入：

```text
<UMM>\Mods\GJKen-BroforceBugFix\
```

重启游戏，在 UMM 中确认 `Broforce Bug Fix 0.3.0` 已启用。加入房间修复只需要安装在出现残留暂停的加入端；箱体销毁修复在联机时建议所有参与端安装相同版本。

## 构建

项目面向 .NET Framework 3.5。复制 `LocalBroforcePath.props.example` 为 `LocalBroforcePath.props`，填写 Broforce `Managed` 和 UMM `Core` 目录。`TestDeployModPath` 是可选的额外测试部署目录，留空时只部署到本机 UMM；该本机配置文件已被 Git 忽略，不应提交。

```powershell
powershell -ExecutionPolicy Bypass -File .\BuildAndDeploy.ps1
```

脚本会更新项目内可复制安装包并部署到本机 UMM；仅在本机配置了 `TestDeployModPath` 时才会额外部署到该测试目录。

## 项目结构与文档

```text
src/                         Mod 源码
src/BugFixSettings.cs        主开关和各修复的持久化开关
BroforceBugFix.csproj        C# 工程文件
BuildAndDeploy.ps1           .NET 3.5 构建和部署脚本
BroforceBugFix/              可复制的 UMM 安装包（DLL + Info.json）
modinfo.json                 UMM 清单模板
LocalBroforcePath.props.example
                             本机路径配置示例
docs/DEVELOPMENT.md          开发、逆向、测试和维护约束
issues/                      问题证据、修复说明和验收记录
```

- [开发与测试文档](docs/DEVELOPMENT.md)
- [问题记录索引](issues/README.md)
- [爆炸弹药箱递归栈溢出](issues/ISSUES-2026-08-27-DoodadCrate爆炸递归栈溢出.md)
