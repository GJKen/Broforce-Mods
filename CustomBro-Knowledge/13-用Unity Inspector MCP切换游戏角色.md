# 用 Unity Inspector MCP 切换游戏角色

本文记录在游戏运行中使用 Unity Inspector MCP 把玩家 1 的当前角色切换为指定官方角色的方法。文中场景名和 `ScorpionBro` 仅是历史示例；目标角色、场景和当前游戏源码版本都应在实际运行环境确认。

## 前置条件

- 游戏已经启动并进入关卡。
- Unity Inspector MCP 可以连接游戏，先调用 `ping` 确认返回 `pong`。
- 目标角色存在于游戏的 `HeroType` 枚举和预制体列表中，例如 `HeroType.ScorpionBro`。
- 只对玩家当前角色实例操作，不要删除同名的角色预制体。

## 为什么不能只改贴图或直接生成角色

游戏中的玩家角色不只是一个显示对象，还需要同时满足以下关系：

1. 角色实例由游戏的网络实例化接口生成。
2. `SetUpHero(..., true)` 把角色注册到 `HeroController.players[playerNum].character`。
3. 玩家对象的角色引用、`heroType`、HUD 和网络同步状态保持一致。
4. 旧角色通过 `DestroyNetworked()` 广播销毁。

只用 `UnityEngine.Object.Destroy` 删除旧角色可能会被网络同步状态重新恢复；只生成新角色则会出现两个角色。稳定顺序是：

```text
读取旧角色位置
    -> 网络生成新角色
    -> 注册新角色到玩家
    -> 设置新角色位置
    -> 网络销毁旧角色
    -> 查询状态确认只有一个角色
```

## MCP 操作流程

下面的代码通过 `mcp__unity_inspector__execute_code` 分步执行。每段都是一个 C# 表达式，不能写 `return` 语句。

### 1. 读取游戏和玩家状态

先调用：

```text
mcp__unity_inspector__ping
mcp__unity_inspector__game_state
```

确认 `isInGame=true`，并记下当前玩家角色名称和位置。也可以检查：

```csharp
new object[]{
    HeroController.players[0] != null,
    HeroController.players[0].character != null,
    HeroController.players[0].character.heroType.ToString(),
    HeroController.players[0].character.transform.position
}
```

### 2. 网络生成目标角色

把 `HeroType.ScorpionBro` 替换为目标角色：

```csharp
Networking.Networking.InstantiateBuffered<TestVanDammeAnim>(
    HeroController.GetHeroPrefab(HeroType.ScorpionBro),
    HeroController.players[0].character.transform.position,
    Quaternion.identity,
    new object[0],
    false
)
```

如果返回对象名称类似 `ScorpionBro(Clone) [local]`，说明网络实例化成功。

### 3. 注册新角色到玩家

使用新实例调用 `SetUpHero`，第三个参数必须为 `true`：

```csharp
GameObject.Find("ScorpionBro(Clone) [local]")
    .GetComponent<TestVanDammeAnim>()
    .SetUpHero(0, HeroType.ScorpionBro, true)
```

如果目标角色的实例名称没有 `[local]`，按 `list_gameobjects` 返回的实际路径替换。注册后应能看到：

```csharp
new object[]{
    HeroController.players[0].character != null,
    HeroController.players[0].character.heroType.ToString()
}
```

结果应为 `true` 和 `ScorpionBro`。

### 4. 再次设置位置

网络实例化时已经传入旧角色位置，但可以再显式同步一次：

```csharp
GameObject.Find("ScorpionBro(Clone) [local]")
    .GetComponent<TestVanDammeAnim>()
    .SetXY(旧角色X, 旧角色Y)
```

随后执行：

```csharp
GameObject.Find("ScorpionBro(Clone) [local]")
    .GetComponent<TestVanDammeAnim>()
    .SetPosition()
```

