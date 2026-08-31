# Custom Map Multiplayer

> [Chinese](README.md)

This is a Unity Mod Manager + Harmony mod for the Steam version of Broforce. It uses the official Steam Lobby/Steam P2P path by default. The optional `FRP Direct` mode uses independent rooms, PIDs, and game RPC, while Workshop content is still downloaded through Steam.

When Workshop map injection or FRP Direct is used, every player must install the same Mod build and subscribe to and download the same Workshop map. Use `BUILD_INFO buildHash` in each player's logs to compare versions. Joining players read the Workshop ID, scene name, and campaign name published by the host, so they do not need to enter the same map configuration manually.

## Current Status

The current version is experimental `0.5.0` and is not yet a stable release.

| Item | Status |
| --- | --- |
| Current distributed build | `buildHash=993e95efdc78a50e7ba6b25fb2495cb01e90d2a0cf551c058b6a43377904c9e3` |
| DLL SHA-256 | `8BD597F4843C0C7F625EE5391A6FCC73F93BFEBB0DFD571C069228303BFF3EB0` |
| Steam multiplayer | Default path; verified with the official lobby entering the same Workshop map and the colored latency list |
| FRP Direct | Disabled by default; three-player basic multiplayer verified, with code support for a host plus up to three remote players |

Verified:

- Two-player entry, late joining, leaving and rejoining the current map, and independent character control on both sides.
- The colored latency list and animated host name in the Esc menu for Steam and FRP Direct; three-player FRP Direct play, the static full-room notice for a `1`-player room, and automatic Host/Client configuration application.
- The host publishes the Workshop map identity and joining players use it automatically. Missing subscriptions show a notice and stop loading. Injection can be disabled while running and the official map flow is restored.
- Workshop loading first reuses the Steam-installed directory or an older local UGC cache. It falls back to a Steam download only when the cache cannot be read, and suppresses duplicate requests while the same map is loading.
- The Workshop entry banner, returning to the lobby with Esc, and the main-menu animation; deterministic standard ammunition crates, remote scanning suppression, and duplicate pickup protection are verified on both FRP sides, while the official Steam lobby and more maps still require retesting.
- In a long high-density combat test, Host frame drops were noticeably reduced. This is currently an observed improvement; unified graphics settings, reversed Host/Client roles, and p50/p95/p99 comparisons are still required for formal acceptance.
- The `Enter AFK now` button in UMM's multiplayer options; when the Host and joining player use it separately, it affects only that client's local character. Manual AFK does not trigger automatic re-entry; returning through the normal flow restores the original slot's lives, hero type, and character, while ordinary network dropout recovery remains automatic. See the [manual AFK issue record](issues/ISSUES-2026-09-01-新增主动AFK按钮.md).

Workshop acid failure samples confirmed that player slots and hero NIDs did not cross. The old patch covered only `CheckForTraps`, while reachable direct `CoverInAcid` calls in `CalculateMovement` and `Damage` bypassed it. The implementation now keeps a scene-level `DoodadAcidPool` list, predicts local death for the joining player, and enforces Host authority at the common `CoverInAcid` entry while rate-limiting the Host scan. Host and joining-player acid deaths have been verified independently without killing the player left at the spawn area. See the [dedicated issue](issues/ISSUES-2026-08-30-Workshop联机酸液池导致双方一起死亡.md).

In the logs, `NullReferenceException` means code tried to use an object that was not initialized or was no longer valid. `DoodadCrate` is the game's native crate-handling class. Repeated crate effects and related errors on the joining side are tracked separately and are not direct evidence of Host combat frame drops.

Open issues: ordinary Mook death final states, level-end re-entry protection, official Steam items, high latency, and long-term re-entry still need expanded acceptance testing. Residual entities after McBrover's turkey self-detonation can still be reproduced, although the probability is significantly lower; see the [separate issue](issues/ISSUES-2026-08-28-McBrover火鸡主动引爆后残留实体.md). Four-player FRP Direct, the `2` to `4` player capacity boundaries, dynamic capacity reduction followed by re-entry, and host migration have not been verified.

The current scope does not include continuous synchronization of active AI, enemy projectiles, coins, golden rewards, terrain damage from ordinary `Grenade`, or historical dynamic-world experiments. See the [development documentation index](docs/DEVELOPMENT.md) and the [issue index](issues/README.md) for implementation details and evidence.

## Installation and First Run

1. Have every player install `r2modman`, create or select the default profile for Broforce, and install UMM in that profile. Start the game once to confirm that UMM loads successfully.
2. Copy the latest compiled `CustomMapMultiplayer` directory in this project to the corresponding profile's `UMM\Mods\` directory. The directory must be named `GJKen-CustomMapMultiplayer`.
3. Append the following entry to the profile's `mods.yml`:

<details><summary>Click to expand</summary>

