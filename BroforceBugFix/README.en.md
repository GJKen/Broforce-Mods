# Broforce Bug Fix

> [Chinese](readme.md)

This is a Unity Mod Manager + Harmony bug-fix plugin for the Steam version of Broforce. The project only includes game defects confirmed through logs and the original game source. Each fix should remain independent and verifiable while changing normal gameplay as little as possible.

## Current Fixes

The current version is `0.2.0` and contains one fix:

- Prevents the same `DoodadCrate` from recursively entering `ActuallyCollapse` before the previous call has finished. This fixes infinite recursion, rapidly increasing explosion effects, severe frame drops, and `StackOverflowException` when adjacent explosive ammunition crates trigger one another.

| Item | Current value |
| --- | --- |
| Version | `0.2.0` |
| DLL SHA-256 | `B81902C1D59F6CD6845B76841C0A619B303137664BFE0E67783674F5561C025C` |
| Static build | Passed |
| In-game retest | Pending offline and official Steam two-sided retesting |

The first collapse still runs the complete original logic, including the explosion, area damage, normal chain reaction, drops, and network RPCs. The patch only skips nested re-entry while the same instance has not finished exiting.

## Fix Toggles

The UMM Mod enabled state is the outermost switch. The plugin panel has two additional levels of persistent settings:

- `Enable all bug fixes`: The plugin-level master switch. When disabled, all fixes are unloaded immediately while each individual toggle selection is retained.
- `Prevent recursive explosive-ammo crate collapse`: The individual toggle for the explosive ammunition crate recursion fix.

Both plugin-level toggles are enabled by default on first installation. The current fix takes effect only when all three conditions are enabled. Setting changes apply immediately and do not require a game restart. Future official bug fixes will each receive their own individual toggle and will all remain controlled by the master switch.

## Installation

Place `BroforceBugFix.dll` and `Info.json` from the project's `BroforceBugFix` package directory in:

```text
<UMM>\Mods\GJKen-BroforceBugFix\
```

Restart the game and confirm that `Broforce Bug Fix 0.2.0` is enabled in UMM. The fix applies to crate-destruction logic executed locally; all participants should use the same version during multiplayer.

## Build

The project targets .NET Framework 3.5. Copy `LocalBroforcePath.props.example` to `LocalBroforcePath.props` and fill in the Broforce `Managed` and UMM `Core` directories. `TestDeployModPath` is an optional extra test deployment directory; leaving it empty deploys only to the local UMM. The machine-specific configuration file is ignored by Git and must not be committed.

```powershell
powershell -ExecutionPolicy Bypass -File .\BuildAndDeploy.ps1
```

The script updates the copyable package in the project and deploys it to the local UMM. It deploys to the extra test directory only when `TestDeployModPath` is configured locally.

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
issues/                      Issue evidence, fix descriptions, and acceptance records
```

- [Development and testing documentation](docs/DEVELOPMENT.md)
- [Issue index](issues/README.en.md)
- [Recursive explosive ammunition crate collapse](issues/ISSUES-2026-08-27-DoodadCrate爆炸递归栈溢出.md)
