using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace CustomMapMultiplayer
{
    internal static partial class HarmonyDiagnostics
    {
        private static readonly Dictionary<Type, Dictionary<string, FieldInfo>> WorldHitFields =
            new Dictionary<Type, Dictionary<string, FieldInfo>>();
        private static readonly FieldInfo WorldRelayDestroyed = AccessTools.Field(typeof(DamageRelay), "isDestroyed");

        private static FieldInfo WorldHitField(object obj, string name)
        {
            var type = obj.GetType();
            Dictionary<string, FieldInfo> fields;
            if (!WorldHitFields.TryGetValue(type, out fields))
                WorldHitFields.Add(type, fields = new Dictionary<string, FieldInfo>());
            FieldInfo field;
            if (!fields.TryGetValue(name, out field)) fields.Add(name, field = AccessTools.Field(type, name));
            return field;
        }

        private static bool IsWorldArmour(BroforceObject obj)
        {
            return obj is MookRiotShield || obj.GetType() == typeof(MookArmouredGuy) || obj is MookHellArmouredBigGuy;
        }

        private static bool IsWorldBossPart(BroforceObject obj)
        {
            return obj is BossPiece || obj is BossBlockPiece || obj is BossBlockWeapon ||
                obj is BossSlugFace || obj is BossSatanFloatingHead || obj is BossSatanArm;
        }

        private static bool HasWorldOwnedHitPresentation(BroforceObject obj)
        {
            return IsWorldBossPart(obj) || obj is Tank || obj is Mech || obj is TankWeapon || obj is MechWeapon;
        }

        private static bool WorldBossReplayRpcPrefix(object targetObject)
        { return _worldApplying == 0 || !IsWorldBossPart(targetObject as BroforceObject); }

        private static void CaptureWorldSpecialHitState(BroforceObject obj, WorldSnapshot state)
        {
            var unit = obj as Unit;
            state.Invulnerable = unit != null && unit.invulnerable;
            if (obj is MookGunplacement) state.GunnerEjected = (bool)WorldHitField(obj, "mookKilled").GetValue(obj);
            var mech = obj as Mech;
            if (mech != null)
            {
                state.Facing = mech.facingDirection;
                state.Turn = (int)WorldHitField(mech, "turnFrame").GetValue(mech);
                if (((Component)WorldHitField(mech, "mechAnimation").GetValue(mech)).gameObject.activeSelf) state.Flags |= WorldFlags.MechAnimated;
            }
            if (IsWorldArmour(obj))
            {
                state.ArmourDamage = (int)WorldHitField(obj, "shieldDamage").GetValue(obj);
                state.FireDamage = (int)WorldHitField(obj, "fireAmount").GetValue(obj);
                if (obj is MookRiotShield) state.ShieldTime = (float)WorldHitField(obj, "shieldTime").GetValue(obj);
                else state.ArmourKnocks = (int)WorldHitField(obj, "knockCount").GetValue(obj);
            }
            if (IsWorldBossPart(obj) && (bool)WorldHitField(obj, "isDead").GetValue(obj)) state.Flags |= WorldFlags.Dead;
            if (obj is BossPiece)
            {
                state.BossDamaged = (bool)WorldHitField(obj, "isDamaged").GetValue(obj);
                state.Invulnerable = ((BossPiece)obj).immortal;
            }
            if (obj is BossBlockPieceSheathed) state.Invulnerable = ((BossBlockPieceSheathed)obj).indestructible;
            if (obj is BossBlockWeapon) state.Invulnerable = ((BossBlockWeapon)obj).indestructible;
        }

        private static void ApplyWorldSpecialHitState(BroforceObject obj, WorldSnapshot state)
        {
            var unit = obj as Unit;
            if (unit != null) unit.invulnerable = state.Invulnerable;
            var gunplacement = obj as MookGunplacement;
            if (gunplacement != null)
            {
                // SpawnMook already network-spawns the ejected soldier on the owner.
                // Replay only the seat state, including before a later Death replay.
                WorldHitField(gunplacement, "mookKilled").SetValue(gunplacement, state.GunnerEjected);
                gunplacement.driverSprite.gameObject.SetActive(!state.GunnerEjected);
            }
            var piece = obj as BossPiece;
            if (piece != null) piece.immortal = state.Invulnerable;
            if (obj is BossBlockPieceSheathed) ((BossBlockPieceSheathed)obj).indestructible = state.Invulnerable;
            if (obj is BossBlockWeapon) ((BossBlockWeapon)obj).indestructible = state.Invulnerable;
            var mechWeapon = obj as MechWeapon;
            if (mechWeapon != null && mechWeapon.mech is GoliathMech && mechWeapon.mech.health <= 0 &&
                (state.Flags & WorldFlags.Dead) != 0)
            {
                // Goliath's lethal child Damage ends in RunHurt restoring this material.
                // Replicas replay Death without that damage timer.
                mechWeapon.GetComponent<Renderer>().sharedMaterial = (Material)WorldHitField(mechWeapon, "baseMaterial").GetValue(mechWeapon);
            }
            var mech = obj as Mech;
            if (mech != null && state.Health > 0)
            {
                mech.facingDirection = state.Facing;
                WorldHitField(mech, "turnFrame").SetValue(mech, state.Turn);
                // A state packet can finish Turning before the replica's animation does.
                // Keep its native weapon hierarchy in the owner's confirmed visual mode.
                if ((state.Flags & WorldFlags.MechAnimated) != 0)
                {
                    if (!((Component)WorldHitField(mech, "mechAnimation").GetValue(mech)).gameObject.activeSelf)
                        AccessTools.Method(typeof(Mech), "FinishTurning", null, null).Invoke(mech, null);
                }
                else AccessTools.Method(typeof(Mech), "SetSpriteTurn", null, null).Invoke(mech, new object[] { state.Turn });
            }
            if (mech != null && state.Health < state.MaxHealth / 2)
            {
                // Mech.Damage and GoliathMech.Damage set these after crossing half health.
                // Restore the confirmed phase without replaying damage or death on the replica.
                mech.tankSpeed = mech.ultraTankSpeed;
                mech.maxUpwardVelocity = mech.ultraMaxUpwardVelocity;
                var goliath = mech as GoliathMech;
                if (goliath != null) goliath.eye.GetComponent<Renderer>().sharedMaterial = goliath.angryEyeMaterial;
            }
            if (piece != null && (bool)WorldHitField(piece, "isDamaged").GetValue(piece) != state.BossDamaged)
            {
                WorldHitField(piece, "isDamaged").SetValue(piece, state.BossDamaged);
                if ((state.Flags & WorldFlags.Dead) == 0 && (float)WorldHitField(piece, "flashCounter").GetValue(piece) <= 0f)
                    piece.GetComponent<Renderer>().sharedMaterial = state.BossDamaged ? piece.damagedMaterial :
                        (Material)WorldHitField(piece, "baseMaterial").GetValue(piece);
            }
            if (!IsWorldArmour(obj)) return;
            WorldHitField(obj, "shieldDamage").SetValue(obj, state.ArmourDamage);
            WorldHitField(obj, "fireAmount").SetValue(obj, state.FireDamage);
            if (obj is MookRiotShield) WorldHitField(obj, "shieldTime").SetValue(obj, state.ShieldTime);
            else WorldHitField(obj, "knockCount").SetValue(obj, state.ArmourKnocks);
        }

        // Resolve the original contact, retaining every relay's immunity/collider gate.
        // No global collider or object scan is required for a hit.
        private static BroforceObject ResolveWorldHitReceiver(GameObject receiver, DamageType type, out DamageType actualType)
        {
            actualType = type;
            var visited = new HashSet<GameObject>();
            while (receiver != null && visited.Add(receiver))
            {
                var relay = receiver.GetComponent<DamageRelay>();
                if (relay == null) return receiver.GetComponent<BroforceObject>();
                if ((bool)WorldRelayDestroyed.GetValue(relay) || WorldHitColliderActive(relay.ignoreDamageWhileColliderActive) ||
                    WorldHitColliderActive(relay.ignoreDamageWhileOtherColliderActive)) return null;
                if (type != relay.immunity)
                {
                    if (relay.unit != null)
                    {
                        actualType = relay.forceDamageType == DamageType.None ? type : relay.forceDamageType;
                        return relay.unit;
                    }
                    if (relay.doodad != null) return relay.doodad;
                    if (relay.bossWeapon != null) return relay.bossWeapon;
                }
                receiver = relay.damageGameObject;
            }
            return null;
        }

        private static bool WorldHitColliderActive(Collider collider)
        { return collider != null && collider.enabled && collider.gameObject.activeInHierarchy; }

        private static bool PresentWorldSpecialHit(BroforceObject target, WorldHitPresentation p, int request,
            int damage, DamageType type, float vx, float vy, int direction, byte kind)
        {
            var unit = target as Unit;
            if (unit != null && unit.invulnerable) return true;
            if (IsWorldArmour(target))
            {
                if (kind != 0 || type != DamageType.Explosion && type != DamageType.Melee &&
                    type != DamageType.Bullet && type != DamageType.Normal && type != DamageType.Blade && type != DamageType.Crush) return true;
                if (p.ArmourDamage < 0)
                {
                    p.ArmourDamage = (int)WorldHitField(target, "shieldDamage").GetValue(target);
                    p.FireDamage = (int)WorldHitField(target, "fireAmount").GetValue(target);
                }
                p.ArmourDamage += damage;
                var shield = target as MookRiotShield;
                bool accepted;
                if (shield != null)
                {
                    var hasShield = shield.hasShield && !p.ShieldLost;
                    var shieldTime = p.ShieldLost ? .08f - (Time.time - p.ShieldLostAt) :
                        (float)WorldHitField(shield, "shieldTime").GetValue(shield);
                    accepted = type == DamageType.Crush || unit.actionState == ActionState.Panicking ||
                        !hasShield && shieldTime < 0f || p.Health <= 0 || p.FireDamage > 35 || direction == 0 ||
                        direction == (int)Mathf.Sign(shield.transform.localScale.x);
                    if (!accepted) shield.Knock(type, vx * .05f, 15f, false);
                    if (p.Health - (accepted ? damage : 0) > 0 && hasShield &&
                        (type == DamageType.Explosion || type == DamageType.Melee || p.ArmourDamage > 35))
                    {
                        p.ShieldLost = true; p.ShieldLostAt = Time.time;
                        var renderer = shield.GetComponent<Renderer>();
                        p.ShieldMaterial = renderer.sharedMaterial;
                        renderer.sharedMaterial = shield.unarmedMaterial;
                        // Only the shield visual, never DisarmShield's physics/state mutation.
                        EffectsController.CreateShrapnel(shield.shieldShrapnel, shield.X + shield.transform.localScale.x * 8f,
                            shield.Y + 8f, .1f, 96f, 1f, shield.xI * .4f, shield.yI * .3f + 200f);
                    }
                }
                else
                {
                    accepted = (type == DamageType.Crush || unit.actionState == ActionState.Panicking ||
                        p.FireDamage > 35 || p.ArmourDamage > 60) && p.Health > 0;
                    if (target is MookHellArmouredBigGuy && direction != 0 && Mathf.Sign(direction) == target.transform.localScale.x)
                        accepted = true;
                    if (!accepted && p.Health > 0)
                        AccessTools.Method(target.GetType(), "PlayDefendSound", null, null).Invoke(target, null);
                }
                // Armour corpses have a separate native lifetime; never turn them into ordinary gibs.
                if (!accepted || p.Health <= 0) return true;
                if (type != DamageType.Melee)
                {
                    var tvd = (TestVanDammeAnim)unit;
                    EffectsController.CreateBloodParticles(tvd.bloodColor, unit.X, unit.Y + 6f, 4, 4f, 5f, 25f, vx * .3f, vy * .3f + 40f);
                    WorldPresentedHitEffects[request] = 2;
                }
                return false;
            }
            if (target.health <= 0) return IsWorldBossPart(target) || target is Tank || target is Mech;
            if (target is Tank || target is Mech)
            {
                AccessTools.Method(target.GetType(), "SetHurtMaterial", null, null).Invoke(target, null);
                return true;
            }
            if (target is TankWeapon || target is MechWeapon)
            {
                target.GetComponent<Renderer>().sharedMaterial = (Material)WorldHitField(target, "hurtMaterial").GetValue(target);
                WorldHitField(target, "hurtCounter").SetValue(target, .0334f);
                return true;
            }
            var satan = target as SatanMiniboss;
            if (satan != null)
            {
                var forceField = WorldHitField(satan, "forceField").GetValue(satan);
                if (forceField != null && !forceField.Equals(null) || damage > 1000) return true;
                // The phase transition is authoritative. An eligible hit can show blood immediately.
                EffectsController.CreateBloodParticles(satan.bloodColor, satan.X, satan.Y + 6f, 4, 4f, 5f, 25f, vx * .3f, vy * .3f + 40f);
                WorldPresentedHitEffects[request] = 2;
                return true;
            }
            var block = target as BossBlockPiece;
            if (block != null)
            {
                var sheathed = block as BossBlockPieceSheathed;
                if (block.IsDead || type == block.damageTypeImmunity || sheathed != null && sheathed.indestructible ||
                    block.ignoreDamageWhileOtherPieceAlive != null && block.ignoreDamageWhileOtherPieceAlive.health > 0) return true;
                block.GetComponent<Renderer>().sharedMaterial = block.hurtMaterial;
                WorldHitField(block, "hurtTime").SetValue(block, .05f);
                block.enabled = true;
                return true;
            }
            var piece = target as BossPiece;
            if (piece != null)
            {
                if (piece.immortal || (bool)WorldHitField(piece, "isDead").GetValue(piece)) return true;
                piece.GetComponent<Renderer>().sharedMaterial = (bool)WorldHitField(piece, "isDamaged").GetValue(piece) ? piece.damagedHurtMaterial : piece.hurtMaterial;
                WorldHitField(piece, "flashCounter").SetValue(piece, .0334f);
                return true;
            }
            var weapon = target as BossBlockWeapon;
            if (weapon != null)
            {
                if (weapon.indestructible || (bool)WorldHitField(weapon, "isDead").GetValue(weapon)) return true;
                AccessTools.Method(typeof(BossBlockWeapon), "SetMaterial", null, null).Invoke(weapon, new object[] { weapon.hurtMaterial });
                WorldHitField(weapon, "hurtTime").SetValue(weapon, .05f);
                weapon.enabled = true;
                return true;
            }
            if (target is BossSatanFloatingHead || target is BossSatanArm)
            {
                if (type != DamageType.Unholy && !(bool)WorldHitField(target, "isDead").GetValue(target))
                    AccessTools.Method(target.GetType(), "SetHurtMaterial", null, null).Invoke(target, null);
                return true;
            }
            var slug = target as BossSlugFace;
            if (slug != null)
            {
                if (!(bool)WorldHitField(slug, "isDead").GetValue(slug))
                {
                    WorldHitField(slug, "hurtTime").SetValue(slug, .0667f);
                    slug.GetComponent<Renderer>().material.SetColor("_TintColor", new Color(.9f, 0f, 0f, 1f));
                }
                return true;
            }
            return false;
        }

        private static void FinishWorldSpecialHit(WorldEntry entry, WorldSnapshot state)
        {
            var shield = entry.Object as MookRiotShield;
            if (shield == null || !entry.Presentation.ShieldLost || !shield.hasShield) return;
            if (state == null || (state.Flags & WorldFlags.Shield) != 0)
            {
                shield.GetComponent<Renderer>().sharedMaterial = entry.Presentation.ShieldMaterial;
                return;
            }
            // Apply the confirmed disarm while its visual prediction still exists.
            // EndWorldHitPresentation clears it before replaying the saved state.
            _worldApplying++;
            try { WorldDisarmShield.Invoke(shield, new object[] { state.VX }); }
            finally { _worldApplying--; }
        }
    }
}