```yaml
- manifestVersion: 1
  name: GJKen-CustomMapMultiplayer
  authorName: GJKen
  websiteUrl: ''
  displayName: Custom Map Multiplayer
  description:
  gameVersion: ''
  networkMode: ''
  packageType: ''
  installMode: ''
  installedAtTime: 1786929010047
  loaders: []
  dependencies: []
  incompatibilities: []
  optionalDependencies: []
  versionNumber:
    major: 0
    minor: 5
    patch: 0
  enabled: true
  onlineSource: false
```
</details>

> This step only makes r2modman recognize that the Mod is already installed locally. The Mod can already work normally after step 2.

4. Restart r2modman and confirm that `Custom Map Multiplayer 0.5.0` is loaded in UMM.
5. Every player must subscribe to and download the same Workshop map, then enable Workshop map injection in `Multiplayer Options`.
Only the host needs to enter the map's Workshop ID in UMM. The campaign name can be left blank, the default scene name is `Test Evan2`, and the scene name can be changed when another map scene is used. The joining player's Workshop ID can be left blank; the Mod automatically uses the map configuration published by the host.
If a joining player has not subscribed to the host's map, a missing-subscription notice appears at the top of the screen. Follow the notice to subscribe to the map.
When both Workshop map injection and FRP Direct are disabled, the official Arcade online map creation flow is restored.

Configuration image:

<img width="781" height="417" alt="configuration image" src="https://github.com/user-attachments/assets/48ad31e3-9103-44cd-ba2d-763c3801294f" />

6. Have either player create an online lobby in Arcade mode. The joining player can find the room and join it directly.

### UMM Settings Panel

The actual UMM settings page uses a vertical feature list on the left and displays the selected feature's content on the right:

- `Multiplayer Options`: Workshop map injection, automatic AFK spectator mode, and the `Enter AFK now` button.
- `FRP Direct`: Direct-transport toggle, Host/Client role, ports, player limit, and connection parameters.
- `Language`: Click the Follow system, English, or Chinese button to change the interface language.
- `Diagnostic Logs`: Diagnostic session identity, log presets, and diagnostic categories.

`umm-settings-preview.html` is only a static preview. The actual UMM interface is defined by `src/Plugin.cs` and `src/SettingsUiText.cs`.

### Common Settings

- When Workshop map injection is disabled in `Multiplayer Options`, the setting is saved immediately and injection state is cleared. The current scene is not forcibly interrupted or changed. Leave the current room and create an official room again from the menu to return to the native map-selection flow; the saved Workshop ID does not need to be deleted.
- `Diagnostic session ID` associates logs from the same test round; use the same value on both sides. `Diagnostic label` only affects log file names and does not participate in multiplayer behavior.
- The `Multiplayer Options` AFK toggle is controlled independently on each client. When it is unchecked, the label says `Enabled: automatic AFK spectator mode`; when it is checked, the label says `Disabled: automatic AFK spectator mode`. To protect both characters, both players must check the option. It does not intercept manual exit, disconnects, or normal deaths.
- The same panel's `Enter AFK now` button immediately puts the local player owned by the current client into the native AFK spectator flow, independently of the automatic AFK toggle. The target is selected using local ownership and the active input controller; if multiple local slots cannot be uniquely resolved, the request is ignored to avoid affecting another character. Manual AFK does not schedule `RequestJoinGame`; the user must return through the normal rejoin flow, which restores the original slot's lives, hero type, and character. Ordinary network dropout still uses automatic re-entry.
- The diagnostic log presets (`Basic`, `Join / Rejoin`, `AFK / Failure`, `Workshop`, and `Full`) and the nine diagnostic categories only filter log output; they do not change multiplayer behavior. Use matching categories on both sides when investigating the same problem.

After each test round, collect the diagnostic `.log` and `.trace.log` files from every participant, and also preserve UMM `Core\Log.txt` and the game's `error.log` when possible. On Windows, logs are stored in `%USERPROFILE%\AppData\LocalLow\Free Lives\Broforce\CustomMapMultiplayer\`, not in the DLL deployment directory. The UMM `Open diagnostic log directory` button opens this location. With logs from only one side, state the evidence gap clearly and do not determine the network root cause from that side alone.

For abnormal deaths caused by acid, align the `PLAYER_ACID` events from both sides for the same session. They record the before/after state around `CoverInAcid`, `CoverInAcidRPC`, and `PlayerHasDiedRPC`, including the player slot, requested RPC slot, character NID, `IsMine`, position, `acidMeltTimer`, and `hasBeenCoverInAcid`. The deduplicated `authority-gate` event also identifies the `host-check`, `client-request`, `authority-wait`, or `native-fallback` decision.

## FRP Direct Multiplayer

FRP Direct is disabled by default.

