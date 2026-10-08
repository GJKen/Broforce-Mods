# Frame Rate Limiter

Frame Rate Limiter is a Unity Mod Manager plugin for the Steam version of Broforce. It limits the local rendering frame rate through Unity's `Application.targetFrameRate` without changing gameplay logic or network protocols.

Current version: `1.1.1`

## Features

- Default target frame rate: `60` FPS.
- Quick presets for `30`, `60`, `120`, `144`, and `240` FPS.
- Custom target frame rate from `1` to `1000` FPS.
- Click `Unlimited` to remove the frame rate limit; the input field accepts only numbers from `1` to `1000`.
- VSync is disabled by default while the mod is enabled so the custom limit can take effect.
- The target frame rate and VSync settings captured before activation are restored when the mod is disabled or unloaded.
- Chinese and English interface switching with saved language selection.
- A bilingual VSync tooltip with an opaque background drawn above other mod content.

## Usage

1. Import `Release\FrameRateLimiter.zip` with r2modman.
2. Start the game and enable `Frame Rate Limiter` in UMM.
3. Choose a target frame rate in the mod panel. Changes apply immediately.
4. Use the `English` or `中文` button in the upper-right area of the panel to switch languages.

The VSync checkbox text changes with the selected behavior:

- Checked: `Disable VSync while this Mod is enabled`.
- Unchecked: `Use the original VSync setting while this Mod is enabled`.

Hover over the VSync option to see an explanation of vertical synchronization, screen tearing, input latency, and custom FPS limits.

### Manual installation

Without r2modman, copy the following directory from the Release package into UMM:

```text
<UMM>\Mods\GJKen-FrameRateLimiter\FrameRateLimiter\
├─ FrameRateLimiter.dll
└─ Info.json
```

Restart the game after replacing the DLL.

## Build

The project targets .NET Framework 3.5. Copy `LocalBroforcePath.props.example` to `LocalBroforcePath.props` and fill in the Broforce `Managed` directory and the UMM `Core` directory. This file is used only for local builds and deployment and is ignored by Git.

Run the following command from the project directory:

```powershell
powershell -ExecutionPolicy Bypass -File .\BuildAndDeploy.ps1
```

The build script uses the installed Broforce Unity assemblies and UMM assembly, creates the Release package, and deploys the mod to the local UMM profile:

```text
Release\
├─ FrameRateLimiter.zip
├─ README.md
├─ manifest.json
└─ UMM\Mods\FrameRateLimiter\
   ├─ FrameRateLimiter.dll
   └─ Info.json
```

To create the Release package without deploying to the local UMM profile:

```powershell
powershell -ExecutionPolicy Bypass -File .\BuildAndDeploy.ps1 -SkipDeploy
```

See [docs/BUILD.md](docs/BUILD.md) for the complete build and deployment details.

## Project Structure and Documentation

| Path | Description |
| --- | --- |
| `src/` | Mod source code directory |
| `src/Plugin.cs` | UMM entry point, settings UI, and frame-rate application logic |
| `src/FrameRateLimiterSettings.cs` | Frame rate, VSync, and language settings |
| `src/TooltipOverlay.cs` | VSync tooltip rendering |
| `FrameRateLimiter.csproj` | C# project file |
| `BuildAndDeploy.ps1` | .NET 3.5 build, packaging, and deployment script |
| `Release/` | r2modman package and UMM plugin files |
| `Release/UMM/Mods/FrameRateLimiter/Info.json` | UMM manifest and version metadata |
| `README.md` | Chinese documentation |
| `README.en.md` | English documentation |
| `docs/BUILD.md` | Build, deployment, and Release package structure |
| `issues/` | Historical issues, test evidence, and acceptance records |
