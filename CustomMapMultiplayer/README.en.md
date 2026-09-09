# Custom Map Multiplayer

> [Chinese](README.md)

This is a Unity Mod Manager + Harmony mod for the Steam version of Broforce. It uses the official Steam Lobby/Steam P2P path by default. The optional `FRP Direct` mode uses independent rooms, PIDs, and game RPC, while Workshop content is still downloaded through Steam.

When Workshop map injection or FRP Direct is used, every player must install the same Mod build and subscribe to and download the same Workshop map. Use `BUILD_INFO buildHash` in each player's logs to compare versions. Joining players read the Workshop ID, scene name, and campaign name published by the host, so they do not need to enter the same map configuration manually.

## Current Status

The current version is experimental `0.5.0` and is not yet a stable release.

| Item | Status |
| --- | --- |
| Current distributed build | `buildHash=aee114e5232701cc14182fd03466f9b6f2a9ef897fcaeaaea33b183dbad66ad7` |
| DLL SHA-256 | `86AF8070BB1E6A38E49DAED8AD8019CCD2D15F7C1B00F929A50A3A21FD3B10E7` |
| DLL assembly version | `0.5.0.0` |
| Steam multiplayer | Default path; verified with the official lobby entering the same Workshop map and the colored latency list |
| FRP Direct | Disabled by default; three-player basic multiplayer verified, with code support for a host plus up to three remote players |

### Verified

- Two-player entry, late joining, leaving and rejoining the current map, and independent character control on both sides. See [Workshop and game state](docs/WORKSHOP.md).
- The colored latency list and animated host name in the Esc menu for Steam and FRP Direct; three-player FRP Direct play, the static full-room notice for a `1`-player room, and automatic Host/Client configuration application. See [networking and rooms](docs/NETWORKING.md) and the [FRP Direct acceptance record](issues/archive/ISSUES-2026-08-24-FRP内网穿透联机方案.md).
- The host publishes the Workshop map identity and joining players use it automatically. Missing subscriptions show a notice and stop loading. Injection can be disabled while running and the official map flow is restored. See [Workshop and game state](docs/WORKSHOP.md), the [missing-map loading record](issues/archive/ISSUES-2026-09-04-Workshop缺图仍进入加载动画.md), and the [injection disablement record](issues/archive/ISSUES-2026-08-28-关闭Workshop注入后恢复官方地图.md).
- Workshop loading first reuses the Steam-installed directory or an older local UGC cache. It falls back to a Steam download only when the cache cannot be read, and suppresses duplicate requests while the same map is loading. See [Workshop and game state](docs/WORKSHOP.md).
- The Workshop entry banner, returning to the lobby with Esc, and the main-menu animation; deterministic standard ammunition crates, remote scanning suppression, and duplicate pickup protection are verified on both FRP sides, while the official Steam lobby and more maps still require retesting. See [networking and rooms](docs/NETWORKING.md) and [Workshop and game state](docs/WORKSHOP.md).
- In a long high-density combat test, Host frame drops were noticeably reduced. This is currently an observed improvement; unified graphics settings, reversed Host/Client roles, and p50/p95/p99 comparisons are still required for formal acceptance.
- The `Enter AFK now` button in the in-game Esc menu; when the Host and joining player use it separately, it affects only that client's local character. Manual AFK does not trigger automatic re-entry; returning through the normal flow restores the original slot's lives, hero type, and character, while ordinary network dropout recovery remains automatic. See the [manual AFK issue record](issues/archive/ISSUES-2026-09-01-新增ESC菜单主动AFK按钮.md).
- Online chat now supports Chinese IME input, Chinese symbols, digits, and native editing, with a 500 UTF-16 character limit, long-message viewport, visual-line Up/Down navigation, an input-box character counter, and control suppression for the active keyboard chat player. These features passed the narrow real-keyboard checks; the Esc-reopen/Enter-send issue is fixed, while the full input matrix and chat-history acceptance still require completion. See the [chat input guide](docs/CHAT.md).
- Swap Bros 2.1.5 `Always spawn as chosen bro` compatibility passed offline and Steam multiplayer testing. Runtime selection uses `Swap_Bros_Mod.Main.GetSelectedBroHeroType(Int32)`, and both first spawn and respawn ended with the locked hero; see the [Swap Bros issue record](<issues/ISSUES-2026-09-07-Swap Bros Always spawn as chosen bro与联机角色生成冲突.md>).
- Host and joining-player acid deaths have been verified independently: the player who contacts the acid dies normally, while the player at the spawn area remains alive. Thrown-acid hits were also accepted in user in-game testing; see the [acid-pool issue](issues/archive/ISSUES-2026-08-30-Workshop联机酸液池导致双方一起死亡.md) and the [thrown-acid issue](issues/ISSUES-2026-09-07-投掷酸液命中角色未正常死亡.md).

