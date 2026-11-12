# Unity Inspector MCP Server

> [English](README.en.md)

一个 MCP（模型上下文协议）服务器，允许 AI 客户端检查并与运行 Unity Inspector Mod 的 Unity 游戏进行交互。

## 前置条件

1. 已安装 Unity Inspector Mod；使用游戏端工具前需在 Broforce 中启用
2. Node.js 18+ 已安装
3. 调用游戏端工具时，Unity 游戏必须正在运行，且 TCP 服务已启动；MCP 的目标地址、端口和口令应与游戏面板一致（默认 `127.0.0.1:9999`）。`connection_status` 和离线测试不要求启动游戏。

## 安装

```bash
npm install
```

## 使用方法

### 测试连接

首先，确保 Unity Inspector Mod 的 TCP 服务器正在 Broforce 中运行。默认端口为 `9999`。

在 Windows 上，使用 PowerShell 测试 TCP 端口：

```powershell
Test-NetConnection 127.0.0.1 -Port 9999
```

手动启动 MCP 服务器：

```powershell
Set-Location 'C:\path\to\unity-inspector-mcp'
npm install
node wrapper.js
```

`index.js` 是核心 MCP 服务器，`wrapper.js` 是推荐的客户端入口。wrapper 会转发 MCP 请求，并提供 `restart_server` 工具，用于在修改 `index.js` 后重启服务器而无需重启客户端。

当 Codex 或其他 MCP 客户端已配置此服务器时，请勿手动启动第二个副本；客户端会自动启动 stdio 服务器。

更新 `index.js` 或 `unity-connection.js` 后，对使用该目录的每个 MCP 服务调用 `restart_server`。客户端仍未展示新增工具时，刷新工具列表或重连该服务。更新 `wrapper.js` 或持久环境变量需重启对应客户端服务。游戏端 DLL 则须在退出游戏后部署，重新启动游戏生效；`restart_server` 只重载 Node.js 服务。

### 运行时切换连接

游戏面板更改端口后，调用 MCP 工具 `configure_connection`，例如：

```json
{"host":"127.0.0.1","port":19999,"token":"","testConnection":true}
```

可分别修改 `host`、`port`、`token` 和 `connectTimeoutMs`（100–30000ms）；省略的字段保留原值。工具会断开旧连接并默认发送 `ping` 检查新目标，返回 `reachable` 和失败原因。设置 `testConnection:false` 则只更新配置。`connection_status` 可查看目标、连接状态和待处理请求数，不会返回口令；其中 `connected` 只表示 TCP 状态，实际可用性以 `ping` 或 `reachable` 为准。运行时修改在当前 MCP 进程内有效，调用 `restart_server` 后恢复环境变量配置。

连接使用唯一请求 ID 和统一的 UTF-8 单行 JSON 解码器，支持并发与 TCP 分包/合包；迟到响应不会交给新请求。不再根据本机进程轮询断言游戏崩溃，连接失败会显示目标地址和端口。连接超时默认为 5 秒；常规命令为 7 秒，代码/脚本编译执行为 35 秒，截图为 10 秒，卸载为 17 秒。

### 远程 Windows 客户端

可通过环境变量更改 MCP 目标。当 Broforce 进程运行在另一台受信任的局域网机器上时很有用：

```text
UNITY_INSPECTOR_HOST=192.0.2.10
UNITY_INSPECTOR_PORT=9999
UNITY_INSPECTOR_TOKEN=<与游戏面板一致的口令>
UNITY_INSPECTOR_PROCESS_SCOPE=remote
UNITY_INSPECTOR_UMM_LOG_PATH=\\192.0.2.10\game-share\Broforce\profiles\Default\UMM\Core\Log.txt
```

远程机器上必须启用 Unity Inspector Mod，在面板勾选“允许局域网/穿透连接”、设置口令并应用。默认只监听 `127.0.0.1`，启用远程后监听 `0.0.0.0`；转发协议应选择 TCP。口令必须与 MCP 一致，本机默认可不设口令。认证不提供加密，跨公网使用时建议通过加密隧道。

`configure_connection` 只修改调试连接，不更改进程管理范围、启动任务或日志路径。远程启动仍需预先配置远程任务；远程目标下 `stop_game` 会提示在目标机器关闭游戏，不会结束本机游戏，也不支持自动远程重启。端口转发到本机回环地址时，用 `UNITY_INSPECTOR_PROCESS_SCOPE=remote` 标明远程进程。远程计划任务的配置见 [内网游戏端启动指南](docs/三方图联机进入与内网游戏端启动方案-2026-10-06.md)。

运行 `npm test` 可执行通信和 stdio 工具集成测试，无需启动游戏。

### 添加到 Claude Desktop

<details>
<summary>点击展开配置</summary>

**Windows：** `%APPDATA%\Claude\claude_desktop_config.json`

**macOS：** `~/Library/Application Support/Claude/claude_desktop_config.json`

**Linux：** `~/.config/claude/claude_desktop_config.json`

