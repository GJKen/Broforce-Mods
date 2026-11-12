# unity-inspector-mcp — 开发指南

Node.js MCP 服务器，桥接 AI 客户端与运行在 Broforce 内的 Unity Inspector Mod。

## 架构

```
AI 客户端 --stdio/MCP--> wrapper.js --stdio--> index.js --TCP:9999--> Unity Inspector Mod(C#) --> Unity 游戏
```

- `wrapper.js`：热重载代理。在 AI 客户端与 `index.js` 之间转发 MCP 消息，注入 `restart_server` 工具。
- `index.js`：实际的 MCP 服务器，包含所有工具实现。
- `unity-connection.js`：TCP 连接与请求分发；唯一 ID、共享连接 Promise、UTF-8 单行解码、迟到响应丢弃、超时及运行时连接配置。
- 部分工具仅在 Node.js 侧处理（文件操作、进程管理）；其余通过 TCP 转发给游戏。

## 关键约束

- **stdio 是 MCP 传输通道**：除 `wrapper.js` 的协议转发外，游戏启动、进程管理等子进程的输出不得混入 MCP stdout。使用 `spawn` 配合 `stdio: "ignore"`，或使用捕获输出的 `execFile`；诊断信息写 stderr。
- **TCP 协议**：请求/响应为单行 JSON，`{"id":"...","method":"...","params":{...}}` → `{"id":"...","success":true,"result":{...}}`。
- **id 判空**：MCP 的 init 消息使用 `id: 0`，判断消息 id 时必须用 `!== undefined && !== null`，不要用真值判断。

## 热重载（wrapper.js）

- `restart_server` 工具：杀死 `index.js` 子进程、重新拉起、重放 MCP `initialize` 握手，客户端无感知。
- 修改 `index.js` 或 `unity-connection.js` 后，对使用该目录的每个 MCP 服务调用 `restart_server`，无需重启客户端。重启会丢弃 `configure_connection` 的临时配置，并重新读取进程环境变量。
- wrapper 会发送工具列表变更通知；客户端仍未显示新增工具时，刷新工具列表或重连该 MCP 服务。
- `wrapper.js` 修改需重启客户端。
- 游戏端 DLL 更新必须先退出游戏再部署，重新启动游戏后生效；`restart_server` 只重启 Node.js 服务。

## 新增一个 MCP 工具

1. 在 `ListToolsRequestSchema` 处理器中添加工具定义（schema + description）。
2. 在 `CallToolRequestSchema` 处理器的 switch 中添加 `case`。
3. 需要访问游戏：`result = await unityClient.sendCommand("method_name", params)`。
4. 仅 Node.js 处理的（文件操作、进程管理）：直接实现，不走 `sendCommand`。
5. 在 [`UnityInspectorMod/src/MessageHandler.cs`](../../UnityInspectorMod/src/MessageHandler.cs) 添加对应游戏端处理器。

## 超时约定

连接超时默认 5 秒；普通命令 7 秒。预算定义在 `unity-connection.js` 的 `commandTimeout()`：
- `execute_code` / `execute_script` / `compile_script`：35 秒
- `unload_script`：17 秒；`take_screenshot`：10 秒
- 带 count 的 `simulate_input`：`(count - 1) × interval + 7000ms`，最长 125 秒
- 游戏端排队普通任务 5 秒、脚本任务 30 秒；等待超时取消未开始的任务，不能强行终止已执行的 Unity 操作。

## 崩溃检测

不再通过本机进程轮询推断游戏崩溃。仅根据 TCP 连接状态与命令超时报告错误，不能把远端断开直接解释为崩溃。

## 连接配置与认证

- `configure_connection` 可运行时更新 host/port/token 及 `connectTimeoutMs`（100–30000ms），默认通过 `ping` 测试；`testConnection:false` 只更新配置。`connection_status` 不连接游戏、不返回口令，`connected` 仅表示 TCP 连接状态，实际可用性以 `ping` 或 `reachable` 为准。修改只在当前进程内生效。
- 持久配置：`UNITY_INSPECTOR_HOST`、`UNITY_INSPECTOR_PORT`、`UNITY_INSPECTOR_TOKEN`。游戏端默认只监听回环地址，远程监听必须设置口令。
- 请求顶层可携带 `token`；禁止在日志或状态结果中打印该字段或完整请求源码。游戏端上限 8 连接、1 MiB 请求，客户端上限 64 待处理请求。
- `npm test` 运行传输及实际 stdio 工具集成测试，不需要游戏。

## C# 脚本系统（Node.js 侧）

- **脚本库**：`scripts/csharp/` — 托管源代码 `.cs` 文件，头部注释携带元数据。
- **元数据解析**：`parseScriptMetadata()` 读取文件头 `// #name`、`// #description`、`// #tags`、`// #args`。
- **`list_scripts`**：仅 Node.js — 扫描 `scripts/csharp/`，解析元数据，返回目录。
- **`execute_script` / `compile_script`**：Node.js 读取文件内容并通过 TCP 发送源码（而非文件路径；游戏可能运行在 Proton 下，文件系统不同）。
- **动态工具描述**：`execute_script` 的工具描述包含当前脚本目录，每次 `ListToolsRequestSchema` 调用时重建。

## 游戏进程管理

- Windows 本机 `launch_game`：通过 `explorer.exe` 打开桌面 `Broforce.url`，可用 `UNITY_INSPECTOR_LOCAL_LAUNCH_URL` 指定快捷方式；非 Windows 本机沿用 `bf`。启动后等待目标端 `ping` 成功。
- 远程 `launch_game`：配置 `UNITY_INSPECTOR_REMOTE_LAUNCH_HOST` 和 `UNITY_INSPECTOR_REMOTE_LAUNCH_TASK` 后通过 `schtasks.exe` 触发远程任务；可选凭据为 `UNITY_INSPECTOR_REMOTE_LAUNCH_USER` / `UNITY_INSPECTOR_REMOTE_LAUNCH_PASSWORD`。仅修改 TCP 目标不会配置远程启动。
- 本机 `stop_game`：Windows 使用 `taskkill`，非 Windows 使用 `pkill`。远程目标不支持该工具终止游戏，也不支持自动远程重启，需在目标机器关闭游戏。
- `UNITY_INSPECTOR_PROCESS_SCOPE=remote` 用于标明远程进程，尤其是隧道入口为本机回环地址时；`configure_connection` 不修改进程管理、启动任务或日志路径。
- `read_log` / `watch_log` 优先使用 `UNITY_INSPECTOR_UMM_LOG_PATH`，未配置或路径不可访问时才尝试内置的 `Default` profile 路径。
