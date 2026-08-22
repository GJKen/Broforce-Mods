# unity-inspector-mcp — 开发指南

Node.js MCP 服务器，桥接 AI 客户端与运行在 Broforce 内的 Unity Inspector Mod。

## 架构

```
AI 客户端 --stdio/MCP--> wrapper.js --stdio--> index.js --TCP:9999--> Unity Inspector Mod(C#) --> Unity 游戏
```

- `wrapper.js`：热重载代理。在 AI 客户端与 `index.js` 之间转发 MCP 消息，注入 `restart_server` 工具。
- `index.js`：实际的 MCP 服务器，包含所有工具实现。
- 部分工具仅在 Node.js 侧处理（文件操作、进程管理）；其余通过 TCP 转发给游戏。

## 关键约束

- **stdio 是 MCP 传输通道**：任何子进程都不得向 stdout 输出。启动子进程必须用 `spawn` 且 `stdio: "ignore"`，绝不能使用会继承 stdio 的 `exec()`。
- **TCP 协议**：请求/响应为单行 JSON，`{"id":"...","method":"...","params":{...}}` → `{"id":"...","success":true,"result":{...}}`。
- **id 判空**：MCP 的 init 消息使用 `id: 0`，判断消息 id 时必须用 `!== undefined && !== null`，不要用真值判断。

## 热重载（wrapper.js）

- `restart_server` 工具：杀死 `index.js` 子进程、重新拉起、重放 MCP `initialize` 握手，客户端无感知。
- 修改 `index.js` 后用 `restart_server` 生效，无需重启客户端。
- `wrapper.js` 本身很薄（约 200 行），修改它需重启客户端。

## 新增一个 MCP 工具

1. 在 `ListToolsRequestSchema` 处理器中添加工具定义（schema + description）。
2. 在 `CallToolRequestSchema` 处理器的 switch 中添加 `case`。
3. 需要访问游戏：`result = await unityClient.sendCommand("method_name", params)`。
4. 仅 Node.js 处理的（文件操作、进程管理）：直接实现，不走 `sendCommand`。
5. 在 `BroforceMods/Unity Inspector Mod/Unity Inspector Mod/MessageHandler.cs` 添加对应处理器。

## 超时约定

默认 TCP 超时 2 秒。慢操作在 `sendCommand()` 中覆盖：
- `execute_script` / `compile_script`：30 秒
- `unload_script`：10 秒
- 带 count 的 `simulate_input`：count × interval

## 崩溃检测

`sendCommand` 等待响应期间每 500ms 轮询 `pgrep`。若游戏进程死亡，立即返回 "Game process died — the command likely caused a crash"，而不是等超时。

## C# 脚本系统（Node.js 侧）

- **脚本库**：`scripts/csharp/` — 托管源代码 `.cs` 文件，头部注释携带元数据。
- **元数据解析**：`parseScriptMetadata()` 读取文件头 `// #name`、`// #description`、`// #tags`、`// #args`。
- **`list_scripts`**：仅 Node.js — 扫描 `scripts/csharp/`，解析元数据，返回目录。
- **`execute_script` / `compile_script`**：Node.js 读取文件内容并通过 TCP 发送源码（而非文件路径；游戏可能运行在 Proton 下，文件系统不同）。
- **动态工具描述**：`execute_script` 的工具描述包含当前脚本目录，每次 `ListToolsRequestSchema` 调用时重建。

## 游戏进程管理

- `launch_game`：`spawn` 启动 `bf`（detached stdio），轮询 TCP 连通性。
- `stop_game`：`pkill` 终止。
- 两者都用 `spawn` 且 `stdio: "ignore"`。