```json
{
  "mcpServers": {
    "unity-inspector": {
      "command": "node",
      "args": ["/path/to/unity-inspector-mcp/wrapper.js"],
      "cwd": "/path/to/unity-inspector-mcp"
    }
  }
}
```

将 `/path/to/unity-inspector-mcp` 替换为实际目录路径。

**WSL 用户使用 Windows 路径格式：**
```json
{
  "mcpServers": {
    "unity-inspector": {
      "command": "wsl",
      "args": [
        "node",
        "/mnt/c/path/to/unity-inspector-mcp/wrapper.js"
      ],
      "cwd": "C:\\path\\to\\unity-inspector-mcp"
    }
  }
}
```

</details>

### Codex 配置

<details>
<summary>点击展开配置</summary>

在 `%USERPROFILE%\.codex\config.toml` 中添加 stdio 服务器条目，然后重启 Codex 或打开新对话：

```toml
[mcp_servers.unity_inspector]
command = "node"
args = ["C:\\path\\to\\unity-inspector-mcp\\wrapper.js"]
cwd = "C:\\path\\to\\unity-inspector-mcp"
startup_timeout_sec = 120
type = "stdio"
```

然后让客户端使用 `unity_inspector`，从 `ping` 和 `game_state` 开始。

对于第二个远程目标，复制条目并修改服务器名称，在其环境变量中设置 `UNITY_INSPECTOR_HOST`、`UNITY_INSPECTOR_PORT`、`UNITY_INSPECTOR_TOKEN`、`UNITY_INSPECTOR_PROCESS_SCOPE=remote`；需要读取远端日志时再配置 `UNITY_INSPECTOR_UMM_LOG_PATH`。工具将暴露在对应的 MCP 服务器名称下，例如 `unity_inspector_remote`。本机和远端分别调用各自的连接配置工具。

</details>

## 可用工具

### 连接与服务管理

- `connection_status` - 查看当前目标、TCP 连接状态和口令是否已配置，不主动连接游戏
- `configure_connection` - 修改当前 MCP 服务的目标地址、端口、口令或连接超时，默认测试连接
- `restart_server` - 重载 Node.js 服务并重新读取进程环境变量；由 `wrapper.js` 提供
- `launch_game` - 本机 Windows 通过桌面 `Broforce.url` 启动；配置远程计划任务后可启动目标机器上的游戏
- `stop_game` - 终止本机游戏；远程目标需在目标机器关闭

### 检查工具
- `ping` - 测试与 Unity Inspector 的连接
- `game_state` - 读取场景、关卡、模式、时间缩放和玩家摘要
- `wait_for_game` - 等待 TCP 服务器响应
- `list_gameobjects` - 列出场景中的所有游戏对象
- `inspect_gameobject` - 按路径检查特定游戏对象
- `query_gameobjects` - 按名称或组件搜索游戏对象
- `inspect_player` - 获取玩家的详细信息
- `list_enemies` - 列出场景中的所有敌人
- `inspect_component` - 检查游戏对象上的特定组件

### 修改工具
- `modify_component` - 修改组件的属性
- `teleport_player` - 将玩家传送到指定坐标
- `set_player_health` - 设置玩家的生命值
- `set_game_speed` - 设置游戏速度（时间缩放）
- `spawn_entity` - 在指定位置生成实体，当前实现有限

### 关卡控制
- `list_campaigns` - 列出所有可用的战役
- `go_to_level` - 直接跳转到特定战役关卡
- `restart_level` - 从头重开当前关卡

### 交互工具
- `simulate_input` - 模拟键盘/控制器输入
- `execute_code` - 在 Unity 上下文中执行 C# 表达式
- `take_screenshot` - 截取游戏截图
- `list_bros` - 列出官方角色及玩家当前角色
- `swap_bro` - 立即切换当前官方角色；会杀死当前角色并在原地重新生成
- `set_bro` - 设置下一次死亡复活或进入关卡时使用的官方角色
- `list_bromaker_bros` - 列出 BroMaker 自定义角色及已协商的双端可用性
- `switch_bromaker_bro` - 通过 BroMaker 的 `Switch to Bro` 加载路径切换当前角色；参数为 `broName` 和 `playerNum`（0–3），仅允许当前端拥有的活动玩家

BroMaker 命令依赖包含对应游戏端处理器的 Inspector DLL，以及现有 BroMaker/RocketLib 联网接入；不经过 Swap Bros API，也不以原生 `HeroType` 选择自定义角色。先从 `game_state` 的 `[local]` 和玩家归属确认槽位，切换后再从另一端核对角色和归属。本批只验证 Drunken Broster 的双端切换，不代表其他角色、解锁规则或技能行为均已验证。若当前客户端未刷新工具列表，可用 `run_test_script` 转发同名游戏端命令。

