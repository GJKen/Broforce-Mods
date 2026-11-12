# Unity Inspector MCP Server

> [中文](README.md)

An MCP (Model Context Protocol) server that allows AI clients to inspect and interact with Unity games running the Unity Inspector Mod.

## Prerequisites

1. Unity Inspector Mod installed; enable it in Broforce before using game-side tools
2. Node.js 18+ installed
3. Game-side tools require a running game and TCP listener. Match the MCP host, port and token to the game panel (default `127.0.0.1:9999`). `connection_status` and offline tests do not require the game.

## Installation

```bash
npm install
```

## Usage

### Testing the connection

First, make sure the Unity Inspector Mod's TCP server is running in Broforce. The default port is `9999`.

On Windows, test the TCP port with PowerShell:

```powershell
Test-NetConnection 127.0.0.1 -Port 9999
```

For a manual MCP server start, use:

```powershell
Set-Location 'C:\path\to\unity-inspector-mcp'
npm install
node wrapper.js
```

`index.js` is the core MCP server. `wrapper.js` is the recommended client entry point: it forwards MCP requests and provides the `restart_server` tool, which restarts the server after `index.js` changes without restarting the client.

When Codex or another MCP client is configured with this server, do not start a second copy manually; the client starts the stdio server itself.

After updating `index.js` or `unity-connection.js`, call `restart_server` on each MCP service using this directory. Refresh the tool list or reconnect the service if new tools remain unavailable. Changes to `wrapper.js` or persistent environment variables require restarting the corresponding client service. Stop the game before deploying an updated DLL, then relaunch it; `restart_server` only reloads Node.js.

### Runtime connection settings

After changing the game port, call the MCP `configure_connection` tool, for example:

```json
{"host":"127.0.0.1","port":19999,"token":"","testConnection":true}
```

Set any of `host`, `port`, `token` and `connectTimeoutMs` (100–30000ms); omitted fields retain their values. This disconnects the old socket and tests the new target with `ping`, reporting `reachable` and any error. Set `testConnection:false` to configure without connecting. `connection_status` reports the target, connection state and pending request count without revealing the token. Its `connected` field describes TCP state; verify usability with `ping` or `reachable`. Runtime changes are lost on `restart_server`, which restores the process environment configuration.

The transport routes unique request IDs through one UTF-8 JSON line decoder, supporting concurrent requests and TCP fragmentation/coalescing while ignoring late replies. It does not infer a game crash from local process polling. Default timeouts are 5 seconds for connecting, 7 for ordinary commands, 35 for code/script compilation and execution, 10 for screenshots, and 17 for script unload.

### Remote Windows client

The MCP target can be changed with environment variables. This is useful when the Broforce process runs on another trusted LAN machine:

```text
UNITY_INSPECTOR_HOST=192.0.2.10
UNITY_INSPECTOR_PORT=9999
UNITY_INSPECTOR_TOKEN=<same token as the game panel>
UNITY_INSPECTOR_PROCESS_SCOPE=remote
UNITY_INSPECTOR_UMM_LOG_PATH=\\192.0.2.10\game-share\Broforce\profiles\Default\UMM\Core\Log.txt
```

Enable the Mod on the remote machine, select `Allow LAN/tunnel connections`, enter an access token and apply. The default listener is local-only at `127.0.0.1`; remote mode listens on `0.0.0.0` and requires a matching token. Use TCP for port forwarding. Local connections may omit the token. Authentication does not encrypt traffic; use an encrypted tunnel across the Internet.

`configure_connection` only changes the debugging connection, not process scope, launch tasks or log paths. Remote launch still requires a configured remote task. For a remote target, `stop_game` reports that the game must be stopped on that machine; automatic remote restart is also unsupported. Use `UNITY_INSPECTOR_PROCESS_SCOPE=remote` when a loopback tunnel forwards to a remote game. See the [remote launch guide (Chinese)](docs/三方图联机进入与内网游戏端启动方案-2026-10-06.md) for scheduled-task setup.

Run `npm test` for transport and stdio tool integration tests without starting the game.

