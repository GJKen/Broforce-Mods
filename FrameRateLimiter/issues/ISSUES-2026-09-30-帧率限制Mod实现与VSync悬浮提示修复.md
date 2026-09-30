# 帧率限制 Mod 实现与 VSync 悬浮提示修复

## 状态

**已实现，最终版本 `1.1.1` 已完成编译、部署和运行界面验证。**

此前用于 tooltip 修复的本地提交为：

```text
9fb9c9983a76a4784f19cee0eb2afb77cb583be8
FrameRateLimiter - 修复 VSync 悬浮提示显示
```

该提交尚未推送远端。本次动态文案和 Release 结构整理尚未创建新提交。当前 Release DLL 的 SHA-256 为：

```text
5262F96F18D87AC169A435F41567296BF884203371E8E3BCF35B05191720A98E
```

## 需求与范围

为 Steam 版 Broforce 增加一个独立的 Unity Mod Manager 插件，用于限制本机渲染帧率。

- 使用 Unity `Application.targetFrameRate` 控制本机渲染帧率。
- 使用 `QualitySettings.vSyncCount` 提供 VSync 设置。
- 不修改游戏逻辑、联机协议、角色同步或地图加载流程。
- 设置保存在 UMM 配置中，禁用或卸载插件时恢复启用前的显示设置。
- 设置面板支持中文和英文切换。
- VSync 选项提供面向普通玩家的中英文悬浮说明。

## 功能实现

### 帧率设置

- 默认目标帧率为 `60` FPS。
- 支持 `30`、`60`、`120`、`144` 和 `240` FPS 快速选择。
- 支持输入 `1` 到 `1000` 的自定义目标帧率。
- 使用 `-1` 表示不限制帧率。
- 输入 `0` 时恢复默认值 `60`；低于 `-1` 的值归一化为 `1`，高于 `1000` 的值归一化为 `1000`。
- 输入框和快速选择修改后立即应用，不必重启游戏。

### VSync 和生命周期

- 插件启用时默认关闭 VSync，使自定义 `Application.targetFrameRate` 能够生效。
- 第一次启用时保存原始 `targetFrameRate` 和 `vSyncCount`。
- 插件禁用或卸载时恢复保存的原始值。
- 插件运行期间修改 VSync 设置会立即应用；关闭插件后仍恢复启用前的原始 VSync 状态。
- 异常发生时将插件标记为未启用，并尝试恢复原始显示设置。
- VSync 复选框会根据当前选择动态显示“启用此 Mod 时关闭 VSync”或“启用此 Mod 时使用原始 VSync 设置”；英文界面同步切换对应文案。

### 本地化

- 设置面板右上角提供 `English` / `中文` 按钮。
- 目标帧率、输入提示、状态文本、VSync 选项和悬浮说明均提供中英文文本。
- 语言选择通过 `FrameRateLimiterSettings.UseChinese` 持久化。
- 旧配置没有语言字段时默认使用中文，原有目标帧率和 VSync 设置继续保留。

## 问题演进与修复

### 初次构建后 UMM 中看不到插件

第一次编译只生成了项目目录下的 DLL，没有复制到当前实际使用的 UMM profile。游戏因此无法扫描到插件，问题不是入口类或 `Info.json` 内容错误。

实际安装目录为：

```text
E:\Games\Broforce Mods\Broforce\profiles\Broforce\UMM\Mods\GJKen-FrameRateLimiter\
```

将 `FrameRateLimiter.dll` 和 `Info.json` 部署到该目录后，UMM 能识别：

```text
Id: FrameRateLimiter
DisplayName: Frame Rate Limiter
EntryMethod: FrameRateLimiter.Plugin.Load
```

后续将部署逻辑加入 `BuildAndDeploy.ps1`，构建后同时生成安装包文件并复制到本机 UMM 目录。

### 中英文切换的编译修复

添加本地化文本时，`GUILayout.Toggle` 调用少了一个右括号，导致第一次本地化构建失败。修正语法后重新编译和部署成功，版本更新为 `1.1.0`。

### VSync 悬浮提示被其它 Mod 覆盖

最初在当前 UMM 内容区域绘制 tooltip，后绘制的其它 Mod 会覆盖提示，且背景存在不透明和层级不明确的问题。随后曾尝试建立独立的 `TooltipOverlay : MonoBehaviour`，设置较高 GUI 层级、持久化对象和不透明背景。

### 独立覆盖层无法收到 tooltip 文本

MCP 运行时检查确认：

- `FrameRateLimiter` DLL 已加载。
- 独立覆盖层对象曾成功创建并处于激活状态。
- 设置面板和 VSync 选项确实可见。
- 覆盖层的 `_tooltip` 始终为空，强制注入文本也会被后续 UMM GUI 事件清除。

