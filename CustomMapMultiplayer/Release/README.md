# Custom Map Multiplayer

Play Steam Workshop maps with friends in Broforce using the game's existing Steam lobbies and invitations. Browse your subscribed maps, follow the host's map selection, and use additional multiplayer fixes and options.

**The host and every joining player must install the same release of this mod. Every joining player must also subscribe to and download the exact same Steam Workshop map as the host before joining.**

**Complete installation, configuration, and usage instructions are available on [GitHub (English)](https://github.com/GJKen/Broforce-Mods/blob/main/CustomMapMultiplayer/README.en.md).** [中文完整说明](https://github.com/GJKen/Broforce-Mods/blob/main/CustomMapMultiplayer/README.md) | [Report a bug](https://github.com/GJKen/Broforce-Mods/issues)

## Requirements

- The Steam version of Broforce.
- [Unity Mod Manager (UMM)](https://thunderstore.io/c/broforce/p/UMM/UMM/) and [RocketLib](https://thunderstore.io/c/broforce/p/RocketLib/RocketLib/) are required. **Every player must install and enable both dependencies; this mod cannot run without them.**

## Quick Start

1. In r2modman, select Broforce and install **UMM**, **RocketLib**, and **CustomMapMultiplayer** in your chosen profile. Make sure all three are enabled, then launch the game with **Start modded**.
2. The host shares the map's Steam Workshop link or ID. **Every joining player must subscribe to that same map and wait for its download to finish before joining.** The host must have it subscribed and downloaded too.
3. Open Custom Map Multiplayer's settings in UMM. On each machine, select **Workshop Map Settings** and enable Workshop map injection.
4. The host selects a subscribed map in the map browser or enters its numeric Workshop ID. Joining players do not need to enter the ID in the mod settings: it is filled from the host's selection. This does not subscribe to or transfer the map for them.
5. The host opens **Arcade** in the game's main menu and creates an **online** room. Other players join that room from the game's room list or accept the host's Steam invitation.

Usually, leave the campaign name empty and the scene set to `Test Evan2`. Change these only when the map requires different settings. Selecting a map in the browser changes its Workshop ID; it does not change the campaign or scene fields.

The mod does not automatically subscribe to Workshop maps. Steam and Broforce handle map downloads. If a missing-subscription notice appears, subscribe, finish the download, restart the game, and rejoin.

## Features

- **Subscribed-map browser:** Search by title or ID, save favorites, and revisit recently played multiplayer maps.
- **Follow the host's map settings:** Joining players use the host's Workshop map ID automatically. They must still subscribe to and download the host's map themselves.
- **Player latency:** View connection latency in the in-game Esc player list.
- **Improved chat:** Chinese text input and support for long messages.
- **Multiplayer options:** Prioritize melee over automatic high fives when players overlap, and correct selected abnormal Workshop respawn positions.
- **AFK spectator mode:** Temporarily step away and return. Automatic AFK is a separate setting controlled by each player; returning from AFK does not revive a player who was already dead.
- **English and Chinese settings:** Choose a language or follow the system language.

### Multiplayer Fixes

Includes targeted fixes for selected cases of:

- Missing or duplicate damage across players, and incorrect damage/death ordering.
- Mine detonation and countdown differences.
- Missing grenade and hit effects, and mismatched damaged scenery or collision states.
- Persistent statuses and active effects not restoring correctly when a player joins mid-game.
- Invisible captives after cage destruction, gun-emplacement driver state differences, and spear impact-position or ending-effect differences.
- Desperabro's companions not ending their performance or updating their positions correctly for other players.
- BroveHeart's skill failing to activate for other players, including when using the last charge.

See the [issue records](https://github.com/GJKen/Broforce-Mods/blob/main/CustomMapMultiplayer/issues/README.en.md) for details and verification scope.

### FRP Direct

When official multiplayer has very high latency, FRP Direct is an alternative to consider.

The host selects **Host** and joining players select **Client**. A working server-side UDP forward and the correct address, ports, and room password are required. Maps must still be subscribed to and downloaded through Steam.

Three-machine basic FRP multiplayer has been verified; four-machine play and player-limit edge cases remain unverified. **FRP does not support host migration:** the host leaving ends the FRP room. This restriction applies to FRP Direct, not ordinary Steam rooms.

See the [FRP setup guide on GitHub (Chinese)](https://github.com/GJKen/Broforce-Mods/blob/main/CustomMapMultiplayer/docs/NETWORKING.md) for configuration.

## Current Status and Limitations

Version **0.5.1** is experimental. Targeted multiplayer checks have been completed, but not every character, skill, form, map mechanism, or long-session scenario has been verified. Compatibility with custom characters is not guaranteed.

## Troubleshooting and Support

- **Joining or loading fails:** Check that everyone has the same mod release and has subscribed to and downloaded the host's map. Check for a missing-subscription notice, and avoid repeatedly clicking Join while the map is loading.
- **Returning to official maps:** Disable Workshop map injection and FRP Direct, leave the room, and create a new official Arcade online lobby. Changing the settings does not immediately switch the current map.
- **Disabling automatic AFK:** Each player must turn it off on their own client under **Multiplayer Options**. The manual AFK option remains available independently.

To [report a problem on GitHub](https://github.com/GJKen/Broforce-Mods/issues), include:

- Your mod version and Workshop map link or ID.
- Which player hosted, the characters used, and the steps to reproduce the issue.
- What the host and joining players each saw.
- Logs from the same reproduction on both machines. Open their location through UMM's **Diagnostic Logs** page.
