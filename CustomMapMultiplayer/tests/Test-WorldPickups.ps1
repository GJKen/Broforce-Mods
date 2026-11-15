param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$worldSource = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $projectRoot 'src\HarmonyDiagnostics.WorldState.cs')
$hitSource = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $projectRoot 'src\HarmonyDiagnostics.WorldProjectiles.cs')
$presentationSource = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $projectRoot 'src\HarmonyDiagnostics.WorldHitPresentation.cs')
$collectSource = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $projectRoot 'src\HarmonyDiagnostics.WorkshopPickup.cs')

function Read-Declaration([string]$source, [string]$signature) {
    $start = $source.IndexOf($signature)
    if ($start -lt 0) { throw "Missing production declaration: $signature" }
    $brace = $source.IndexOf('{', $start)
    $depth = 1
    $end = $brace + 1
    while ($depth -gt 0 -and $end -lt $source.Length) {
        if ($source[$end] -eq '{') { $depth++ }
        if ($source[$end] -eq '}') { $depth-- }
        $end++
    }
    return $source.Substring($start, $end - $start)
}

$declarations = @(
    Read-Declaration $worldSource 'private enum WorldFlags'
    Read-Declaration $worldSource 'private sealed class WorldSnapshot'
    Read-Declaration $worldSource 'private static bool IsWorldObject('
    Read-Declaration $worldSource 'private static bool IsWorldReplica('
    Read-Declaration $worldSource 'private static bool WorldMutationPrefix('
    Read-Declaration $worldSource 'private static void WorldBlockRpcPrefix('
    Read-Declaration $collectSource 'private static bool AllowWorkshopPickupCollectPrefix('
    Read-Declaration $collectSource 'private static bool IsWorkshopPickupAmmoFull('
    Read-Declaration $worldSource 'private static void WriteWorldId('
    Read-Declaration $worldSource 'private static NID ReadWorldId('
    Read-Declaration $worldSource 'private static void WriteWorldState('
    Read-Declaration $worldSource 'private static WorldSnapshot ReadWorldState('
    Read-Declaration $hitSource 'private static BroforceObject GetWorldHitSource('
    Read-Declaration $hitSource 'private static bool WorldProjectileHitPrefix('
    Read-Declaration $hitSource 'private static bool WorldProjectileBlockHitPrefix('
    Read-Declaration $hitSource 'private static void SendWorldProjectileHit('
    Read-Declaration $hitSource 'private static void SendWorldProjectileContactHit('
    Read-Declaration $hitSource 'private static void SendWorldOwnedHitPresentation('
    Read-Declaration $hitSource 'private static void ReceiveWorldOwnedHitPresentationRPC('
    Read-Declaration $hitSource 'private static void ReceiveWorldProjectileHitRPC('
    Read-Declaration $hitSource 'private static void SendWorldHitResult('
    Read-Declaration $hitSource 'private static void FlushWorldHitMessages('
    Read-Declaration $hitSource 'private static int BeginWorldHitPresentation('
    Read-Declaration $hitSource 'private static void ReceiveWorldHitAcknowledgementRPC('
    Read-Declaration $hitSource 'private static void EndWorldHitPresentation('
    Read-Declaration $worldSource 'private static bool WorldMovementPrefix('
    Read-Declaration $worldSource 'private static void WorldMovementFinalizer('
    Read-Declaration $presentationSource 'private sealed class WorldHitPresentation'
    Read-Declaration $presentationSource 'private static bool HasPlainWorldDamage('
    Read-Declaration $presentationSource 'private static void PresentWorldHit('
    Read-Declaration $presentationSource 'private static void PresentWorldHitDirected('
    Read-Declaration $presentationSource 'private static void HideWorldHitRenderers('
    Read-Declaration $presentationSource 'private static void ShowWorldPredictedCorpse('
    Read-Declaration $presentationSource 'private static void FinishWorldHitPresentation('
    Read-Declaration $presentationSource 'private static bool WorldBlockPresentationEffectPrefix('
    Read-Declaration $presentationSource 'private static bool WaitForWorldGibConfirmation('
) -join "`r`n"