### Adding to Claude Desktop

<details>
<summary>Click to expand config</summary>

**Windows:** `%APPDATA%\Claude\claude_desktop_config.json`

**macOS:** `~/Library/Application Support/Claude/claude_desktop_config.json`

**Linux:** `~/.config/claude/claude_desktop_config.json`

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

Replace `/path/to/unity-inspector-mcp` with the actual path to this directory.

**For WSL users, use the Windows path format:**
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

### Codex configuration

<details>
<summary>Click to expand config</summary>

Add a stdio server entry to `%USERPROFILE%\.codex\config.toml` and restart Codex or open a new conversation:

```toml
[mcp_servers.unity_inspector]
command = "node"
args = ["C:\\path\\to\\unity-inspector-mcp\\wrapper.js"]
cwd = "C:\\path\\to\\unity-inspector-mcp"
startup_timeout_sec = 120
type = "stdio"
```

Then ask the client to use `unity_inspector`, starting with `ping` and `game_state`.

For a second remote target, duplicate the entry with a different server name and set `UNITY_INSPECTOR_HOST`, `UNITY_INSPECTOR_PORT`, `UNITY_INSPECTOR_TOKEN` and `UNITY_INSPECTOR_PROCESS_SCOPE=remote` in its environment. Add `UNITY_INSPECTOR_UMM_LOG_PATH` if remote logs are needed. Tools are exposed under that server name, for example `unity_inspector_remote`; configure each target through its own tools.

</details>

## Available Tools

### Connection and Service Management

- `connection_status` - Read the target, TCP state and whether a token is configured without connecting to the game
- `configure_connection` - Change this MCP service's host, port, token or connect timeout, testing by default
- `restart_server` - Reload Node.js and restore process environment settings; provided by `wrapper.js`
- `launch_game` - Open the local Windows desktop `Broforce.url`, or trigger a configured remote scheduled task
- `stop_game` - Stop the local game; remote games must be stopped on the target machine

### Inspection Tools
- `ping` - Test connection to Unity Inspector
- `game_state` - Read scene, level, mode, time scale, and player summary
- `wait_for_game` - Wait for the TCP server to become responsive
- `list_gameobjects` - List all GameObjects in the scene
- `inspect_gameobject` - Inspect a specific GameObject by path
- `query_gameobjects` - Search for GameObjects by name or component
- `inspect_player` - Get detailed information about the player(s)
- `list_enemies` - List all enemies in the scene
- `inspect_component` - Inspect a specific component on a GameObject

### Modification Tools
- `modify_component` - Modify properties of a component
- `teleport_player` - Teleport the player to specific coordinates
- `set_player_health` - Set the player's health
- `set_game_speed` - Set the game speed (time scale)
- `spawn_entity` - Spawn an entity at a position; implementation is limited

### Level Control
- `list_campaigns` - List all available campaigns
- `go_to_level` - Go directly to a specific campaign level
- `restart_level` - Restart the current level from the beginning

### Interaction Tools
- `simulate_input` - Simulate keyboard/controller input
- `execute_code` - Execute C# expressions in the Unity context
- `take_screenshot` - Take a screenshot of the game
- `list_bros` - List official bros and current player selections
- `swap_bro` - Change the active official bro by killing and respawning the current character at the same position
- `set_bro` - Select the official bro for the next respawn or level
- `list_bromaker_bros` - List custom BroMaker characters and negotiated peer availability
- `switch_bromaker_bro` - Load a custom character through BroMaker's `Switch to Bro` path using `broName` and `playerNum` (0–3); requires an active player owned by this client

BroMaker commands require the corresponding game-side handlers and BroMaker/RocketLib networking support. Check the player slot and ownership with `game_state`, then verify the result from the other peer. Existing validation covers Drunken Broster switching between two peers; it does not establish support for every character, unlock rule or ability. If the client tool list has not refreshed, `run_test_script` can forward these game-side commands.

