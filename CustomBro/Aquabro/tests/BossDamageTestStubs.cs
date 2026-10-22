// Offline receiver models deliberately do not inherit Unit. No per-frame
// protection: duplicate requests must be prevented by the production code.
using System.Collections.Generic;
using UnityEngine;

internal class Tank : Unit { }
internal class TankWeapon : Unit { }
internal class Mech : Unit { }
internal class MechWeapon : Unit { }
internal class AlienSandWorm : Unit { }
internal class AlienMinibossSandWorm : AlienSandWorm { }
internal class HellBoneWormMiniboss : AlienMinibossSandWorm { }
internal class AlienGiantBossSandworm : AlienMinibossSandWorm { }
internal class AlienWormFacehuggerLauncher : AlienMinibossSandWorm { }
internal class SatanMiniboss : Unit { }
internal class MookVehicleDigger : Unit { }
internal class MookTruck : Tank { }
internal class MookAPC : MookTruck { }
internal class DrillCarrier : MookTruck { }
internal class MookDumpTruck : MookTruck { }
internal class MookArtilleryTruck : MookTruck { }
internal class Mookopter : Tank { }
internal class MammothKopter : Mookopter { }
internal class SawBlade : MonoBehaviour
{
    public bool hidden,immuneToHeroDamage,CanBeDamagedByMooks=true,Detached,Dead;
    public int health=5;
    public float invulnerabilityTime=.25f;
    public bool IsDetached() { return Detached; }
    public void Receive(int damage)
    {
        if (Detached && invulnerabilityTime>=0) return;
        health-=damage;
        if (health<0)
        {
            if (!Detached) { Detached=true;health=3;invulnerabilityTime=2.25f; }
            else Dead=true;
        }
    }
}

// Deliberately absent from the production adapters. Native Damage owns its
// private phase gate; derived types and new receiver instances must work.
internal class UnlistedDamageReceiver : MonoBehaviour
{
    internal int health=1000;
    internal bool closed;
    private void Damage(DamageObject damage) { if (!closed) health-=damage.damage; }
    internal void Receive(DamageObject damage) { Damage(damage); }
}
internal class UnlistedDerivedReceiver : UnlistedDamageReceiver { }

