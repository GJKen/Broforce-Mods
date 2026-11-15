# Aquabro multiplayer integration

This is a character-specific integration example for the shared RocketLib and BroMaker networking changes. The implementation was generated with AI; testing covers selected offline cases and limited two-player sessions, not every multiplayer scenario.

## Dependencies

Use matching framework builds on every peer. These source changes depend on the networking work in [RocketLib PR #1](https://github.com/Gorzontrok/RocketLib/pull/1) and [BroMaker PR #29](https://github.com/Gorzontrok/Bro-Maker/pull/29). Existing published framework packages may not contain those changes. Both peers also need the same Aquabro code and resources.

The repository records compatible framework revisions through its RocketLibMultiplayer and BroMakerMultiplayer submodules. BroMaker's PR branch contains the same public framework sources as the recorded BroMaker revision; it excludes historical local test reports.

## Code to review

| Behavior | Implementation |
| --- | --- |
| Normal and charged trident spawning | `FireTrident` in [Aquabro.cs](../src/Aquabro.cs) uses the game's `SpawnProjectileOverNetwork` entry point on the owning character with immediate execution. Remote characters do not initiate a second spawn. |
| Charge and throw animation | `BroadcastTridentAnimation` sends a reliable immediate event when the owner enters charge, throw recovery or idle/cancellation. Remote characters advance that visual state instead of reconstructing attacks from interpolated input snapshots. |
| Trident impact and splash | `ResolveWaterImpact` reuses the registered spear's native `MakeEffects` RPC with immediate execution. The remote path requires the matching projectile NID and sender owner before applying the splash, and the impact marker prevents repeated application to the same instance. |
| Projectile lifetime | `RunTridentLife`, `LimitTridentStep`, and `RestoreTridentStep` stop remote travel at expiry while retaining the object for the owner's impact notification. They also preserve the charged spear's per-frame movement budget. |
| Skill selection and casting | [AquabroSkills.cs](../src/AquabroSkills.cs) sends character-specific selection and cast events; remote peers apply the corresponding visuals. |
| Shark control | Shark position and direction use state updates. Breaching uses a separate reliable event, including handling when that event arrives before the remote shark visual exists. |
| Maelstrom control | The caster sends an immediate reliable cast ID and authoritative position, age, route bounds and remaining route state. Remote visuals follow that route instead of checking their own terrain. Each victim's owner applies continuous suction and heavy-Mook control; replicas do not apply a second impulse. Pulse and final damage remain caster-only. |
| Damage | Relevant damage paths run on the owning character. This is an implementation choice to review; it is not proof that every target or skill has correct shared damage. |

Other character authors can reuse the public framework interfaces, but must adapt their own random outcomes, skill state, object spawning, event handling, and damage rules. Aquabro's skill RPCs are not a general-purpose framework protocol.

## Validation and limits

The checked-in offline tests cover extracted production methods with controlled dependencies:

- [Test-Charge.ps1](../tests/Test-Charge.ps1): charge and interruption timing, immediate animation events and spawning, rejection of stale remote input as an attack trigger, owner-only firing, visual handling, and melee timing.
- [Test-TridentLifetime.ps1](../tests/Test-TridentLifetime.ps1): movement budgets, expiry, wall contact, and remote waiting for the owner's impact.
- [Test-WaterBlastZones.ps1](../tests/Test-WaterBlastZones.ps1): splash contacts and damage boundaries, owner/NID checks, and duplicate-impact suppression.

These tests do not simulate real transport or certify shared damage. The 2026-11-13 attack-animation update passed its offline checks and compiled for .NET 3.5, but only the local machine was updated. Dual-peer animation timing, late-join charge recovery, and multiplayer damage remain unverified; see the [issue record](../issues/2026-11-13-普攻与蓄力攻击动画联机延迟.md). Full skill coverage, cross-map behavior, and three-player sessions also remain outside the verified scope.

Publishing source does not update the bundled DLL or either test machine. Build matching dependencies and the character before testing the current source.

The 2026-11-16 maelstrom regression separates caster ownership from victim ownership, including live/corpse and normal/heavy/subclass cases. It also checks immediate cast/state messages, repeated casts, stale and late updates, conflicting remote terrain and wall removal at 30/60/120 FPS. The compiled skill test executable supports `--maelstrom-network` for these targeted checks; the full four-skill suite includes them. See the [maelstrom issue record](../issues/2026-11-16-漩涡敌人状态联机分歧.md) for deployment and actual transport validation.
