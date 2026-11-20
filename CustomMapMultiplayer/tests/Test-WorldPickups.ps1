param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
[xml]$props = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $projectRoot 'LocalBroforcePath.props')
$harmonyPath = Join-Path $props.Project.PropertyGroup.UnityModManagerPath '0Harmony.dll'
$worldSource = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $projectRoot 'src\HarmonyDiagnostics.WorldState.cs')
$hitSource = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $projectRoot 'src\HarmonyDiagnostics.WorldProjectiles.cs')
$presentationSource = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $projectRoot 'src\HarmonyDiagnostics.WorldHitPresentation.cs')
$collectSource = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $projectRoot 'src\HarmonyDiagnostics.WorkshopPickup.cs')
$doodadSource = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $projectRoot 'src\HarmonyDiagnostics.WorldDoodads.cs')
$registrationSource = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $projectRoot 'src\HarmonyDiagnostics.WorldRegistration.cs')
$reflectionSource = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $projectRoot 'src\HarmonyDiagnostics.Reflection.cs')
$statusSource = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $projectRoot 'src\HarmonyDiagnostics.WorldPersistentStatus.cs')
$effectSource = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $projectRoot 'src\HarmonyDiagnostics.WorldPersistentEffects.cs')

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
    Read-Declaration $reflectionSource 'private static MethodInfo GetPatchMethod('
    Read-Declaration $worldSource 'private static bool AcceptWorldRpc('
    Read-Declaration $worldSource 'private static bool IsWorldRpcOwner('
    Read-Declaration $worldSource 'private static bool IsWorldRpcObject('
    Read-Declaration $worldSource 'private enum WorldFlags'
    Read-Declaration $worldSource 'private sealed class WorldSnapshot'
    Read-Declaration $worldSource 'private static bool WorldCriticalChanged('
    Read-Declaration $doodadSource 'private static bool WorldDoodadChanged('
    Read-Declaration $registrationSource 'private static bool MatchesWorldTombstone('
    Read-Declaration $statusSource 'private static bool WorldPersistentStatusChanged('
    Read-Declaration $statusSource 'private static void WriteWorldPersistentStatus('
    Read-Declaration $statusSource 'private static void ReadWorldPersistentStatus('
    Read-Declaration $effectSource 'private sealed class WorldEffectState'
    Read-Declaration $effectSource 'private static void WriteWorldEffect('
    Read-Declaration $effectSource 'private static WorldEffectState ReadWorldEffect('
    Read-Declaration $effectSource 'private static bool IsNewWorldEffect('
    Read-Declaration $effectSource 'private static bool IsExpiredWorldEffect('
    Read-Declaration $effectSource 'private static long WorldEffectKey('
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
namespace UnityEngine {
 public class Object { public static void Destroy(object o) { } }
 public class MonoBehaviour { }
 public class Component:MonoBehaviour { public GameObject gameObject; }
 public struct Color { public Color(float r,float g,float b,float a) { } }
 public class Material { public void SetColor(string n,Color c) { } }
 public class Renderer { public bool enabled=true; public Transform transform=new Transform(null); public Material sharedMaterial=new Material(); public Material material { get { return sharedMaterial; } } public T GetComponentInParent<T>() where T:class { for(var t=transform;t!=null;t=t.parent) { var c=t.GetComponent<T>(); if(c!=null)return c; } return null; } }
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
 public static PID MyID=new PID(7), ServerID=new PID(6), TargetServer=new PID(0), TargetOthers=new PID(-1), TargetAll=new PID(-2);
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
public class DamageObject { public int damage; public DamageType damageType; public float xForce,yForce,x,y; public MonoBehaviour damageSender;
 public DamageObject(int d,DamageType t,float vx,float vy,float x,float y,MonoBehaviour a) { damage=d;damageType=t;xForce=vx;yForce=vy;this.x=x;this.y=y;damageSender=a; } }
public class Player { public TestVanDammeAnim character; }
public class HeroController { public static Player[] players=new Player[4]; }
public class BroforceObject:Component {
 public NID Nid; public PID Owner; public bool IsMine; public Transform transform;
 public float xI,yI; public BroforceObject() { gameObject=new GameObject(); gameObject.owner=this; transform=new Transform(this); Renderers[0].transform=transform; }
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
 public DamageObject DirectHit; public int DirectDirection,GibCalls; public bool Fail; public Unit Collateral;
 [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
 public virtual void Damage(int d,DamageType t,float vx,float vy,int dir,MonoBehaviour a,float x,float y) {
  if(Fail)throw new InvalidOperationException("fixture failure");
  DamageCalls++;DirectHit=new DamageObject(d,t,vx,vy,x,y,a);DirectDirection=dir;
  if(Collateral!=null)Collateral.Damage(d,t,vx,vy,dir,a,x,y);
 }
 [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
 public virtual void GibNow(DamageType t,float vx,float vy) { Gib(t,vx,vy); }
 [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
 protected virtual void Gib(DamageType t,float vx,float vy) { GibCalls++; }
 public void HeadShot(int d,DamageType t,float vx,float vy,int dir,float x,float y,MonoBehaviour a) { DamageCalls++; }
 public void Knock(DamageType t,float vx,float vy,bool local) { KnockCalls++; }
 public int BurnCalls,BurnDamage,BurnDirection; public void BurnInternal(int d,int direction) { BurnCalls++;BurnDamage=d;BurnDirection=direction; }
}
public class TestVanDammeAnim:Unit { public bool canGib=true,WillReviveAlready,hasBeenCoverInAcid,AmmoFull; public object inseminatorUnit; public float xIBlast,yIBlast; public BloodColor bloodColor; public bool IsAmmoFull() { return AmmoFull; } public void ChangeFrame() { } public void Sound() { } }
public class GuardedUnit:Unit {
 public int Overrides; public bool Shield;
 [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
 public override void Damage(int d,DamageType t,float vx,float vy,int dir,MonoBehaviour a,float x,float y) { Overrides++;if(!Shield)base.Damage(d,t,vx,vy,dir,a,x,y); }
}
public static class AreaFactory {
 [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
 public static void DamageDoodads(MonoBehaviour sender,int playerNum,out bool hit) { CustomMapMultiplayer.HarmonyDiagnostics.ObserveArea();hit=true; }
 [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
 public static void BurnUnitsAround_Local(MonoBehaviour firedBy,int playerNum) { CustomMapMultiplayer.HarmonyDiagnostics.ObserveArea(); }
 [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
 public static void HitProjectiles(int playerNum) { CustomMapMultiplayer.HarmonyDiagnostics.ObserveArea(); }
 [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
 public static void DamageProjectile(int newPlayerNum) { CustomMapMultiplayer.HarmonyDiagnostics.ObserveArea(); }
}
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
public class MookGunplacement:Tank { private bool mookKilled; public BroforceObject driverSprite=new BroforceObject(); public bool CanFire() { return !mookKilled; } }
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
public class Doodad:BroforceObject { public int Hits; public DamageObject LastHit; public bool DamageOptional(DamageObject hit,ref bool show) { Hits++;LastHit=hit;show=true;return true; } }
public class DoodadDestroyable:Doodad { public int Ignitions; public void SetAlight_Local() { Ignitions++; } }
public class BlockPiece:BroforceObject { }
public class Block:BroforceObject {
 public bool protectedFromDestruction; public float collapseChance=1; public object shrapnelPrefab,shrapnelBitPrefab; public BlockPiece backgroundMesh;
 public Mine mine; public int Collapses,NativeMineChecks; public bool FailCollapse;
 public void CheckForMine() { NativeMineChecks++; }
 public void Sound() { }
 public void ActuallyCollapse(float x,float y,bool around) { if(FailCollapse)throw new InvalidOperationException(); if(destroyed)return;Collapses++;destroyed=true;if(mine!=null)mine.Detonate(true); }
 public void DestroyBlockInternal(bool around) { }
 public void Damage() { }
 public int Ignitions; public void SetAlight_Local() { Ignitions++; }
}
public class DirtBlock:Block { public void ShowBrokenBackground() { backgroundMesh=new BlockPiece(); } }
public class Projectile:BroforceObject { public int Hits,NewPlayer; public float Delay; public void Damage(int d,DamageType t,float vx,float vy,float delay,int player) { Hits++;Delay=delay;NewPlayer=player; } }
public class Grenade:BroforceObject { }
public class RemoteControlExplosiveCar:Grenade { }
public class Mine:BroforceObject {
 public bool exploded,detonated;public int Starts,ImmediateExplosions; public Action ExplosionAction;
 public void Detonate(bool immediate) { if(!detonated){detonated=true;Starts++;}if(immediate&&!exploded){ImmediateExplosions++;exploded=true;} }
 public void Explode(int playerNum) { if(exploded)return;exploded=true;if(ExplosionAction!=null)ExplosionAction(); }
}
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
 public static NID GetNID(object o) { var obj=o as BroforceObject;return obj==null?NID.NoID:obj.Nid; }
 public static object GetObject(NID id) { BroforceObject o; return Items.TryGetValue(id,out o)?o:null; }
}
public class RPCInfo { public PID Sender; }
public class RPCObject { public RPCInfo messageInfo=new RPCInfo(); }
public class CreatedRPC { public object Target;public MethodInfo Method;public object[] Parameters;public bool Immediate; }
public static class RPCController {
 public static RPCObject CurrentRPC;
 public static CreatedRPC CreateRPC(object targetObject,MethodInfo methodInfo,object[] parameters,bool immediate) { return new CreatedRPC{Target=targetObject,Method=methodInfo,Parameters=parameters,Immediate=immediate}; }
}
public static class RPCBatcher { public static int Flushes; public static void FlushQueue() { Flushes++; } }
namespace Networking {
 public class AllowedRPCAttribute:Attribute { }
 public delegate void RpcSignature<T,U>(T a,U b);
 public delegate void RpcSignature<T,U,V>(T a,U b,V c);
 public static class Networking {
  public static int Calls; public static PID Target; public static bool Immediate; public static byte[] Bytes; public static CreatedRPC Last;
  public static void RPC<T,U>(PID target,RpcSignature<T,U> method,T a,U b,bool immediate) {
   Calls++; Target=target; Immediate=immediate; Bytes=(object)b as byte[];
   Last=RPCController.CreateRPC(method.Target,method.Method,new object[]{a,b},immediate);
  }
  public static void RPC<T,U,V>(PID target,RpcSignature<T,U,V> method,T a,U b,V c,bool immediate) { }
 }
}
namespace CustomMapMultiplayer {
 internal static partial class HarmonyDiagnostics {
  private const BindingFlags WorldFields=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
  private static long _worldEpoch=123; private static int _worldApplying, _worldGrenadeReplicaUpdate, _worldNetworkGrenadeSpawnDepth;
  private static bool Session=true; private static BroforceObject _worldDamageSource;
  private static Unit _worldStatusSource;
  private static bool _workshopDuplicatePickupCollectLogged;
  private static bool IsWorkshopOnlineSession() { return Session; }
  private static class DiagnosticLog { public static void Info(string message) { } }
  private static int _worldSequence,Published,DamageAtCapture,_worldHitSequence,Applied;
  private const float WorldSendInterval=1f/12f;
  private static bool _worldHitMessagesPending;
  private sealed class WorldEntry { public bool Dirty; public int Life=1,DirectSequence; public Dictionary<int,int> DirectReceived; public WorldSnapshot Last; public float SentAt,ReceivedAt,PendingHitUntil; public int PendingHit; public Vector2 LerpFrom; public BroforceObject Object; public WorldHitPresentation Presentation; public WorldFlags PresentedFlags; }
  private static int _worldSettlingHit,_worldHitRequestor,_worldSuppressCrateDebris,Gibs,Corpses;
  private static Dictionary<int,byte> WorldPresentedHitEffects=new Dictionary<int,byte>();
  private static Dictionary<Type,bool> WorldPlainDamageTypes=new Dictionary<Type,bool>();
  private static FieldInfo WorldGibThreshold=typeof(Threshold).GetField("Value");
  private static FieldInfo WorldFrozen=typeof(Threshold).GetField("Frozen");
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
  private static WorldSnapshot CaptureWorldState(WorldEntry e) { if(e.Object is Unit)DamageAtCapture=((Unit)e.Object).DamageCalls; return new WorldSnapshot { SentAt=42, Health=e.Object.health,Life=e.Life }; }
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
    var gas=new WorldEffectState { Owner=6,Serial=42,Revision=7,Prefab="TearGasExplosion",Type="FlashBangExplosion",Remaining=3.5f,Clock=123,Position=new Vector3(12,24,1),Data=new byte[]{1,0,8} };
    WriteWorldEffect(w,gas); WriteWorldEffect(w,new WorldEffectState { Owner=7,Serial=42,Revision=9,Prefab="beam",Type="LaserBeam",Removed=true,Laser=true });
    w.Flush();stream.Position=0;
    using(var r=new BinaryReader(stream)) {
     var decoded=ReadWorldEffect(r); var removed=ReadWorldEffect(r);
     Check(decoded.Owner==6 && decoded.Serial==42 && decoded.Revision==7 && decoded.Remaining==3.5f,"effect identity and remaining duration survive production codec");
     Check(decoded.Position.y==24 && decoded.Data.Length==3 && decoded.Data[2]==8,"effect grid payload survives production codec");
     Check(removed.Laser && removed.Removed && stream.Position==stream.Length,"effect removal and adjacent framing survive production codec");
     Check(WorldEffectKey(6,42)!=WorldEffectKey(7,42),"different owners cannot collide at the same pool serial");
     Check(!IsNewWorldEffect(gas,decoded),"duplicate effect state is ignored");
     decoded.Revision=6;Check(!IsNewWorldEffect(gas,decoded),"older baseline cannot overwrite newer live effect");
     decoded.Revision=8;Check(IsNewWorldEffect(gas,decoded),"newer live effect is accepted");
     decoded.Removed=true;Check(IsNewWorldEffect(gas,decoded),"newer Removed terminates a live effect");
     gas.Revision=9;Check(!IsNewWorldEffect(decoded,gas),"terminated pool life cannot be resurrected by a delayed active state");
     Check(IsExpiredWorldEffect(gas,3.5f) && !IsExpiredWorldEffect(gas,3.4f),"pending elapsed time prevents full-duration restart");
     Check(IsExpiredWorldEffect(removed,0),"removed effects expire without an object or duration");
     Check(IsNewWorldEffect(null,gas),"new pool identity can start a separate lifecycle");
    }
   }
   using(var stream=new MemoryStream())using(var w=new BinaryWriter(stream)) {
    active.DoodadVisual=15; active.DoodadColliders=10; active.DoodadMaterial=2;
    active.DoodadPixel=new Vector2(128,64); active.DoodadChildren=new byte[] {0,1,0};
    active.StunTime=8; active.FrozenTime=6; active.ShockTime=3; active.PlasmaTime=4; active.PlasmaDamage=2;
    active.Acid=true; active.AcidTime=.6f; active.StatusMaterial="acid"; active.TearGas=true;
    active.Inseminator=new NID(new SID(20),PID.TargetServer,110,3); active.InseminationTime=1.2f; active.InseminatedTime=.8f; active.InseminationFrame=4;
    WriteWorldState(w,active); WriteWorldState(w,inactive); w.Flush(); stream.Position=0;
    using(var r=new BinaryReader(stream)) {
     var a=ReadWorldState(r); var b=ReadWorldState(r);
     Check(a.Id==active.Id && a.PickupType==active.PickupType && a.PickupDelay==.3f && a.X==800,"pickup wire round trip");
     Check(float.IsPositiveInfinity(a.LifeTime) && (b.Flags&WorldFlags.PickupActive)==0 && stream.Position==stream.Length,"adjacent snapshot framing");
     Check(a.DoodadVisual==15 && a.DoodadColliders==10 && a.DoodadMaterial==2 && a.DoodadPixel.x==128 && a.DoodadPixel.y==64,"doodad stage and appearance survive the production codec");
     Check(a.DoodadChildren.Length==3 && a.DoodadChildren[1]==1 && b.DoodadVisual==0,"doodad children preserve exact active states without changing adjacent non-doodad framing");
     var same=ReadDoodadCopy(a);
     Check(a.StunTime==8 && a.FrozenTime==6 && a.ShockTime==3 && a.PlasmaTime==4 && a.PlasmaDamage==2,"persistent unit timers survive production codec");
     Check(a.Acid && a.AcidTime==.6f && a.StatusMaterial=="acid" && a.TearGas,"acid material and choking survive production codec");
     Check(a.Inseminator==active.Inseminator && a.InseminationTime==1.2f && a.InseminatedTime==.8f && a.InseminationFrame==4,"parasite dependency and progress survive production codec");
     Check(!WorldPersistentStatusChanged(a,same),"duplicate persistent status does not become a new transition");
     same.FrozenTime=0; Check(WorldCriticalChanged(a,same),"freeze expiry needs reliable state");
     same=ReadDoodadCopy(a); same.Acid=false; Check(WorldCriticalChanged(a,same),"acid recovery needs reliable state");
     same=ReadDoodadCopy(a); same.Inseminator=NID.NoID; Check(WorldCriticalChanged(a,same),"parasite removal needs reliable state");
     same=ReadDoodadCopy(a);
     Check(!WorldDoodadChanged(a,same),"repeated damaged state is unchanged");
     same.DoodadColliders=4;
     Check(WorldCriticalChanged(a,same),"collision phase changes require reliable delivery at unchanged health");
     same=ReadDoodadCopy(a); same.DoodadChildren[0]=1;
     Check(WorldCriticalChanged(a,same),"pristine child state changes require reliable delivery");
     same=ReadDoodadCopy(a); same.DoodadMaterial=1;
     Check(WorldCriticalChanged(a,same),"material stage changes require reliable delivery");
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
   PresentWorldHitDirected(corpse,fatal,3,DamageType.Explosion,100,200,1,0);
   Check(Corpses==1 && corpse.health==3 && corpse.actionState==ActionState.Idle && corpse.DamageCalls==0,"death presentation must be immediate without changing gameplay health or running Death");
   Check((Entry.Presentation.Flags&WorldFlags.Dead)!=0 && _worldApplying==0,"corpse presentation tracks terminal feedback and unwinds isolation");
   FinishWorldHitPresentation(Entry,new WorldSnapshot { Health=3 });
   Check(Entry.Presentation==null && Entry.PresentedFlags==0,"denied lethal prediction restores live presentation");
   fatal=BeginWorldHitPresentation(corpse,DamageType.Explosion,100,200,true);
   PresentWorldHitDirected(corpse,fatal,14,DamageType.Explosion,100,200,1,0);
   Check(Gibs==1 && !corpse.Renderers[0].enabled && corpse.health==3,"gibs hide original visual without destroying registered target");
   PresentWorldHitDirected(corpse,fatal,14,DamageType.Explosion,100,200,1,0);
   Check(Gibs==1,"repeated pending hits do not replay gibs");
   FinishWorldHitPresentation(Entry,new WorldSnapshot { Flags=WorldFlags.Removed|WorldFlags.Gibbed });
   Check((Entry.PresentedFlags&WorldFlags.Gibbed)!=0 && !corpse.Renderers[0].enabled,"confirmed gibs retain deduplication until removal");
   var correction=new MookTrooper { Nid=new NID(new SID(20),PID.TargetServer,84,3) };
   int corrected=BeginWorldHitPresentation(correction,DamageType.Explosion,100,200,true);
   PresentWorldHitDirected(correction,corrected,14,DamageType.Explosion,100,200,1,0);
   Check(WaitForWorldGibConfirmation(Entry,new WorldSnapshot { Flags=WorldFlags.Dead }),"older corpse state waits for the newer predicted gib acknowledgement");
   Check(!WaitForWorldGibConfirmation(Entry,new WorldSnapshot { Flags=WorldFlags.Dead|WorldFlags.Gibbed|WorldFlags.Removed }),"matching gib removal can finish immediately without replaying gibs");
   FinishWorldHitPresentation(Entry,new WorldSnapshot { Flags=WorldFlags.Dead });
   Check(correction.Renderers[0].enabled && (Entry.PresentedFlags&WorldFlags.Gibbed)==0,"owner corpse correction restores renderer without suppressing a future real gib");
   var deadBody=new MookTrooper { Nid=new NID(new SID(20),PID.TargetServer,83,3),health=0,actionState=ActionState.Dead };
   int corpseHit=BeginWorldHitPresentation(deadBody,DamageType.Explosion,100,200,true);
   PresentWorldHitDirected(deadBody,corpseHit,14,DamageType.Explosion,100,200,1,0);
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
   PresentWorldHitDirected(closed,bossHit,14,DamageType.Explosion,0,0,1,2);
   Check(closed.hurtTime==0 && closed.health==3,"closed boss part cannot show damage");
   closed.indestructible=false; PresentWorldHitDirected(closed,bossHit,14,DamageType.Explosion,0,0,1,2);
   Check(closed.hurtTime==.05f && closed.health==3 && !closed.IsDead,"open boss part flashes without predicting death or stage");
   closed.hurtTime=0; closed.ignoreDamageWhileOtherPieceAlive=new BossBlockPiece();
   PresentWorldHitDirected(closed,bossHit,14,DamageType.Explosion,0,0,1,2);
   Check(closed.hurtTime==0,"living armour piece closes the weak point");
   var battery=new BossBlockWeapon { Nid=new NID(new SID(20),PID.TargetServer,89,3),enabled=false,indestructible=true };
   bossHit=BeginWorldHitPresentation(battery,DamageType.Explosion,0,0,false);
   PresentWorldHitDirected(battery,bossHit,14,DamageType.Explosion,0,0,1,2);
   Check(!battery.enabled && battery.hurtTime==0 && battery.GetComponent<Renderer>().sharedMaterial!=battery.hurtMaterial,"sheathed weapon does not flash or enable");
   battery.indestructible=false;
   PresentWorldHitDirected(battery,bossHit,14,DamageType.Explosion,0,0,1,2);
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
   var gunplacement=new MookGunplacement(); var occupiedSeat=new WorldSnapshot { Type="MookGunplacement" };
   CaptureWorldSpecialHitState(gunplacement,occupiedSeat); ApplyWorldSpecialHitState(gunplacement,occupiedSeat);
   Check(!occupiedSeat.GunnerEjected && gunplacement.driverSprite.gameObject.activeSelf && gunplacement.CanFire(),"occupied gun seat keeps its driver and fire eligibility");
   WorldHitField(gunplacement,"mookKilled").SetValue(gunplacement,true);
   var emptySeat=new WorldSnapshot { Type="MookGunplacement" }; CaptureWorldSpecialHitState(gunplacement,emptySeat);
   Check(emptySeat.GunnerEjected && WorldCriticalChanged(occupiedSeat,emptySeat),"driver ejection alone requires reliable world state delivery");
   using(var seatStream=new MemoryStream())using(var seatWriter=new BinaryWriter(seatStream)) {
    WriteWorldState(seatWriter,emptySeat); WriteWorldState(seatWriter,occupiedSeat); seatWriter.Flush(); seatStream.Position=0;
    using(var seatReader=new BinaryReader(seatStream)) {
     var replica=new MookGunplacement(); var seat=ReadWorldState(seatReader);
     ApplyWorldSpecialHitState(replica,seat); ApplyWorldSpecialHitState(replica,seat);
     Check(!replica.driverSprite.gameObject.activeSelf && !replica.CanFire(),"repeated encoded ejection hides the phantom driver and disables fire");
     Check(replica.health==3 && replica.DamageCalls==0,"seat replay never reapplies damage or ejects another soldier");
     seat=ReadWorldState(seatReader); ApplyWorldSpecialHitState(replica,seat);
     Check(!seat.GunnerEjected && replica.driverSprite.gameObject.activeSelf && replica.CanFire() && seatStream.Position==seatStream.Length,"occupied baseline restores the seat and preserves adjacent packet alignment");
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
   PresentWorldHitDirected(tank,bossHit,14,DamageType.Explosion,0,0,1,0);
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
   PresentWorldHitDirected(box,broken,30,DamageType.Explosion,0,200,1,2);
   var boxEntry=WorldEntries[box.Nid];
   Check(EffectsController.Debris==3 && !box.Renderers[0].enabled && !box.destroyed && box.health==3,"crate debris and disappearance precede confirmation without collapsing gameplay state");
   Check(loot.Launches==1 && box.Rewards==0 && loot.gameObject.activeSelf && !box.HasPickup,"prediction reveals only the existing registered drop");
   Check(WorldPickupUpdatePrefix(loot) && loot.pickupDelay>=1,"predicted drop moves every frame while collection awaits owner");
   FinishWorldHitPresentation(boxEntry,new WorldSnapshot { Health=3 });
   Check(box.Renderers[0].enabled && box.HasPickup && !loot.gameObject.activeSelf && WorldEntries[loot.Nid].Presentation==null,"rejected crate prediction restores renderer and hidden drop");
   broken=BeginWorldHitPresentation(box,DamageType.Explosion,0,200,true); PresentWorldHitDirected(box,broken,30,DamageType.Explosion,0,200,1,2);
   FinishWorldHitPresentation(boxEntry,new WorldSnapshot { Flags=WorldFlags.Destroyed });
   Check(!WorldBlockPresentationEffectPrefix(box,typeof(CrateBlock).GetMethod("MakeEffects")) && box.Rewards==1 && _worldSuppressCrateDebris==0,"confirmed collapse releases rewards once and omits predicted debris");
   var cage=new Block { Nid=new NID(new SID(20),PID.TargetServer,93,4) };
   var hostage=new TestVanDammeAnim { Nid=new NID(new SID(20),PID.TargetServer,93,8),invulnerable=true };
   hostage.transform.parent=cage.transform;
   var bubble=new Renderer { transform=new Transform(null) { parent=hostage.transform } };
   var hiddenGun=new Renderer { enabled=false,transform=new Transform(null) { parent=hostage.transform } };
   var cageBar=new Renderer { transform=new Transform(null) { parent=cage.transform } };
   cage.Renderers=new[] { cage.Renderers[0],cageBar,hostage.Renderers[0],bubble,hiddenGun };
   int cageHit=BeginWorldHitPresentation(cage,DamageType.Explosion,0,200,true);
   PresentWorldHitDirected(cage,cageHit,30,DamageType.Explosion,0,200,1,2);
   var cageEntry=WorldEntries[cage.Nid];
   Check(!cage.Renderers[0].enabled && !cageBar.enabled && cageEntry.Presentation.Hidden.Length==2,"cage prediction hides the shell and bars only");
   Check(hostage.Renderers[0].enabled && bubble.enabled && !hiddenGun.enabled,"registered captive and rescue bubble retain visibility during cage prediction");
   hostage.transform.parent=null; FinishWorldHitPresentation(cageEntry,new WorldSnapshot { Flags=WorldFlags.Destroyed });
   Check(hostage.Renderers[0].enabled && bubble.enabled && !hiddenGun.enabled && !cageBar.enabled,"confirmed cage destruction preserves the detached captive and its original hidden gun");
   cage.Renderers[0].enabled=true; cageBar.enabled=true; hostage.transform.parent=cage.transform;
   cageHit=BeginWorldHitPresentation(cage,DamageType.Explosion,0,200,true); PresentWorldHitDirected(cage,cageHit,30,DamageType.Explosion,0,200,1,2);
   FinishWorldHitPresentation(cageEntry,new WorldSnapshot { Health=3 });
   Check(cage.Renderers[0].enabled && cageBar.enabled && hostage.Renderers[0].enabled && bubble.enabled && !hiddenGun.enabled,"rejected cage prediction restores only its own renderers");
   RunMutationChecks();
   _worldApplying=1;_worldGrenadeReplicaUpdate=1;
   var grenadeEnd=typeof(Pickupable).GetMethod("Death");
   Check(!WorldMutationPrefix(new Grenade(),grenadeEnd),"replica grenade update cannot expire into native damage or children during visual replay");
   Check(WorldMutationPrefix(new Pickupable(),grenadeEnd),"grenade update guard preserves unrelated pickup lifecycle replay");
   _worldGrenadeReplicaUpdate=0;
   Check(WorldMutationPrefix(new Grenade(),grenadeEnd),"leaving grenade update restores explicit lifecycle replay");
   _worldApplying=0;
   var late=new Doodad { Owner=PID.TargetServer, IsMine=false };
   var tombstone=new WorldEntry { Object=late, Last=new WorldSnapshot { Type="Doodad",Owner=PID.TargetServer.AsInt,Life=1,Flags=WorldFlags.Removed } };
   Check(MatchesWorldTombstone(tombstone),"late matching replica consumes its lifecycle tombstone");
   late.IsMine=true; Check(!MatchesWorldTombstone(tombstone),"registration cannot delete an owned object before or after native ownership setup"); late.IsMine=false;
   tombstone.Last.Type="Unit";Check(!MatchesWorldTombstone(tombstone),"a tombstone cannot retire a different component type");tombstone.Last.Type="Doodad";
   tombstone.Last.Owner=8;Check(!MatchesWorldTombstone(tombstone),"a tombstone cannot retire an object with a different final owner");tombstone.Last.Owner=PID.TargetServer.AsInt;
   tombstone.Life=2;Check(!MatchesWorldTombstone(tombstone),"an older tombstone cannot delete a newer lifecycle");tombstone.Life=1;
   tombstone.Last.Flags=WorldFlags.None;Check(!MatchesWorldTombstone(tombstone),"an active state never schedules terminal cleanup");
   Console.WriteLine("PASS: "+Checks+" world pickup, codec and owner-hit regression checks");
  }
  private static WorldSnapshot ReadDoodadCopy(WorldSnapshot state) {
   using(var stream=new MemoryStream())using(var w=new BinaryWriter(stream)) {
    WriteWorldState(w,state);w.Flush();stream.Position=0;
    using(var r=new BinaryReader(stream))return ReadWorldState(r);
   }
  }
  private static void RunMutationChecks() {
   _worldApplying=0;_worldReceivingHit=0;_worldDamageSource=null;_worldStatusSource=null;RPCController.CurrentRPC=null;Session=true;
   var source=new TestVanDammeAnim { IsMine=true,IsHero=true,Nid=new NID(new SID(20),new PID(8),501,3),Owner=new PID(8) };
   var target=new Doodad { Nid=new NID(new SID(20),PID.TargetServer,502,3),Owner=PID.TargetServer };
   Registry.Items[source.Nid]=source;Registry.Items[target.Nid]=target;
   var optional=typeof(Doodad).GetMethod("DamageOptional");
   object[] args={new DamageObject(7,DamageType.Explosion,12,34,56,78,source),false};
   int before=Networking.Networking.Calls;
   Check(!WorldDirectMutationPrefix(target,optional,args) && Networking.Networking.Calls==before+1 && target.Hits==0,"direct optional damage sends one request without mutating the replica");
   byte[] packet=Networking.Networking.Bytes;source.IsMine=false;target.IsMine=true;
   before=Networking.Networking.Calls;
   Check(!WorldDirectMutationPrefix(target,optional,args) && Networking.Networking.Calls==before,"remote hero replay does not settle or send another direct hit");
   RPCController.CurrentRPC=new RPCObject();RPCController.CurrentRPC.messageInfo.Sender=source.Owner;
   ReceiveWorldProjectileHitRPC(123,packet);
   Check(target.Hits==1 && target.LastHit.damage==7 && target.LastHit.x==56 && target.LastHit.yForce==34,"owner receives the exact optional-damage virtual entry and parameters");
   Check(_worldReceivingHit==0,"direct settlement scope is restored");
   Check(IsWorldRemoteHeroAttack(source,new Unit { IsMine=true,Nid=target.Nid }),"common public hits reject the remote hero animation");
   var localVictim=new Unit { IsMine=true,Nid=target.Nid };
   Check(!WorldProjectileHitPrefix(source,localVictim,2,DamageType.Explosion,0,0,1,false,0,0,typeof(HarmonyDiagnostics).GetMethod("KnockAndDamageUnit")),"public unit route suppresses the remote animation on the target owner");
   Check(!WorldProjectileBlockHitPrefix(source,target.gameObject,2,DamageType.Explosion,0,0,0,0),"public contact route suppresses the remote animation on the target owner");
   _worldReceivingHit=1;Check(!IsWorldRemoteHeroAttack(source,target),"authoritative settlement can invoke nested public methods");_worldReceivingHit=0;
   source.IsMine=true;target.IsMine=false;_worldDirectSource=source;
   _worldReceivingHit=1;_worldDamageSource=source;before=Networking.Networking.Calls;
   Check(!WorldDirectMutationPrefix(target,optional,args) && Networking.Networking.Calls==before+1,"settlement can forward collateral damage to another owner");
   _worldReceivingHit=0;_worldDamageSource=null;
   var burnTarget=new Unit { Nid=new NID(new SID(20),PID.TargetServer,503,3),Owner=PID.TargetServer };Registry.Items[burnTarget.Nid]=burnTarget;
   Check(!WorldDirectMutationPrefix(burnTarget,typeof(Unit).GetMethod("BurnInternal"),new object[]{5,-1}),"direct burn sends through the shared request");
   packet=Networking.Networking.Bytes;burnTarget.IsMine=true;ReceiveWorldProjectileHitRPC(123,packet);
   Check(burnTarget.BurnCalls==1 && burnTarget.BurnDamage==5 && burnTarget.BurnDirection==-1,"owner retains native burn amount and direction");
   var block=new Block { Nid=new NID(new SID(20),PID.TargetServer,504,3),Owner=PID.TargetServer };Registry.Items[block.Nid]=block;
   Check(!WorldDirectMutationPrefix(block,typeof(Block).GetMethod("SetAlight_Local"),new object[0]),"direct ground ignition forwards");
   packet=Networking.Networking.Bytes;block.IsMine=true;ReceiveWorldProjectileHitRPC(123,packet);Check(block.Ignitions==1,"owner ignites ground once");
   var shot=new Projectile { Nid=new NID(new SID(20),PID.TargetServer,505,3),Owner=PID.TargetServer };Registry.Items[shot.Nid]=shot;
   Check(!WorldDirectMutationPrefix(shot,typeof(Projectile).GetMethod("Damage"),new object[]{4,DamageType.Explosion,11f,22f,.25f,2}),"projectile damage forwards");
   packet=Networking.Networking.Bytes;shot.IsMine=true;ReceiveWorldProjectileHitRPC(123,packet);
   Check(shot.Hits==1 && shot.Delay==.25f && shot.NewPlayer==2,"projectile delay and newPlayerNum survive the request codec");
   _worldApplying=1;before=Networking.Networking.Calls;
   Check(!WorldDirectMutationPrefix(target,optional,args) && Networking.Networking.Calls==before,"visual replay neither mutates nor forwards damage");
   _worldApplying=0;_worldDirectSource=null;_worldStatusSource=null;
   Check(WorldDirectMutationKind(typeof(Unit).GetMethod("Damage"))==7,"eight-parameter Unit.Damage has an explicit request kind");
   RunDirectUnitChecks();
  }
  private static void RunDirectUnitChecks() {
   var harmony=new Harmony("cmm.direct-unit.regression");
   foreach(var t in new[]{typeof(Unit),typeof(GuardedUnit)})foreach(var m in t.GetMethods(WorldFields|BindingFlags.DeclaredOnly))
    if(WorldDirectMutationKind(m)>=7)harmony.Patch(m,new HarmonyMethod(typeof(HarmonyDiagnostics).GetMethod("WorldDirectMutationPrefix",BindingFlags.NonPublic|BindingFlags.Static)),null,null,null);
   var source=new Unit { IsHero=true,IsMine=true,Owner=new PID(8),Nid=new NID(new SID(20),new PID(8),510,3) };
   var target=new GuardedUnit { Owner=PID.TargetServer,Nid=new NID(new SID(20),PID.TargetServer,511,3) };
   Registry.Items[source.Nid]=source;Registry.Items[target.Nid]=target;
   RPCController.CurrentRPC=null;_worldStatusSource=source;
   int before=Networking.Networking.Calls;
   target.Damage(13,DamageType.Melee,14,15,-1,source,16,17);byte[] damage=Networking.Networking.Bytes;
   target.GibNow(DamageType.Explosion,18,19);byte[] gib=Networking.Networking.Bytes;
   Check(Networking.Networking.Calls==before+2 && target.Overrides==0 && target.GibCalls==0,"direct replica Damage/GibNow send one ordered request per call; no base or Gib duplicate");
   source.IsMine=false;target.IsMine=true;RPCController.CurrentRPC=new RPCObject();RPCController.CurrentRPC.messageInfo.Sender=source.Owner;
   ReceiveWorldProjectileHitRPC(123,damage);ReceiveWorldProjectileHitRPC(123,gib);
   Check(target.Overrides==1 && target.DamageCalls==1 && target.GibCalls==1 && Networking.Networking.Calls==before+2,"owner virtual override/base and GibNow/Gib execute once without recursive requests");
   Check(target.DirectHit.damage==13 && target.DirectHit.damageType==DamageType.Melee && target.DirectHit.xForce==14 && target.DirectHit.yForce==15 && target.DirectDirection==-1 && target.DirectHit.x==16 && target.DirectHit.y==17 && target.DirectHit.damageSender==source,"all direct damage parameters and actor survive production codec");
   ReceiveWorldProjectileHitRPC(123,damage);ReceiveWorldProjectileHitRPC(123,gib);
   Check(target.DamageCalls==1 && target.GibCalls==1,"duplicate direct and terminal packets are rejected");
   ReceiveWorldProjectileHitRPC(124,damage);RegisterWorldObject(target).Life=2;ReceiveWorldProjectileHitRPC(123,gib);
   Check(target.GibCalls==1,"expired epoch and older life terminal are rejected");RegisterWorldObject(target).Life=1;
   RPCController.CurrentRPC=null;before=Networking.Networking.Calls;
   target.Damage(1,DamageType.Melee,0,0,1,source,0,0);target.GibNow(DamageType.Explosion,0,0);
   Check(target.DamageCalls==1 && target.GibCalls==1 && Networking.Networking.Calls==before,"remote hero animation cannot duplicate direct damage or terminal on target owner");
   source.IsMine=true;target.IsMine=false;
   target.Damage(1,DamageType.Melee,0,0,1,source,0,0);byte[] first=Networking.Networking.Bytes;
   target.Damage(2,DamageType.Plasma,0,0,1,source,0,0);byte[] second=Networking.Networking.Bytes;
   target.IsMine=true;source.IsMine=false;RPCController.CurrentRPC=new RPCObject();RPCController.CurrentRPC.messageInfo.Sender=source.Owner;
   ReceiveWorldProjectileHitRPC(123,first);ReceiveWorldProjectileHitRPC(123,second);
   Check(target.DamageCalls==3 && target.DirectHit.damageType==DamageType.Plasma,"same-frame legitimate multi-hit and additional Plasma both settle");
   var collateral=new Unit { Owner=new PID(9),Nid=new NID(new SID(20),new PID(9),512,3) };Registry.Items[collateral.Nid]=collateral;
   source.IsMine=true;target.IsMine=false;RPCController.CurrentRPC=null;
   target.Damage(3,DamageType.Melee,0,0,1,source,0,0);byte[] nested=Networking.Networking.Bytes;
   source.IsMine=false;target.IsMine=true;target.Collateral=collateral;RPCController.CurrentRPC=new RPCObject();RPCController.CurrentRPC.messageInfo.Sender=source.Owner;
   before=Networking.Networking.Calls;ReceiveWorldProjectileHitRPC(123,nested);byte[] forwarded=Networking.Networking.Bytes;
   Check(Networking.Networking.Calls==before+1 && Networking.Networking.Target==collateral.Owner,"owner settlement forwards nested damage to a different owner once");
   target.Collateral=null;target.IsMine=false;collateral.IsMine=true;RPCController.CurrentRPC.messageInfo.Sender=PID.ServerID;
   ReceiveWorldProjectileHitRPC(123,forwarded);
   Check(collateral.DamageCalls==1 && collateral.DirectHit.damageSender==source,"nested owner request keeps original actor while checking settlement authority");
   source.IsMine=true;target.IsMine=false;RPCController.CurrentRPC=null;
   target.Damage(4,DamageType.Melee,0,0,1,source,0,0);byte[] shield=Networking.Networking.Bytes;
   target.IsMine=true;target.Shield=true;RPCController.CurrentRPC=new RPCObject();RPCController.CurrentRPC.messageInfo.Sender=source.Owner;
   before=target.DamageCalls;ReceiveWorldProjectileHitRPC(123,shield);
   Check(target.DamageCalls==before,"owner override shield gate is preserved");target.Shield=false;
   source.IsMine=true;target.IsMine=false;RPCController.CurrentRPC=null;
   target.Damage(5,DamageType.Melee,0,0,1,source,0,0);byte[] failing=Networking.Networking.Bytes;
   target.IsMine=true;target.Fail=true;RPCController.CurrentRPC=new RPCObject();RPCController.CurrentRPC.messageInfo.Sender=source.Owner;
   try { ReceiveWorldProjectileHitRPC(123,failing); }catch(InvalidOperationException) { }
   Check(_worldReceivingHit==0 && _worldReceivingTarget==null && _worldSettlingHit==0,"throwing owner virtual call restores all settlement scopes");target.Fail=false;
   source.IsMine=true;target.IsMine=false;RPCController.CurrentRPC=null;
   target.GibNow(DamageType.Explosion,21,22);byte[] late=Networking.Networking.Bytes;
   source.IsMine=false;target.IsMine=true;RPCController.CurrentRPC=new RPCObject();RPCController.CurrentRPC.messageInfo.Sender=source.Owner;
   int gibs=target.GibCalls;RegisterWorldObject(target).Life=2;ReceiveWorldProjectileHitRPC(123,late);
   Check(target.GibCalls==gibs,"a fresh unconsumed terminal packet cannot affect a newer life");RegisterWorldObject(target).Life=1;
   ReceiveWorldProjectileHitRPC(124,late);Check(target.GibCalls==gibs,"a fresh terminal packet from an expired epoch is rejected");
   RPCController.CurrentRPC.messageInfo.Sender=new PID(9);ReceiveWorldProjectileHitRPC(123,late);
   Check(target.GibCalls==gibs,"a different sender cannot claim the source owner");
   RPCController.CurrentRPC.messageInfo.Sender=source.Owner;source.Owner=new PID(9);ReceiveWorldProjectileHitRPC(123,late);
   Check(target.GibCalls==gibs,"source Registry ownership must agree with sender");source.Owner=new PID(8);
   target.destroyed=true;ReceiveWorldProjectileHitRPC(123,late);Check(target.GibCalls==gibs,"removed terminal targets reject late requests");target.destroyed=false;
   ReceiveWorldProjectileHitRPC(123,late);Check(target.GibCalls==gibs+1,"rejecting stale or invalid packets does not consume a valid terminal request");
   source.IsMine=true;target.IsMine=false;RPCController.CurrentRPC=null;
   target.Damage(6,DamageType.Melee,0,0,1,null,0,0);byte[] noActor=Networking.Networking.Bytes;
   source.IsMine=false;target.IsMine=true;RPCController.CurrentRPC=new RPCObject();RPCController.CurrentRPC.messageInfo.Sender=source.Owner;
   ReceiveWorldProjectileHitRPC(123,noActor);Check(target.DirectHit.damageSender==null,"context-sourced damage preserves an original null actor");
   source.IsMine=true;target.IsMine=false;RPCController.CurrentRPC=null;
   var gibMethod=typeof(Unit).GetMethod("Gib",BindingFlags.Instance|BindingFlags.NonPublic);
   gibMethod.Invoke(target,new object[]{DamageType.Explosion,23f,24f});byte[] protectedGib=Networking.Networking.Bytes;
   source.IsMine=false;target.IsMine=true;RPCController.CurrentRPC=new RPCObject();RPCController.CurrentRPC.messageInfo.Sender=source.Owner;
   ReceiveWorldProjectileHitRPC(123,protectedGib);Check(target.GibCalls==gibs+2,"direct protected Gib retains its virtual dispatch path");
   RPCController.CurrentRPC=null;_worldStatusSource=null;before=Networking.Networking.Calls;
   var unknown=new Unit { IsHero=true,IsMine=true,Nid=NID.NoID };target.IsMine=false;
   target.Damage(1,DamageType.Melee,0,0,1,unknown,0,0);
   Check(Networking.Networking.Calls==before,"an unregistered source keeps the existing replica boundary");
   _worldStatusSource=source;source.IsMine=true;
   target.Damage(1,DamageType.Melee,0,0,1,new MonoBehaviour(),0,0);
   Check(Networking.Networking.Calls==before,"an unregistered explicit actor is not silently replaced with a null sender");_worldStatusSource=null;
   target.IsMine=true;RPCController.CurrentRPC=new RPCObject();before=target.DamageCalls;
   target.Damage(1,DamageType.Melee,0,0,1,source,0,0);
   Check(target.DamageCalls==before+1,"native RPC execution retains the existing owner gate without a new request");
   RPCController.CurrentRPC=null;_worldStatusSource=source;source.IsMine=true;
   harmony.Patch(typeof(Doodad).GetMethod("DamageOptional"),new HarmonyMethod(typeof(HarmonyDiagnostics).GetMethod("WorldDirectOptionalMutationPrefix",BindingFlags.NonPublic|BindingFlags.Static)),null,null,null);
   var optional=new Doodad { Owner=PID.TargetServer,Nid=new NID(new SID(20),PID.TargetServer,513,3) };Registry.Items[optional.Nid]=optional;
   bool show=true;before=Networking.Networking.Calls;
   Check(!optional.DamageOptional(new DamageObject(7,DamageType.Melee,0,0,0,0,source),ref show) && show && Networking.Networking.Calls==before+1,"typed optional prefix preserves the caller ref and sends one request");
   byte[] optionalPacket=Networking.Networking.Bytes;optional.IsMine=true;RPCController.CurrentRPC=new RPCObject();RPCController.CurrentRPC.messageInfo.Sender=source.Owner;
   ReceiveWorldProjectileHitRPC(123,optionalPacket);Check(optional.Hits==1,"typed optional entry retains owner execution without recursion");
   foreach(var m in typeof(AreaFactory).GetMethods(BindingFlags.Public|BindingFlags.Static)) {
    string prefix=m.Name=="DamageDoodads"?"WorldAreaDamageSenderPrefix":m.Name=="BurnUnitsAround_Local"?"WorldAreaDamageFiredPrefix":m.Name=="DamageProjectile"?"WorldAreaDamageProjectilePrefix":"WorldAreaDamagePrefix";
    harmony.Patch(m,new HarmonyMethod(typeof(HarmonyDiagnostics).GetMethod(prefix,BindingFlags.NonPublic|BindingFlags.Static)),null,null,new HarmonyMethod(typeof(HarmonyDiagnostics).GetMethod("WorldAreaDamageFinalizer",BindingFlags.NonPublic|BindingFlags.Static)));
   }
   HeroController.players[1]=new Player { character=new TestVanDammeAnim() };_worldDirectSource=null;
   AreaFactory.DamageDoodads(source,1,out show);
   Check(show && ObservedArea==source && _worldDirectSource==null,"named sender prefix retains out bool and restores the context");
   AreaFactory.BurnUnitsAround_Local(source,1);Check(ObservedArea==source,"named firedBy prefix keeps explicit actor ahead of playerNum");
   AreaFactory.HitProjectiles(1);Check(ObservedArea==HeroController.players[1].character && _worldDirectSource==null,"player-only area prefix sets and restores the source");
   _worldDirectSource=source;AreaFactory.DamageProjectile(1);
   Check(ObservedArea==source && _worldDirectSource==source,"nested projectile area keeps the existing source ahead of newPlayerNum");_worldDirectSource=null;
   var mine=new Mine { Owner=PID.TargetServer,Nid=new NID(new SID(20),PID.TargetServer,514,3) };Registry.Items[mine.Nid]=mine;
   var receivingBlock=new Block { IsMine=true,Owner=PID.TargetServer,Nid=new NID(new SID(20),PID.TargetServer,515,3) };
   _worldReceivingHit=1;_worldReceivingTarget=receivingBlock;_worldDamageSource=source;_worldDirectSource=source;
   RPCController.CurrentRPC=new RPCObject();RPCController.CurrentRPC.messageInfo.Sender=source.Owner;
   Check(WorldDirectActor(mine)==mine && GetWorldHitSource(mine)==null,"native mine explosion keeps its explicit source instead of inheriting a received hit or projectile");
   target.IsMine=false;before=Networking.Networking.Calls;int mineHits=target.DamageCalls;
   target.Damage(20,DamageType.Explosion,0,0,1,mine,0,0);
   Check(Networking.Networking.Calls==before && target.DamageCalls==mineHits,"native mine collateral does not send a duplicate hit before the replica collapse explosion");
   Check(WorldProjectileHitPrefix(mine,target,20,DamageType.Explosion,0,0,1,false,0,0,typeof(HarmonyDiagnostics).GetMethod("KnockAndDamageUnit")) && Networking.Networking.Calls==before,"native mine public unit hit keeps per-peer owner settlement");
   Check(WorldProjectileBlockHitPrefix(mine,receivingBlock.gameObject,12,DamageType.Explosion,0,0,0,0) && Networking.Networking.Calls==before,"native mine ground hit retains the native terrain path");
   target.IsMine=true;target.Damage(20,DamageType.Explosion,0,0,1,mine,0,0);
   Check(target.DamageCalls==mineHits+1 && target.DirectHit.damageSender==mine,"native mine still damages its local owned target once");
   _worldApplying=1;target.Damage(20,DamageType.Explosion,0,0,1,mine,0,0);_worldApplying=0;
   Check(target.DamageCalls==mineHits+1,"mine explosion during snapshot application does not replay damage");
   target.IsMine=false;target.Damage(3,DamageType.Melee,0,0,1,source,0,0);
   Check(Networking.Networking.Calls==before+1 && _worldReceivingTarget==receivingBlock && _worldDamageSource==source,"other nested owner hits remain routed with their existing scopes");
   _worldReceivingHit=0;_worldReceivingTarget=null;_worldDamageSource=null;_worldDirectSource=null;
   harmony.UnpatchAll(harmony.Id);_worldStatusSource=null;RPCController.CurrentRPC=null;
   RunMineCountdownChecks();
   RunMinePrimeChecks();
  }
  private static void WorldBlockRpcPrefix(object targetObject,MethodInfo methodInfo,ref bool immediate) {
   object[] parameters={0f,0f,true};WorldBlockRpcPrefix(ref targetObject,ref methodInfo,ref parameters,ref immediate);
  }
  private static void RunMineCountdownChecks() {
   var h=new Harmony("cmm.mine-countdown.regression");
   h.Patch(typeof(Mine).GetMethod("Explode"),new HarmonyMethod(typeof(HarmonyDiagnostics).GetMethod("WorldMineExplosionPrefix",BindingFlags.Static|BindingFlags.NonPublic)),null,null,new HarmonyMethod(typeof(HarmonyDiagnostics).GetMethod("WorldMineExplosionFinalizer",BindingFlags.Static|BindingFlags.NonPublic)));
   h.Patch(typeof(RPCController).GetMethod("CreateRPC"),new HarmonyMethod(typeof(HarmonyDiagnostics).GetMethod("WorldBlockRpcPrefix",BindingFlags.Static|BindingFlags.NonPublic,null,new[]{typeof(object).MakeByRefType(),typeof(MethodInfo).MakeByRefType(),typeof(object[]).MakeByRefType(),typeof(bool).MakeByRefType()},null)),null,null,null);
   var mine=new Mine();var neighbor=new Mine();var block=new Block{IsMine=true,Owner=PID.TargetServer,Nid=new NID(Connect.SessionID,PID.TargetServer,520,0),mine=mine};
   var other=new Block{IsMine=true,Owner=PID.TargetServer,Nid=new NID(Connect.SessionID,PID.TargetServer,521,0),mine=neighbor};
   var collapse=typeof(Block).GetMethod("ActuallyCollapse");CreatedRPC self=null,adjacent=null;
   neighbor.ExplosionAction=delegate{self=RPCController.CreateRPC(block,collapse,new object[]{12f,34f,false},false);adjacent=RPCController.CreateRPC(other,collapse,new object[]{0f,0f,true},false);};
   mine.ExplosionAction=delegate{neighbor.Explode(5);};mine.Explode(-10);
   Check(self.Target==null&&self.Method.Name=="ReceiveWorldMineCollapseRPC"&&self.Immediate&&self.Parameters.Length==5&&(long)self.Parameters[0]==123&&(NID)self.Parameters[1]==block.Nid&&(float)self.Parameters[2]==12&&(float)self.Parameters[3]==34&&!(bool)self.Parameters[4],"native countdown self-collapse is encoded with epoch/block and exact native arguments through actual Harmony CreateRPC");
   Check(adjacent.Target==other&&adjacent.Method==collapse&&_worldCountdownMine==null,"neighbor immediate explosion retains native collapse while nested countdown scope restores");
   Registry.Items[block.Nid]=block;block.IsMine=false;block.mine=new Mine{detonated=true};var pending=block.mine;
   RPCController.CurrentRPC=new RPCObject();RPCController.CurrentRPC.messageInfo.Sender=PID.ServerID;
   self.Method.Invoke(null,self.Parameters);self.Method.Invoke(null,self.Parameters);
   Check(block.Collapses==1&&block.mine==pending&&pending.detonated&&!pending.exploded&&pending.ImmediateExplosions==0,"owner collapse delivery destroys replica terrain once without preempting its existing mine countdown");
   block.destroyed=false;block.mine=new Mine();pending=block.mine;self.Method.Invoke(null,self.Parameters);
   Check(pending.Starts==1&&!pending.exploded&&block.mine==pending,"collapse arriving before delayed native priming starts one countdown without immediate explosion");
   var fresh=new Mine();other.mine=fresh;other.ActuallyCollapse(0,0,true);
   Check(fresh.Starts==1&&fresh.ImmediateExplosions==1,"fresh mine destruction retains native immediate explosion");
   block.destroyed=false;pending=block.mine=new Mine();int count=block.Collapses;
   ReceiveWorldMineCollapseRPC(124,block.Nid,0,0,true);RPCController.CurrentRPC.messageInfo.Sender=new PID(9);ReceiveWorldMineCollapseRPC(123,block.Nid,0,0,true);
   Check(block.Collapses==count&&pending.Starts==0,"expired epoch and non-owner mine-collapse replay do not prime or destroy terrain");
   RPCController.CurrentRPC.messageInfo.Sender=PID.ServerID;block.FailCollapse=true;
   try{ReceiveWorldMineCollapseRPC(123,block.Nid,0,0,true);}catch(InvalidOperationException){}
   Check(block.mine==pending,"throwing native collapse restores the attached mine reference");block.FailCollapse=false;
   Session=false;mine.exploded=false;mine.ExplosionAction=delegate{self=RPCController.CreateRPC(block,collapse,new object[]{0f,0f,true},false);};mine.Explode(-10);Session=true;
   Check(self.Target==block&&self.Method==collapse&&!self.Immediate,"disabled world session retains native mine collapse RPC");
   mine.exploded=false;mine.ExplosionAction=delegate{throw new InvalidOperationException();};try{mine.Explode(-10);}catch(InvalidOperationException){}
   Check(_worldCountdownMine==null,"throwing native explosion restores countdown context");
   h.UnpatchAll(h.Id);RPCController.CurrentRPC=null;
  }
  private static void RunMinePrimeChecks() {
   var h=new Harmony("cmm.mine-prime.regression");
   h.Patch(typeof(Block).GetMethod("CheckForMine"),new HarmonyMethod(typeof(HarmonyDiagnostics).GetMethod("WorldCheckForMinePrefix",BindingFlags.Static|BindingFlags.NonPublic)),null,null,null);
   var id=new NID(Connect.SessionID,PID.TargetServer,530,0);
   var mine=new Mine{Nid=new NID(Connect.SessionID,PID.TargetServer,900,3)};
   var block=new Block{IsMine=false,Nid=id,mine=mine};int calls=Networking.Networking.Calls;
   block.CheckForMine();var sent=Networking.Networking.Last;
   Check(block.NativeMineChecks==0&&Networking.Networking.Calls==calls+1&&Networking.Networking.Target==PID.TargetAll&&!sent.Immediate&&sent.Target==null&&sent.Method.Name=="ReceiveWorldMinePrimeRPC"&&(long)sent.Parameters[0]==123&&(NID)sent.Parameters[1]==id,"actual patched CheckForMine primes by block identity with native queued TargetAll semantics from either owner");
   var replicaMine=new Mine{Nid=new NID(Connect.SessionID,PID.TargetServer,903,3)};
   var replica=new Block{Nid=id,mine=replicaMine};var unrelated=new Mine{Nid=mine.Nid};
   Registry.Items[id]=replica;Registry.Items[mine.Nid]=unrelated;
   RPCController.CurrentRPC=new RPCObject();RPCController.CurrentRPC.messageInfo.Sender=new PID(9);
   sent.Method.Invoke(null,sent.Parameters);
   Check(replicaMine.Starts==1&&!replicaMine.exploded&&unrelated.Starts==0,"priming resolves the attached replica mine despite mismatched mine keys and leaves the misleading same-key mine untouched");
   sent.Method.Invoke(null,sent.Parameters);replica.CheckForMine();
   Check(replicaMine.Starts==1&&replicaMine.ImmediateExplosions==0&&Networking.Networking.Calls==calls+1,"repeated delivery and stepping keep one native delayed start without an immediate explosion");
   replicaMine.exploded=true;sent.Method.Invoke(null,sent.Parameters);replica.CheckForMine();
   Check(replicaMine.Starts==1&&Networking.Networking.Calls==calls+1,"already exploded mine remains idempotent for repeated stepping and delivery");
   replica.mine=new Mine();ReceiveWorldMinePrimeRPC(124,id);ReceiveWorldMinePrimeRPC(123,new NID(new SID(21),PID.TargetServer,530,0));
   RPCController.CurrentRPC=null;ReceiveWorldMinePrimeRPC(123,id);
   Check(replica.mine.Starts==0,"expired epoch, foreign scene and calls outside RPC execution cannot prime a mine");
   RPCController.CurrentRPC=new RPCObject();ReceiveWorldMinePrimeRPC(123,new NID(Connect.SessionID,PID.TargetServer,999,0));replica.mine=null;ReceiveWorldMinePrimeRPC(123,id);
   Check(unrelated.Starts==0,"missing block or attached mine cannot redirect priming to another registry object");
   Session=false;block.CheckForMine();ReceiveWorldMinePrimeRPC(123,id);Session=true;
   _worldEpoch=0;block.CheckForMine();_worldEpoch=123;
   Check(block.NativeMineChecks==2&&Networking.Networking.Calls==calls+1,"disabled session and absent epoch retain the native CheckForMine entry");
   block.Nid=NID.NoID;block.CheckForMine();
   Check(block.NativeMineChecks==3&&Networking.Networking.Calls==calls+1,"unregistered blocks retain the native entry without inventing an identity");
   h.UnpatchAll(h.Id);RPCController.CurrentRPC=null;
  }
  private static BroforceObject ObservedArea;
  public static void ObserveArea() { ObservedArea=_worldDirectSource; }
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
    Copy-Item -LiteralPath $harmonyPath -Destination (Join-Path $temporaryPath '0Harmony.dll')
    $compiler = Join-Path $env:windir 'Microsoft.NET\Framework64\v3.5\csc.exe'
    & $compiler /nologo /nowarn:0649 /target:exe "/reference:$harmonyPath" "/out:$outputPath" $fixturePath (Join-Path $projectRoot 'src\HarmonyDiagnostics.WorldPickups.cs') (Join-Path $projectRoot 'src\HarmonyDiagnostics.WorldSpecialHits.cs') (Join-Path $projectRoot 'src\HarmonyDiagnostics.WorldMutations.cs')
    if ($LASTEXITCODE -ne 0) { throw 'World pickup fixture compilation failed.' }
    & $outputPath
    if ($LASTEXITCODE -ne 0) { throw 'World pickup regression failed.' }
}
finally {
    Remove-Item -LiteralPath $temporaryPath -Recurse -Force
}
