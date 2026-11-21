# Aquabro multiplayer integration

This is a character-specific integration example for the shared RocketLib and BroMaker networking changes. The implementation was generated with AI; testing covers selected offline cases and limited two-player sessions, not every multiplayer scenario.

Updated 2026-11-21. The [current deployment and validation summary](../当前角色实际应用状态.md) records the shipped DLL, matching peer deployments and remaining work. Dated issue records retain the evidence and limits of each repair.

## Dependencies

Use matching framework builds on every peer. These source changes depend on the networking work in [RocketLib PR #1](https://github.com/Gorzontrok/RocketLib/pull/1) and [BroMaker PR #29](https://github.com/Gorzontrok/Bro-Maker/pull/29). Existing published framework packages may not contain those changes. Both peers also need the same Aquabro code and resources.

The repository records compatible framework revisions through its RocketLibMultiplayer and BroMakerMultiplayer submodules. BroMaker's PR branch contains the same public framework sources as the recorded BroMaker revision; it excludes historical local test reports.

The additional 60 Hz trident state and smoothing path is integrated into CustomMapMultiplayer. Its final dual-peer flight-continuity acceptance remains pending; see the [trident issue](../issues/2026-11-15-三叉戟飞行联机断续.md).

## Code to review

| Behavior | Implementation |
| --- | --- |
| Normal and charged trident spawning | `FireTrident` in [Aquabro.cs](../src/Aquabro.cs) uses the game's `SpawnProjectileOverNetwork` entry point on the owning character with immediate execution. Remote characters do not initiate a second spawn. |
| Charge and throw animation | `BroadcastTridentAnimation` sends a reliable immediate event when the owner enters charge, throw recovery or idle/cancellation. Remote characters advance that visual state instead of reconstructing attacks from interpolated input snapshots. |
| Trident impact and splash | `ResolveWaterImpact` reuses the registered spear's native `MakeEffects` RPC with immediate execution. The remote path requires the matching projectile NID and sender owner before applying the splash, and the impact marker prevents repeated application to the same instance. |
| Projectile lifetime | `RunTridentLife`, `LimitTridentStep`, and `RestoreTridentStep` stop remote travel at expiry while retaining the object for the owner's impact notification. They also preserve the charged spear's per-frame movement budget. |
| Skill selection and casting | [AquabroSkills.cs](../src/AquabroSkills.cs) sends character-specific selection and cast events; remote peers apply the corresponding visuals. |
| Shark control | The owner sends immediate position, facing, cast ID and cast-age updates at up to 30 Hz. Remote submerged sharks smooth the authoritative samples with at most 100 ms of velocity prediction; they do not replay hero input or query local terrain. Older samples and updates from another cast are ignored. Manual and timeout breaches use one immediate reliable event with the exact launch position, including events received before the remote visual exists. Both peers require this matching RPC signature. |
| Maelstrom control | The caster sends an immediate reliable cast ID and authoritative position, age, route bounds and remaining route state. Remote visuals follow that route instead of checking their own terrain. Each victim's owner applies continuous suction and heavy-Mook control; replicas do not apply a second impulse. Pulse and final damage remain caster-only. |
| Damage | Relevant damage paths run on the owning character. This is an implementation choice to review; it is not proof that every target or skill has correct shared damage. |

Other character authors can reuse the public framework interfaces, but must adapt their own random outcomes, skill state, object spawning, event handling, and damage rules. Aquabro's skill RPCs are not a general-purpose framework protocol.

## Validation and limits

### Current evidence

The three 2026-11-21 size-reduction rounds passed the recorded offline regressions, comparisons between old and new game processes, and targeted two-player checks. The latest round used matching DLLs on the local host and the 5700G client; see the [third-round report](精简批量补充验证-2026-11-21.md).

| Area | Confirmed scope |
| --- | --- |
| Normal and charged tridents | Both peers threw at the expected speeds, observed the expected projectile and splash counts, and retained owner-only firing and charged-blast resolution. |
| Four skills | Targeted casts from both peers covered visuals, damage and suction authority, the seven-column shared damage budget, shark steering and breach events, ammo, HUD selection and effect cleanup. |
| Shark movement repair | The 2026-11-17 owner/observer descent probes agreed and preserved pause; the user accepted the dual-peer gameplay result. The initial Inspector authentication failure was resolved during that repair. See the [shark record](../issues/2026-11-17-鲨鱼潜行远端移动顿挫.md). |
| Maelstrom ownership and route | Offline ownership/route cases and actual transport checks are recorded in the [maelstrom issue](../issues/2026-11-16-漩涡敌人状态联机分歧.md); the 2026-11-21 report includes casts from both peers. |

### Regression entry points

The checked-in offline tests exercise production code with controlled dependencies. They support the recorded cases; the game-session evidence above establishes actual transport coverage.

- [Test-Charge.ps1](../tests/Test-Charge.ps1): charge, interruptions, immediate animation/spawn events, rejection of stale remote input as an attack trigger, owner-only firing and melee timing.
- [Test-TridentLifetime.ps1](../tests/Test-TridentLifetime.ps1): movement budgets, expiry, wall contact and remote waiting for the owner's impact.
- [Test-WaterBlastZones.ps1](../tests/Test-WaterBlastZones.ps1): splash contacts, damage boundaries, owner/NID checks and duplicate-impact suppression.
- [Test-Spring.ps1](../tests/Test-Spring.ps1): the complete four-skill suite. Its compiled executable also supports `--maelstrom-network` and `--shark-network` for the targeted ownership, route and movement cases described in the issue records.

Environment requirements, the remaining regression entry points and probe limits are maintained in the [test guide](../tests/Readme.md).

### Remaining scope

Precise attack-animation latency, late-join charge recovery and the final trident flight-continuity gameplay check remain open. The targeted results also leave all-map coverage, full Boss fights, three-player sessions and extreme latency/loss matrices unverified. The [attack-animation record](../issues/2026-11-13-普攻与蓄力攻击动画联机延迟.md) distinguishes its initial local-only deployment from the later two-peer projectile checks.
