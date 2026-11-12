# unity-inspector-mcp — Development Guide

Node.js MCP server that bridges AI clients to the Unity Inspector Mod running inside Broforce.

## Architecture

```
AI Client --stdio/MCP--> wrapper.js --stdio--> index.js --TCP:9999--> Unity Inspector Mod(C#) --> Unity Game
```

- `wrapper.js`: Hot-reload proxy. Forwards MCP messages between the AI client and `index.js`, injects a `restart_server` tool.
- `index.js`: The actual MCP server with all tool implementations.
- `unity-connection.js`: TCP connections and request routing, including unique IDs, a shared connection promise, UTF-8 JSON lines, late-response handling, timeouts and runtime configuration.
- Some tools are handled entirely in Node.js (file operations, process management); the rest are forwarded to the game via TCP.

## Key Constraints

- **stdio is the MCP transport channel**: Apart from protocol forwarding in `wrapper.js`, game launch and process-management output must not reach MCP stdout. Use `spawn` with `stdio: "ignore"` or capture output with `execFile`; send diagnostics to stderr.
- **TCP Protocol**: Request/response is single-line JSON: `{"id":"...","method":"...","params":{...}}` → `{"id":"...","success":true,"result":{...}}`.
- **id null-check**: MCP uses `id: 0` for the init message. Always check message ids with `!== undefined && !== null` — never use truthiness checks.

## Hot Reload (wrapper.js)

- `restart_server` tool: kills the `index.js` child process, restarts it, replays the MCP `initialize` handshake — the client never notices the swap.
- After modifying `index.js` or `unity-connection.js`, call `restart_server` on each MCP service using this directory. This discards temporary `configure_connection` settings and reloads the process environment variables.
- The wrapper sends a tool-list change notification. If new tools remain unavailable, refresh the tool list or reconnect that MCP service.
- Modifying `wrapper.js` requires a client restart.
- Stop the game before deploying an updated game-side DLL, then relaunch it. `restart_server` only restarts the Node.js service.

## Adding a New MCP Tool

1. Add tool definition (schema + description) in the `ListToolsRequestSchema` handler.
2. Add a `case` in the `CallToolRequestSchema` handler switch.
3. If it needs game access: `result = await unityClient.sendCommand("method_name", params)`.
4. If Node.js-only (file ops, process management): implement directly, no `sendCommand`.
5. Add the corresponding game-side handler in [`UnityInspectorMod/src/MessageHandler.cs`](../../UnityInspectorMod/src/MessageHandler.cs).

## Timeout Conventions

Connection timeout defaults to 5 seconds; ordinary commands use 7 seconds. Budgets are defined by `commandTimeout()` in `unity-connection.js`:
- `execute_code` / `execute_script` / `compile_script`: 35 seconds
- `unload_script`: 17 seconds; `take_screenshot`: 10 seconds
- `simulate_input` with count: `(count - 1) × interval + 7000ms`, capped at 125 seconds
- Game-side queue waits allow 5 seconds for ordinary tasks and 30 seconds for script tasks. A timeout cancels unstarted work; an executing Unity operation cannot be forcibly interrupted.

## Crash Detection

The transport no longer polls local processes to infer a game crash. Report TCP connection failures and command timeouts as observed; a remote disconnect alone does not establish a crash.

## Connection Configuration and Authentication

- `configure_connection` updates host/port/token and `connectTimeoutMs` (100–30000ms), testing with `ping` by default. Set `testConnection:false` to configure without connecting. `connection_status` neither connects nor returns the token. Its `connected` field describes TCP state; verify usability with `ping` or `reachable`. Changes last only for the current process.
- Persistent configuration uses `UNITY_INSPECTOR_HOST`, `UNITY_INSPECTOR_PORT` and `UNITY_INSPECTOR_TOKEN`. The game listens on loopback by default; remote listening requires a token.
- Requests may carry a top-level `token`. Never print it or full request source in logs or status results. The game permits 8 connections and 1 MiB requests; the client permits 64 pending requests.
- `npm test` runs transport and actual stdio tool integration tests without the game.

## C# Script System (Node.js Side)

- **Script library**: `scripts/csharp/` — version-controlled `.cs` files with metadata in header comments.
- **Metadata parsing**: `parseScriptMetadata()` reads `// #name`, `// #description`, `// #tags`, `// #args` from file headers.
- **`list_scripts`**: Node.js only — scans `scripts/csharp/`, parses metadata, returns a catalog.
- **`execute_script` / `compile_script`**: Node.js reads the file content and sends the source over TCP (not the file path — the game may run under Proton with a different filesystem).
- **Dynamic tool description**: The `execute_script` tool's description includes the current script catalog, rebuilt on each `ListToolsRequestSchema` call.

## Game Process Management

- Local Windows `launch_game` opens the desktop `Broforce.url` through `explorer.exe`; override the shortcut with `UNITY_INSPECTOR_LOCAL_LAUNCH_URL`. Non-Windows local launches use `bf`. The tool waits for a successful target `ping`.
- Remote `launch_game` uses `schtasks.exe` after configuring `UNITY_INSPECTOR_REMOTE_LAUNCH_HOST` and `UNITY_INSPECTOR_REMOTE_LAUNCH_TASK`. Optional credentials use `UNITY_INSPECTOR_REMOTE_LAUNCH_USER` / `UNITY_INSPECTOR_REMOTE_LAUNCH_PASSWORD`. Changing the TCP target alone does not configure remote launch.
- Local `stop_game` uses `taskkill` on Windows and `pkill` elsewhere. Remote termination and automatic remote restart are unsupported; stop the game on the target machine.
- Set `UNITY_INSPECTOR_PROCESS_SCOPE=remote` for remote processes, especially through a loopback tunnel. `configure_connection` does not change process scope, launch tasks or log paths.
- `read_log` / `watch_log` prefer `UNITY_INSPECTOR_UMM_LOG_PATH`, then try built-in `Default` profile paths if it is unset or inaccessible.
