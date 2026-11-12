# Unity Inspector Mod

> [Chinese](README.md)

This directory contains the Unity Inspector Mod source code, build scripts, and local configuration templates. The Mod provides TCP inspection and runtime debugging interfaces for Broforce and is used by `unity-inspector-mcp`.

## Prerequisites

- Windows with Broforce and Unity Mod Manager (UMM) installed.
- An installed Unity Inspector Mod package containing `mcs.dll` and `Newtonsoft.Json.dll`. These runtime DLLs are not committed to this repository.
- If the dependency package is not in the current UMM profile's default Mods directory, set `InspectorModDependenciesPath` in the local configuration.

## Configuration and Build

From PowerShell at the repository root, run the following commands to create the local configuration and then fill in the actual paths:

```powershell
Set-Location '.\UnityInspectorMod'
Copy-Item .\LocalBroforcePath.props.example .\LocalBroforcePath.props
notepad .\LocalBroforcePath.props
```

`LocalBroforcePath.props` must contain `BroforceManagedPath` and `UnityModManagerPath`. If the script cannot find `mcs.dll` and `Newtonsoft.Json.dll` automatically, uncomment and fill in `InspectorModDependenciesPath`. This file contains machine-specific paths and must not be committed to the public repository.

After configuration, exit Broforce and confirm its process has stopped, then build and deploy:

```powershell
& .\BuildAndDeploy.ps1
```

The script will:

- Compile the Mod using the game and UMM DLLs referenced by `LocalBroforcePath.props`;
- Read `mcs.dll` and `Newtonsoft.Json.dll` from `InspectorModDependenciesPath` or an installed Unity Inspector Mod package;
- Generate a copyable Mod package in the `UnityInspectorMod` directory;
- Deploy it to the configured `UMM\Mods\Unity Inspector Mod` directory.

The source is in `src\`. `libs\` is retained only as a legacy local dependency cache. It is not used by the standard build and is not committed to Git.

Build without deploying:

```powershell
& .\BuildAndDeploy.ps1 -SkipDeploy
```

If r2modman installed the Mod under `UMM\Mods\alexneargarder-UnityInspectorMod` or another directory, use `-SkipDeploy` and copy `Unity Inspector Mod.dll`, `mcs.dll`, `Newtonsoft.Json.dll` and `Info.json` from the generated package into the enabled installation. Preserve settings, manifests and other files. The script's deployment destination is fixed at `UMM\Mods\Unity Inspector Mod`; avoid creating two installations with the same Mod ID.

Relaunch the game after updating its DLL. After updating the MCP `index.js` or `unity-connection.js`, call `restart_server` separately for local and remote services. This only reloads Node.js; it cannot load a new DLL into an already running game. Refresh the client tool list or reconnect the service if new tools remain unavailable.

## Runtime and MCP

The panel defaults to Chinese. Click `English` / `中文` at the top to switch immediately and save the selected language for the next launch. Status text, port controls and error messages follow the selected language without interrupting the server or its connections. The instructions below use the English labels.

1. Start Broforce.
2. Enable `Unity Inspector Mod` in UMM.
3. Confirm that the Mod panel shows `TCP Server Status: Running` and check its actual listening address and port. The initial default is `9999`; later launches use saved settings. If stopped, click `Start Server`; the server normally starts automatically when the Mod loads.
4. Install the Node.js 18+ dependencies and configure the MCP client in `Broforce_src\unity-inspector-mcp`. See the [MCP README](../Broforce_src/unity-inspector-mcp/README.en.md). When the client already has this service configured, do not start a second instance manually.

Enter a port in `Port (1-65535)` and click `Apply Settings` to switch while the game is running. The editor retains only ASCII digits; applying validates the range. Editing does not affect the server. An invalid or occupied new port leaves the existing listener and connections intact. A successful port change disconnects old clients and saves settings. When stopped, applying only saves settings; `Start Server` starts listening.

Use the MCP `configure_connection` tool to change the target without restarting the client. For example, after changing the game port to `19999`, pass:

```json
{"host":"127.0.0.1","port":19999,"token":"","testConnection":true}
```

`connection_status` reports the target and TCP state without connecting or revealing the token; verify usability with `ping` or the `reachable` result from `configure_connection`. Runtime changes last for this MCP process; `restart_server` restores the `UNITY_INSPECTOR_HOST`, `UNITY_INSPECTOR_PORT` and `UNITY_INSPECTOR_TOKEN` environment configuration. Apply the game listener settings and MCP target separately.

The listener defaults to `127.0.0.1`. For LAN or TCP tunnel access, enable `Allow LAN/tunnel connections`, enter an access token and apply. This listens on `0.0.0.0` and requires the same MCP token. Local connections can also use a token. Rotating the token disconnects existing clients. Changing the listening address on the same port restarts the listener and attempts rollback on failure; old clients must reconnect. UMM stores the token in the local settings XML, which must not be committed.

On Windows, the port can be checked first (replace `9999` if changed):

```powershell
Test-NetConnection 127.0.0.1 -Port 9999
```

MCP connects only to the Broforce instance running on the currently configured machine. A separate MCP target is required for another client on the LAN. It supports inspection and runtime debugging actions and is not read-only. Once the host exits, that client's state can no longer be inspected.

## Logs

Connection, script, and runtime errors from Unity Inspector Mod are still written to the UMM log:

```text
<r2modman profile>\UMM\Core\Log.txt
```

MCP connection information is returned through standard error. `read_log` and `watch_log` prefer `UNITY_INSPECTOR_UMM_LOG_PATH`, then try built-in `Default` profile paths if unset or inaccessible. For a custom profile such as `profiles\Broforce`, set this variable to the actual `Core\Log.txt`; remote logs need a path readable by the MCP process.

Token authentication does not encrypt traffic. Use Tailscale, ZeroTier or another encrypted tunnel for access across the Internet.

## Scheduling, cleanup and verification

- Responses are routed by unique request ID with UTF-8 decoding and JSON line framing. Late replies are discarded.
- Unity object access and script state operations run on the main thread. Screenshot capture is dispatched there, while file waiting stays on the network thread. Script compilation is serialized with expression compilation on the main thread to protect shared Mono compiler state.
- The queue holds at most 256 tasks and starts at most 32 per frame with a 4ms scheduling budget. A running task cannot be forcibly interrupted. Unstarted tasks are cancelled on timeout, disconnection or disable; a running-task timeout explicitly reports that execution already started.
- Each listener allows 8 connections and requests up to 1 MiB, with a 120-second idle read timeout. The MCP reconnects on the next call after closure.
- Disabling or unloading closes sockets, releases simulated inputs and timers, unloads script patches and tracked objects, and removes this Mod's patches and dispatcher. Dynamically compiled assemblies remain loaded until the game process exits.

Run `& .\tests\Test-PortSwitch.ps1` in this directory for connection, UI, scheduling and cleanup checks. Run `npm test` in the MCP directory for transport and stdio integration tests.
