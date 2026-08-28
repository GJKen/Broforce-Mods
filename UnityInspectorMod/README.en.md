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

After configuration, build and deploy:

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

## Runtime and MCP

1. Start Broforce.
2. Enable `Unity Inspector Mod` in UMM.
3. Confirm that the Mod panel shows `TCP Server Status: Running` on port `9999`. If the status is `Stopped`, click `Start Server` in the panel; the server normally starts automatically when the Mod loads.
4. Install the Node.js 18+ dependencies and configure the MCP client in `Broforce_src\unity-inspector-mcp`. See the [MCP README](../Broforce_src/unity-inspector-mcp/README.en.md). When the client already has this service configured, do not start a second instance manually.

On Windows, the port can be checked first:

```powershell
Test-NetConnection 127.0.0.1 -Port 9999
```

MCP connects only to the Broforce instance running on the currently configured machine. A separate MCP target is required for another client on the LAN. It supports inspection and runtime debugging actions and is not read-only. Once the host exits, that client's state can no longer be inspected.

## Logs

Connection, script, and runtime errors from Unity Inspector Mod are still written to the UMM log:

```text
<r2modman profile>\UMM\Core\Log.txt
```

MCP connection information is returned to the MCP client through standard error. MCP's `read_log` and `watch_log` currently look for the `Default` profile by default. If a custom profile such as `profiles\Broforce` is used, collect the actual `Core\Log.txt` directly.

The Mod's TCP service has no authentication and listens on all network interfaces by default. Use it only on a trusted local machine or LAN, restrict access to port `9999` with a firewall, and never expose the port to the public Internet.
