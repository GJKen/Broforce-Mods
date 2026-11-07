# Aquabro multiplayer integration

This is a character-specific integration example for the shared RocketLib and BroMaker networking changes. The implementation was generated with AI; testing covers selected offline cases and limited two-player sessions, not every multiplayer scenario.

## Dependencies

Use matching framework builds on every peer. These source changes depend on the networking work in [RocketLib PR #1](https://github.com/Gorzontrok/RocketLib/pull/1) and [BroMaker PR #29](https://github.com/Gorzontrok/Bro-Maker/pull/29). Existing published framework packages may not contain those changes. Both peers also need the same Aquabro code and resources.

The repository records compatible framework revisions through its RocketLibMultiplayer and BroMakerMultiplayer submodules. BroMaker's PR branch contains the same public framework sources as the recorded BroMaker revision; it excludes historical local test reports.

## Code to review

| Behavior | Implementation |
| --- | --- |
| Normal and charged trident spawning | `FireTrident` in [Aquabro.cs](../src/Aquabro.cs) uses the game's `SpawnProjectileOverNetwork` entry point on the owning character. Remote characters retain their animation without initiating a second spawn. |
| Trident impact and splash | `ResolveWaterImpact` reuses the registered spear's native `MakeEffects` RPC. The remote path requires the matching projectile NID and sender owner before applying the splash, and the impact marker prevents repeated application to the same instance. |
| Projectile lifetime | `RunTridentLife`, `LimitTridentStep`, and `RestoreTridentStep` stop remote travel at expiry while retaining the object for the owner's impact notification. They also preserve the charged spear's per-frame movement budget. |
| Skill selection and casting | [AquabroSkills.cs](../src/AquabroSkills.cs) sends character-specific selection and cast events; remote peers apply the corresponding visuals. |
| Shark control | Shark position and direction use state updates. Breaching uses a separate reliable event, including handling when that event arrives before the remote shark visual exists. |
| Damage | Relevant damage paths run on the owning character. This is an implementation choice to review; it is not proof that every target or skill has correct shared damage. |

Other character authors can reuse the public framework interfaces, but must adapt their own random outcomes, skill state, object spawning, event handling, and damage rules. Aquabro's skill RPCs are not a general-purpose framework protocol.

## Validation and limits

Earlier limited two-player testing covered normal character registration, normal and charged tridents in both ownership directions, matching impact positions and splashes, and selected skill and short-melee behavior. Historical failures and different installed versions must not be combined into a claim that all mechanisms pass on the latest framework.

The checked-in offline tests cover extracted production methods with controlled dependencies:

- [Test-Charge.ps1](../tests/Test-Charge.ps1): charge and interruption timing, the network spawn call, owner-only firing, visual handling, and melee timing.
- [Test-TridentLifetime.ps1](../tests/Test-TridentLifetime.ps1): movement budgets, expiry, wall contact, and remote waiting for the owner's impact.
- [Test-WaterBlastZones.ps1](../tests/Test-WaterBlastZones.ps1): splash contacts and damage boundaries, owner/NID checks, and duplicate-impact suppression.

These tests do not simulate real transport or certify shared damage. After the latest RocketLib upstream merge, the public frameworks were compiled and their offline fixtures passed; a full in-game regression was not rerun. Full skill coverage, shared damage across all targets, cross-map behavior, and three-player sessions remain outside the verified scope.

For the 2026-11-07 source publication, Aquabro also compiled against those updated framework builds and all three tests above passed, including 1,363 lifetime checks and 24,576 pixel-contact comparisons. This was an offline check, with no game launch or deployment.

Publishing this source does not replace the bundled Aquabro DLL or deploy it to either test machine. Build matching dependencies and the character before testing the current source.
