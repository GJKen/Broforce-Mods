# Broforce Bug Fix

> [Chinese](readme.md)

This is a Unity Mod Manager + Harmony bug-fix plugin for the Steam version of Broforce. The project only includes game defects confirmed through logs and the original game source. Each fix should remain independent and verifiable while changing normal gameplay as little as possible.

## Current Fixes

The current version is `0.5.0` and contains five fixes:

- Prevents the same `DoodadCrate` from recursively entering `ActuallyCollapse` before the previous call has finished. This fixes infinite recursion, rapidly increasing explosion effects, severe frame drops, and `StackOverflowException` when adjacent explosive ammunition crates trigger one another.
- Clears a previous room's paused RPC stream and queued messages when joining a new lobby from MainMenu. This prevents an unfinished Workshop load from blocking the next official-map join, independently of CustomMapMultiplayer.
- Fixes the original null-target exception when a snake/facehugger detaches, allowing victim cleanup to finish instead of repeating death and blood each frame. Offline A/B and controlled FRP tests on both peers passed. The initial cause of the lost target reference remains unconfirmed; see the [issue record](issues/ISSUES-2026-11-14-蛇脱离空目标导致反复出血.md).
- Fixes alien egg and AlienEggListener deregistration after Map teardown clears their collections, preserving egg base destruction and listener registration-flag cleanup. Other wildlife and non-null collections retain native behavior. See the [issue record](issues/ISSUES-2026-11-18-AlienEgg换关清理空引用.md) for fix boundaries and version-specific validation.
- Defers sandstorm initialization while map data is temporarily absent during scene transitions, preventing a null reference and partial setup. Native initialization resumes when data is ready, preserving behavior on both storm and non-storm maps. See the [issue record](issues/ISSUES-2026-11-21-沙尘暴切图初始化空引用.md) for the cause, multiplayer impact, and validation.

| Item | Current value |
| --- | --- |
| Version | `0.5.0` |
| Static build | Passed |
| In-game retest | Sandstorm passed offline/two-peer targeted checks, native Steam join/reloads, and assisted two-peer tests covering both levels of the official storm campaign in segments. See the [validation summary](docs/DEVELOPMENT.md#验证范围与复测要求) for each fix's tested scope and remaining scenarios |

The first collapse still runs the complete original logic, including the explosion, area damage, normal chain reaction, drops, and network RPCs. The patch only skips nested re-entry while the same instance has not finished exiting.

## Fix Toggles

The UMM Mod enabled state is the outermost switch. The plugin panel has two additional levels of persistent settings:

- `Enable all bug fixes`: The plugin-level master switch. When disabled, all fixes are unloaded immediately while each individual toggle selection is retained.
- `Prevent recursive explosive-ammo crate collapse`: The individual toggle for the explosive ammunition crate recursion fix.
- `Clear a stalled previous room's network stream when joining`: The individual toggle for clearing a stalled stream at the new-lobby boundary.
- `Fix snake/facehugger detach errors and repeated bleeding`: The individual toggle for the null-target detach fix.
- `Fix alien egg cleanup after the map is unloaded`: The individual toggle for alien egg cleanup during map teardown.
- `Wait for map data before sandstorm setup`: The individual toggle for sandstorm initialization during scene transitions.

All plugin-level toggles are enabled by default. Each fix requires UMM, the master switch, and its individual switch to be enabled. Setting changes apply immediately.

## Installation

Close Broforce and confirm that its process has exited before placing `BroforceBugFix.dll` and `Info.json` from the project's `BroforceBugFix` package directory in:

```text
<UMM>\Mods\GJKen-BroforceBugFix\
```

Restart the game and confirm that `Broforce Bug Fix 0.5.0` is enabled in UMM. The lobby fix is required on the affected joining machine. All participants should use the same version for the crate, alien egg, and sandstorm fixes.

## Build

The project targets .NET Framework 3.5. Copy `LocalBroforcePath.props.example` to `LocalBroforcePath.props` and fill in the Broforce `Managed` and UMM `Core` directories. `TestDeployModPath` is an optional extra test deployment directory; leaving it empty deploys only to the local UMM. The machine-specific configuration file is ignored by Git and must not be committed.

Before running the script, close Broforce on the local machine and any extra deployment target, and confirm that the processes have exited. The script deploys directly and does not stop the game. Run from this project directory:

```powershell
powershell -ExecutionPolicy Bypass -File .\BuildAndDeploy.ps1
```

The script updates the copyable package in the project and deploys it to the local UMM. It deploys to the extra test directory only when `TestDeployModPath` is configured locally.

For in-game validation, restart the game and confirm the version and fix toggles. Close the game after validation and temporary-object cleanup. Documentation-only changes do not require a build or deployment.

## Project Structure and Documentation

```text
src/                         Mod source code
src/BugFixSettings.cs        Master and per-fix persistent toggles
BroforceBugFix.csproj        C# project file
BuildAndDeploy.ps1           .NET 3.5 build and deployment script
BroforceBugFix/              Copyable UMM package (DLL + Info.json)
README.en.md                 English documentation
readme.md                    Default Chinese documentation
modinfo.json                 UMM manifest template
LocalBroforcePath.props.example
                             Local path configuration example
docs/DEVELOPMENT.md          Development, reverse-engineering, testing, and maintenance constraints
issues/                      Issue records, fix descriptions, and acceptance entry points
```

- [Development and testing documentation](docs/DEVELOPMENT.md)
- [Issue index](issues/README.en.md)
- [Recursive explosive ammunition crate collapse](issues/ISSUES-2026-08-27-DoodadCrate爆炸递归栈溢出.md)
