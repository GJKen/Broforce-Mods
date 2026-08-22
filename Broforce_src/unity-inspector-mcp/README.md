# Unity Inspector MCP Server

> [English](README.en.md)

一个 MCP（模型上下文协议）服务器，允许 AI 客户端检查并与运行 Unity Inspector Mod 的 Unity 游戏进行交互。

## 前置条件

1. Unity Inspector Mod 已安装并在你的 Unity 游戏（Broforce）中运行
2. Node.js 18+ 已安装
3. Unity 游戏必须正在运行，且 TCP 服务器已启动（端口 9999）

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

本仓库不包含 `test_tcp.py` 脚本。手动启动 MCP 服务器：

```powershell
Set-Location 'C:\path\to\unity-inspector-mcp'
npm install
node wrapper.js
```

`index.js` 是核心 MCP 服务器，`wrapper.js` 是推荐的客户端入口。wrapper 会转发 MCP 请求，并提供 `restart_server` 工具，用于在修改 `index.js` 后重启服务器而无需重启客户端。

当 Codex 或其他 MCP 客户端已配置此服务器时，请勿手动启动第二个副本；客户端会自动启动 stdio 服务器。

### 远程 Windows 客户端

可通过环境变量更改 MCP 目标。当 Broforce 进程运行在另一台受信任的局域网机器上时很有用：

```text
UNITY_INSPECTOR_HOST=192.0.2.10
UNITY_INSPECTOR_PORT=9999
UNITY_INSPECTOR_UMM_LOG_PATH=\\192.0.2.10\game-share\Broforce\profiles\Default\UMM\Core\Log.txt
```

远程机器上必须启用 Unity Inspector Mod，且其 TCP 服务器必须监听局域网接口。该服务没有身份验证，因此仅允许监控机器访问 TCP 9999，不要暴露在受信任的局域网之外。

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

WSL 用户使用 Windows 路径格式：
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

对于第二个远程目标，复制条目并修改服务器名称，添加上述三个环境变量。工具将暴露在对应的 MCP 服务器名称下，例如 `unity_inspector_remote`。

</details>

## 可用工具

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

### 关卡控制
- `list_campaigns` - 列出所有可用的战役
- `go_to_level` - 直接跳转到特定战役关卡

### 交互工具
- `simulate_input` - 模拟键盘/控制器输入
- `execute_code` - 在 Unity 上下文中执行 C# 表达式
- `take_screenshot` - 截取游戏截图

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

如果在 WSL 中运行且无法连接：
- 服务器会自动检测 WSL 并使用 Windows 主机 IP
- 可以手动测试：`ip route show default` 获取 Windows IP

### 服务器无响应

1. 检查 Unity 游戏是否正在运行
2. 检查 Unity Inspector Mod 是否已加载
3. 如果未运行，在 Mod 的 UI 中点击"Start Server"
4. 默认端口为 9999

MCP 服务器目前搜索默认的 r2modman 配置文件路径以使用 `read_log` 和 `watch_log`。如果你使用自定义配置文件，例如 `profiles\Broforce`，请直接在以下位置验证实际的 UMM 日志：

```text
<r2modman 配置文件>\UMM\Core\Log.txt
```

Unity Inspector Mod 也会将其错误写入该 UMM 日志。MCP 连接消息写入 MCP 进程的标准错误流，通常由 MCP 客户端显示，而不是保存为单独的游戏日志。

默认情况下，MCP 连接同一台机器上的 Broforce 进程。第二个 MCP 服务器实例可以通过 `UNITY_INSPECTOR_HOST`、`UNITY_INSPECTOR_PORT` 和 `UNITY_INSPECTOR_UMM_LOG_PATH` 定位受信任的局域网客户端。远程主机退出后，MCP 无法检查该已退出客户端的游戏状态。

## 测试脚本

测试脚本允许你自动化一系列命令，用于可重复的调试和测试。当你需要在代码更改后重复测试同一场景时，这非常有用。

### 创建测试脚本

测试脚本是存储在 `scripts/` 目录中的 JSON 文件：

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
  - **command**：任何可用的 MCP 工具名称
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
- `BroforceMods/Unity Inspector Mod/MessageHandler.cs` - Unity 端的消息处理

面向开发者的指南（架构、热重载、约束、如何添加工具）请参阅 [AGENTS.md](AGENTS.md)（英文版：[AGENTS.en.md](AGENTS.en.md)）。