### Test Automation
- `list_test_scripts` - List all available test scripts
- `run_test_script` - Execute a test script with a sequence of commands
- `list_scripts` - List the C# runtime script library
- `compile_script` - Compile a C# runtime script without executing it
- `execute_script` - Execute a C# runtime script
- `unload_script` - Unload an active runtime script
- `read_log` - Read the configured UMM log file
- `watch_log` - Read new log entries since the previous call

## Architecture

```
[AI Client] <--MCP--> [MCP Server (Node.js)] <--TCP--> [Unity Inspector Mod] <--> [Unity Game]
```

The MCP server acts as a bridge between the AI client and the Unity game, translating MCP tool calls into TCP commands that the Unity Inspector Mod can understand.

## Troubleshooting

### Connection Issues

In WSL, the MCP attempts to detect the Windows host IP; check it with `ip route show default`. The updated game defaults to a Windows loopback listener, which is not reachable through the host network-interface IP. Run Node.js on Windows against `127.0.0.1`, or enable LAN listening in the game, set a token and configure the host IP and matching token.

### Server not responding

1. Use that target's `connection_status` to check the host and port.
2. Check that the target game is running, the Mod is enabled and its panel shows `Running`; click `Start Server` if stopped.
3. Match the actual panel port; `9999` is only the default. Remote access also requires LAN listening and a matching token.
4. Test with `configure_connection` or `ping`. For `Authentication failed`, check the token; a TCP connection alone does not prove authentication.

The updated transport no longer reports `Game process died` based on local process polling. If an old service still returns it, reload that service and verify with the same target's `ping` and `game_state`. A command timeout may occur after execution starts; check game state before repeating an operation that changes it.

`read_log` and `watch_log` prefer `UNITY_INSPECTOR_UMM_LOG_PATH`, then try built-in `Default` profile paths if unset or inaccessible. For a custom profile such as `profiles\Broforce` or a remote target, set this variable to the actual UMM log path readable by the MCP process, or inspect it directly:

```text
<r2modman profile>\UMM\Core\Log.txt
```

The Unity Inspector Mod also writes its own errors to that UMM log. MCP connection messages are written to the MCP process standard error stream and are normally shown by the MCP client rather than saved as a separate game log.

By default, MCP connects to Broforce on the same machine. Configure remote hosts, ports, tokens and process scope as described above. Log paths are independent and do not change with `configure_connection`. After the remote game exits, MCP cannot inspect that instance.

## Test Scripts

Test scripts allow you to automate sequences of commands for repeatable debugging and testing. This is useful when you need to test the same scenario repeatedly after making code changes.

### Creating Test Scripts

Test scripts are JSON files stored in the `scripts/` directory. Each step's `command` is sent directly to a game-side handler. Call Node.js tools such as `configure_connection`, `connection_status` and `restart_server` separately; they cannot be JSON test steps. See the [test script guide](scripts/README.md).

```json
{
  "name": "My Test Script",
  "description": "Description of what this tests",
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

### Script Format

- **name**: Human-readable name for the script
- **description**: What the script tests or demonstrates
- **steps**: Array of commands to execute in sequence
  - **command**: A TCP command supported by the game-side handler
  - **params**: Parameters for the command (optional)
  - **wait**: Milliseconds to wait after command completes (optional)

### Using Test Scripts

1. **List available scripts:**
   ```
   Use the list_test_scripts tool
   ```

2. **Run a script:**
   ```
   Use run_test_script with script name (e.g., "movement-test")
   or absolute path to a script file
   ```

3. **View example scripts:**
   Check `scripts/examples/` for sample test scripts

The script executor will run each step in sequence, wait for the specified delays, and report detailed results including success/failure status and execution time for each step.

## Development

To modify the available tools, edit:
- `index.js` - MCP server implementation and test script execution
- [`UnityInspectorMod/src/MessageHandler.cs`](../../UnityInspectorMod/src/MessageHandler.cs) - Unity-side message handling

For a developer-focused guide (architecture, hot reload, constraints, how to add tools), see [AGENTS.md](AGENTS.md) (English: [AGENTS.en.md](AGENTS.en.md)).
