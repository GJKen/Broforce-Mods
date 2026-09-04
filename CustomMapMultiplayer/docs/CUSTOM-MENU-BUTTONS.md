# 自定义菜单按钮开发手册

[返回开发文档索引](DEVELOPMENT.md) · [AFK 行为](AFK.md) · [测试与验收](TESTING.md)

## 适用范围

本文说明如何在 Broforce 的原生菜单中增加由 RocketLib 注册、由 Mod 执行的自定义按钮。当前实现以游戏内 Esc 的 `PauseMenu` 主动 AFK 按钮为完整示例；其它菜单应先确认对应的 `TargetMenu`、锚点名称和菜单生命周期，再复用相同原则。

按钮实现由三部分组成：

1. RocketLib Action 注册：定义固定的动作标识、回调、目标菜单、插入位置和可见性。
2. 功能回调：只处理业务请求，不依赖菜单文字判断功能。
3. 菜单 UI 更新：在菜单实例化或刷新后设置当前语言文字，必要时修复字体和材质。

## 注册规则

### 只注册一个 Action

每个自定义功能只注册一个 RocketLib Action。注册代码应有一次性保护，并使用固定的内部显示名：

```csharp
private static bool _customActionRegistered;
internal const string CustomActionRegistrationName = "Enter AFK now";

private static void RegisterCustomAction()
{
    if (_customActionRegistered)
    {
        return;
    }

    _customActionRegistered = true;
    MenuRegistry.RegisterAction(
        CustomActionRegistrationName,
        menu => RequestCustomAction(),
        TargetMenu.PauseMenu,
        PositionMode.After,
        "OPTIONS",
        0,
        menu => CanShowCustomAction());
}
```

`RegisterAction` 的第一个字符串同时可能参与 RocketLib 的注入和去重。它不是可靠的最终 UI 文案，不能在语言切换时改成中文，也不能分别注册 English 和中文两个 Action。否则菜单重建或切换语言时可能出现重复按钮、动作错位，甚至把原生按钮的动作映射到错误项目。

注册时必须确认：

- `TargetMenu` 是目标菜单，而不是凭当前画面猜测的菜单类型。
- `PositionMode` 和锚点名称（例如 `After`、`OPTIONS`）在目标游戏版本中确实存在。
- 回调指向唯一的功能入口，例如 `RequestLocalAfk()`；不要让显示文字决定执行哪一个动作。
- 可见性只表达“当前是否应该显示”，不要在可见性回调中执行功能或修改菜单数组。

如果注册过程可能重复执行，应保留一次性字段；如果注册失败，需要记录异常，并确保下一次加载仍能重试。当前 AFK 注册入口位于 `src/Plugin.cs` 的 `RegisterPauseMenuAfkAction`。

## 功能回调

回调应尽快转入独立的业务方法，并在那里完成权限、目标对象和当前会话状态检查。例如主动 AFK 需要确认当前房间不是单人房，并按本地所有权和输入控制器确定目标玩家。菜单层不应直接遍历所有玩家并猜测目标。

建议保持以下边界：

- 菜单回调只发出请求；生命周期状态由功能模块维护。
- 不能唯一确定目标时不执行，避免影响远程玩家或另一条本地槽位。
- 业务失败应显示当前语言的提示，并写入可关联的诊断日志。
- 回调不能依赖按钮当前文字，因为文字会随语言改变。

## 本地化显示

RocketLib 的注册名保持固定，实际 UI 文案在菜单实例生成后更新。将每个按钮的文字加入 `SettingsUiText`，并让 `SettingsUiLocalization.Get()` 根据 `en`、`zh` 和 `system` 返回对应文本：

```csharp
internal string CustomButton;

// English
CustomButton = "Enter AFK now";

// Chinese
CustomButton = "立即进入 AFK";
```

`system` 应沿用现有本地化选择逻辑：系统语言为中文时使用中文，否则使用英文。显示文字更新时应覆盖实际 UI 使用的全部文字字段；当前 PauseMenu 实现同步了 `MenuBarItemUI.text`、`ItemText.text` 和 `BackdropText.text`。

不要通过 `text == "Enter AFK now"` 或 `text == "立即进入 AFK"` 反向查找按钮。文字是可变数据，可靠的定位依据应是固定的 RocketLib `invokeMethod`、注册路由或明确的自定义标记。

