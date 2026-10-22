using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace AquabroMod
{
    // A collision receiver is not necessarily a Unit. Keep the native relay /
    // part entry point, and use its actual damage recipients for hit budgets.
    internal sealed class CollisionDamageTarget
    {
        internal readonly Collider Contact;
        internal readonly object Key;
        internal Unit UnitTarget { get { return Key as Unit; } }
        internal bool IsWaveBoss { get { return UnitTarget != null ? IsWaveUnit(UnitTarget) : !(Key is Block); } }
        // Solid non-unit damage receivers (boss bodies/parts), never crates or cages.
        internal bool StopsWave
        {
            get
            {
                if (Key is Block || !Active(Contact)) return false;
                if (Key is SawBlade) return false;
                if (UnitTarget != null) return IsWaveUnit(UnitTarget) && UnitTarget.health > 0;
                BossPiece piece = Key as BossPiece;
                if (piece != null) return piece.health > 0;
                BossBlockPiece block = Key as BossBlockPiece;
                if (block != null) return !block.IsDead && block.BossBlockHealth > 0;
                BossBlockWeapon weapon = Key as BossBlockWeapon;
                if (weapon != null) return weapon.health > 0;
                BossSlugFace face = Key as BossSlugFace;
                if (face != null) return face.health > 0;
                BossSatanFloatingHead head = Key as BossSatanFloatingHead;
                if (head != null) return head.health > 0;
                BossSatanArm arm = Key as BossSatanArm;
                return arm == null || arm.health > 0;
            }
        }
        private readonly List<DamageRelay> relays;
        private readonly bool routeOpen;
        private readonly GameObject receiver;
        private readonly DamageType requestedType;
        private static readonly Dictionary<Type, bool> DamageHandlers = new Dictionary<Type, bool>();
        private static readonly FieldInfo RelayDestroyed = typeof(DamageRelay).GetField("isDestroyed",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo SlugDead = typeof(BossSlugFace).GetField("isDead",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo SatanHeadDead = typeof(BossSatanFloatingHead).GetField("isDead",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo SatanArmDead = typeof(BossSatanArm).GetField("isDead",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private CollisionDamageTarget(Collider contact, object key, GameObject receiver,
            List<DamageRelay> relays, bool routeOpen, DamageType type)
        { Contact = contact; Key = key; this.receiver = receiver; this.relays = relays; this.routeOpen = routeOpen; requestedType = type; }

        internal static CollisionDamageTarget Find(Collider contact)
        { return Find(contact, DamageType.Explosion); }

        // Skills share one budget for a cage's body and its individual bars.
        // Props remain separate from terrain so delayed explosions cannot stop a wave.
        internal static CollisionDamageTarget FindSkill(Collider contact)
        {
            if (contact == null || contact.isTrigger) return null;
            // SawBlade is not in destroyableDoodads and DamageOptional always
            // rejects hits. Keep its native Damage/Detach/Death entry instead.
            SawBlade saw = contact.GetComponent<SawBlade>();
            if (saw != null)
                return new CollisionDamageTarget(contact, saw, contact.gameObject, null, true, DamageType.Explosion);
            Unit unit = contact.GetComponent<Unit>();
            if (IsWaveUnit(unit))
                return new CollisionDamageTarget(contact, unit, contact.gameObject, null, true, DamageType.Explosion);
            Block prop = contact.GetComponent<FallingBlock>();
            if (prop == null) prop = contact.GetComponent<Cage>();
            if (prop == null && contact.GetComponent<CageBar>() != null)
                prop = contact.GetComponentInParent<Cage>();
            return prop == null ? Find(contact) :
                new CollisionDamageTarget(contact, prop, contact.gameObject, null, true, DamageType.Explosion);
        }

        internal static bool IsWaveUnit(Unit unit)
        {
            return unit is Tank || unit is TankWeapon || unit is Mech || unit is MechWeapon ||
                unit is AlienSandWorm || unit is SatanMiniboss || unit is MookVehicleDigger || unit is MookBigGuy;
        }

        internal static CollisionDamageTarget Find(Collider contact, DamageType type)
        {
            if (contact == null || contact.isTrigger) return null;
            GameObject receiver = contact.gameObject;
            List<DamageRelay> relays = null;
            HashSet<GameObject> visited = null;
            object key = null;
            bool open = true;
            while (true)
            {
                if (receiver == null) return null;
                DamageRelay relay = receiver.GetComponent<DamageRelay>();
                if (relay == null) break;
                if (visited == null) { visited = new HashSet<GameObject>(); relays = new List<DamageRelay>(); }
                if (!visited.Add(receiver)) return null;
                relays.Add(relay);
                // Match DamageRelay.Damage's branch order, including its
                // damageGameObject fallback when a typed branch is immune.
                open &= RelayOpen(relay, type);
                if (relay.immunity != type)
                {
                    if (relay.unit != null)
                    {
                        key = relay.unit; receiver = relay.unit.gameObject;
                        break;
                    }
                    if (relay.doodad != null) return null; // Existing scenery pass owns doodads.
                    if (relay.bossWeapon != null) { key = relay.bossWeapon; break; }
                }
                if (relay.damageGameObject == null)
                {
                    // Keep the unit identity even when closed, so its body
                    // pass cannot bypass the relay's armour/immunity gate.
                    if (relay.unit != null) { key = relay.unit; receiver = relay.unit.gameObject; }
                    else if (relay.bossWeapon != null) key = relay.bossWeapon;
                    else return null;
                    open = false; break;
                }
                receiver = relay.damageGameObject;
            }
            // Terrain and props retain their existing damage/stop rules.
            // Explicit unit relays join this pass; direct units keep body geometry.
            if (key == null && (receiver.GetComponent<Block>() != null || receiver.GetComponent<Doodad>() != null)) return null;
            if (key == null && receiver.GetComponent<Unit>() != null)
            {
                if (relays == null) return null;
                key = receiver.GetComponent<Unit>();
            }
            // These adapters provide eligibility and propagation budgets only;
            // discovering a new Damage receiver does not require a new type.
            if (key == null) key = receiver.GetComponent<BossPiece>();
            if (key == null) key = receiver.GetComponent<BossBlockPiece>();
            if (key == null) key = receiver.GetComponent<BossBlockWeapon>();
            if (key == null) key = receiver.GetComponent<BossSlugFace>();
            if (key == null) key = receiver.GetComponent<BossSatanFloatingHead>();
            if (key == null) key = receiver.GetComponent<BossSatanArm>();
            if (key == null && HasDamageHandler(receiver)) key = receiver;
            return key == null ? null : new CollisionDamageTarget(contact, key, receiver, relays, open, type);
        }

        private static bool HasDamageHandler(GameObject receiver)
        {
            foreach (object component in receiver.GetComponents(typeof(MonoBehaviour)))
            {
                if (component == null) continue;
                Type type = component.GetType();
                bool handles;
                if (!DamageHandlers.TryGetValue(type, out handles))
                {
                    handles = false;
                    for (Type declaring = type; declaring != null && !handles; declaring = declaring.BaseType)
                        handles = declaring.GetMethod("Damage", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                            null, new Type[] { typeof(DamageObject) }, null) != null;
                    DamageHandlers[type] = handles;
                }
                if (handles) return true;
            }
            return false;
        }

        internal static bool RelayOpen(DamageRelay relay, DamageType type)
        {
            return relay == null ||
                ((RelayDestroyed == null || !(bool)RelayDestroyed.GetValue(relay)) &&
                (relay.immunity != type || relay.damageGameObject != null) &&
                !Active(relay.ignoreDamageWhileColliderActive) && !Active(relay.ignoreDamageWhileOtherColliderActive));
        }

        private static bool Active(Collider collider)
        { return collider != null && collider.enabled && collider.gameObject.activeInHierarchy; }

        internal bool CanDamage(int playerNum, DamageType type)
        {
            if (type != requestedType && relays != null)
            {
                CollisionDamageTarget route = Find(Contact, type);
                return route != null && route.CanDamage(playerNum, type);
            }
            if (!Active(Contact) || Contact.isTrigger || !routeOpen || receiver == null || !receiver.activeInHierarchy) return false;
            if (relays != null)
                foreach (DamageRelay relay in relays) if (!RelayOpen(relay, type)) return false;
            Unit unit = UnitTarget;
            SawBlade saw = Key as SawBlade;
            if (saw != null) return !saw.hidden && (!saw.IsDetached() || saw.invulnerabilityTime < 0f) &&
                (playerNum >= 0 ? !saw.immuneToHeroDamage : saw.CanBeDamagedByMooks);
            Block prop = Key as Block;
            if (prop != null) return prop.health > 0;
            if (unit != null) return !unit.invulnerable && unit.gameObject.activeInHierarchy &&
                GameModeController.DoesPlayerNumDamage(playerNum, unit.playerNum);
            BossPiece piece = Key as BossPiece;
            if (piece != null) return piece.health > 0 && !piece.immortal &&
                GameModeController.DoesPlayerNumDamage(playerNum, piece.playerNum);
            BossBlockPiece block = Key as BossBlockPiece;
            if (block != null) return !block.IsDead && block.BossBlockHealth > 0 && block.damageTypeImmunity != type &&
                (block.ignoreDamageWhileOtherPieceAlive == null || block.ignoreDamageWhileOtherPieceAlive.BossBlockHealth <= 0);
            // The slug head is neither Unit nor BossBlockPiece. Its mouth and
            // outer head colliders all relay to this one native health pool.
            BossSlugFace face = Key as BossSlugFace;
            if (face != null) return face.health > 0 && (SlugDead == null || !(bool)SlugDead.GetValue(face));
            // Head-side relays and foreground arm relays have their own health
            // pools. Keep armour/collider gates on the original relay; never
            // merge the six independent arms into the head's damage budget.
            BossSatanFloatingHead head = Key as BossSatanFloatingHead;
            if (head != null) return type != DamageType.Unholy && head.health > 0 &&
                (SatanHeadDead == null || !(bool)SatanHeadDead.GetValue(head));
            BossSatanArm arm = Key as BossSatanArm;
            if (arm != null) return type != DamageType.Unholy && arm.health > 0 &&
                (SatanArmDead == null || !(bool)SatanArmDead.GetValue(arm));
            BossBlockWeapon weapon = Key as BossBlockWeapon;
            if (weapon != null) return weapon.health > 0 && !weapon.indestructible;
            // Unknown receivers enforce their own health/phase rules in native
            // Damage. Count submissions; do not infer fields or bypass Damage.
            return true;
        }

        internal static object[] DamageKeys(object target)
        {
            BossPiece piece = target as BossPiece;
            if (piece == null || piece.connectedPieces == null || piece.connectedPieces.Count == 0)
                return new object[] { target };
            List<object> keys = new List<object>();
            keys.Add(piece);
            // Damage() forwards only to this list, not recursively to a whole
            // root or graph. Keep independent weak points independent.
            foreach (BossPiece connected in piece.connectedPieces)
                if (connected != null && !keys.Contains(connected)) keys.Add(connected);
            return keys.ToArray();
        }

        internal static bool AlreadyHit(object target, HashSet<object> hits)
        {
            foreach (object key in DamageKeys(target)) if (hits.Contains(key)) return true;
            return false;
        }

        internal static void Register(object target, HashSet<object> hits)
        { foreach (object key in DamageKeys(target)) hits.Add(key); }

        // Terrain destruction must not also submit an untracked boss hit.
        internal static Collider[] IgnoreForGround(float x, float y, float range)
        {
            List<Collider> ignored = new List<Collider>();
            foreach (Collider contact in Physics.OverlapSphere(new Vector3(x, y, 0f), range * .5f,
                Map.groundAndDamageableObjects))
                if (contact != null && (contact.isTrigger || contact.GetComponent<DamageRelay>() != null || Find(contact) != null)) ignored.Add(contact);
            return ignored.ToArray();
        }
    }
}