# Compile the production pickup methods and wire codec; stubs model only engine/RPC boundaries.
$fixture = @'
using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using Networking;
using HarmonyLib;
namespace HarmonyLib { public class CodeInstruction { public object operand; } public static class AccessTools {
 public static MethodInfo Method(Type t,string n,Type[] p,Type[] g) { return t.GetMethod(n,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static); }
 public static FieldInfo Field(Type t,string n) { for(;t!=null;t=t.BaseType) { var f=t.GetField(n,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static); if(f!=null)return f; } return null; }
} }
namespace UnityEngine {
 public class Object { public static void Destroy(object o) { } }
 public class MonoBehaviour { }
 public class Component:MonoBehaviour { public GameObject gameObject; }
 public struct Color { public Color(float r,float g,float b,float a) { } }
 public class Material { public void SetColor(string n,Color c) { } }
 public class Renderer { public bool enabled=true; public Transform transform=new Transform(null); public Material sharedMaterial=new Material(); public Material material { get { return sharedMaterial; } } }
 public class Collider { public bool enabled=true; public GameObject gameObject=new GameObject(); }
 public struct Vector2 { public float x,y; public Vector2(float a,float b) { x=a; y=b; } public static Vector2 Lerp(Vector2 a,Vector2 b,float t) { return new Vector2(a.x+(b.x-a.x)*t,a.y+(b.y-a.y)*t); } }
 public struct Vector3 { public float x,y,z; public Vector3(float a,float b,float c) { x=a; y=b; z=c; } }
 public static class Time { public static float timeScale=1f, deltaTime=.02f, unscaledTime,time; }
 public static class Mathf {
  public static float Max(float a,float b) { return Math.Max(a,b); }
  public static float Sign(float a) { return a<0?-1:1; }
  public static float Clamp(float v,float a,float b) { return Math.Max(a,Math.Min(b,v)); }
  public static float Clamp01(float v) { return Clamp(v,0,1); }
 }
 public class Transform {
  public Transform parent; public BroforceObject owner;
  public Vector3 position,localScale=new Vector3(1,1,1);
  public Transform(BroforceObject o) { owner=o; }
  public T GetComponent<T>() where T:class { return owner as T; }
  public bool IsChildOf(Transform other) { return false; }
 }
 public class GameObject {
  public bool activeSelf; public bool activeInHierarchy { get { return activeSelf; } }
  public BroforceObject owner; public DamageRelay relay;
  public void SetActive(bool value) { activeSelf=value; }
  public T GetComponent<T>() where T:class { return relay as T ?? owner as T; }
  public void SendMessage(string name,object value) { }
 }
}
public struct SID {
 public byte AsByte; public SID(byte n) { AsByte=n; }
 public static bool operator ==(SID a,SID b) { return a.AsByte==b.AsByte; }
 public static bool operator !=(SID a,SID b) { return !(a==b); }
 public override bool Equals(object o) { return o is SID && this==(SID)o; }
 public override int GetHashCode() { return AsByte; }
}
public struct PID {
 public int AsInt; public PID(int n) { AsInt=n; }
 public static PID MyID=new PID(7), ServerID=new PID(6), TargetServer=new PID(0), TargetOthers=new PID(-1);
 public static bool operator ==(PID a,PID b) { return a.AsInt==b.AsInt; }
 public static bool operator !=(PID a,PID b) { return !(a==b); }
 public override bool Equals(object o) { return o is PID && this==(PID)o; }
 public override int GetHashCode() { return AsInt; }
}
public struct NID {
 public SID Sid; public PID OwnerID; public uint GameObjectID; public ushort ComponentID;
 public static NID NoID=default(NID);
 public NID(SID s,PID o,uint g,ushort c) { Sid=s; OwnerID=o; GameObjectID=g; ComponentID=c; }
 public static bool operator ==(NID a,NID b) { return a.Sid==b.Sid && a.OwnerID==b.OwnerID && a.GameObjectID==b.GameObjectID && a.ComponentID==b.ComponentID; }
 public static bool operator !=(NID a,NID b) { return !(a==b); }
 public override bool Equals(object o) { return o is NID && this==(NID)o; }
 public override int GetHashCode() { return (int)GameObjectID+ComponentID; }
}
public enum DamageType { None, Normal, Melee, Explosion, Bullet, Blade, Crush, Fire, Acid, Plasma, Unholy }
public enum ActionState { Idle, Dead, Panicking }
public enum PickupType { Ammo, Airstrike, RemotecontrolCar }
public class DamageObject { public DamageObject(int d,DamageType t,float vx,float vy,float x,float y,MonoBehaviour a) { } }
public class BroforceObject:Component {
 public NID Nid; public PID Owner; public bool IsMine; public Transform transform;
 public float xI,yI; public BroforceObject() { gameObject=new GameObject(); gameObject.owner=this; transform=new Transform(this); }
 public float X,Y; public int health=3,maxHealth=3; public bool destroyed;
 public Renderer[] Renderers=new[] {new Renderer()};
 public bool enabled=true;
 public T GetComponent<T>() where T:class { return Renderers[0] as T; }
 public T[] GetComponentsInChildren<T>(bool all) { return (T[])(object)Renderers; }
 public void SetPosition(Vector3 p) { X=p.x; Y=p.y; transform.position=p; }
}
public class Unit:BroforceObject {
 public ActionState actionState;
 public bool IsHero,deathNotificationSent,invulnerable; public int DamageCalls,KnockCalls;
 public void Damage(int d,DamageType t,float vx,float vy,int dir,MonoBehaviour a,float x,float y) { DamageCalls++; }
 public void HeadShot(int d,DamageType t,float vx,float vy,int dir,float x,float y,MonoBehaviour a) { DamageCalls++; }
 public void Knock(DamageType t,float vx,float vy,bool local) { KnockCalls++; }
}
public class TestVanDammeAnim:Unit { public bool canGib=true,WillReviveAlready,hasBeenCoverInAcid,AmmoFull; public object inseminatorUnit; public float xIBlast,yIBlast; public BloodColor bloodColor; public bool IsAmmoFull() { return AmmoFull; } public void ChangeFrame() { } public void Sound() { } }
public class Mook:TestVanDammeAnim { }
public class MookTrooper:Mook { }
public class MookBigGuy:Mook { }
public class MookDog:Mook { }
public class MookSuicide:Mook { }
public class MookArmouredGuy:Mook { public int shieldDamage,fireAmount,knockCount,Defends; public void PlayDefendSound() { Defends++; } public void AnimateDeath() { } public new void Damage(int d,DamageType t,float a,float b,int c,MonoBehaviour s,float x,float y) { } }
public class MookHellBigGuy:MookBigGuy { }
public class MookHellArmouredBigGuy:MookHellBigGuy { public int shieldDamage,fireAmount,knockCount,Defends; public void PlayDefendSound() { Defends++; } public new void Damage(int d,DamageType t,float a,float b,int c,MonoBehaviour s,float x,float y) { } }
public class MookRiotShield:Mook { public int shieldDamage,fireAmount; public float shieldTime=.2f; public bool hasShield=true; public Material unarmedMaterial=new Material(); public object shieldShrapnel; public new void Damage(int d,DamageType t,float a,float b,int c,MonoBehaviour s,float x,float y) { } public void DisarmShield(float vx) { CustomMapMultiplayer.HarmonyDiagnostics.ObserveShieldDisarm(this); } }
public class Tank:Unit { public int Flashes; public void SetHurtMaterial() { Flashes++; } }
public class Mech:Tank {
 public float tankSpeed=150,ultraTankSpeed=200,maxUpwardVelocity=120,ultraMaxUpwardVelocity=150;
 public int facingDirection=-1,turnFrame; public BroforceObject mechAnimation=new BroforceObject();
 public void FinishTurning() { turnFrame=0; mechAnimation.gameObject.SetActive(true); }
 public void SetSpriteTurn(int frame) { mechAnimation.gameObject.SetActive(false); }
}
public class GoliathMech:Mech { public BroforceObject eye=new BroforceObject(); public Material angryEyeMaterial=new Material(); }
public class TankWeapon:Unit { public Material hurtMaterial=new Material(); public float hurtCounter; }
public class MechWeapon:TankWeapon { public Mech mech; public Material baseMaterial=new Material(); }
public class SatanMiniboss:MookBigGuy { public object forceField; }
public class BossBlockPiece:BroforceObject { public bool isDead; public bool IsDead { get { return isDead; } } public DamageType damageTypeImmunity; public BossBlockPiece ignoreDamageWhileOtherPieceAlive; public Material hurtMaterial=new Material(); public float hurtTime; }
public class BossBlockPieceSheathed:BossBlockPiece { public bool indestructible=true; }
public class BossBlockWeapon:BroforceObject { public bool indestructible,isDead; public float hurtTime; public Material hurtMaterial=new Material(); public void SetMaterial(Material m) { GetComponent<Renderer>().sharedMaterial=m; } }
public class BossPiece:BroforceObject { public bool isDead,isDamaged,immortal; public float flashCounter; public Material hurtMaterial=new Material(),damagedHurtMaterial=new Material(),damagedMaterial=new Material(),baseMaterial=new Material(); public List<BossPiece> connectedPieces=new List<BossPiece>(); }
public class BossSatanFloatingHead:BroforceObject { public bool isDead; public void SetHurtMaterial() { } }
public class BossSatanArm:BossSatanFloatingHead { }
public class BossSlugFace:BroforceObject { public bool isDead; public float hurtTime; }
public class DamageRelay:MonoBehaviour { public bool isDestroyed; public Collider ignoreDamageWhileColliderActive,ignoreDamageWhileOtherColliderActive; public Unit unit; public Doodad doodad; public BossBlockWeapon bossWeapon; public DamageType immunity,forceDamageType; public GameObject damageGameObject,gameObject; }
public enum BloodColor { Red }
public class EffectsController {
 public static int Blood,Strikes,Debris;
 public static void CreateBloodParticles(BloodColor c,float x,float y,int n,float rx,float ry,float f,float vx,float vy) { Blood++; }
 public static void CreateMeleeStrikeEffect(float x,float y,float vx,float vy) { Strikes++; }
 public static void CreateShrapnel(object p,float x,float y,float r,float f,float n,float vx,float vy) { Debris++; }
 public static void CreatePuffDisappearRingEffect(float x,float y,float vx,float vy) { }
}
public static class SortOfFollow { public static bool Visible; public static bool IsItSortOfVisible(float x,float y,float a,float b) { return Visible; } }
public class Doodad:BroforceObject { }
public class BlockPiece:BroforceObject { }
public class Block:BroforceObject {
 public bool protectedFromDestruction; public float collapseChance=1; public object shrapnelPrefab,shrapnelBitPrefab; public BlockPiece backgroundMesh;
 public void Sound() { }
 public void ActuallyCollapse(float x,float y,bool around) { }
 public void DestroyBlockInternal(bool around) { }
 public void Damage() { }
}
public class DirtBlock:Block { public void ShowBrokenBackground() { backgroundMesh=new BlockPiece(); } }
public class Projectile:BroforceObject { }
public class Grenade:BroforceObject { }
public class FallingBlock:Block { public int collapseHealthPoint=-15; }
public class CrateBlock:FallingBlock {
 public object shrapnelBitPrefab3; public bool containsPresent; public int Rewards;
 public void MakeEffects() { Rewards++; }
 private Pickupable pickup;
 public void Hold(Pickupable p) { pickup=p; p.transform.parent=transform; }
 public bool HasPickup { get { return pickup!=null; } }
}
public class Pickupable:BroforceObject {
 public bool collected; public PickupType pickupType; public float life,pickupDelay,xExtraOffset,extraLaunchForce;
 public int Launches;
 public void Launch(float x,float y,float vx,float vy) {
  Launches++; transform.parent=null; gameObject.SetActive(true); X=x+xExtraOffset; Y=y;
  xI=vx; yI=vy+extraLaunchForce; life*=.9f; PickupableController.AddPickupable(this);
 }
 public void Death() { }
 public void Collect(Unit hero) { hero.DamageCalls++; collected=true; gameObject.SetActive(false); PickupableController.RemovePickupable(this); }
}
public static class PickupableController {
 public static List<Pickupable> Items=new List<Pickupable>();
 public static void AddPickupable(Pickupable p) { Items.Add(p); }
 public static void RemovePickupable(Pickupable p) { Items.Remove(p); }
}
public static class Connect { public static SID SessionID=new SID(20); }
public static class Registry {
 public static Dictionary<NID,BroforceObject> Items=new Dictionary<NID,BroforceObject>();
 public static NID GetNID(object o) { return ((BroforceObject)o).Nid; }
 public static object GetObject(NID id) { BroforceObject o; return Items.TryGetValue(id,out o)?o:null; }
}
public class RPCInfo { public PID Sender; }
public class RPCObject { public RPCInfo messageInfo=new RPCInfo(); }
public static class RPCController { public static RPCObject CurrentRPC; }
public static class RPCBatcher { public static int Flushes; public static void FlushQueue() { Flushes++; } }
namespace Networking {
 public delegate void RpcSignature<T,U>(T a,U b);
 public delegate void RpcSignature<T,U,V>(T a,U b,V c);
 public static class Networking {
  public static int Calls; public static PID Target; public static bool Immediate; public static byte[] Bytes;
  public static void RPC<T,U>(PID target,RpcSignature<T,U> method,T a,U b,bool immediate) {
   Calls++; Target=target; Immediate=immediate; Bytes=(byte[])(object)b;
  }
  public static void RPC<T,U,V>(PID target,RpcSignature<T,U,V> method,T a,U b,V c,bool immediate) { }
 }
}
namespace CustomMapMultiplayer {
 internal static partial class HarmonyDiagnostics {
  private const BindingFlags WorldFields=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
  private static long _worldEpoch=123; private static int _worldApplying;
  private static bool Session=true; private static BroforceObject _worldDamageSource;
  private static bool _workshopDuplicatePickupCollectLogged;
  private static bool IsWorkshopOnlineSession() { return Session; }
  private static class DiagnosticLog { public static void Info(string message) { } }
  private static int _worldSequence,Published,DamageAtCapture,_worldHitSequence,Applied;
  private const float WorldSendInterval=1f/12f;
  private static bool _worldHitMessagesPending;
  private sealed class WorldEntry { public bool Dirty; public WorldSnapshot Last; public float SentAt,ReceivedAt,PendingHitUntil; public int PendingHit; public Vector2 LerpFrom; public BroforceObject Object; public WorldHitPresentation Presentation; public WorldFlags PresentedFlags; }
  private static int _worldSettlingHit,_worldHitRequestor,_worldSuppressCrateDebris,Gibs,Corpses;
  private static Dictionary<int,byte> WorldPresentedHitEffects=new Dictionary<int,byte>();
  private static Dictionary<Type,bool> WorldPlainDamageTypes=new Dictionary<Type,bool>();
  private static FieldInfo WorldGibThreshold=typeof(Threshold).GetField("Value");
  private static FieldInfo WorldFrozenTime=typeof(Threshold).GetField("Frozen");
  private static FieldInfo WorldBlockBackground=typeof(Block).GetField("backgroundMesh");
  private class Threshold { public static int Value=-10; public static float Frozen=0; }
  private static MethodInfo WorldDeathSound=typeof(TestVanDammeAnim).GetMethod("Sound"),WorldBlockDeathSound=typeof(Block).GetMethod("Sound"),WorldCrateMakeEffects=typeof(CrateBlock).GetMethod("MakeEffects");
  private static MethodInfo WorldDisarmShield=typeof(MookRiotShield).GetMethod("DisarmShield");
  private static int ConfirmedDisarms;
  public static void ObserveShieldDisarm(MookRiotShield shield) {
   Check(_worldApplying>0 && WorldEntries[shield.Nid].Presentation.ShieldLost,"confirmed disarm retains prediction during native replay");
   ConfirmedDisarms++; shield.hasShield=false;
  }
  private static void WorldBaseGibEffects(TestVanDammeAnim u,DamageType t,float x,float y) { Gibs++; }
  private static void WorldBaseDeathAnimation(TestVanDammeAnim u) { Check(u.health==0 && u.actionState==ActionState.Dead,"corpse animation must see a dead unit"); Corpses++; }
  private static void WorldBaseBlockHitEffects(Block b,float x,float y,float f) { EffectsController.Debris++; }
  private static readonly Dictionary<NID,WorldEntry> WorldEntries=new Dictionary<NID,WorldEntry>();
  private static WorldEntry Entry=new WorldEntry();
  private static WorldEntry RegisterWorldObject(BroforceObject o) { if(!WorldEntries.TryGetValue(o.Nid,out Entry)) { Entry=new WorldEntry { Object=o }; WorldEntries.Add(o.Nid,Entry); } return Entry; }
  private static bool ApplyWorldObjectState(WorldEntry e,WorldSnapshot s) { Applied++; return true; }
  private static WorldSnapshot CaptureWorldState(WorldEntry e) { if(e.Object is Unit)DamageAtCapture=((Unit)e.Object).DamageCalls; return new WorldSnapshot { SentAt=42, Health=e.Object.health }; }
  private static void SendWorldStates(IList<WorldSnapshot> states,PID target,bool reliable) {
   Check(target==PID.TargetOthers && reliable,"hit results must reliably reach all replicas"); Published+=states.Count;
  }
  private static bool IsWorldSession() { return Session; }
  public static void KnockAndDamageUnit() { }
  private static int Checks;
  private static void Check(bool ok,string message) { Checks++; if(!ok)throw new Exception(message); }
  private static WorldSnapshot State(bool active,bool collected) {
   return new WorldSnapshot { Id=new NID(new SID(20),PID.TargetServer,20140,27), Type="Pickupable", Flags=WorldFlags.Pickup|
    (active?WorldFlags.PickupActive:0)|(collected?WorldFlags.PickupCollected:0), PickupType=(int)PickupType.RemotecontrolCar,
    PickupDelay=.3f, LifeTime=float.PositiveInfinity, X=800, Y=144, VX=10, VY=90 };
  }
  public static void Main() {
   try { RunChecks(); } catch(Exception e) { Console.WriteLine("FAIL after "+Checks+": "+e.GetType().Name+" "+e.Message); Environment.Exit(1); }
  }
  private static void RunChecks() {
   var ammoHero=new TestVanDammeAnim { Owner=new PID(8), AmmoFull=true };
   RPCController.CurrentRPC=new RPCObject(); RPCController.CurrentRPC.messageInfo.Sender=ammoHero.Owner;
   Check(!IsWorkshopPickupAmmoFull(ammoHero),"owner Collect must consume ammo even when the remote hero mirror is already full");
   ammoHero.IsMine=true;
   Check(IsWorkshopPickupAmmoFull(ammoHero),"a local full-ammo hero must still reject collection");
   ammoHero.AmmoFull=false;
   Check(!IsWorkshopPickupAmmoFull(ammoHero),"a local hero with missing ammo can collect");
   ammoHero.IsMine=false; ammoHero.AmmoFull=true;
   RPCController.CurrentRPC.messageInfo.Sender=PID.ServerID;
   Check(IsWorkshopPickupAmmoFull(ammoHero),"another peer cannot override the owning hero's ammo decision");
   RPCController.CurrentRPC=null;
   Check(IsWorkshopPickupAmmoFull(ammoHero),"a direct remote-mirror call retains the native full-ammo check");
   RPCController.CurrentRPC=new RPCObject(); RPCController.CurrentRPC.messageInfo.Sender=ammoHero.Owner;
   Session=false;
   Check(IsWorkshopPickupAmmoFull(ammoHero),"offline collection retains the native full-ammo check");
   Session=true; RPCController.CurrentRPC=null;
   var p=new Pickupable { Nid=new NID(new SID(20),PID.TargetServer,20140,27), xExtraOffset=3, extraLaunchForce=20 };
   var crate=new CrateBlock(); crate.Hold(p);
   Check(IsWorldObject(p),"hidden registered pickups must enroll in world sync");
   var active=State(true,false); ApplyWorldPickupState(p,active);
   Check(p.gameObject.activeSelf && p.transform.parent==null && !crate.HasPickup,"snapshot must release hidden crate child");
   Check(p.X==800 && p.Y==144 && p.xI==10 && p.yI==90,"release must preserve authoritative coordinates and velocity");
   Check(float.IsPositiveInfinity(p.life) && p.pickupDelay==.3f,"native Launch randomization must not change owner lifetime");
   ApplyWorldPickupState(p,active);
   Check(p.Launches==1 && PickupableController.Items.Count==1,"duplicate release must not enroll twice");
   var inactive=State(false,false); ApplyWorldPickupState(p,inactive);
   Check(!p.gameObject.activeSelf && PickupableController.Items.Count==0 && !p.collected,"expiry hides without inventing collection");
   ApplyWorldPickupState(p,active); ApplyWorldPickupState(p,State(false,true));
   Check(!p.collected && !p.gameObject.activeSelf && PickupableController.Items.Count==0,"collection snapshot hides without acknowledging hero delivery");
   var collector=new Unit();
   Check(AllowWorkshopPickupCollectPrefix(p),"snapshot-first native Collect must still deliver to the hero");
   if(AllowWorkshopPickupCollectPrefix(p))p.Collect(collector);
   Check(collector.DamageCalls==1 && p.collected,"native Collect acknowledges one hero delivery");
   Check(!AllowWorkshopPickupCollectPrefix(p),"duplicate Collect stays suppressed after actual delivery");
   ApplyWorldPickupState(p,active); ApplyWorldPickupState(p,inactive);
   Check(p.collected && !p.gameObject.activeSelf && p.Launches==2,"older snapshots must not resurrect collected pickup");
   var captured=new WorldSnapshot(); CaptureWorldPickupState(p,captured);
   Check((captured.Flags&WorldFlags.PickupCollected)!=0 && (captured.Flags&WorldFlags.PickupActive)==0,"capture preserves collected flags");
   foreach(bool released in new bool[] {false,true}) {
    var first=new Pickupable { Nid=p.Nid }; if(released)ApplyWorldPickupState(first,active);
    Check(AllowWorkshopPickupCollectPrefix(first),"native delivery can precede crate release");
    if(AllowWorkshopPickupCollectPrefix(first))first.Collect(collector);
    ApplyWorldPickupState(first,active); ApplyWorldPickupState(first,State(false,true));
    Check(first.collected && !first.gameObject.activeSelf && !AllowWorkshopPickupCollectPrefix(first),"collect-first remains hidden and cannot award twice");
   }
   bool immediate=false;
   WorldBlockRpcPrefix(crate,typeof(Block).GetMethod("ActuallyCollapse"),ref immediate);
   Check(immediate,"native collapse visuals must run before an immediate destroyed snapshot");
   immediate=false; WorldBlockRpcPrefix(crate,typeof(Block).GetMethod("DestroyBlockInternal"),ref immediate);
   Check(immediate,"native direct destruction retains send order");
   immediate=false; WorldBlockRpcPrefix(crate,typeof(Block).GetMethod("Damage"),ref immediate);
   Check(!immediate,"unrelated block RPC timing remains unchanged");
   immediate=false; WorldBlockRpcPrefix(collector,typeof(Block).GetMethod("ActuallyCollapse"),ref immediate);
   Check(!immediate,"non-block RPC timing remains unchanged");
   Session=false; WorldBlockRpcPrefix(crate,typeof(Block).GetMethod("ActuallyCollapse"),ref immediate);
   Check(!immediate,"offline block RPC timing remains unchanged"); Session=true;
   using(var stream=new MemoryStream())using(var w=new BinaryWriter(stream)) {
    WriteWorldState(w,active); WriteWorldState(w,inactive); w.Flush(); stream.Position=0;
    using(var r=new BinaryReader(stream)) {
     var a=ReadWorldState(r); var b=ReadWorldState(r);
     Check(a.Id==active.Id && a.PickupType==active.PickupType && a.PickupDelay==.3f && a.X==800,"pickup wire round trip");
     Check(float.IsPositiveInfinity(a.LifeTime) && (b.Flags&WorldFlags.PickupActive)==0 && stream.Position==stream.Length,"adjacent snapshot framing");
    }
   }
   p.collected=false; p.pickupDelay=.3f;
   Check(!WorldPickupUpdatePrefix(p) && p.pickupDelay<.3f,"replica keeps collection delay without local motion");
   p.IsMine=true; Check(WorldPickupUpdatePrefix(p),"owner retains native movement"); p.IsMine=false;
   Session=false; Check(WorldPickupUpdatePrefix(p),"offline native movement preserved"); Session=true;
   Check(WorldMutationPrefix(p,typeof(Pickupable).GetMethod("Death")),"replica native collection may disappear");
   var hero=new Unit { IsHero=true, IsMine=true, Owner=PID.MyID, Nid=new NID(new SID(20),PID.MyID,70003,4) };
   var target=new Unit { Owner=PID.TargetServer, Nid=new NID(new SID(20),PID.TargetServer,19701,3) };
   Registry.Items.Add(hero.Nid,hero); Registry.Items.Add(target.Nid,target);
   Check(GetWorldHitSource(hero)==hero,"hero splash outside Projectile.Update must route to owner");
   var method=typeof(HarmonyDiagnostics).GetMethod("KnockAndDamageUnit");
   Check(!WorldProjectileHitPrefix(hero,target,8,DamageType.Melee,110,90,1,true,800,144,method),"remote target must use a single owner request");
   Check(Networking.Networking.Calls==1 && Networking.Networking.Immediate && Networking.Networking.Target==PID.TargetServer,"hit request is immediate and targeted");
   Check(_worldHitMessagesPending,"hit request schedules a frame-end send");
   FlushWorldHitMessages(); FlushWorldHitMessages();
   Check(RPCBatcher.Flushes==1,"idle frame-end calls must not flush again");
   target.IsMine=true; RPCController.CurrentRPC=new RPCObject(); RPCController.CurrentRPC.messageInfo.Sender=PID.MyID;
   ReceiveWorldProjectileHitRPC(_worldEpoch,Networking.Networking.Bytes);
   Check(target.DamageCalls==1 && target.KnockCalls==1,"owner settles one damage and knock");
   Check(Published==1 && DamageAtCapture==1 && Entry.Last.Revision==1 && Entry.SentAt==42 && !Entry.Dirty,"complete hit result publishes without the 12 Hz wait");
   Check(_worldHitMessagesPending,"owner result schedules a frame-end send");
   FlushWorldHitMessages(); FlushWorldHitMessages();
   Check(RPCBatcher.Flushes==2,"owner reply flushes once");
   Entry.Last.Flags|=WorldFlags.Removed; SendWorldHitResult(target);
   Check(Published==1 && (Entry.Last.Flags&WorldFlags.Removed)!=0,"hit result must not replace a removal tombstone");
   SendWorldHitResult(hero); Check(Published==1,"hero damage must not enter world snapshots");
   Check(WorldProjectileHitPrefix(hero,target,8,DamageType.Melee,110,90,1,true,800,144,method) && Networking.Networking.Calls==1,"local targets retain native path");
   target.IsMine=false; target.IsHero=true;
   Check(WorldProjectileHitPrefix(hero,target,8,DamageType.Melee,110,90,1,true,800,144,method) && Networking.Networking.Calls==1,"hero targets retain native damage policy");
   var batteryId=new NID(new SID(20),PID.MyID,19740,8);
   var batteryOwner=new BossBlockWeapon { Nid=batteryId, Owner=PID.MyID, IsMine=true, health=5 };
   Registry.Items.Add(batteryId,batteryOwner);
   var attack=new Projectile { Nid=new NID(new SID(20),PID.MyID,70004,4), Owner=PID.MyID, IsMine=true };
   _worldDamageSource=attack; Networking.Networking.Calls=0; _worldHitMessagesPending=false;
   Check(WorldProjectileBlockHitPrefix(attack,batteryOwner.gameObject,14,DamageType.Explosion,-90,0,1500,200),
    "owner hit keeps native damage settlement");
   Check(Networking.Networking.Calls==1 && Networking.Networking.Target==PID.TargetOthers && Networking.Networking.Immediate &&
    _worldHitMessagesPending,"owner hit sends an immediate visual-only event to replicas");
   var batteryReplica=new BossBlockWeapon { Nid=batteryId, Owner=PID.MyID, IsMine=false, health=5, enabled=false };
   Registry.Items[batteryId]=batteryReplica;
   RPCController.CurrentRPC=new RPCObject(); RPCController.CurrentRPC.messageInfo.Sender=PID.TargetServer;
   ReceiveWorldOwnedHitPresentationRPC(_worldEpoch,Networking.Networking.Bytes);
   Check(batteryReplica.hurtTime==0 && batteryReplica.health==5,"non-owner cannot send the presentation event");
   RPCController.CurrentRPC.messageInfo.Sender=PID.MyID;
   ReceiveWorldOwnedHitPresentationRPC(_worldEpoch,Networking.Networking.Bytes);
   Check(batteryReplica.health==5 && batteryReplica.enabled && batteryReplica.hurtTime==.05f &&
    batteryReplica.GetComponent<Renderer>().sharedMaterial==batteryReplica.hurtMaterial,
    "owner hit gives the replica rocket-battery feedback without applying damage");
   Check(HasWorldOwnedHitPresentation(new BossPiece()) && HasWorldOwnedHitPresentation(new BossBlockPiece()) &&
    HasWorldOwnedHitPresentation(new BossSlugFace()) && HasWorldOwnedHitPresentation(new BossSatanArm()) &&
    HasWorldOwnedHitPresentation(new Tank()) && HasWorldOwnedHitPresentation(new MechWeapon()),
    "same owner-hit path covers other boss parts and vehicle receivers");
   _worldDamageSource=null;
   hero.IsMine=false; Check(GetWorldHitSource(hero)==null,"remote hero mirrors cannot originate requests");
   _worldDamageSource=new Projectile(); Check(GetWorldHitSource(hero)==_worldDamageSource,"existing projectile context remains valid");
   var knocked=new TestVanDammeAnim { Nid=new NID(new SID(20),PID.TargetServer,77,3), Owner=PID.TargetServer };
   int request=BeginWorldHitPresentation(knocked,DamageType.Melee,100,300,true);
   Check(request!=0 && knocked.KnockCalls==1 && knocked.DamageCalls==0,"attacker presents knock immediately without dealing damage");
   bool movement; Check(WorldMovementPrefix(knocked,out movement) && movement && _worldApplying==1,"pending knock uses native movement in presentation context");
   WorldMovementFinalizer(movement); Check(_worldApplying==0,"movement context must unwind");
   Entry.Last=new WorldSnapshot(); RPCController.CurrentRPC.messageInfo.Sender=PID.MyID;
   ReceiveWorldHitAcknowledgementRPC(_worldEpoch,knocked.Nid,request); Check(Entry.PendingHit==request,"non-owner cannot acknowledge the hit");
   RPCController.CurrentRPC.messageInfo.Sender=PID.ServerID;
   ReceiveWorldHitAcknowledgementRPC(_worldEpoch,knocked.Nid,request+1); Check(Entry.PendingHit==request,"other hit acknowledgements cannot end this prediction");
   ReceiveWorldHitAcknowledgementRPC(_worldEpoch,knocked.Nid,request); Check(Entry.PendingHit==0 && Applied==1,"matching owner result restores authoritative state");
   Check(!WorldMovementPrefix(knocked,out movement) && !movement,"idle replica movement remains authoritative");
   BeginWorldHitPresentation(knocked,DamageType.Melee,100,300,true); Time.unscaledTime=2;
   Check(!WorldMovementPrefix(knocked,out movement),"unconfirmed presentation cannot continue past its deadline");
   Check(BeginWorldHitPresentation(hero,DamageType.Melee,100,300,true)==0,"heroes do not receive world knock prediction");
   Check(IsWorldPickupVisible(9000,9000,128,128),"network pickup physics must advance outside the owner camera");
   Session=false; Check(!IsWorldPickupVisible(9000,9000,128,128),"offline pickup visibility remains native"); Session=true;
   p.IsMine=false; p.collected=false; p.gameObject.SetActive(true);
   var smooth=RegisterWorldObject(p); smooth.Last=State(true,false); smooth.Last.X=100; smooth.Last.Y=50;
   smooth.LerpFrom=new Vector2(0,0); smooth.ReceivedAt=10; Time.unscaledTime=10+WorldSendInterval*.25f;
   WorldPickupUpdatePrefix(p); Check(Math.Abs(p.X-25)<.001f && Math.Abs(p.Y-12.5f)<.001f,"pickup moves between owner snapshots");
   Time.unscaledTime=10+WorldSendInterval*.75f; WorldPickupUpdatePrefix(p);
   Check(Math.Abs(p.X-75)<.001f,"pickup keeps progressing every frame without another packet");
   Time.unscaledTime=11; WorldPickupUpdatePrefix(p); Check(p.X==100 && p.Y==50,"pickup interpolation stops at the latest authoritative sample");
   var corpse=new MookTrooper { Nid=new NID(new SID(20),PID.TargetServer,80,3) };
   int fatal=BeginWorldHitPresentation(corpse,DamageType.Explosion,100,200,true);
   PresentWorldHit(corpse,fatal,3,DamageType.Explosion,100,200,0);
   Check(Corpses==1 && corpse.health==3 && corpse.actionState==ActionState.Idle && corpse.DamageCalls==0,"death presentation must be immediate without changing gameplay health or running Death");
   Check((Entry.Presentation.Flags&WorldFlags.Dead)!=0 && _worldApplying==0,"corpse presentation tracks terminal feedback and unwinds isolation");
   FinishWorldHitPresentation(Entry,new WorldSnapshot { Health=3 });
   Check(Entry.Presentation==null && Entry.PresentedFlags==0,"denied lethal prediction restores live presentation");
   fatal=BeginWorldHitPresentation(corpse,DamageType.Explosion,100,200,true);
   PresentWorldHit(corpse,fatal,14,DamageType.Explosion,100,200,0);
   Check(Gibs==1 && !corpse.Renderers[0].enabled && corpse.health==3,"gibs hide original visual without destroying registered target");
   PresentWorldHit(corpse,fatal,14,DamageType.Explosion,100,200,0);
   Check(Gibs==1,"repeated pending hits do not replay gibs");
   FinishWorldHitPresentation(Entry,new WorldSnapshot { Flags=WorldFlags.Removed|WorldFlags.Gibbed });
   Check((Entry.PresentedFlags&WorldFlags.Gibbed)!=0 && !corpse.Renderers[0].enabled,"confirmed gibs retain deduplication until removal");
   var correction=new MookTrooper { Nid=new NID(new SID(20),PID.TargetServer,84,3) };
   int corrected=BeginWorldHitPresentation(correction,DamageType.Explosion,100,200,true);
   PresentWorldHit(correction,corrected,14,DamageType.Explosion,100,200,0);
   Check(WaitForWorldGibConfirmation(Entry,new WorldSnapshot { Flags=WorldFlags.Dead }),"older corpse state waits for the newer predicted gib acknowledgement");
   Check(!WaitForWorldGibConfirmation(Entry,new WorldSnapshot { Flags=WorldFlags.Dead|WorldFlags.Gibbed|WorldFlags.Removed }),"matching gib removal can finish immediately without replaying gibs");
   FinishWorldHitPresentation(Entry,new WorldSnapshot { Flags=WorldFlags.Dead });
   Check(correction.Renderers[0].enabled && (Entry.PresentedFlags&WorldFlags.Gibbed)==0,"owner corpse correction restores renderer without suppressing a future real gib");
   var deadBody=new MookTrooper { Nid=new NID(new SID(20),PID.TargetServer,83,3),health=0,actionState=ActionState.Dead };
   int corpseHit=BeginWorldHitPresentation(deadBody,DamageType.Explosion,100,200,true);
   PresentWorldHit(deadBody,corpseHit,14,DamageType.Explosion,100,200,0);
   Check(corpseHit!=0 && Gibs==3 && deadBody.health==0 && !deadBody.destroyed,"hitting an existing corpse presents gibs without local destruction");
   Check(!WorldMovementPrefix(deadBody,out movement) && !movement,"pending corpse movement cannot trigger another local gib or explosion");
   var armoured=new MookArmouredGuy { Nid=new NID(new SID(20),PID.TargetServer,85,3) };
   int armourHit=BeginWorldHitPresentation(armoured,DamageType.Explosion,100,200,true);
   int bloodBefore=EffectsController.Blood;
   PresentWorldHitDirected(armoured,armourHit,30,DamageType.Explosion,100,200,1,0);
   Check(armoured.Defends==1 && Entry.Presentation.Health==3 && EffectsController.Blood==bloodBefore,"armour block never fakes blood or health loss");
   PresentWorldHitDirected(armoured,armourHit,30,DamageType.Explosion,100,200,1,0);
   Check(armoured.Defends==2 && Entry.Presentation.Health==3,"exact 60 armour threshold still blocks");
   PresentWorldHitDirected(armoured,armourHit,1,DamageType.Explosion,100,200,1,0);
   Check(Entry.Presentation.Health==2 && armoured.health==3 && EffectsController.Blood==bloodBefore+1,"pending hits cross armour threshold without local settlement");
   var armourState=new WorldSnapshot(); CaptureWorldSpecialHitState(armoured,armourState);
   armourState.ArmourDamage=70; armourState.FireDamage=36; ApplyWorldSpecialHitState(armoured,armourState);
   Check(armoured.shieldDamage==70 && armoured.fireAmount==36,"armour correction carries owner counters");
   var riot=new MookRiotShield { Nid=new NID(new SID(20),PID.TargetServer,86,3) };
   int shieldHit=BeginWorldHitPresentation(riot,DamageType.Melee,100,200,true);
   int debrisBefore=EffectsController.Debris; bloodBefore=EffectsController.Blood;
   PresentWorldHitDirected(riot,shieldHit,8,DamageType.Melee,100,200,-1,0);
   Check(Entry.Presentation.ShieldLost && riot.hasShield && riot.health==3 && EffectsController.Blood==bloodBefore,"front shield hit only predicts shield visual");
   PresentWorldHitDirected(riot,shieldHit,8,DamageType.Melee,100,200,-1,0);
   Check(EffectsController.Debris==debrisBefore+1 && EffectsController.Blood==bloodBefore,"shield visual deduplicates and retains 80ms protection");
   FinishWorldHitPresentation(Entry,new WorldSnapshot { Flags=WorldFlags.Shield });
   Check(riot.GetComponent<Renderer>().sharedMaterial!=riot.unarmedMaterial,"rejected disarm restores shield material");
   shieldHit=BeginWorldHitPresentation(riot,DamageType.Melee,100,200,true);
   PresentWorldHitDirected(riot,shieldHit,8,DamageType.Melee,100,200,-1,0);
   Entry.Last=new WorldSnapshot { Health=3 };
   EndWorldHitPresentation(Entry);
   Check(ConfirmedDisarms==1 && !riot.hasShield && Entry.Presentation==null && _worldApplying==0,"ack confirms shield before clearing visual deduplication");
   EndWorldHitPresentation(Entry);
   Check(ConfirmedDisarms==1,"repeated confirmation cannot disarm again");
   var closed=new BossBlockPieceSheathed { Nid=new NID(new SID(20),PID.TargetServer,87,3) };
   int bossHit=BeginWorldHitPresentation(closed,DamageType.Explosion,0,0,false);
   PresentWorldHit(closed,bossHit,14,DamageType.Explosion,0,0,2);
   Check(closed.hurtTime==0 && closed.health==3,"closed boss part cannot show damage");
   closed.indestructible=false; PresentWorldHit(closed,bossHit,14,DamageType.Explosion,0,0,2);
   Check(closed.hurtTime==.05f && closed.health==3 && !closed.IsDead,"open boss part flashes without predicting death or stage");
   closed.hurtTime=0; closed.ignoreDamageWhileOtherPieceAlive=new BossBlockPiece();
   PresentWorldHit(closed,bossHit,14,DamageType.Explosion,0,0,2);
   Check(closed.hurtTime==0,"living armour piece closes the weak point");
   var battery=new BossBlockWeapon { Nid=new NID(new SID(20),PID.TargetServer,89,3),enabled=false,indestructible=true };
   bossHit=BeginWorldHitPresentation(battery,DamageType.Explosion,0,0,false);
   PresentWorldHit(battery,bossHit,14,DamageType.Explosion,0,0,2);
   Check(!battery.enabled && battery.hurtTime==0 && battery.GetComponent<Renderer>().sharedMaterial!=battery.hurtMaterial,"sheathed weapon does not flash or enable");
   battery.indestructible=false;
   PresentWorldHit(battery,bossHit,14,DamageType.Explosion,0,0,2);
   Check(battery.enabled && battery.hurtTime==.05f && battery.GetComponent<Renderer>().sharedMaterial==battery.hurtMaterial && battery.health==3,"disabled boss weapon resumes native flash expiry without applying damage");
   var bossPiece=new BossPiece(); var bossState=new WorldSnapshot { BossDamaged=true };
   ApplyWorldSpecialHitState(bossPiece,bossState);
   Check(bossPiece.isDamaged && bossPiece.GetComponent<Renderer>().sharedMaterial==bossPiece.damagedMaterial,"confirmed boss damage stage changes the replica material");
   CaptureWorldSpecialHitState(bossPiece,bossState);
   using(var stageStream=new MemoryStream())using(var stageWriter=new BinaryWriter(stageStream)) {
    bossState.Type="BossPiece";bossState.ArmourDamage=61;bossState.FireDamage=36;bossState.ShieldTime=.08f;bossState.Invulnerable=true;
    WriteWorldState(stageWriter,bossState);stageWriter.Flush();stageStream.Position=0;
    using(var stageReader=new BinaryReader(stageStream)) { var stage=ReadWorldState(stageReader);
     Check(stage.BossDamaged && stage.ArmourDamage==61 && stage.FireDamage==36 && stage.ShieldTime==.08f && stage.Invulnerable,"armour and boss gates survive wire encoding");
    }
   }
   var goliath=new GoliathMech { health=360,maxHealth=360 };
   var mechState=new WorldSnapshot { Health=180,MaxHealth=360 };
   ApplyWorldSpecialHitState(goliath,mechState);
   Check(goliath.tankSpeed==150 && goliath.eye.GetComponent<Renderer>().sharedMaterial!=goliath.angryEyeMaterial,"exact half health keeps the original mech phase");
   mechState.Health=179; ApplyWorldSpecialHitState(goliath,mechState);
   Check(goliath.tankSpeed==200 && goliath.maxUpwardVelocity==150 && goliath.eye.GetComponent<Renderer>().sharedMaterial==goliath.angryEyeMaterial,"confirmed below-half mech phase restores speed and angry eye");
   ApplyWorldSpecialHitState(goliath,mechState);
   Check(goliath.health==360 && goliath.DamageCalls==0,"repeated phase confirmation never settles damage");
   mechState.Flags=WorldFlags.MechAnimated; mechState.Facing=-1; mechState.Turn=0;
   ApplyWorldSpecialHitState(goliath,mechState);
   Check(goliath.mechAnimation.gameObject.activeSelf && goliath.turnFrame==0 && goliath.facingDirection==-1,"confirmed turn completion restores the native weapon hierarchy");
   mechState.Flags=WorldFlags.None; mechState.Turn=4; ApplyWorldSpecialHitState(goliath,mechState);
   Check(!goliath.mechAnimation.gameObject.activeSelf && goliath.turnFrame==4,"turning confirmation retains the owner turn frame");
   CaptureWorldSpecialHitState(goliath,mechState);
   Check(mechState.Facing==-1 && mechState.Turn==4 && (mechState.Flags&WorldFlags.MechAnimated)==0,"capture preserves mech visual mode and direction");
   mechState.Flags=WorldFlags.MechAnimated;
   using(var mechStream=new MemoryStream())using(var mechWriter=new BinaryWriter(mechStream)) {
    mechState.Type="GoliathMech"; WriteWorldState(mechWriter,mechState); mechWriter.Flush(); mechStream.Position=0;
    using(var mechReader=new BinaryReader(mechStream)) Check((ReadWorldState(mechReader).Flags&WorldFlags.MechAnimated)!=0,"mech visual mode survives the existing protocol 12 flag field");
   }
   var tank=new Tank { Nid=new NID(new SID(20),PID.TargetServer,88,3),invulnerable=true };
   var mechWeapon=new MechWeapon { mech=goliath };
   var weaponMaterial=mechWeapon.GetComponent<Renderer>().sharedMaterial;
   var weaponState=new WorldSnapshot { Flags=WorldFlags.Dead };
   ApplyWorldSpecialHitState(mechWeapon,weaponState);
   Check(mechWeapon.GetComponent<Renderer>().sharedMaterial==weaponMaterial,"live goliath keeps its weapon material");
   goliath.health=0; ApplyWorldSpecialHitState(mechWeapon,weaponState);
   Check(mechWeapon.GetComponent<Renderer>().sharedMaterial==mechWeapon.baseMaterial,"confirmed goliath child death restores the native weapon terminal material");
   ApplyWorldSpecialHitState(mechWeapon,weaponState);
   Check(mechWeapon.DamageCalls==0 && mechWeapon.health==3,"repeated weapon terminal confirmation does not settle damage");
   Check(BeginWorldHitPresentation(tank,DamageType.Explosion,0,0,false)==0,"invulnerable non-TVD boss has no prediction");
   tank.invulnerable=false; bossHit=BeginWorldHitPresentation(tank,DamageType.Explosion,0,0,false);
   PresentWorldHit(tank,bossHit,14,DamageType.Explosion,0,0,0);
   Check(tank.Flashes==1 && tank.health==3 && tank.DamageCalls==0,"native boss flash never executes Damage");
   var relay=new DamageRelay { unit=tank,immunity=DamageType.Melee,forceDamageType=DamageType.Bullet };
   var contactObject=new GameObject { relay=relay }; DamageType routedType;
   Check(ResolveWorldHitReceiver(contactObject,DamageType.Explosion,out routedType)==tank && routedType==DamageType.Bullet,"relay preserves native forced damage type");
   Check(ResolveWorldHitReceiver(contactObject,DamageType.Melee,out routedType)==null,"relay immunity cannot be bypassed");
   relay.ignoreDamageWhileColliderActive=new Collider(); relay.ignoreDamageWhileColliderActive.gameObject.activeSelf=true;
   Check(ResolveWorldHitReceiver(contactObject,DamageType.Explosion,out routedType)==null,"closed boss collider blocks route");
   Check(!HasPlainWorldDamage(corpse,DamageType.Fire,0) && !HasPlainWorldDamage(corpse,DamageType.Melee,1),"damage-over-time and decapitation stay authoritative");
   EffectsController.Debris=0;
   var box=new CrateBlock { Nid=new NID(new SID(20),PID.TargetServer,81,3), X=800,Y=144,containsPresent=true };
   var loot=new Pickupable { Nid=new NID(new SID(20),PID.TargetServer,82,3) }; box.Hold(loot);
   int broken=BeginWorldHitPresentation(box,DamageType.Explosion,0,200,true);
   PresentWorldHit(box,broken,30,DamageType.Explosion,0,200,2);
   var boxEntry=WorldEntries[box.Nid];
   Check(EffectsController.Debris==3 && !box.Renderers[0].enabled && !box.destroyed && box.health==3,"crate debris and disappearance precede confirmation without collapsing gameplay state");
   Check(loot.Launches==1 && box.Rewards==0 && loot.gameObject.activeSelf && !box.HasPickup,"prediction reveals only the existing registered drop");
   Check(WorldPickupUpdatePrefix(loot) && loot.pickupDelay>=1,"predicted drop moves every frame while collection awaits owner");
   FinishWorldHitPresentation(boxEntry,new WorldSnapshot { Health=3 });
   Check(box.Renderers[0].enabled && box.HasPickup && !loot.gameObject.activeSelf && WorldEntries[loot.Nid].Presentation==null,"rejected crate prediction restores renderer and hidden drop");
   broken=BeginWorldHitPresentation(box,DamageType.Explosion,0,200,true); PresentWorldHit(box,broken,30,DamageType.Explosion,0,200,2);
   FinishWorldHitPresentation(boxEntry,new WorldSnapshot { Flags=WorldFlags.Destroyed });
   Check(!WorldBlockPresentationEffectPrefix(box,typeof(CrateBlock).GetMethod("MakeEffects")) && box.Rewards==1 && _worldSuppressCrateDebris==0,"confirmed collapse releases rewards once and omits predicted debris");
   Console.WriteLine("PASS: "+Checks+" world pickup, codec and owner-hit regression checks");
  }
  __PRODUCTION_DECLARATIONS__
 }
}
'@

$fixture = $fixture.Replace('__PRODUCTION_DECLARATIONS__', $declarations)
$temporaryPath = Join-Path $PSScriptRoot ('world-pickups-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temporaryPath | Out-Null
try {
    $fixturePath = Join-Path $temporaryPath 'Fixture.cs'
    $outputPath = Join-Path $temporaryPath 'Fixture.exe'
    [IO.File]::WriteAllText($fixturePath, $fixture, (New-Object Text.UTF8Encoding($false)))
    $compiler = Join-Path $env:windir 'Microsoft.NET\Framework64\v3.5\csc.exe'
    & $compiler /nologo /nowarn:0649 /target:exe "/out:$outputPath" $fixturePath (Join-Path $projectRoot 'src\HarmonyDiagnostics.WorldPickups.cs') (Join-Path $projectRoot 'src\HarmonyDiagnostics.WorldSpecialHits.cs')
    if ($LASTEXITCODE -ne 0) { throw 'World pickup fixture compilation failed.' }
    & $outputPath
    if ($LASTEXITCODE -ne 0) { throw 'World pickup regression failed.' }
}
finally {
    Remove-Item -LiteralPath $temporaryPath -Recurse -Force
}
