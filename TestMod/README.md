# Test Mod

`Test Mod` 是 Broforce 的测试辅助 UMM Mod，用于清除当前屏幕内的敌人，以及调速、暂停和逐帧查看角色动作，并提供 AI / Unity Inspector MCP 运行时调用入口。

## 当前功能

- 分类界面：参考 CustomMapMultiplayer 的左侧导航、右侧分组内容、深色圆角面板、圆角按钮和橙色选中高亮。
- 语言：`跟随系统`、`English`、`中文`。
- 自动击杀：进入关卡后，Mod 在游戏内自动检测敌人；发现当前屏幕内符合条件的敌人后立即执行一次清除。
- 手动击杀：UMM 面板按钮，或通过 MCP 调用公开 API。
- 游戏速度：启用后可设为 `0.01x` 到 `4x`，提供常用倍率按钮。
- 暂停与单帧推进：每次推进 `1/60` 秒的游戏时间，之后继续暂停，不受实际渲染帧率影响。
- 暂停 / 恢复、下一帧可分别绑定键盘按键，默认均未绑定，不占用 F8；单按推进一帧，长按可连续推进。
- 连续推进：长按下一帧快捷键 0.35 秒后开始，默认每秒 10 帧，可调为每秒 1～60 帧；可关闭长按连发。
- “启用自动击杀”设置由 UMM 保存；关闭后自动击杀和手动击杀都会被禁用。

## 击杀范围

每次清除只处理同时满足以下条件的对象：

- 当前屏幕可见；
- 属于当前游戏实例（`IsMine`）；
- 存活的敌方 `Mook`；
- 不是英雄。

执行的是游戏原生 `Mook.Death()`，不直接销毁 GameObject，以保留游戏自己的死亡流程和联机同步行为。

## UMM 设置

在 UMM 中启用 `Test Mod` 后：

1. 左侧“速度与动作帧”页包含播放速度、暂停与逐帧查看、快捷键三个分组。
2. “敌人清理”页包含自动清理、手动清理和清理范围；关闭“启用自动击杀”后，自动和手动清敌都会禁用。
3. “语言设置”页可选择 `跟随系统`、`English` 或 `中文`，立即切换显示语言。
4. 侧栏持续显示播放状态和暂停时的推进帧数，并提供“恢复播放”，切换分类后仍可恢复。
5. 记住上次使用的分类；功能设置会在 UMM 保存时持久化，下一次启动仍然保留。

当前默认设置为启用自动击杀，以便测试；不需要时可以在 UMM 中关闭，也可以通过下面的 MCP API 关闭。

## AI / MCP 调用接口

### 调速和逐帧查看

在 UMM 的“速度与动作帧”页启用速度控制，选择倍率，然后用“暂停当前画面”和“下一帧”查看动作。“快捷键”分组内点击绑定按钮后按目标键，`Esc` 取消；“清除”取消绑定。两个动作不能使用同一个按键，新绑定会清除另一个动作的同键绑定；切换分类会取消尚未完成的绑定。

“快捷键”分组中可开启“长按下一帧快捷键时连续推进”，通过“连续推进速度”滑块设置每秒推进次数，设置会保存。每次按下立即推进一帧，持续按住 0.35 秒后开始按设定速度连续推进；松开后保持暂停。该速度按真实时间计时，独立于播放倍率，每次推进仍为 `1/60` 秒游戏时间，最高受实际渲染帧率限制。切出窗口、开始绑定按键、打开游戏原生暂停菜单、恢复播放或关闭速度控制都会停止长按连发，需要重新按下才能继续。

暂停不会打开游戏暂停菜单。若游戏原生暂停菜单已经打开，需要先关闭再推进。每次推进的是一个游戏更新帧，角色动画的一张贴图可能持续多个游戏帧，需要多按几次才能切换动作帧。

暂停时阻止游戏程序集中的 `Update`、`LateUpdate`、`FixedUpdate` 回调，保留 UMM 和 Mod 的操作。速度控制启用时接管原生慢动作；关闭控制、禁用或卸载 Mod 会恢复接管前的时间倍率和步长。暂停状态不保存，下一次启动从设置的倍率播放；倍率、速度开关和按键会保存。

速度控制只影响当前游戏实例，双端安装不会自动同步速度和逐帧操作。联机时另一端仍可能继续推进，查看动作建议在本地关卡进行。

公开 API（游戏内主线程调用）：

