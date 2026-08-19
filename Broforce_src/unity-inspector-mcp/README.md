# Unity Inspector MCP Server

An MCP (Model Context Protocol) server that allows Claude to inspect and interact with Unity games running the Unity Inspector Mod.

## Prerequisites

1. Unity Inspector Mod installed and running in your Unity game (Broforce)
2. Node.js 18+ installed
3. The Unity game must be running with the TCP server started (port 9999)

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

The repository does not include a `test_tcp.py` script. For a manual MCP server start, use:

```powershell
Set-Location 'D:\Study\C#\Broforce-Mods\Broforce_src\unity-inspector-mcp'
npm install
npm start
```

When Codex or another MCP client is configured with this server, do not start a second copy manually; the client starts the stdio server itself.

### Remote Windows client

The MCP target can be changed with environment variables. This is useful when the Broforce process runs on another trusted LAN machine:

```text
UNITY_INSPECTOR_HOST=192.168.1.181
UNITY_INSPECTOR_PORT=9999
UNITY_INSPECTOR_UMM_LOG_PATH=\\192.168.1.181\Epan\Games\Broforce Mods\Broforce\profiles\Broforce\UMM\Core\Log.txt
```

The Unity Inspector Mod must be enabled on the remote machine and its TCP server must listen on the LAN interface. The service has no authentication, so allow TCP 9999 only from the monitoring machine and do not expose it outside the trusted LAN.

### Adding to Claude Desktop

Add this to your Claude Desktop configuration file:

**Windows:** `%APPDATA%\Claude\claude_desktop_config.json`
**macOS:** `~/Library/Application Support/Claude/claude_desktop_config.json`
**Linux:** `~/.config/claude/claude_desktop_config.json`

```json
{
  "mcpServers": {
    "unity-inspector": {
      "command": "node",
      "args": ["/path/to/unity-inspector-mcp/index.js"],
      "cwd": "/path/to/unity-inspector-mcp"
    }
  }
}
```

Replace `/path/to/unity-inspector-mcp` with the actual path to this directory.

For WSL users, use the Windows path format:
```json
{
  "mcpServers": {
    "unity-inspector": {
      "command": "wsl",
      "args": [
        "node",
        "/mnt/c/Users/YOUR_USERNAME/repos/unity-inspector-mcp/index.js"
      ],
      "cwd": "C:\\Users\\YOUR_USERNAME\\repos\\unity-inspector-mcp"
    }
  }
}
```

### Codex configuration

For Codex, add a stdio server entry to `C:\Users\<YourName>\.codex\config.toml` and restart Codex or open a new conversation:

```toml
[mcp_servers.unity_inspector]
command = "node"
args = ["D:\\Study\\C#\\Broforce-Mods\\Broforce_src\\unity-inspector-mcp\\index.js"]
cwd = "D:\\Study\\C#\\Broforce-Mods\\Broforce_src\\unity-inspector-mcp"
startup_timeout_sec = 120
type = "stdio"
```

Then ask the client to use `unity_inspector`, starting with `ping` and `game_state`.

For a second remote target, duplicate the entry with a different server name and add the three environment variables above. The tools will then be exposed under the corresponding MCP server name, for example `unity_inspector_remote`.

## Available Tools

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

### Level Control
- `list_campaigns` - List all available campaigns
- `go_to_level` - Go directly to a specific campaign level

### Interaction Tools
- `simulate_input` - Simulate keyboard/controller input
- `execute_code` - Execute C# expressions in the Unity context
- `take_screenshot` - Take a screenshot of the game

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
[Claude Desktop] <--MCP--> [MCP Server (Node.js)] <--TCP--> [Unity Inspector Mod] <--> [Unity Game]
```

The MCP server acts as a bridge between Claude and the Unity game, translating MCP tool calls into TCP commands that the Unity Inspector Mod can understand.

## Troubleshooting

### Connection Issues

If running in WSL and can't connect:
- The server automatically detects WSL and uses the Windows host IP
- You can manually test with: `ip route show default` to get the Windows IP

### Server not responding

1. Check the Unity game is running
2. Check Unity Inspector Mod is loaded
3. Click "Start Server" in the mod's UI if not running
4. Default port is 9999

The MCP server currently searches the default r2modman profile paths for `read_log` and `watch_log`. If you use a custom profile such as `profiles\Broforce`, verify the actual UMM log directly at:

```text
<r2modman profile>\UMM\Core\Log.txt
```

The Unity Inspector Mod also writes its own errors to that UMM log. MCP connection messages are written to the MCP process standard error stream and are normally shown by the MCP client rather than saved as a separate game log.

By default, MCP connects to the Broforce process on the same machine. A second MCP server instance can target a trusted LAN client with `UNITY_INSPECTOR_HOST`, `UNITY_INSPECTOR_PORT`, and `UNITY_INSPECTOR_UMM_LOG_PATH`. After a remote host exits, MCP cannot inspect that exited client's state.

## Test Scripts

Test scripts allow you to automate sequences of commands for repeatable debugging and testing. This is useful when you need to test the same scenario repeatedly after making code changes.

### Creating Test Scripts

Test scripts are JSON files stored in the `scripts/` directory:

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
  - **command**: Any available MCP tool name
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
- `BroforceMods/Unity Inspector Mod/MessageHandler.cs` - Unity-side message handling
