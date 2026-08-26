# Broforce Bug Fix

这是一个面向 Steam 版 Broforce 的 Unity Mod Manager + Harmony 修复插件。项目只收纳已经通过日志和原版源码确认的游戏缺陷；每个修复都应保持独立、可验证，并尽量不改变正常玩法。

## 当前修复

当前版本为 `0.2.0`，包含一个修复：

- 阻止同一个 `DoodadCrate` 在尚未完成 `ActuallyCollapse` 时递归进入该方法，修复相邻爆炸弹药箱互相触发后无限递归、爆炸特效暴增、严重掉帧及 `StackOverflowException`。

| 项目 | 当前值 |
| --- | --- |
| 版本 | `0.2.0` |
| DLL SHA-256 | `B81902C1D59F6CD6845B76841C0A619B303137664BFE0E67783674F5561C025C` |
| 静态构建 | 已通过 |
| 游戏内复测 | 待离线与官方 Steam 双端复测 |

第一次坍塌仍执行完整的原版逻辑，包括爆炸、周围伤害、正常连锁反应、掉落物和网络 RPC。补丁只跳过同一实例尚未退出时发生的嵌套重入。

## 修复开关

UMM 的 Mod 启用状态是最外层开关。插件面板内还有两级持久化设置：

- `Enable all bug fixes`：插件内主开关。关闭时立即卸载所有修复，但保留各独立开关的选择。
- `Prevent recursive explosive-ammo crate collapse`：爆炸弹药箱递归修复的独立开关。

两个插件内开关首次安装时均默认开启。三个条件全部开启时，当前修复才会生效。设置变化会立即应用，不要求重启游戏。以后新增的官方 Bug 修复会各自增加独立开关，并统一受主开关控制。

## 安装

将项目内安装包 `BroforceBugFix` 目录中的 `BroforceBugFix.dll` 和 `Info.json` 放入：

```text
<UMM>\Mods\GJKen-BroforceBugFix\
```

重启游戏，在 UMM 中确认 `Broforce Bug Fix 0.2.0` 已启用。该修复作用于本机执行的箱体销毁逻辑；联机时建议所有参与端安装相同版本。

## 构建

项目面向 .NET Framework 3.5。复制 `LocalBroforcePath.props.example` 为 `LocalBroforcePath.props`，填写 Broforce `Managed` 和 UMM `Core` 目录，然后运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\BuildAndDeploy.ps1
```

脚本会更新项目内可复制安装包，并部署到配置的本机和内网测试端。

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
