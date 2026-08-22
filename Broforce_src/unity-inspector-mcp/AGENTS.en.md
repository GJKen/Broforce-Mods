# unity-inspector-mcp — Development Guide

Node.js MCP server that bridges AI clients to the Unity Inspector Mod running inside Broforce.

## Architecture

```
AI Client --stdio/MCP--> wrapper.js --stdio--> index.js --TCP:9999--> Unity Inspector Mod(C#) --> Unity Game
```

- `wrapper.js`: Hot-reload proxy. Forwards MCP messages between the AI client and `index.js`, injects a `restart_server` tool.
- `index.js`: The actual MCP server with all tool implementations.
- Some tools are handled entirely in Node.js (file operations, process management); the rest are forwarded to the game via TCP.

## Key Constraints

- **stdio is the MCP transport channel**: No child process may write to stdout. Always use `spawn` with `stdio: "ignore"` for child processes; never use `exec()`, which inherits stdio and corrupts MCP transport.
- **TCP Protocol**: Request/response is single-line JSON: `{"id":"...","method":"...","params":{...}}` → `{"id":"...","success":true,"result":{...}}`.
- **id null-check**: MCP uses `id: 0` for the init message. Always check message ids with `!== undefined && !== null` — never use truthiness checks.

## Hot Reload (wrapper.js)

- `restart_server` tool: kills the `index.js` child process, restarts it, replays the MCP `initialize` handshake — the client never notices the swap.
- After modifying `index.js`, call `restart_server` to apply changes without restarting the client.
- `wrapper.js` itself is thin (~200 lines); modifying it requires a client restart.

## Adding a New MCP Tool

1. Add tool definition (schema + description) in the `ListToolsRequestSchema` handler.
2. Add a `case` in the `CallToolRequestSchema` handler switch.
3. If it needs game access: `result = await unityClient.sendCommand("method_name", params)`.
4. If Node.js-only (file ops, process management): implement directly, no `sendCommand`.
5. Add the corresponding handler in `BroforceMods/Unity Inspector Mod/Unity Inspector Mod/MessageHandler.cs`.

## Timeout Conventions

Default TCP timeout is 2 seconds. Override in `sendCommand()` for slow operations:
- `execute_script` / `compile_script`: 30 seconds
- `unload_script`: 10 seconds
- `simulate_input` with count: count × interval

## Crash Detection

While waiting for a response, `sendCommand` polls `pgrep` every 500ms. If the game process dies, it immediately returns "Game process died — the command likely caused a crash" instead of waiting for a timeout.

## C# Script System (Node.js Side)

- **Script library**: `scripts/csharp/` — version-controlled `.cs` files with metadata in header comments.
- **Metadata parsing**: `parseScriptMetadata()` reads `// #name`, `// #description`, `// #tags`, `// #args` from file headers.
- **`list_scripts`**: Node.js only — scans `scripts/csharp/`, parses metadata, returns a catalog.
- **`execute_script` / `compile_script`**: Node.js reads the file content and sends the source over TCP (not the file path — the game may run under Proton with a different filesystem).
- **Dynamic tool description**: The `execute_script` tool's description includes the current script catalog, rebuilt on each `ListToolsRequestSchema` call.

## Game Process Management

- `launch_game`: `spawn` to start `bf` (detached stdio), polls for TCP connectivity.
- `stop_game`: terminates via `pkill`.
- Both use `spawn` with `stdio: "ignore"`.