## PauseMenu 专项处理

PauseMenu 的 RocketLib 注入存在两个需要单独验证的数组：

- `masterItems`：菜单项目及动作定义。
- `items`：实际生成的 UI 项目。

两者必须长度相同，并且索引一一对应。若 RocketLib 注入后长度不一致，先调用原生 `Menu.InstantiateItems()` 重新生成 UI，再重新读取两个数组。只有确认长度相同后，才能用自定义 Action 的索引读取 UI 并设置文字。

当前 AFK 实现的补丁顺序是：

1. `PauseMenu.InstantiateItems` Postfix：同步 `masterItems` 和 `items`，并更新按钮文字。
2. `Menu.Update` Postfix：菜单刷新或语言设置改变后再次更新文字和视觉属性。
3. 用固定 `RocketLib_...` 路由定位按钮；不使用当前显示文字定位。
4. 从一个原生菜单项复制可用的字体和材质，避免中文文字缺字或显示异常。

其它菜单不一定有 `masterItems` 和 `items`，不能直接复制这段反射逻辑。应先通过反编译或运行时诊断确认目标菜单的项目定义、实例数组和重建方法，再决定是否需要对应补丁。相关实现位于 `src/HarmonyDiagnostics.Afk.cs` 的 `PatchPauseMenuAfkMenu`、`SynchronizePauseMenuItems` 和 `UpdatePauseMenuAfkTextAndVisuals`。

## 位置与动作一致性

插入位置决定项目顺序，但不保证所有菜单内部数组会自动保持一致。增加或隐藏项目后必须检查：

- 菜单上显示的文字与实际回调一一对应。
- 原生相邻按钮仍执行原生功能。
- 同一个自定义项目不会因菜单重建而重复出现。
- 语言切换后项目数量和顺序不变，只改变可见文字。
- 重新打开 Esc 菜单、返回大厅、进入下一关后没有残留旧 UI 或旧路由。

## 测试清单

每个新按钮至少完成以下测试，并在对应功能文档或 issue 中记录结果：

### 注册与显示

- 首次加载菜单时只出现一个目标按钮。
- 连续打开、关闭并重新打开目标菜单，按钮没有重复。
- English 显示英文，中文显示中文，system 跟随系统语言。
- 语言切换后按钮数量、顺序和高亮状态正常。
- 中文字体、材质和按钮宽度正常，没有缺字或覆盖相邻项目。

### 功能与相邻动作

- 点击自定义按钮只执行目标功能一次。
- 目标状态不满足时按钮按预期隐藏或拒绝请求。
- 自定义按钮前后的原生按钮仍执行自己的功能。
- 菜单重建、返回大厅和重新进入游戏后动作映射仍正确。

### 联机与生命周期

如果按钮改变玩家、房间或网络状态，还要覆盖房主、加入方、多本地槽位、重复点击、断线、重入和下一局状态清理。测试结论应同时保留双方日志；只有单端画面不能证明网络动作只影响了正确的一端。

## 新按钮实现模板

新增按钮时可按以下顺序执行：

1. 在 `Plugin.cs` 增加固定注册名和一次性注册入口。
2. 在对应功能模块增加业务回调和可见性判断。
3. 在 `SettingsUiText.cs` 增加 English、中文和 system 选择所需的显示文本。
4. 确定目标菜单是否需要实例化 Postfix、刷新 Postfix、数组同步或字体材质修复。
5. 通过固定路由定位项目，更新 UI 文本，不通过当前文字定位。
6. 完成上面的显示、相邻动作和生命周期测试，并在文档中记录历史问题与验收范围。

## 相关实现

- `src/Plugin.cs`：RocketLib Action 注册和可见性入口。
- `src/SettingsUiText.cs`：English、中文和 system 的本地化文本。
- `src/HarmonyDiagnostics.Afk.cs`：PauseMenu 注入后的数组同步、路由定位、文字和字体材质更新。
- `docs/AFK.md`：主动 AFK 的业务行为和生命周期。
- `issues/ISSUES-2026-09-01-新增ESC菜单主动AFK按钮.md`：PauseMenu 语言切换错位问题和主动 AFK 生命周期的完整修复记录。
