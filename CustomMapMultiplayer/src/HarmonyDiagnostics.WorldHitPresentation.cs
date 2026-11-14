using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace CustomMapMultiplayer
{
    internal static partial class HarmonyDiagnostics
    {
        private sealed class WorldHitPresentation
        {
            internal int Health;
            internal WorldFlags Flags;
            internal Renderer[] Hidden;
            internal WorldEntry Drop;
            internal WorldSnapshot DropBefore;
            internal WorldEntry Crate;
            internal int ArmourDamage = -1, FireDamage;
            internal bool ShieldLost;
            internal float ShieldLostAt;
            internal Material ShieldMaterial;
            internal BlockPiece Background;
        }

        private static readonly FieldInfo WorldGibThreshold = AccessTools.Field(typeof(TestVanDammeAnim), "gibThreshold");
        private static readonly MethodInfo WorldDeathAnimation = AccessTools.Method(typeof(TestVanDammeAnim), "AnimateDeath", null, null);
        private static readonly MethodInfo WorldDeathSound = AccessTools.Method(typeof(TestVanDammeAnim), "PlayDeathSound", null, null);
        private static readonly MethodInfo WorldBlockDeathSound = AccessTools.Method(typeof(Block), "PlayDeathSound", null, null);
        private static readonly FieldInfo WorldFrozenTime = AccessTools.Field(typeof(TestVanDammeAnim), "frozenTime");
        private static readonly FieldInfo WorldBlockBackground = AccessTools.Field(typeof(Block), "backgroundMesh");
        private static readonly Dictionary<Type, bool> WorldPlainDamageTypes = new Dictionary<Type, bool>();
        private static readonly Dictionary<int, byte> WorldPresentedHitEffects = new Dictionary<int, byte>();
        private static readonly MethodInfo WorldCrateMakeEffects = AccessTools.Method(typeof(CrateBlock), "MakeEffects", null, null);
        private static int _worldSuppressCrateDebris;
        private static int _worldSuppressShieldDebris;
        private static int _worldSettlingHit, _worldHitRequestor = -1;

        private static void PatchWorldHitPresentation()
        {
            _harmony.CreateReversePatcher(WorldDeathAnimation, new HarmonyMethod(AccessTools.Method(
                typeof(HarmonyDiagnostics), "WorldBaseDeathAnimation", null, null))).Patch(HarmonyReversePatchType.Original);
            _harmony.CreateReversePatcher(AccessTools.Method(typeof(Block), "EffectsDestroyed", null, null), new HarmonyMethod(AccessTools.Method(
                typeof(HarmonyDiagnostics), "WorldBaseBlockHitEffects", null, null))).Patch(HarmonyReversePatchType.Original);
            foreach (var type in typeof(Unit).Assembly.GetTypes())
                if (typeof(Block).IsAssignableFrom(type))
                    foreach (var method in type.GetMethods(WorldFields | BindingFlags.DeclaredOnly))
                        if (method.Name == "EffectsDestroyed" || method.Name == "EffectsCollapse" || method.Name == "PlayDeathSound")
                            PatchWorldMethod(method, "WorldBlockPresentationEffectPrefix", null);
            foreach (var method in typeof(EffectsController).GetMethods(BindingFlags.Public | BindingFlags.Static))
                if (method.Name == "CreateShrapnel" || method.Name == "CreatePuffDisappearRingEffect")
                    PatchWorldMethod(method, "WorldCrateDebrisPrefix", null);
        }

        // Special receivers first pass their native armour/phase gates below.
        private static bool HasPlainWorldDamage(TestVanDammeAnim unit, DamageType type, byte kind)
        {
            if (kind != 0 || unit.inseminatorUnit != null || unit.hasBeenCoverInAcid ||
                type != DamageType.Explosion && type != DamageType.Melee && type != DamageType.Normal &&
                type != DamageType.Bullet && type != DamageType.Blade && type != DamageType.Crush) return false;
            bool plain;
            var runtimeType = unit.GetType();
            if (WorldPlainDamageTypes.TryGetValue(runtimeType, out plain)) return plain;
            plain = true;
            for (var current = runtimeType; current != typeof(TestVanDammeAnim); current = current.BaseType)
            {
                var method = current.GetMethod("Damage", WorldFields | BindingFlags.DeclaredOnly, null,
                    new[] { typeof(int), typeof(DamageType), typeof(float), typeof(float), typeof(int),
                        typeof(MonoBehaviour), typeof(float), typeof(float) }, null);
                if (method != null && current != typeof(Mook) && current != typeof(MookTrooper) &&
                    current != typeof(MookBigGuy) && current != typeof(MookDog) && current != typeof(MookSuicide) &&
                    current != typeof(MookArmouredGuy) && current != typeof(MookRiotShield) &&
                    current != typeof(MookHellArmouredBigGuy) && current != typeof(MookHellBigGuy))
                { plain = false; break; }
            }
            WorldPlainDamageTypes.Add(runtimeType, plain);
            return plain;
        }

        private static void PresentWorldHit(BroforceObject target, int request, int damage,
            DamageType type, float vx, float vy, byte kind)
        {
            PresentWorldHitDirected(target, request, damage, type, vx, vy, (int)Mathf.Sign(vx), kind);
        }

        private static void PresentWorldHitDirected(BroforceObject target, int request, int damage,
            DamageType type, float vx, float vy, int direction, byte kind)
        {
            if (request == 0 || damage <= 0) return;
            var entry = WorldEntries[target.Nid];
            var presentation = entry.Presentation;
            if (presentation == null)
                entry.Presentation = presentation = new WorldHitPresentation { Health = target.health };
            if (PresentWorldSpecialHit(target, presentation, request, damage, type, vx, vy, direction, kind)) return;
            var unit = target as TestVanDammeAnim;
            if (unit != null)
            {
                if (!HasPlainWorldDamage(unit, type, kind)) return;
                presentation.Health -= damage;
                if ((presentation.Flags & WorldFlags.Gibbed) != 0) return;
                _worldApplying++;
                try
                {
                    if (type == DamageType.Melee)
                    {
                        EffectsController.CreateMeleeStrikeEffect(unit.X, unit.Y + 8f, -vx * .2f, -vy * .5f + 24f);
                        EffectsController.CreateBloodParticles(unit.bloodColor, unit.X, unit.Y + 6f,
                            damage >= 6 ? 20 : 4, 4f, 5f, damage >= 6 ? 50f : 25f, vx * .3f, vy * .3f + 40f);
                        WorldPresentedHitEffects[request] = 3;
                    }
                    if (presentation.Health > 0) return;
                    byte effects;
                    WorldPresentedHitEffects.TryGetValue(request, out effects);
                    WorldPresentedHitEffects[request] = (byte)(effects | 2);
                    if (unit.canGib && (presentation.Health < (int)WorldGibThreshold.GetValue(unit) || (float)WorldFrozenTime.GetValue(unit) > 0f) && !unit.WillReviveAlready)
                    {
                        // The base visual method omits grenades/explosions in Gib overrides.
                        WorldBaseGibEffects(unit, type, unit.xI + vx + unit.xIBlast, unit.yI + vy + unit.yIBlast);
                        presentation.Flags |= WorldFlags.Gibbed;
                        HideWorldHitRenderers(entry);
                    }
                    else if (unit.health > 0 && (presentation.Flags & WorldFlags.Dead) == 0)
                    {
                        presentation.Flags |= WorldFlags.Dead;
                        WorldDeathSound.Invoke(unit, null);
                        EffectsController.CreateBloodParticles(unit.bloodColor, unit.X, unit.Y + 6f, 10, 4f, 5f, 60f, vx * .15f, vy * .15f);
                        ShowWorldPredictedCorpse(unit);
                    }
                }
                finally { _worldApplying--; }
                return;
            }
            var block = target as Block;
            if (block == null || kind != 2 || block.destroyed || block.protectedFromDestruction ||
                block.collapseChance <= 0f || type == DamageType.Fire || type == DamageType.Acid) return;
            presentation.Health -= damage + (type == DamageType.Plasma ? 4 : 0);
            var falling = block as FallingBlock;
            if (presentation.Health > (falling == null ? 0 : falling.collapseHealthPoint) ||
                (presentation.Flags & WorldFlags.Destroyed) != 0) return;
            _worldApplying++;
            try
            {
                var crate = block as CrateBlock;
                if (crate != null)
                {
                    EffectsController.CreateShrapnel(crate.shrapnelPrefab, crate.X, crate.Y, 12f, 150f, 7f, 0f, 120f);
                    EffectsController.CreateShrapnel(crate.shrapnelBitPrefab, crate.X, crate.Y, 12f, 150f, 8f, 0f, 120f);
                    EffectsController.CreateShrapnel(crate.shrapnelBitPrefab3, crate.X, crate.Y, 12f, 150f, 8f, 0f, 120f);
                    WorldBlockDeathSound.Invoke(crate, null);
                    var pickup = WorldCratePickup.GetValue(crate) as Pickupable;
                    if (pickup != null && !pickup.collected && !pickup.gameObject.activeSelf)
                    {
                        presentation.Drop = RegisterWorldObject(pickup);
                        presentation.DropBefore = CaptureWorldState(presentation.Drop);
                        // Reuse the registered child. Never create coins, pigs or new rewards here.
                        pickup.Launch(crate.X, crate.Y, 0f, 200f);
                        presentation.Drop.Presentation = new WorldHitPresentation { Crate = entry };
                        WorldCratePickup.SetValue(crate, null);
                    }
                    if (crate.containsPresent) EffectsController.CreatePuffDisappearRingEffect(crate.X, crate.Y, 0f, 0f);
                }
                else WorldBaseBlockHitEffects(block, vx, vy, 65f + 24f * damage);
                presentation.Flags |= WorldFlags.Destroyed;
                HideWorldHitRenderers(entry);
            }
            finally { _worldApplying--; }
        }

        private static void HideWorldHitRenderers(WorldEntry entry)
        {
            var block = entry.Object as DirtBlock;
            var background = block == null ? null : WorldBlockBackground.GetValue(block) as BlockPiece;
            var renderers = new List<Renderer>();
            foreach (var renderer in entry.Object.GetComponentsInChildren<Renderer>(true))
            {
                // The broken background survives native terrain collapse. Hiding it (or
                // waiting for confirmation to create it) briefly exposes the blue scenery.
                if (background != null && renderer.transform.IsChildOf(background.transform)) continue;
                if (renderer.enabled) { renderers.Add(renderer); renderer.enabled = false; }
            }
            entry.Presentation.Hidden = renderers.ToArray();
            if (block != null && background == null)
            {
                block.ShowBrokenBackground();
                entry.Presentation.Background = WorldBlockBackground.GetValue(block) as BlockPiece;
            }
        }

        private static void ShowWorldPredictedCorpse(TestVanDammeAnim unit)
        {
            var action = unit.actionState;
            var health = unit.health;
            unit.actionState = ActionState.Dead;
            unit.health = 0;
            try
            {
                if (unit.GetType() == typeof(MookArmouredGuy))
                    AccessTools.Method(typeof(MookArmouredGuy), "AnimateDeath", null, null).Invoke(unit, null);
                else WorldBaseDeathAnimation(unit);
            }
            finally { unit.actionState = action; unit.health = health; }
        }

        private static bool WorldBlockPresentationEffectPrefix(Block __instance, MethodBase __originalMethod)
        {
            WorldEntry entry;
            if (!WorldEntries.TryGetValue(__instance.Nid, out entry) ||
                (((entry.Presentation == null ? WorldFlags.None : entry.Presentation.Flags) | entry.PresentedFlags) & WorldFlags.Destroyed) == 0) return true;
            var crate = __instance as CrateBlock;
            if (crate != null && __originalMethod.Name != "PlayDeathSound")
            {
                // Execute reward release once, while omitting debris already shown locally.
                _worldSuppressCrateDebris++;
                try { WorldCrateMakeEffects.Invoke(crate, null); }
                finally { _worldSuppressCrateDebris--; }
            }
            return false;
        }

        private static bool WorldCrateDebrisPrefix() { return _worldSuppressCrateDebris == 0 && _worldSuppressShieldDebris == 0; }

        private static bool WaitForWorldGibConfirmation(WorldEntry entry, WorldSnapshot state)
        {
            // A first hit's corpse can arrive while a newer hit has already
            // presented gibs. Retain those visuals until that newer hit is acknowledged.
            return entry.PendingHit != 0 && entry.Presentation != null &&
                (entry.Presentation.Flags & WorldFlags.Gibbed) != 0 &&
                (state.Flags & WorldFlags.Dead) != 0 && (state.Flags & (WorldFlags.Gibbed | WorldFlags.Removed)) == 0;
        }

        private static void FinishWorldHitPresentation(WorldEntry entry, WorldSnapshot state)
        {
            var presentation = entry.Presentation;
            if (presentation == null) return;
            FinishWorldSpecialHit(entry, state);
            var confirmed = state != null && (state.Flags & (WorldFlags.Dead | WorldFlags.Gibbed | WorldFlags.Destroyed | WorldFlags.Removed)) != 0;
            if (!confirmed && presentation.Background != null)
            {
                WorldBlockBackground.SetValue(entry.Object, null);
                UnityEngine.Object.Destroy(presentation.Background.gameObject);
            }
            if (presentation.Hidden != null && (!confirmed || (state.Flags & WorldFlags.Gibbed) == 0 && entry.Object is Unit))
                foreach (var renderer in presentation.Hidden) if (renderer != null) renderer.enabled = true;
            if (presentation.Drop != null && !confirmed)
            {
                var pickup = presentation.Drop.Object as Pickupable;
                presentation.Drop.Presentation = null;
                if (pickup != null && !pickup.collected)
                {
                    ApplyWorldPickupState(pickup, presentation.DropBefore);
                    pickup.transform.parent = entry.Object.transform;
                    WorldCratePickup.SetValue(entry.Object, pickup);
                }
            }
            // Keep terminal visual deduplication until its authoritative lifecycle runs.
            entry.Presentation = null;
            if (confirmed) entry.PresentedFlags |= presentation.Flags & state.Flags;
            else if (entry.Object is TestVanDammeAnim && (presentation.Flags & WorldFlags.Dead) != 0)
                AccessTools.Method(typeof(TestVanDammeAnim), "ChangeFrame", null, null).Invoke(entry.Object, null);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void WorldBaseDeathAnimation(TestVanDammeAnim unit) { throw new NotSupportedException(); }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void WorldBaseBlockHitEffects(Block block, float vx, float vy, float force) { throw new NotSupportedException(); }
    }
}
