# Custom Map Multiplayer

> [中文](README.md)

Makes it easier to play Workshop maps together in the Steam version of Broforce, with fixes for selected multiplayer damage, world-state, and visual issues.

<a id="current-status"></a>

The current version is experimental **0.5.1**. Targeted multiplayer checks have been completed, but coverage of every character, form, and mechanism is incomplete.

Current build identifiers, package details, and verification scope are in the [version review](docs/world-sync-coverage/recheck.md#基线和证据范围).

## Main Features

| Feature | What it does |
| --- | --- |
| Steam multiplayer | Uses the game's existing Steam rooms and invitations |
| Workshop maps | Select subscribed maps, search, save favorites, and view recently played maps |
| Follow the host's map | Joining players adopt the host's map settings without copying the map ID |
| Multiplayer information and chat | Shows player latency in the Esc menu and supports Chinese input and long messages |
| Multiplayer options | Prioritizes melee when players overlap and corrects selected abnormal respawn positions |
| AFK spectator mode | Lets you leave temporarily and return, with a separate automatic AFK setting |
| FRP Direct | An optional connection method, disabled by default |

## Installation and First Run

1. All players install `r2modman`, create or select the default Broforce profile, and install UMM in that profile. Start the game once to confirm that UMM loads.
2. Import this project's [CustomMapMultiplayer.zip](Release/CustomMapMultiplayer.zip) through r2modman > Settings > Profile > Import local mod. The ZIP includes the DLL and `Info.json` under `UMM\Mods\CustomMapMultiplayer`.
3. Configure the Workshop map and injection options:
   - Both players must subscribe to and download the same Workshop map beforehand. Enable Workshop map injection on the separate `Workshop Map Configuration` page in the left-hand list.
   - The host can enter a numeric Workshop ID or click the subscribed-map selection button. The campaign name may be left blank; the default scene is `Test Evan2`. Change it when using another scene.
   - The map selection page supports title/ID search, all maps, recent multiplayer maps, starred maps, refresh, and favorites. In the starred filter, an `×` appears beside the tab when starred maps exist; it clears all stars without unsubscribing from Steam. Selecting a map changes only the Workshop ID, leaving campaign and scene names unchanged. Use the upper-right `X` to return to configuration.
   - Joining players do not need to enter the Workshop ID. The host's published map ID is adopted when joining, even if a different ID is saved locally.
   - If a joining player has not subscribed to the host's map, a message appears at the top of the screen. Follow it to complete the subscription.
   - The Mod does not subscribe, search the community, or actively download maps. Broforce and Steam handle downloads. To return to official maps, disable Workshop injection and FRP Direct, leave the current room, and create a new official Arcade online lobby.
   - Configuration illustration:

     ![UMM settings](https://github.com/user-attachments/assets/a39d9e2c-c5e0-48fd-a3a4-67731b9a61c8)

4. Either player creates an online lobby in Arcade mode; the other finds and joins the room.

## UMM Settings Panel

Use the list on the left to select a page; its options appear on the right.

- **Workshop Map Configuration**: Map injection, selection, search, favorites, and recently played maps.
- **Multiplayer Options**: Melee priority when players overlap, Workshop respawn-position repair, and automatic AFK.
- **FRP Direct**: Host or Client role and direct-connection settings.
- **Language**: Follow system, English, or Chinese.
- **Diagnostic Logs**: Diagnostic session ID, log presets, and diagnostic categories.

Melee priority is enabled by default. When players overlap, pressing melee performs the character's melee action instead of an automatic high five. Disable it to restore native behavior.

The respawn-position repair addresses selected abnormal direct-entry positions while preserving native checkpoint, rescue, cage, transport, and parachute spawns.

### AFK

The Esc menu offers an immediate AFK button when at least two participants are in the room. It affects only the local player and does not automatically return them to play.

Returning after entering AFK alive restores the character normally. Returning in the same level after entering AFK dead preserves the death state and portrait; normal rescue or the next round is still required.

Each player controls automatic AFK independently. If both players want to avoid automatic spectator mode while idle, both must disable automatic AFK on their own clients. This setting is independent of the manual AFK button.

See [AFK behavior](docs/AFK.md).

## Selected Fixes

Fixes supported by recorded samples include:

- Selected cross-peer attacks failing to deal damage, duplicate damage, and incorrect damage/gib ordering.
- Mines failing to explode, exploding at the wrong location, or having their countdown interrupted.
- Selected missing grenade and hit effects, and inconsistent damaged scenery appearance or collision state.
- Selected persistent statuses and surviving skill effects not being restored correctly on late join.
- Invisible captives after cage destruction, gun-emplacement driver-ejection differences, and spear endpoint and terminal-effect issues.
- Desperabro's companions not ending their performance or updating their positions correctly for other players.
- BroveHeart's skill failing to activate for other players, including when using the last charge.

Each fix has a specific verification scope. See the [issue index](issues/README.en.md) and [world-sync review](docs/world-sync-coverage/recheck.md) for samples and limits.

## Known Limits

Some skills, independent forms, attack directions, and mechanism combinations remain unverified. Some projectiles and follow-up skill effects also lack complete verification; untested cases are not necessarily defects.

Custom-character adaptation was outside this round; Aquabro's existing implementation and historical acceptance remain.

Historical level-transition, black-screen, and joining issues retain their records. They should be assessed against actual subsequent incidents, rather than assumed to reproduce in the current version. Complete coverage of every character, mechanism, or long session is not claimed.

## FRP Direct

When official multiplayer has very high latency, FRP Direct is an alternative to consider.

The host selects **Host** and joining players select **Client**. A working server-side UDP forward and the correct address, ports, and room password are required. Maps must still be subscribed to and downloaded through Steam.

Three-machine basic FRP multiplayer has been verified; four-machine play and complete capacity boundaries remain unverified. **FRP does not support host migration**: the host leaving ends the room. This FRP restriction does not describe ordinary Steam rooms.

See [networking and rooms](docs/NETWORKING.md) for configuration.

## Common Questions

**What if I cannot join a room or load the map?**

First check that both players have the same Mod build and have subscribed to and downloaded the same map. Look for a missing-subscription message. Avoid repeatedly joining during map loading. If the issue continues, record the screen each player is on and the actions that led to it.

**How do I return to official maps?**

Disable Workshop injection and FRP Direct, leave the room, and create a new official Arcade online room. Disabling the options does not immediately switch the map currently being played.

**Where are the logs?**

Open the diagnostic log directory from UMM's Diagnostic Logs page. When reporting a problem, include the map, both characters, each player's role and observations, logs from the same reproduction on both machines, and reproduction steps.

## Further Reading

- [Workshop maps and multiplayer](docs/WORKSHOP.md) · [Networking and rooms](docs/NETWORKING.md)
- [AFK spectator mode](docs/AFK.md) · [Chinese chat](docs/CHAT.md)
- [Issue records](issues/README.en.md) · [World-sync fixes and verification scope](docs/world-sync-coverage/recheck.md)
- [Build and deployment](docs/BUILD.md) · [Developer documentation](docs/DEVELOPMENT.md)