public class BossPiece
{
    public int health = 1000, playerNum = -1;
    public bool immortal;
    public List<BossPiece> connectedPieces = new List<BossPiece>();
}
public class BossBlockPiece
{
    public int BossBlockHealth = 1000;
    public bool IsDead;
    public DamageType damageTypeImmunity = DamageType.Drill;
    public BossBlockPiece ignoreDamageWhileOtherPieceAlive;
}
public class BossBlockWeapon
{
    public int health = 1000;
    public bool indestructible;
}
public class BossSlugFace
{
    public int health = 1000;
    private bool isDead;
    public bool Dead { get { return isDead; } }
    public void MarkDead(bool value) { isDead = value; }
}
public class BossSatanFloatingHead
{
    public int health = 1000;
    private bool isDead;
    public bool Dead { get { return isDead; } }
    public void MarkDead(bool value) { isDead = value; }
}
public class BossSatanArm
{
    public int health = 1000;
    private bool isDead;
    public bool Dead { get { return isDead; } }
    public void MarkDead(bool value) { isDead = value; }
}
internal static class BossDamageTestStubs
{
    internal static readonly Dictionary<object, int> Damage = new Dictionary<object, int>();
    internal static readonly Dictionary<object, int> Hits = new Dictionary<object, int>();
    internal static int Requests;
    internal static float LastForceX, LastForceY;
    internal static void Reset() { Damage.Clear(); Hits.Clear(); Requests = 0; }
    private static void Record(object key, int damage)
    {
        int old; Damage.TryGetValue(key, out old); Damage[key] = old + damage;
        Hits.TryGetValue(key, out old); Hits[key] = old + 1;
    }
    internal static bool Submit(GameObject receiver, int damage, DamageType type, float fx, float fy)
    {
        HashSet<GameObject> visited=new HashSet<GameObject>();
        DamageRelay relay = receiver.GetComponent<DamageRelay>();
        while (relay != null)
        {
            if(!visited.Add(receiver))throw new System.Exception("Native relay recursion reached by damage submission");
            if((relay.ignoreDamageWhileColliderActive!=null && relay.ignoreDamageWhileColliderActive.enabled && relay.ignoreDamageWhileColliderActive.gameObject.activeInHierarchy) ||
               (relay.ignoreDamageWhileOtherColliderActive!=null && relay.ignoreDamageWhileOtherColliderActive.enabled && relay.ignoreDamageWhileOtherColliderActive.gameObject.activeInHierarchy))return true;
            if (relay.unit != null || relay.doodad != null) return false;
            if (relay.bossWeapon != null && type!=relay.immunity)
            {
                Requests++; if (!relay.bossWeapon.indestructible) Record(relay.bossWeapon, damage);
                return true;
            }
            if (relay.damageGameObject == null) return false;
            receiver = relay.damageGameObject;
            relay = receiver.GetComponent<DamageRelay>();
        }
        UnlistedDamageReceiver unknown=receiver.GetComponent<UnlistedDamageReceiver>();
        SawBlade saw=receiver.GetComponent<SawBlade>();
        if(saw!=null)
        {
            Requests++;LastForceX=fx;LastForceY=fy;Record(saw,damage);saw.Receive(damage);return true;
        }
        Unit vehicle = receiver.GetComponent<Unit>();
        if (vehicle is Tank || vehicle is TankWeapon || vehicle is Mech || vehicle is MechWeapon || vehicle is AlienSandWorm || vehicle is SatanMiniboss)
        {
            Requests++; LastForceX=fx; LastForceY=fy;
            vehicle.health-=damage; Record(vehicle, damage); return true;
        }
        if(unknown==null)unknown=receiver.GetComponent<UnlistedDerivedReceiver>();
        if(unknown!=null)
        {
            Requests++;LastForceX=fx;LastForceY=fy;
            int before=unknown.health;
            unknown.Receive(new DamageObject(damage,type,fx,fy,0,0,null));
            if(before!=unknown.health)Record(receiver,before-unknown.health);
            return true;
        }
        BossPiece piece = receiver.GetComponent<BossPiece>();
        BossBlockPiece block = receiver.GetComponent<BossBlockPiece>();
        BossBlockWeapon weapon = receiver.GetComponent<BossBlockWeapon>();
        BossSlugFace face = receiver.GetComponent<BossSlugFace>();
        BossSatanFloatingHead head = receiver.GetComponent<BossSatanFloatingHead>();
        BossSatanArm arm = receiver.GetComponent<BossSatanArm>();
        if (piece == null && block == null && weapon == null && face == null && head == null && arm == null) return false;
        Requests++; LastForceX = fx; LastForceY = fy;
        if (piece != null)
        {
            if (!piece.immortal) { piece.health -= damage; Record(piece, damage); }
            foreach (BossPiece connected in piece.connectedPieces)
                if (connected != piece && !connected.immortal) { connected.health -= damage; Record(connected, damage); }
        }
        if (block != null && block.damageTypeImmunity != type &&
            (block.ignoreDamageWhileOtherPieceAlive == null || block.ignoreDamageWhileOtherPieceAlive.BossBlockHealth <= 0))
        { block.BossBlockHealth -= damage; Record(block, damage); }
        if (weapon != null && !weapon.indestructible) { weapon.health -= damage; Record(weapon, damage); }
        if (face != null && !face.Dead) { face.health -= damage; Record(face, damage); }
        if (head != null && !head.Dead && type != DamageType.Unholy) { head.health -= damage; Record(head, damage); }
        if (arm != null && !arm.Dead && type != DamageType.Unholy) { arm.health -= damage; Record(arm, damage); }
        return true;
    }
}
