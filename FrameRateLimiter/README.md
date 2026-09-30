# Frame Rate Limiter

这是一个面向 Steam 版 Broforce 的 Unity Mod Manager 帧率限制插件。启用后通过 Unity 的 `Application.targetFrameRate` 限制本机渲染帧率，不修改游戏逻辑和联机协议。

当前版本：`1.1.1`

## 功能

- 默认目标帧率为 `60` FPS。
- 支持 `30`、`60`、`120`、`144`、`240` FPS 快速选择。
- 支持输入 `1` 到 `1000` 的自定义目标帧率。
- 点击“不限制”按钮可以取消帧率限制；输入框只接受 `1` 到 `1000` 的数字。
- 默认在插件启用时关闭 VSync，使自定义帧率设置生效。
- 禁用或卸载插件时恢复启用前的目标帧率和 VSync 设置。
- 支持中文和英文界面切换，语言选择自动保存。
- VSync 选项提供中英文鼠标悬浮说明，背景不透明并显示在其它 Mod 内容之上。

## 使用

1. 使用 r2modman 导入 `Release\FrameRateLimiter.zip`。
2. 启动游戏，在 UMM 中启用 `Frame Rate Limiter`。
3. 在插件设置中选择目标帧率，设置会立即生效。
4. 点击设置面板右上角的 `English` 或 `中文` 按钮切换界面语言。

VSync 复选框的文案会随当前选择动态变化：

- 勾选时：`启用此 Mod 时关闭 VSync`。
- 取消勾选时：`启用此 Mod 时使用原始 VSync 设置`。

将鼠标悬浮在 VSync 选项上，可以查看垂直同步、画面撕裂、输入延迟和自定义帧率之间关系的说明。

### 手动安装

如果不使用 r2modman，也可以将 Release 包中的以下目录复制到 UMM：

```text
<UMM>\Mods\GJKen-FrameRateLimiter\FrameRateLimiter\
├─ FrameRateLimiter.dll
└─ Info.json
```

替换 DLL 后需要重启游戏。

## 构建

项目目标为 .NET Framework 3.5。复制 `LocalBroforcePath.props.example` 为 `LocalBroforcePath.props`，填写 Broforce 的 `Managed` 目录和 UMM 的 `Core` 目录。该配置只用于本机构建部署，已被 Git 忽略。

从项目目录执行：

```powershell
powershell -ExecutionPolicy Bypass -File .\BuildAndDeploy.ps1
```

构建脚本会使用 Broforce 实际 Unity 程序集和 UMM 程序集编译源码，生成 Release 包并部署到本机 UMM：

```text
Release\
├─ FrameRateLimiter.zip
├─ README.md
├─ manifest.json
└─ UMM\Mods\FrameRateLimiter\
   ├─ FrameRateLimiter.dll
   └─ Info.json
```

只生成 Release 包而不部署到本机 UMM：

```powershell
powershell -ExecutionPolicy Bypass -File .\BuildAndDeploy.ps1 -SkipDeploy
```

完整构建说明见 [docs/BUILD.md](docs/BUILD.md)。

## 项目结构与文档

| 路径 | 说明 |
| --- | --- |
| `src/` | Mod 源码目录 |
| `src/Plugin.cs` | UMM 入口、设置界面和帧率应用逻辑 |
| `src/FrameRateLimiterSettings.cs` | 帧率、VSync 和语言设置 |
| `src/TooltipOverlay.cs` | VSync 悬浮提示绘制 |
| `FrameRateLimiter.csproj` | C# 工程文件 |
| `BuildAndDeploy.ps1` | .NET 3.5 构建、打包和部署脚本 |
| `Release/` | r2modman 安装包和 UMM 插件文件 |
| `Release/UMM/Mods/FrameRateLimiter/Info.json` | UMM 清单和版本元数据 |
| `README.md` | 中文说明文档 |
| `README.en.md` | 英文说明文档 |
| `docs/BUILD.md` | 构建、部署和 Release 包结构说明 |
| `issues/` | 历史问题、测试证据和验收记录 |