```csharp
TestMod.Plugin.SetGameSpeed(0.25f)   // 启用控制并以四分之一速度播放
TestMod.Plugin.SetGamePaused(true)  // 暂停，保留恢复时使用的倍率
TestMod.Plugin.StepOneFrame()       // 请求推进一帧，返回是否接受；之后仍暂停
TestMod.Plugin.SetGamePaused(false) // 按保存的倍率恢复
TestMod.Plugin.SetGameSpeedEnabled(false) // 释放速度控制
TestMod.Plugin.IsGamePaused
TestMod.Plugin.SteppedFrames        // 当前 Mod 实例已完成的推进帧数
```

`SetGameSpeed(0f)` 等同暂停。倍率 API 接受 `0` 到 `4`，正倍率最小为 `0.01`；非法数值会抛出参数异常。重复请求在同一帧内合并为一次推进。

前提：游戏已经启动、Unity Inspector Mod 已连接、`Test Mod` 已启用。以下 C# 调用会改变游戏状态，只应在用户明确要求测试时执行。

本机游戏使用 `mcp__unity_inspector__execute_code`；5700G 内网游戏使用 `mcp__unity_inspector_remote__execute_code`。两者的 C# 表达式完全相同。

### 查询自动击杀开关

```csharp
TestMod.Plugin.IsAutoKillEnabled
```

返回 `true` 表示允许击杀，返回 `false` 表示功能已关闭。

### 通过 MCP 开关自动击杀

启用：

```csharp
TestMod.Plugin.SetAutoKillEnabled(true)
```

禁用：

```csharp
TestMod.Plugin.SetAutoKillEnabled(false)
```

该设置会立即生效并保存。禁用后，自动清敌不会执行，手动调用也会返回 `0`。

### 立即清除一次

使用对应端点的 `execute_code`，只调用这一条表达式：

```csharp
TestMod.Plugin.KillVisibleEnemiesNow()
```

返回值是本次实际调用 `Mook.Death()` 的数量。返回 `0` 可能表示：开关关闭、Mod 尚未启用、当前没有可见且符合条件的敌人，或调用发生在地图过渡尚未完成时。

### 读取最近一次结果

```csharp
TestMod.Plugin.LastKillCount
```

该值是最近一次清除调用处理的数量；进入新场景时会重置为 `0`。

## 推荐的快速三方图流程

对于已经配置好的本机测试：

1. 启动目标游戏端并等待 Unity Inspector 连接。
2. 使用一次 `quick-online-workshop.cs` 进入预设 Workshop 三方图。
3. 不要在地图刚加载后连续调用 `list_enemies`、`game_state` 等查询；自动击杀由 `Test Mod` 在游戏帧内自行执行。
4. 如果测试需要手动补杀，只调用一次 `TestMod.Plugin.KillVisibleEnemiesNow()`。
5. 需要证据时，再读取 `TestMod.Plugin.LastKillCount` 或 `TestMod.Plugin.IsAutoKillEnabled`。

自动击杀存在的目的就是避开“地图已经进入，但 AI 还在等待 MCP 查询”的延迟窗口。MCP 负责启动、联机和必要的最终确认，不应成为自动清敌的关键路径。

## 稳定对象与程序集名称

- Mod ID：`TestMod`
- 程序集：`TestMod.dll`
- 入口：`TestMod.Plugin.Load`
- 常驻 GameObject：`TestMod`
- 行为组件：`TestMod.TestModBehaviour`
- 项目目录：`D:\Study\C#\Broforce-Mods\TestMod`

## 本机安装目录

```text
E:\SteamLibrary\steamapps\common\Broforce\r2mod\Broforce\profiles\Broforce\UMM\Mods\TestMod
```

发布目录：

```text
D:\Study\C#\Broforce-Mods\TestMod\Release\UMM\Mods\TestMod
```

## 维护提示

使用 `powershell -File .\TestMod\BuildAndDeploy.ps1` 编译并部署到本机及 `5700G` 的 `UMM\Mods\TestMod`；加 `-SkipDeploy` 只编译。脚本读取本地 `LocalBroforcePath.props` 的依赖路径，使用 .NET 3.5 编译器。

- 修改源码后需要重新编译 `TestMod.dll`，并覆盖本机或目标测试端 UMM 目录中的 DLL。
- DLL 更新后需要重启游戏，正在运行的 Unity 进程不会自动加载新程序集。
- 不要同时保留旧的 `ScreenEnemyKiller` 部署，否则旧 Mod 可能继续执行旧的自动击杀逻辑。