### 测试自动化
- `list_test_scripts` - 列出所有可用的测试脚本
- `run_test_script` - 执行包含一系列命令的测试脚本
- `list_scripts` - 列出 C# 运行时脚本库
- `compile_script` - 编译 C# 运行时脚本（不执行）
- `execute_script` - 执行 C# 运行时脚本
- `unload_script` - 卸载活动的运行时脚本
- `read_log` - 读取配置的 UMM 日志文件
- `watch_log` - 读取自上次调用以来的新日志条目

## 架构

```
[AI 客户端] <--MCP--> [MCP 服务器 (Node.js)] <--TCP--> [Unity Inspector Mod] <--> [Unity 游戏]
```

MCP 服务器充当 AI 客户端与 Unity 游戏之间的桥梁，将 MCP 工具调用转换为 Unity Inspector Mod 能理解的 TCP 命令。

## 故障排除

### 连接问题

如果在 WSL 中运行且无法连接，MCP 会尝试检测 Windows 主机 IP，也可用 `ip route show default` 核对。新版游戏默认仅监听 Windows 回环地址，不能直接通过主机网卡 IP 访问；可在 Windows 运行 Node.js 连接 `127.0.0.1`，或在游戏面板允许局域网连接、设置口令，再配置对应主机 IP 和口令。

### 服务器无响应

1. 调用对应端点的 `connection_status` 核对目标地址和端口。
2. 检查目标游戏已运行、Unity Inspector Mod 已启用，面板显示服务“运行中”；未运行时点击“启动服务”。
3. 按面板实际端口配置 MCP；`9999` 是默认值。远程连接还需允许局域网连接并配置相同口令。
4. 调用 `configure_connection` 测试新配置，或用 `ping` 验证；`Authentication failed` 应核对口令，TCP 连接成功不代表认证成功。

新版不再通过本机进程轮询报告 `Game process died`。旧服务若仍返回该提示，先重载服务并用同一端点的 `ping`、`game_state` 复核，不凭该提示判定崩溃。命令超时可能发生在操作已开始之后，重试会改变状态的操作前先检查游戏状态。

`read_log` 和 `watch_log` 优先读取 `UNITY_INSPECTOR_UMM_LOG_PATH`，未设置或路径不可访问时尝试内置的 `Default` profile 路径。使用 `profiles\Broforce` 等自定义 profile 或远端目标时，应将该环境变量设置为 MCP 进程可读取的实际 UMM 日志路径，也可直接查看：

```text
<r2modman 配置文件>\UMM\Core\Log.txt
```

Unity Inspector Mod 也会将其错误写入该 UMM 日志。MCP 连接消息写入 MCP 进程的标准错误流，通常由 MCP 客户端显示，而不是保存为单独的游戏日志。

默认情况下，MCP 连接同一台机器上的 Broforce。远端目标应按上文配置地址、端口、口令和进程范围；日志路径独立配置，不会随 `configure_connection` 自动切换。远程游戏退出后，MCP 无法继续检查该实例。

## 测试脚本

测试脚本允许你自动化一系列命令，用于可重复的调试和测试。当你需要在代码更改后重复测试同一场景时，这非常有用。

### 创建测试脚本

测试脚本是存储在 `scripts/` 目录中的 JSON 文件。步骤中的 `command` 会直接发送给游戏端处理器。`configure_connection`、`connection_status`、`restart_server` 等 Node.js 工具需单独调用，不能作为 JSON 测试步骤；完整说明见 [测试脚本指南](scripts/README.md)。

```json
{
  "name": "我的测试脚本",
  "description": "描述此脚本测试的内容",
  "steps": [
    {
      "command": "go_to_level",
      "params": {
        "campaignIndex": 0,
        "levelIndex": 0
      },
      "wait": 3000
    },
    {
      "command": "simulate_input",
      "params": {
        "action": "right",
        "duration": 1000
      },
      "wait": 500
    },
    {
      "command": "take_screenshot"
    }
  ]
}
```

### 脚本格式

- **name**：脚本的人类可读名称
- **description**：脚本测试或演示的内容
- **steps**：按顺序执行的命令数组
  - **command**：游戏端支持的 TCP 命令名称
  - **params**：命令的参数（可选）
  - **wait**：命令完成后等待的毫秒数（可选）

### 使用测试脚本

1. **列出可用脚本：**
   ```
   使用 list_test_scripts 工具
   ```

2. **运行脚本：**
   ```
   使用 run_test_script 并指定脚本名称（例如 "movement-test"）
   或脚本文件的绝对路径
   ```

3. **查看示例脚本：**
   查看 `scripts/examples/` 中的示例测试脚本

脚本执行器将按顺序运行每个步骤，等待指定的延迟，并报告详细结果，包括每个步骤的成功/失败状态和执行时间。

## 开发

要修改可用工具，请编辑：
- `index.js` - MCP 服务器实现和测试脚本执行
- [`UnityInspectorMod/src/MessageHandler.cs`](../../UnityInspectorMod/src/MessageHandler.cs) - Unity 端的消息处理

面向开发者的指南（架构、热重载、约束、如何添加工具）请参阅 [AGENTS.md](AGENTS.md)（英文版：[AGENTS.en.md](AGENTS.en.md)）。
