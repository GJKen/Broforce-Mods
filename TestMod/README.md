# Test Mod

`Test Mod` 是 Broforce 的测试辅助 UMM Mod，当前用于清除当前屏幕内的敌人，并为后续 AI / Unity Inspector MCP 测试保留稳定的运行时调用入口。

## 当前功能

- 语言：`跟随系统`、`English`、`中文`。
- 自动击杀：进入关卡后，Mod 在游戏内自动检测敌人；发现当前屏幕内符合条件的敌人后立即执行一次清除。
- 手动击杀：UMM 面板按钮，或通过 MCP 调用公开 API。
- 不绑定任何默认键盘按键，不占用 F8。
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

1. 在语言区域选择 `跟随系统`、`English` 或 `中文`。
2. 用“启用自动击杀 / Enable automatic killing”开关控制功能。
3. 关闭开关后，自动清敌不会执行，手动清敌按钮也会被禁用。
4. 设置会在 UMM 保存设置时持久化，下一次启动仍然保留。

当前默认设置为启用自动击杀，以便测试；不需要时可以在 UMM 中关闭，也可以通过下面的 MCP API 关闭。

## AI / MCP 调用接口

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

- 修改源码后需要重新编译 `TestMod.dll`，并覆盖本机或目标测试端 UMM 目录中的 DLL。
- DLL 更新后需要重启游戏，正在运行的 Unity 进程不会自动加载新程序集。
- 不要同时保留旧的 `ScreenEnemyKiller` 部署，否则旧 Mod 可能继续执行旧的自动击杀逻辑。
