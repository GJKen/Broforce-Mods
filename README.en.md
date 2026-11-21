# Broforce-Mods

> [Chinese](README.md)

This repository contains source code for Broforce mods and the development tools used with them. It is maintained as an archive and backup.

New features and maintenance in this repository are primarily completed with AI assistance.

For installation, build, and usage instructions, follow the project documentation links below.

## Credits

- [BroforceMods Wiki](https://github.com/alexneargarder/BroforceMods)
- [Gorzontrok](https://github.com/Gorzontrok)

## Projects

- [`CustomMapMultiplayer`](CustomMapMultiplayer/README.en.md): Multiplayer support for third-party Workshop maps and FRP Direct.
- [`RocketLibMultiplayer`](RocketLibMultiplayer/README.md): Source and build project for the shared RocketLib multiplayer layer; see the [multiplayer integration guide](RocketLibMultiplayer/docs/networking.md) for mod authors.
- [`BroMakerMultiplayer`](BroMakerMultiplayer/README.md): BroMaker integration with the shared multiplayer framework; see the [custom bro author guide](BroMakerMultiplayer/docs/networking.md). Authors must adapt mod-specific behavior.
- [`BroforceBugFix`](BroforceBugFix/README.en.md): Independent original-game fixes for recursive crate collapse, stale paused streams when joining a lobby, null-target snake/facehugger detach, alien egg and Listener cleanup during map teardown, and sandstorm initialization during scene transitions. See the project documentation for each fix's validation scope.
- [`UnityInspectorMod`](UnityInspectorMod/README.en.md): Provides TCP inspection and runtime debugging interfaces for Broforce, used by `unity-inspector-mcp`.
- [`Broforce_src`](Broforce_src/unity-inspector-mcp/README.en.md): Unity Inspector MCP tools; installation, configuration, and usage are documented in [`unity-inspector-mcp/README.en.md`](Broforce_src/unity-inspector-mcp/README.en.md).