### Pending verification

#### Death and entity final states

- **Ordinary Mook final states:** Authoritative death events and corpse final-state submission are implemented, but low-probability state divergence remains; late-join death snapshot replay is not implemented. See the [Mook final-state issue](issues/ISSUES-2026-08-28-全联机死亡实体与尸体终态同步.md).
- **McBrover turkey self-detonation:** Residual entities can still be reproduced, although the probability is significantly lower; the root cause and remote lifecycle chain are not closed. See the [McBrover issue](issues/ISSUES-2026-08-28-McBrover火鸡主动引爆后残留实体.md).

#### Workshop level transitions

- **`3715087178` level-end re-entry protection:** The protection patch has been built and deployed, but still needs in-game completion testing. Confirm that repeated `LevelEndSuccess` no longer advances the level number every frame and that Host and Client reach the same next level or outcome scene. See the [level-end re-entry issue](issues/ISSUES-2026-08-26-3715087178联机通关黑屏与关卡结束重入.md).
- **`3781818421` fourth-level black screen:** The black screen may still occur when entering level four after completing level three; investigate it separately from the `3715087178` re-entry protection issue. See the [re-entry and fourth-level black-screen issue](issues/ISSUES-2026-08-22-重复退出重入加入方失败与3781818421进入第4关黑屏.md).

#### Multiplayer coverage

- **Official Steam lobby and more maps for item synchronization:** The standard ammunition-crate behavior verified on both FRP sides has not yet been retested in the official Steam lobby and on more Workshop maps.
- **High latency and long-term re-entry:** There is still no evidence from controlled latency conditions and repeated leave/re-entry rounds.
- **FRP Direct extended scenarios:** Four-player play, total-capacity boundaries from `2` to `4`, and re-entry after dynamic capacity reduction remain unverified; FRP Direct does not support Host migration, so it is not listed as a pending capability.

#### Runtime errors and performance

- **Joining-side crate anomaly (separate issue):** In historical Steam sessions, the joining player's logs recorded `NullReferenceException` at `DoodadCrate.SetupBlockAtStart` and `DestroyBlockInternal`, together with repeated crate-collapse effects. The latest two-sided short regression on 2026-08-31 did not reproduce them. Because that run did not directly trigger the crate-protection branches, acceptance still requires a targeted test that opens or collapses a crate. The current evidence confirms a joining-side crate-handling anomaly, but does not show that it caused Host combat frame drops. See the [joining-side crate issue](issues/ISSUES-2026-08-30-加入方箱子坍塌特效持续重复.md).
- **Host combat frame rate (separate issue):** The high-density combat test showed only an observed improvement. Unified graphics settings, reversed Host roles, and p50/p95/p99 comparisons are still incomplete, so formal acceptance remains pending. The crate anomaly has no matching Host call stack or direct causal evidence and is excluded from this assessment. See the [Host performance issue](issues/ISSUES-2026-08-30-联机房主低帧率与Host专属扫描性能问题.md).

The current scope does not include continuous synchronization of active AI, enemy projectiles, coins, golden rewards, terrain damage from ordinary `Grenade`, or historical dynamic-world experiments. See the [development documentation index](docs/DEVELOPMENT.md) and the [issue index](issues/README.md) for implementation details and evidence.

## Installation and First Run