The `Host`/`Client` role is still selected explicitly. Changing the role saves immediately and switches the connection automatically: Host uses only the local UDP listen port and completely ignores the saved Client public address; Client uses only the FRP public `host:port` and completely ignores the Host local listen port. The two configurations are kept separately, so switching back to the original role does not require entering its values again. The settings page has no manual Apply button. The global toggle and role take effect immediately; the port, address, and password are saved and reconnected automatically after input stops. Heartbeats, timeout detection, and ordinary disconnect retries are handled by the transport layer.

Host:

```text
FRP Direct role: Host
Local UDP listen port: 27045
FRP room player limit: Click one of the 1, 2, 3, or 4 buttons; takes effect immediately
FRP room password: A temporary password agreed on by all participants, or leave blank
```

The player-limit buttons set the total room capacity: `1` allows only the host, `2` allows the host plus one joining player, `3` allows the host plus two joining players, and `4` allows the host plus three joining players. This does not exceed Broforce's native four-player limit. The host can open UMM after entering a map and change the limit directly without restarting FRP. The new limit applies immediately to later joins, while players already in the room are not kicked. For example, if three players are currently present and the limit is changed to `1`, they can continue playing, but a player who leaves cannot rejoin until the limit is raised again.

After normal startup, the status should show `Listening on UDP 27045`. `frpc` forwards the public UDP port to `127.0.0.1:27045`. Changing connection parameters for the active role automatically restarts the connection, so adjust them before starting or after finishing a multiplayer session.

Client:

```text
FRP Direct role: Client
FRP server endpoint: The complete host:port provided by the service provider (use [address]:port for IPv6)
FRP room password: The same password as the host
```

When all participants use the same standard build and password, the Client status should be `Handshake complete; heartbeat active`, and the Host shows the number of authenticated clients. A protocol-version, `buildHash`, or password mismatch rejects the handshake and does not fall back automatically. The password is stored in the local UMM settings file, but is not written to logs or sent over the network in plaintext. Use a temporary password and do not reuse passwords from other accounts. The FRP token belongs only to `frpc`; do not enter it in the Mod.

When the native online player list is opened with `Esc`, FRP players appear as `xxxms | player name`: `0-80ms` is green, `81-150ms` is yellow, and `151ms` or higher is red. Before the first RTT sample arrives, the value is shown as gray `--ms`. The host appears as `HOST | host name`, with a dynamic colored gradient that cycles every four seconds. This latency is the round-trip time from each machine to the host; in a room with multiple players, the host synchronizes each connection's measurement with joining players.

After the handshake completes, the host creates an online lobby as usual. Joining players select the unique FRP room in the online lobby list. All players occupy separate positions in the `p1-p4` screen, and then the host enters the Workshop map. The room accepts joining players according to the host's current `1` to `4` player limit; requests after the limit is reached are rejected. Lowering the limit in a map only closes later slots and does not remove existing members; raising it immediately reopens slots. The host relays RPCs between clients. Basic three-player play and the static full-room notice for a `1`-player room have been verified; four-player play, the `2` to `4` capacity boundaries, and re-entry after dynamic capacity reduction still require dedicated acceptance testing. FRP Direct currently does not support host migration. See the [FRP Direct acceptance record](issues/archive/ISSUES-2026-08-24-FRP内网穿透联机方案.md) for the complete protocol and historical failure records.

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

The standard script creates the project package and deploys it to the local UMM directory while calculating and embedding the SHA-256 `buildHash`. Deployment overwrites the DLL and `Info.json` so that the name, version, and entry point match the current build. An optional test deployment directory is read only from the uncommitted `LocalBroforcePath.props`; do not write test-machine addresses, shared paths, or usernames to the repository. If a configured deployment path cannot be accessed, directory creation fails, or the DLL cannot be copied, the build is considered failed and two-sided testing must not continue. Do not replace a standard-script-verified build with an unverified IDE or manual build; such a build is recorded as `UNBUILT`.

## Project Structure and Documentation

```text
src/                              Mod source code
src/SettingsUiText.cs             UMM settings text in English and Chinese
CustomMapMultiplayer.csproj       C# project file
BuildAndDeploy.ps1                .NET 3.5 build and deployment script
CustomMapMultiplayer/             Copyable UMM package (DLL + Info.json)
README.md                         Default Chinese documentation
README.en.md                      English documentation
modinfo.json                      UMM manifest template
LocalBroforcePath.props.example   Local path configuration example
docs/                             Development documentation index and topic guides
issues/                           Historical issues, test evidence, and acceptance records
umm-settings-preview.html         UMM settings interface preview
```

- [Development documentation index](docs/DEVELOPMENT.md)
- [Issue index](issues/README.md)
- [BroforceMods Wiki](https://github.com/alexneargarder/BroforceMods/wiki)
- [Viewing Broforce's Code](https://github.com/alexneargarder/BroforceMods/wiki/Viewing-Broforce's-Code)