`旧角色X` 和 `旧角色Y` 替换为步骤 1 读取的数值。若实例化时使用了正确的位置，这一步可以省略。

### 5. 网络销毁旧角色

确认新角色已经注册后，再销毁旧角色。下面示例中旧角色是 `Brobocop`：

```csharp
GameObject.Find("BroboCop(Clone) [local]")
    .GetComponent<Brobocop>()
    .DestroyNetworked()
```

不同角色使用对应的组件类型，例如旧角色是 `Rambro` 就改为 `GetComponent<Rambro>()`。调用 `DestroyNetworked()`，不要只调用 `UnityEngine.Object.Destroy(...)`。

### 6. 验证结果

调用：

```text
mcp__unity_inspector__game_state
mcp__unity_inspector__inspect_player
```

还可以执行：

```csharp
new object[]{
    GameObject.Find("BroboCop(Clone) [local]") == null,
    GameObject.Find("ScorpionBro(Clone) [local]") != null,
    HeroController.players[0].character.heroType.ToString()
}
```

正确结果应为：

```text
true
true
ScorpionBro
```

`game_state` 应显示 `playerCount=1`、`heroType=ScorpionBro`，`inspect_player` 应只列出一个玩家角色。

## 优先使用游戏入口

游戏源码提供了正常入口：

```csharp
HeroController.ChangeBro(0, HeroType.ScorpionBro)
```

优先使用这个已注册的游戏入口。调用后检查 `game_state`、玩家引用和场景对象；只有正常入口在目标场景不适用且已确认网络/注册前置条件时，才使用上文的手动实例化流程。文中手动步骤属于诊断与兼容性备用方案。

备用流程的关键不是强制修改 `heroType` 字段，而是重新走以下游戏对象生命周期：

```text
Networking.Networking.InstantiateBuffered
    -> TestVanDammeAnim.SetUpHero(..., true)
    -> DestroyNetworked
```

## 常见问题

### 画面出现两个角色

新角色已经注册成功，但旧角色还存在于场景中。先确认：

```csharp
new object[]{
    GameObject.Find("Brobocop(Clone) [local]") != null,
    GameObject.Find("ScorpionBro(Clone) [local]") != null
}
```

如果两项都是 `true`，对旧角色组件调用 `DestroyNetworked()`。

### 调用 MCP 返回 `Game process died`

Unity Inspector MCP 偶尔会误报。按仓库约定，先调用 `ping` 和 `game_state`：

- `ping` 返回 `pong` 且 `game_state` 正常：继续操作，不视为游戏崩溃。
- 两者都无法连接：检查游戏是否真的退出，再重新启动 MCP 连接。

### 误删了角色预制体

`/ScorpionBro`、`/Brobocop` 这类没有 `(Clone)` 的对象可能是预制体或资源对象。只操作运行时实例，例如：

```text
/ScorpionBro(Clone) [local]
/BroboCop(Clone) [local]
```

### `SetUpHero` 注册后仍然没有控制权

确认第三个参数是 `true`，并检查：

```csharp
new object[]{
    HeroController.players[0].character != null,
    HeroController.players[0].character.IsMine,
    HeroController.players[0].character.IsHero,
    HeroController.players[0].character.heroType.ToString()
}
```

玩家 1 应分别得到 `true`、`true`、`true` 和目标 `HeroType` 名称。

## 相关源码

- `Broforce_src\Broforce-Source\HeroController.cs`：`ChangeBro`、`SetPlayerCharacter`、`RegisterHeroToPlayer`
- `Broforce_src\Broforce-Source\Player.cs`：`SpawnHero`、网络实例化玩家角色
- `Broforce_src\Broforce-Source\TestVanDammeAnim.cs`：`SetUpHero`
- `Broforce_src\Broforce-Source\NetworkObject.cs`：`DestroyNetworked`
- `Broforce_src\unity-inspector-mcp\README.md`：MCP 工具说明