根因是独立 `MonoBehaviour.OnGUI` 与 UMM 设置面板的 `OnGUI` 事件时序不一致。`GUI.tooltip` 在独立覆盖层绘制时已经被 UMM 的下一阶段清理，因此覆盖层无法稳定取得提示文本。

最终实现保留 `TooltipOverlay` 作为样式和背景资源持有者，但不使用它自己的 `OnGUI` 事件绘制提示：

- `Plugin.OnGUI` 记录 VSync 控件的实际 Rect，并在同一轮根据鼠标位置判断是否悬浮。
- 只在 `EventType.Repaint` 阶段调用 `TooltipOverlay.DrawInGui`。
- 提示在同一个 UMM Mod 绘制回调的末尾完成，避免独立 `MonoBehaviour.OnGUI` 与 UMM 事件时序不一致。
- 提示框默认绘制在 VSync 控件上方；与控件的间距为 `4px`，顶部空间不足时在控件下方以相同间距回退。
- 使用 Alpha 为 `1` 的深色纹理作为背景，固定内边距和换行样式。

这样既保留了可复用的 tooltip 样式，也避免依赖独立 GUI 事件传递 `GUI.tooltip` 状态。

## 构建与部署

### 项目

- 项目：`FrameRateLimiter/FrameRateLimiter.csproj`
- 目标框架：`.NET Framework 3.5`
- 入口：`FrameRateLimiter.Plugin.Load`
- 清单版本：`1.1.1`
- 清单文件：`Release/UMM/Mods/FrameRateLimiter/Info.json`
- Release 包：`Release/FrameRateLimiter.zip`

构建脚本使用 Broforce 实际安装目录中的 Unity 程序集和 UMM 程序集。为解决 `TextAnchor` 缺少程序集的问题，项目和脚本补充了 `UnityEngine.TextRenderingModule.dll` 引用。

```powershell
powershell -ExecutionPolicy Bypass -File .\BuildAndDeploy.ps1
```

构建脚本会：

- 使用 .NET Framework 3.5 `csc.exe` 编译全部源文件。
- 生成 `Release/UMM/Mods/FrameRateLimiter/FrameRateLimiter.dll` 和 `Info.json`。
- 生成可导入 r2modman 的 `Release/FrameRateLimiter.zip`。
- 部署到本机 UMM 的 `GJKen-FrameRateLimiter/FrameRateLimiter` 目录。
- 输出 DLL SHA-256。

`LocalBroforcePath.props` 只保存本机路径，已被 Git 忽略，不属于提交内容。项目结构和 Release 包布局参考 `CustomMapMultiplayer`，不再把 DLL 和 UMM 清单作为项目根目录的分发文件。

## 验证记录

### 静态和构建验证

- 使用 Broforce 实际 `Managed` 目录中的 Unity 程序集完成 .NET 3.5 编译。
- 修正 tooltip 方案后重新编译无警告。
- 项目文件、PowerShell 构建脚本和 JSON 清单通过语法检查。
- 仓库 DLL 与部署到 UMM 的 DLL 哈希一致。
- `git diff --check` 和提交范围检查通过。
- Release 包内 DLL 与本机 UMM 部署 DLL 哈希一致；本轮没有把本机路径配置或临时输出目录纳入 Release 包。

### 运行时验证

- 通过 r2modman 的 `Start modded` 启动 Broforce。
- MCP 确认游戏进入 `MainMenu`，并能打开 `Frame Rate Limiter` 设置页。
- MCP 截图确认 VSync 选项可见。
- 鼠标悬浮在 VSync 选项上时，中文说明正常显示。
- 提示背景为完全不透明的深色，并显示在下方 Mod 内容之上。
- 当前结构保留 `FrameRateLimiterTooltipOverlay` 对象用于持有 tooltip 样式，但提示绘制由 `Plugin.OnGUI` 同步调用，未使用独立覆盖层的 `OnGUI` 绘制链路。

## 验收边界

本轮已完成编译、部署、UMM 识别和 tooltip 界面运行验证，但没有建立独立的 30/60/120 FPS 帧时间采样矩阵。因此当前记录可以确认插件路径、设置应用逻辑和 UI 修复已落地，不能仅凭本轮截图宣称不同硬件上的实际帧率误差或 VSync 行为已经完成性能验收。

该插件只影响本机渲染设置。多人游戏双方是否使用相同的显示帧率设置不改变联机协议，也不会由插件自动同步。

## 相关文件

- `FrameRateLimiter/src/Plugin.cs`
- `FrameRateLimiter/src/FrameRateLimiterSettings.cs`
- `FrameRateLimiter/FrameRateLimiter.csproj`
- `FrameRateLimiter/BuildAndDeploy.ps1`
- `FrameRateLimiter/Release/UMM/Mods/FrameRateLimiter/Info.json`
- `FrameRateLimiter/Release/FrameRateLimiter.zip`
- `FrameRateLimiter/docs/BUILD.md`
- `FrameRateLimiter/README.md`
- `FrameRateLimiter/README.en.md`