1. Have every player install `r2modman`, create or select the default profile for Broforce, and install UMM in that profile. Start the game once to confirm that UMM loads successfully.
2. Import `Release\CustomMapMultiplayer.zip` into the Broforce profile in r2modman. The ZIP already contains the DLL and `Info.json` under `UMM\Mods\CustomMapMultiplayer`.
3. Configure the Workshop map and injection options:
   - Both players should subscribe to and download the same Workshop map, then enable Workshop map injection in the separate `Workshop Map Settings` page.
   - The host can enter a numeric Workshop ID manually or open `Choose a subscribed map`. The campaign name can be left blank, the default scene name is `Test Evan2`, and the scene name can be changed when another map scene is used.
   - The map page supports title/ID search, All, Recent online maps, Starred, refresh, and favorites. When the Starred tab is active and contains maps, `×` appears beside it and clears all starred maps at once; it does not unsubscribe from Steam Workshop, and it is hidden on the other tabs. Selecting a map changes only the Workshop ID, does not change the campaign or scene name, and keeps the user on the selection page until the top-right `X` is used.
   - The joining player does not need to enter a Workshop ID. Even if a saved local ID is incorrect, the client adopts the host's published Workshop ID after joining the room.
   - If a joining player has not subscribed to the host's map, a missing-subscription notice appears at the top of the screen. Follow the notice to subscribe to the map.
   - The Mod does not subscribe, search the community, or actively download maps; Broforce/Steam handles map downloads. When Workshop map injection and FRP Direct are disabled, leave the current room and create an official Arcade online lobby again to restore the official map flow.
   - Configuration image:

     ![UMM settings interface](https://github.com/user-attachments/assets/a39d9e2c-c5e0-48fd-a3a4-67731b9a61c8)

4. Have either player create an online lobby in Arcade mode. The joining player can find the room and join it directly.

### UMM Settings Panel

The actual UMM settings page uses a vertical feature list on the left and displays the selected feature's content on the right:

- `Workshop Map Settings`: Workshop map injection, manual Workshop ID, subscribed-map selection, search, filters, favorites, and recent online maps; advanced campaign and scene settings remain in this page.
- `Multiplayer Options`: player-overlap melee, Workshop drop-in respawn-position repair, and automatic AFK spectator mode toggles; the manual AFK button is in the in-game Esc menu.
- `FRP Direct`: Direct-transport toggle, Host/Client role, ports, player limit, and connection parameters.
- `Language`: Click the Follow system, English, or Chinese button to change the interface language.
- `Diagnostic Logs`: Diagnostic session identity, log presets, and diagnostic categories.

`umm-settings-preview.html` is only a static preview. The actual UMM interface is defined by `src/Plugin.cs` and `src/SettingsUiText.cs`.

### Common Settings

- When Workshop map injection is disabled in `Workshop Map Settings`, the setting is saved immediately and injection state is cleared. The current scene is not forcibly interrupted or changed. Leave the current room and create an official room again from the menu to return to the native map-selection flow; the saved Workshop ID does not need to be deleted.
- `Diagnostic session ID` associates logs from the same test round; use the same value on both sides. `Diagnostic label` only affects log file names and does not participate in multiplayer behavior.
- The `Multiplayer Options` AFK toggle is controlled independently on each client. When it is unchecked, the label says `Enabled: automatic AFK spectator mode`; when it is checked, the label says `Disabled: automatic AFK spectator mode`. To protect both characters, both players must check the option. It does not intercept manual exit, disconnects, or normal deaths.
- `Multiplayer Options` enables `melee overrides player-overlap high-five` by default. When enabled, pressing melee while players overlap starts the selected bro's melee; when disabled, Broforce's automatic high-five behavior is restored.
- `Multiplayer Options` enables the `Workshop drop-in respawn position fix` by default. In an active Workshop injection session, it records the first stable direct map spawn and uses that backup only when a later `DropInDuringGame` respawn position is abnormal; transport, parachute, checkpoint, rescue, and cage spawns remain native. Disabling the toggle leaves those positions untouched, and the setting can be changed while the game is running.
- The in-game Esc menu's `Enter AFK now` button immediately puts the local player owned by the current client into the native AFK spectator flow, independently of the automatic AFK toggle. The target is selected using local ownership and the active input controller; if multiple local slots cannot be uniquely resolved, the request is ignored to avoid affecting another character. Manual AFK does not schedule `RequestJoinGame`; the user must return through the normal rejoin flow, which restores the original slot's lives, hero type, and character. Ordinary network dropout still uses automatic re-entry.
- The diagnostic log presets (`Basic`, `Join / Rejoin`, `AFK / Failure`, `Workshop`, and `Full`) and the nine diagnostic categories only filter log output; they do not change multiplayer behavior. Use matching categories on both sides when investigating the same problem.

### Testing and Logs

After each test round, collect diagnostic logs for the same session from every participant, and preserve UMM and game logs when possible. Use UMM's `Open diagnostic log directory` action to locate Mod logs; do not publish user directories, shared paths, or usernames. With logs from only one side, state the evidence gap clearly and do not determine the network root cause from that side alone.

### Acid Troubleshooting

For abnormal deaths caused by acid, align the `PLAYER_ACID` events from both sides for the same session. They record the before/after state around `CoverInAcid`, `CoverInAcidRPC`, and `PlayerHasDiedRPC`, including the player slot, requested RPC slot, character NID, `IsMine`, position, `acidMeltTimer`, and `hasBeenCoverInAcid`. The deduplicated `authority-gate` event also identifies the `host-check`, `client-request`, `authority-wait`, or `native-fallback` decision.

## FRP Direct Multiplayer

FRP Direct is disabled by default.

The `Host`/`Client` role is still selected explicitly. Changing the role saves immediately and switches the connection automatically: Host uses only the local UDP listen port and completely ignores the saved Client public address; Client uses only the FRP public `host:port` and completely ignores the Host local listen port. The two configurations are kept separately, so switching back to the original role does not require entering its values again. The settings page has no manual Apply button. The global toggle and role take effect immediately; the port, address, and password are saved and reconnected automatically after input stops. Heartbeats, timeout detection, and ordinary disconnect retries are handled by the transport layer.

### Host

```text
FRP Direct role: Host
Local UDP listen port: 27045
FRP room player limit: Click one of the 1, 2, 3, or 4 buttons; takes effect immediately
FRP room password: A temporary password agreed on by all participants, or leave blank
```

The player-limit buttons set the total room capacity: `1` allows only the host, `2` allows the host plus one joining player, `3` allows the host plus two joining players, and `4` allows the host plus three joining players. This does not exceed Broforce's native four-player limit. The host can open UMM after entering a map and change the limit directly without restarting FRP. The new limit applies immediately to later joins, while players already in the room are not kicked. For example, if three players are currently present and the limit is changed to `1`, they can continue playing, but a player who leaves cannot rejoin until the limit is raised again.

After normal startup, the status should show `Listening on UDP 27045`. `frpc` forwards the public UDP port to `127.0.0.1:27045`. Changing connection parameters for the active role automatically restarts the connection, so adjust them before starting or after finishing a multiplayer session.

### Client

```text
FRP Direct role: Client
FRP server endpoint: The complete host:port provided by the service provider (use [address]:port for IPv6)
FRP room password: The same password as the host
```

When all participants use the same standard build and password, the Client status should be `Handshake complete; heartbeat active`, and the Host shows the number of authenticated clients. A protocol-version, `buildHash`, or password mismatch rejects the handshake and does not fall back automatically. The password is stored in the local UMM settings file, but is not written to logs or sent over the network in plaintext. Use a temporary password and do not reuse passwords from other accounts. The FRP token belongs only to `frpc`; do not enter it in the Mod.

When the native online player list is opened with `Esc`, FRP players appear as `xxxms | player name`: `0-80ms` is green, `81-150ms` is yellow, and `151ms` or higher is red. Before the first RTT sample arrives, the value is shown as gray `--ms`. The host appears as `HOST | host name`, with a dynamic colored gradient that cycles every four seconds. This latency is the round-trip time from each machine to the host; in a room with multiple players, the host synchronizes each connection's measurement with joining players.

After the handshake completes, the host creates an online lobby as usual. Joining players select the unique FRP room in the online lobby list. All players occupy separate positions in the `p1-p4` screen, and then the host enters the Workshop map. The room accepts joining players according to the host's current `1` to `4` player limit; requests after the limit is reached are rejected. Lowering the limit in a map only closes later slots and does not remove existing members; raising it immediately reopens slots. The host relays RPCs between clients. Basic three-player play and the static full-room notice for a `1`-player room have been verified; four-player play, the `2` to `4` capacity boundaries, and re-entry after dynamic capacity reduction still require dedicated acceptance testing. FRP Direct currently does not support host migration; user comparison testing confirmed that the joining player returns directly to MainMenu after the host exits and does not reproduce this issue's Steam black screen. See the [FRP Direct acceptance record](issues/archive/ISSUES-2026-08-24-FRP内网穿透联机方案.md) for the complete protocol and historical failure records.

When the FRP room list shows that the room is full, a joining player sees "The host's room has reached its player limit and cannot be joined right now." at the top of the screen. If a room still has a slot when clicked but becomes full before the request reaches the host, the host's `room_full` response shows the same notice. The notice disappears five seconds after the last trigger; repeated clicks do not stack notices and only restart the five-second timer. The missing-subscription notice for the host's Workshop map remains persistent and is not affected by this timer.

## Build

The project targets .NET Framework 3.5. Before building or deploying, read the project's `LocalBroforcePath.props`:

- `BroforceManagedPath`: the local Broforce `Managed` directory.
- `UnityModManagerPath`: the local UMM core directory.
- `TestDeployModPath`: the local test-machine deployment directory; an empty value explicitly disables the extra test deployment.

This file contains machine-specific paths and is only used for building or deploying. It must not be written to public files, commit messages, log excerpts, or external replies. For first-time setup, copy `LocalBroforcePath.props.example` to `LocalBroforcePath.props`, fill in the local paths, and run the following command from the project directory:

```powershell
powershell -ExecutionPolicy Bypass -File .\BuildAndDeploy.ps1
```

The standard script reads and keeps `Release\UMM\Mods\CustomMapMultiplayer\Info.json`, creates `Release\CustomMapMultiplayer.zip`, and deploys it to the local UMM directory while calculating and embedding the SHA-256 `buildHash`. Deployment overwrites the DLL and `Info.json` so that the name, version, and entry point match the current build; the DLL assembly version is generated from the version in that `Info.json`. An optional test deployment directory is read only from the uncommitted `LocalBroforcePath.props`; do not write test-machine addresses, shared paths, or usernames to the repository. If a configured deployment path cannot be accessed, directory creation fails, or the DLL cannot be copied, the build is considered failed and two-sided testing must not continue. Do not replace a standard-script-verified build with an unverified IDE or manual build; such a build is recorded as `UNBUILT`.

## Project Structure and Documentation

| Path | Description |
| --- | --- |
| `src/` | Mod source code directory; source code responsibilities and module relationships are described in Architecture and Code Responsibilities |
| `src/SettingsUiText.cs` | UMM settings text in English and Chinese |
| `CustomMapMultiplayer.csproj` | C# project file |
| `BuildAndDeploy.ps1` | .NET 3.5 build and deployment script |
| `Release/` | r2modman package and UMM plugin files |
| `README.md` | Default Chinese documentation |
| `README.en.md` | English documentation |
| `Release/UMM/Mods/CustomMapMultiplayer/Info.json` | UMM manifest and build metadata source |
| `LocalBroforcePath.props.example` | Local path configuration example |
| `docs/DEVELOPMENT.md` | [Development documentation index](docs/DEVELOPMENT.md) |
| `docs/CHAT.md` | Online chat input, viewport, and history display |
| `issues/` | [Historical issues, test evidence, and acceptance records](issues/README.md) |
| `umm-settings-preview.html` | UMM settings interface preview |

### Other Documentation

- [BroforceMods Wiki](https://github.com/alexneargarder/BroforceMods/wiki)
- [Viewing Broforce's Code](https://github.com/alexneargarder/BroforceMods/wiki/Viewing-Broforce's-Code)
