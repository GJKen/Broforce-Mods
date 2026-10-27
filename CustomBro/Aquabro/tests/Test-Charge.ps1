param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$source = [System.IO.File]::ReadAllText((Join-Path $projectRoot 'src\Aquabro.cs'), [System.Text.Encoding]::UTF8)
function Read-Method([string]$signature) {
    $start = $source.IndexOf($signature)
    if ($start -lt 0) { throw "未找到生产方法：$signature" }
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
$methods = @(
    Read-Method 'private void SetupTridentPrefabs()'
    Read-Method 'protected override void RunFiring()'
    Read-Method 'private void FireTrident()'
    Read-Method 'private void SpawnTridentLocally(bool charged, float x, float y, float speedX, float speedY,'
    Read-Method 'private void ReceiveTrident(bool charged, float x, float y, float speedX, float speedY,'
    Read-Method 'private void ReceiveTridentImpact('
    Read-Method 'private static void ApplyTridentVisual('
    Read-Method 'private static int GetAnimationFrame('
    Read-Method 'private static bool AllowOriginalSpearTrail('
    Read-Method 'private static int GetChargeAnimationFrame('
    Read-Method 'private static int GetRunningChargeAnimationFrame('
) -join "`r`n"
# Compile the production ownership/RPC gate; combat settlement has its own zone tests.
$impact = Read-Method 'private static void ResolveWaterImpact('
$methods += "`r`n" + $impact.Substring(0, $impact.IndexOf('float radius')) + "DamageCalls++;`r`n}"
$tridentFields = [regex]::Match($source, 'private int tridentSequence, lastReceivedTrident;').Value + "`r`n" +
    [regex]::Match($source, 'private readonly HashSet<int> pendingTridentImpacts = new HashSet<int>\(\);').Value
$constants = [regex]::Matches($source, 'private const float (FullChargeDuration|ThrowCooldown|ThrowAnimationDuration|MeleeSpeedMultiplier|MeleeHitTime|MeleeDuration|RunningChargeLoopDuration|RunningChargeFrameDuration) = [^;]+;')
$constantText = ($constants | ForEach-Object { $_.Value }) -join "`r`n"
$constantText += "`r`n" + [regex]::Match($source, 'private static readonly float\[\] ChargedFrameDurations = \{[^}]+\};').Value

# 直接编译生产方法。外部对象仅记录发射调用，验证输入时序、原版 prefab 和参数。
$harness = @'
using System;
using System.Collections.Generic;
using Networking;
namespace UnityEngine {
 public static class Random { public static int Range(int min, int max) { return 4242; } }
 static class Object {
  public static T Instantiate<T>(T prefab, Vector3 position, Quaternion rotation) where T:Projectile {
   Projectile p= prefab is PredabroSpear ? (Projectile)new PredabroSpear() : new Projectile();
   ProjectileController.Count++; ProjectileController.InstantiateCalls++; ProjectileController.LastPrefab=prefab;
   ProjectileController.Last=p; ProjectileController.Instances.Add(p); return (T)p;
  }
  public static object[] FindObjectsOfType(Type type) {
   List<object> result=new List<object>(); foreach(Projectile p in ProjectileController.Instances)
    if(!p.gameObject.Destroyed && type.IsInstanceOfType(p)) result.Add(p); return result.ToArray();
  }
  public static void Destroy(object target) { ((GameObject)target).Destroyed=true; }
 }
}
class Vector3 { public Vector3(float x,float y,float z) { } }
class Quaternion { public static readonly Quaternion identity=new Quaternion(); }
class GameObject {
 public bool Destroyed; object component;
 public T AddComponent<T>() where T:new() { T value=new T(); component=value; return value; }
 public T GetComponent<T>() where T:class { return component as T; }
}
class AquabroWaterImpact { }
class AquabroWaterEffect {
 public static int Calls; public static float X,Y; public static bool Charged;
 public static AquabroWaterEffect Play(string path,string texture,float x,float y,int size,int frames,bool charged) {
  Calls++; X=x; Y=y; Charged=charged; return new AquabroWaterEffect();
 }
 public void BeginBlast() { AquabroProbe.DamageCalls++; }
}
namespace Networking {
 public enum PID { TargetOthers }
 [AttributeUsage(AttributeTargets.Method)] public sealed class AllowedRPC : Attribute { }
 public delegate void RpcSignature<T1, T2, T3, T4, T5, T6, T7>(T1 a, T2 b, T3 c, T4 d, T5 e, T6 f, T7 g);
 public delegate void RpcSignature<T1,T2,T3,T4>(T1 a,T2 b,T3 c,T4 d);
 public static class Connect { public static bool IsOffline; }
 public static class Networking {
  public static int Calls;
  public static PID Target;
  public static object[] Arguments;
  public static List<string> Events=new List<string>();
  public static void RPC<T1, T2, T3, T4, T5, T6, T7>(PID target, bool immediate, bool ignoreSessionID,
      RpcSignature<T1, T2, T3, T4, T5, T6, T7> method, T1 a, T2 b, T3 c, T4 d, T5 e, T6 f, T7 g) {
   Calls++; Target = target; Arguments = new object[] { a, b, c, d, e, f, g }; Events.Add(method.Method.Name);
  }
  public static void RPC<T1,T2,T3,T4>(PID target,bool immediate,bool ignoreSessionID,RpcSignature<T1,T2,T3,T4> method,T1 a,T2 b,T3 c,T4 d) {
   Calls++; Target=target; Arguments=new object[] {a,b,c,d}; Events.Add(method.Method.Name);
  }
 }
}
class Mathf {
 public static float Abs(float value) { return Math.Abs(value); }
 public static float Max(float a, float b) { return Math.Max(a, b); }
 public static int FloorToInt(float x) { return (int)Math.Floor(x); }
 public static int Clamp(int x, int a, int b) { return Math.Max(a, Math.Min(b, x)); }
}
enum HeroType { Predabro }
class MonoBehaviour { }
class Mook {
    public bool Throwable = true;
    public bool CanBeThrown() { return Throwable; }
}
class Renderer { public object gameObject; public bool enabled = true; }
class Projectile : MonoBehaviour {
    public GameObject gameObject = new GameObject();
    public Renderer Child = new Renderer { gameObject = new object() };
    public SpriteSM Sprite = new SpriteSM();
    public float X, Y, xI, yI;
    public int playerNum, seed;
    public MonoBehaviour firedBy;
    public static Action<Projectile> FireHook;
    public T GetComponent<T>() where T : class { return typeof(T)==typeof(SpriteSM) ? Sprite as T : gameObject.GetComponent<T>(); }
    public T[] GetComponentsInChildren<T>(bool inactive) where T : class {
        return new T[] { new Renderer { gameObject = gameObject } as T, Child as T };
    }
    public virtual void Fire(float x, float y, float speedX, float speedY, float z, int player, MonoBehaviour owner) {
        X = x; Y = y; xI = speedX; yI = speedY; playerNum = player; firedBy = owner;
        ProjectileController.LastOwner=owner; ProjectileController.X=x; ProjectileController.Y=y; ProjectileController.Speed=speedX;
        if(FireHook!=null) FireHook(this);
        AquabroProbe.Visual(this, owner);
    }
    public void SetSeed(int value) { seed = value; }
}
class BrocheteMachete : Projectile { }
class PredabroSpear : BrocheteMachete { }
class SpriteSM {
    public class Renderer { public object material; }
    public Renderer meshRender = new Renderer();
    public float Width, Height, PixelWidth, PixelHeight, CellX, CellY;
    public int TextureRefreshes;
    public void SetPixelDimensions(int x, int y) { PixelWidth = x; PixelHeight = y; }
    public void SetSize(float x, float y) { Width = x; Height = y; }
    public void SetLowerLeftPixel(float x, float y) { CellX = x; CellY = y; }
    public void RecalcTexture() { TextureRefreshes++; }
    public void CalcUVs() { }
    public void UpdateUVs() { }
}
class CustomInfo { public string path = "Aquabro"; }
class ResourcesController {
    public static object GetMaterial(string path, string filename) { return path + "/" + filename; }
}
class AudioClip { }
class SoundHolder { public AudioClip[] attackSounds = new AudioClip[1]; }
class Predabro {
    public Projectile projectile = new PredabroSpear(), chargedSpearProectilePrefab = new PredabroSpear();
    public SoundHolder soundHolder = new SoundHolder();
}
class HeroController {
    public static Predabro Prefab = new Predabro();
    public static object GetHeroPrefab(HeroType type) { return Prefab; }
}
class GestureElement { public enum Gestures { None, Other } }
class Transform {
    public Scale localScale = new Scale();
    public class Scale { public float x = 1f; }
}
class ProjectileController {
    public static int Count, LocalSpawnCalls, InstantiateCalls;
    public static List<Projectile> Instances=new List<Projectile>();
    public static Projectile Last;
    public static Projectile LastPrefab;
    public static MonoBehaviour LastOwner;
    public static bool LastSynced;
    public static float X, Y, Speed;
    public static Projectile SpawnProjectileLocally(Projectile p, MonoBehaviour owner, float x, float y,
        float speed, float verticalSpeed, bool synced, int player, bool target, bool immediate, float z) {
        Count++; LocalSpawnCalls++; LastPrefab = p; LastOwner = owner; LastSynced = synced;
        Last = p is PredabroSpear ? (Projectile)new PredabroSpear() : new Projectile();
        X = x; Y = y; Speed = speed;
        Last.Fire(x, y, speed, verticalSpeed, z, player, owner);
        return Last;
    }
}
class Map { public static void DisturbWildLife(float x, float y, float range, int player) { } }
class SimulationBase : MonoBehaviour {
    public float t, fireDelay, avatarTime, avatarFireDelay;
    public int BaseCalls;
    protected virtual void RunFiring() { BaseCalls++; fireDelay -= t; }
}
class AquabroProbe : SimulationBase {
    __CONSTANTS__
    __TRIDENT_FIELDS__
    static float[] ChargeLoopDurations = { .034f, .033f, .033f, .033f, .034f, .033f, .033f, .033f, .034f, .033f, .033f, .033f, .034f, .033f, .033f, .033f };
    static Projectile normalTridentPrefab, chargedTridentPrefab;
    static SoundHolder tridentSoundHolder;
    public float chargeTime, throwDelay, throwCooldown, meleeElapsed, counter, X = 100f, Y = 50f;
    public int health = 1, frame, playerNum;
    public bool fire, wasFire, doingMelee, meleeAttackEmitted, ducking, highFive, IsMine = true;
    public Mook nearbyMook, ThrownMook;
    public int KnifeHits, ThrowCalls;
    public static int DamageCalls;
    public bool isOnHelicopter, throwingHeldObject, hasBeenCoverInAcid, Incapacitated;
    public bool usingSpecial, usingPockettedSpecial, usingSpecial2, usingSpecial3, usingSpecial4;
    public bool PoseAllowed = true, GunVisible;
    public int AtlasWrites;
    public object impaledByTransform, strungUpBy, inseminatorUnit;
    public GestureElement.Gestures currentGesture;
    public Transform transform = new Transform();
    public CustomInfo Info = new CustomInfo();
    public AquabroProbe() { SetupTridentPrefabs(); }
    public void DeliverTrident(bool charged, float x, float y, float speedX, float speedY, int projectilePlayerNum, int seed) {
        ReceiveTrident(charged, x, y, speedX, speedY, projectilePlayerNum, seed);
    }
    public void DeliverImpact(int seed,bool charged,float x,float y) { ReceiveTridentImpact(seed,charged,x,y); }
    public void Emit(bool charged) { chargeTime=charged?FullChargeDuration:0f; FireTrident(); }
    public static void Contact(PredabroSpear shot,AquabroProbe owner) { ResolveWaterImpact(shot,owner,120.05f,115f,null); }
    bool IsIncapacitated() { return Incapacitated; }
    bool CanShowTridentPose() { return PoseAllowed; }
    void ActivateGun() { GunVisible = true; }
    void DeactivateGun() { GunVisible = false; }
    void SetGunPosition(float x, float y) { }
    void SetGunAtlasCell(int cell) { AtlasWrites++; }
    void SetGunSprite(int x, int y) { }
    void PerformKnifeMeleeAttack(bool a, bool b) { KnifeHits++; }
    void CancelMelee() { doingMelee = false; }
    void ThrowBackMook(Mook mook) { ThrownMook = mook; ThrowCalls++; throwingHeldObject = true; }
    void ChangeFrame() { }
    void PlayAttackSound() { }
    public void Tick(bool held, float delta) { wasFire = fire; fire = held; t = delta; RunFiring(); }
    public static void Visual(Projectile shot, MonoBehaviour owner) { ApplyTridentVisual(shot, owner); }
    public static int WaterFrame(float elapsed, bool charged) { return GetAnimationFrame(elapsed, charged); }
    public static bool Trail(BrocheteMachete shot) { return AllowOriginalSpearTrail(shot); }
    public static int ChargePose(float elapsed, bool running) { return running ? GetRunningChargeAnimationFrame(elapsed) : GetChargeAnimationFrame(elapsed); }
    __METHODS__
}
class ChargeTests {
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static bool Near(float a, float b) { return Math.Abs(a - b) < 0.00001f; }
    static void Shot(float held, bool charged, bool left, bool ducking) {
        ProjectileController.Count = 0;
        ProjectileController.LocalSpawnCalls = 0;
        ProjectileController.InstantiateCalls = 0;
        Networking.Networking.Calls = 0;
        var p = new AquabroProbe { ducking = ducking };
        p.transform.localScale.x = left ? -1f : 1f;
        p.Tick(true, held);
        Check(ProjectileController.Count == 0, "长按时提前发射");
        p.Tick(false, 0.001f);
        Check(ProjectileController.Count == 1, "松键没有发射一次");
        Check(object.ReferenceEquals(ProjectileController.LastPrefab, charged ? HeroController.Prefab.chargedSpearProectilePrefab : HeroController.Prefab.projectile), "未使用对应原版 prefab");
        float direction = left ? -1f : 1f;
        Check(Near(ProjectileController.Speed, direction * (charged ? 664f : 400f)), "弹速不符合 Predabro");
        Check(Near(ProjectileController.X, 100f + direction * 14f) && Near(ProjectileController.Y, ducking ? 58f : 61f), "发射位置不符合 Predabro");
        Check(ProjectileController.InstantiateCalls == 1 && ProjectileController.LocalSpawnCalls == 0, "拥有端没有手动生成长矛并在Fire前初始化");
        Check(object.ReferenceEquals(ProjectileController.LastOwner, p) && object.ReferenceEquals(ProjectileController.Last.firedBy, p) &&
              ProjectileController.Last.playerNum == p.playerNum && Near(ProjectileController.Last.yI, 0f), "拥有端长矛没有初始化发射者、玩家编号或竖直速度");
        Check(ProjectileController.Last.seed == 1 && ProjectileController.Last.Sprite.meshRender.material.ToString() == "Aquabro/trident.png", "拥有端没有递增种子或应用自定义贴图");
        Check(Networking.Networking.Calls == 1 && Networking.Networking.Target == PID.TargetOthers, "拥有端没有且仅广播一次长矛");
        Check((bool)Networking.Networking.Arguments[0] == charged && Near((float)Networking.Networking.Arguments[1], ProjectileController.X) &&
              Near((float)Networking.Networking.Arguments[2], ProjectileController.Y) && Near((float)Networking.Networking.Arguments[3], ProjectileController.Speed) &&
              Near((float)Networking.Networking.Arguments[4], 0f) && (int)Networking.Networking.Arguments[5] == p.playerNum &&
              (int)Networking.Networking.Arguments[6] == 1, "广播没有携带完整的投掷数据与共享序号");
        Check(Near(p.chargeTime, 0f) && Near(p.fireDelay, 0.4f), "发射后未重置");
        p.Tick(false, 0.02f);
        Check(ProjectileController.Count == 1, "松键重复发射");
    }
    static void Interrupted(int kind) {
        ProjectileController.Count = 0;
        var p = new AquabroProbe();
        p.Tick(true, 1.4f);
        if (kind == 0) p.health = 0;
        if (kind == 1) p.Incapacitated = true;
        if (kind == 2) p.doingMelee = true;
        if (kind == 3) p.usingSpecial = true;
        p.Tick(true, 0.01f);
        p.health = 1; p.Incapacitated = p.doingMelee = p.usingSpecial = false;
        p.Tick(false, 0.01f);
        Check(Near(p.chargeTime, 0f) && ProjectileController.Count == 0, "被打断后仍发射旧蓄力");
    }
    static void MeleeThrow() {
        // Feed the same nearby-mook/held-key state as native PressHighFiveMelee.
        foreach (float delta in new float[] { 1f / 15f, 1f / 30f, 1f / 60f, 1f / 120f, .3f }) {
            foreach (bool hold in new bool[] { false, true }) {
                foreach (bool throwable in new bool[] { false, true }) {
                    Mook enemy = new Mook { Throwable = throwable };
                    var p = new AquabroProbe { doingMelee = true, highFive = hold, nearbyMook = enemy };
                    for (int i = 0; i < 100 && p.doingMelee; i++) p.Tick(false, delta);
                    bool shouldThrow = hold && throwable;
                    Check(p.ThrowCalls == (shouldThrow ? 1 : 0), "长按近战丢兵次数错误");
                    Check(p.KnifeHits == (shouldThrow ? 0 : 1), "丢兵同时刺伤目标，或普通前刺丢失");
                    if (shouldThrow) {
                        Check(object.ReferenceEquals(p.ThrownMook, enemy) && p.nearbyMook == null && p.throwingHeldObject,
                            "没有转入原版丢兵流程或清理附近目标");
                        p.Tick(false, delta);
                        Check(p.ThrowCalls == 1 && p.KnifeHits == 0, "丢兵后重复出手");
                    }
                }
            }
            var empty = new AquabroProbe { doingMelee = true, highFive = true };
            for (int i = 0; i < 100 && empty.doingMelee; i++) empty.Tick(false, delta);
            Check(empty.KnifeHits == 1 && empty.ThrowCalls == 0, "无可丢目标时未正常前刺");
        }
        var paused = new AquabroProbe { doingMelee = true, highFive = true, nearbyMook = new Mook() };
        paused.Tick(false, 0f);
        Check(paused.ThrowCalls == 0 && paused.KnifeHits == 0, "暂停时仍丢兵或前刺");
    }
    static void ImpactAuthority() {
        Networking.Networking.Calls=0; Networking.Networking.Events.Clear(); AquabroProbe.DamageCalls=0; AquabroWaterEffect.Calls=0;
        var first=new AquabroProbe(); var second=new AquabroProbe();
        Projectile.FireHook=delegate(Projectile shot) {
            Check(shot.seed==1,"贴墙Fire尚未设置投掷序号");
            Check(Networking.Networking.Calls==1 && Networking.Networking.Events[0]=="ReceiveTrident","贴墙Fire早于生成广播");
            AquabroProbe.Contact((PredabroSpear)shot,first); AquabroProbe.Contact((PredabroSpear)shot,first);
        };
        first.Emit(true); Projectile.FireHook=null;
        Check(Networking.Networking.Events.Count==2 && Networking.Networking.Events[1]=="ReceiveTridentImpact" &&
              AquabroProbe.DamageCalls==1,"贴墙重复回调没有只发送一次拥有端命中结果");
        Check((int)Networking.Networking.Arguments[0]==1 && (bool)Networking.Networking.Arguments[1] &&
              Near((float)Networking.Networking.Arguments[2],120.05f),"命中广播没有携带拥有端的序号和坐标");
        first.Emit(false); Check(ProjectileController.Last.seed==2,"同一拥有端投掷序号没有递增");
        second.Emit(false); Check(ProjectileController.Last.seed==1,"不同拥有端共享了投掷序号");
        int before=ProjectileController.Count,effects=AquabroWaterEffect.Calls;
        first.DeliverTrident(false,1f,2f,400f,0f,0,99); first.DeliverImpact(1,true,999f,999f);
        Check(ProjectileController.Count==before && AquabroWaterEffect.Calls==effects,"拥有端接收了另一端的生成或命中结果");
        var remoteA=new AquabroProbe { IsMine=false }; var remoteB=new AquabroProbe { IsMine=false };
        Networking.Networking.Calls=0; AquabroProbe.DamageCalls=0;
        remoteA.DeliverTrident(true,300f,115f,-664f,0f,0,1); var expired=(PredabroSpear)ProjectileController.Last;
        before=ProjectileController.Count; remoteA.DeliverTrident(true,300f,115f,-664f,0f,0,1);
        remoteA.DeliverTrident(false,300f,115f,400f,0f,0,0);
        Check(ProjectileController.Count==before,"重复或旧生成RPC再次创建长矛");
        remoteB.DeliverTrident(false,300f,115f,400f,0f,1,1); var other=ProjectileController.Last;
        AquabroProbe.Contact(expired,remoteA); AquabroProbe.Contact(expired,remoteA);
        Check(expired.gameObject.Destroyed && AquabroWaterEffect.Calls==effects && AquabroProbe.DamageCalls==0 &&
              Networking.Networking.Calls==0,"远端自行碰撞或到期产生水花、伤害或命中广播");
        remoteA.DeliverImpact(1,true,120.05f,115f);
        Check(AquabroWaterEffect.Calls==effects+1 && Near(AquabroWaterEffect.X,120.05f) && Near(AquabroWaterEffect.Y,115f) &&
              AquabroWaterEffect.Charged && !other.gameObject.Destroyed,"本地先销毁后丢失权威结果，或误毁另一拥有者同序号长矛");
        remoteA.DeliverImpact(1,true,999f,999f); remoteA.DeliverTrident(true,300f,115f,-664f,0f,0,1);
        Check(AquabroWaterEffect.Calls==effects+1 && ProjectileController.Count==before+1,"已消费结果被重复播放或整批重发重新生成长矛");
        remoteB.DeliverImpact(1,false,420f,115f);
        Check(other.gameObject.Destroyed && AquabroWaterEffect.Calls==effects+2,"尚存视觉矛未按拥有者和序号清理");
        var unordered=new AquabroProbe { IsMine=false };
        unordered.DeliverTrident(false,300f,115f,400f,0f,0,10); var older=ProjectileController.Last;
        unordered.DeliverTrident(true,300f,115f,-664f,0f,0,11); var newer=ProjectileController.Last;
        unordered.DeliverImpact(11,true,120f,115f);
        Check(newer.gameObject.Destroyed && !older.gameObject.Destroyed,"逆序结果清理了错误长矛");
        unordered.DeliverImpact(10,false,420f,115f);
        Check(older.gameObject.Destroyed && AquabroWaterEffect.Calls==effects+4,"先投后命中的合法旧序号结果被丢弃");
        unordered.DeliverImpact(10,false,0f,0f); unordered.DeliverImpact(11,true,0f,0f); unordered.DeliverImpact(99,true,0f,0f);
        Check(AquabroWaterEffect.Calls==effects+4 && AquabroProbe.DamageCalls==0 && Networking.Networking.Calls==0,
              "重复或未知结果再次播放，或远端结果调用伤害/转发RPC");
    }
    public static int Main() {
        try {
            for (int f = 0; f < 6; f++)
                Check(AquabroProbe.WaterFrame(f * .1f + .05f, true) == f, "水球爆裂末6帧播放顺序错误");
            Check(AquabroProbe.WaterFrame(.601f, true) == -1, "水球爆裂未在600ms结束");
            Check(AquabroProbe.WaterFrame(.01f, false) == 0 && AquabroProbe.WaterFrame(.53f, false) == 11 && AquabroProbe.WaterFrame(.541f, false) == -1, "普通水花时序发生变化");
            foreach (bool running in new bool[] { false, true }) {
                Check(AquabroProbe.ChargePose(0.02f, running) == 1, "起手姿势错误");
                Check(AquabroProbe.ChargePose(0.1f, running) == 2 && AquabroProbe.ChargePose(0.499f, running) == 2, "未满蓄力提前出现水流");
                Check(AquabroProbe.ChargePose(0.5f, running) == 3 && AquabroProbe.ChargePose(0.8f, running) > 3, "满蓄力未播放水流循环");
            }
            Shot(0.02f, false, false, false);
            Shot(0.3f, false, false, false);
            Shot(0.499f, false, true, true);
            Shot(0.5f, true, false, false);
            Shot(2f, true, true, true);
            var p = new AquabroProbe();
            p.Tick(true, 0.1f); p.Tick(false, 0.01f);
            p.Tick(true, 0.199f);
            Check(Near(p.chargeTime, 0f), "投掷间隔内开始蓄力");
            p.Tick(true, 0.002f);
            Check(p.chargeTime > 0f && Near(p.throwDelay, 0f), "200ms 后未开始蓄力，或收招动画仍覆盖蓄力");
            int count = ProjectileController.Count;
            p.Tick(false, 0.01f);
            Check(ProjectileController.Count == count + 1, "连续投掷失败");
            for (int i = 0; i < 4; i++) Interrupted(i);
            MeleeThrow();
            var terrain = new AquabroProbe { PoseAllowed = false };
            terrain.Tick(true, 0.5f);
            Check(!terrain.GunVisible && terrain.AtlasWrites == 0, "地形蓄力仍显示旧武器格");
            count = ProjectileController.Count;
            terrain.Tick(false, 0.01f);
            Check(ProjectileController.Count == count + 1, "移除旧蓄力贴图后地形投掷失效");
            p = new AquabroProbe { IsMine = false };
            count = ProjectileController.Count;
            p.Tick(true, 1.4f); p.Tick(false, 0.01f);
            Check(ProjectileController.Count == count, "非拥有端重复生成长矛");
            var remote = new AquabroProbe { IsMine = false };
            Networking.Networking.Calls = 0;
            int remoteSpawnStart = ProjectileController.InstantiateCalls;
            remote.DeliverTrident(false, 22f, 33f, 400f, 0f, 2, 4242);
            Check(ProjectileController.InstantiateCalls == remoteSpawnStart + 1 && object.ReferenceEquals(ProjectileController.LastPrefab, HeroController.Prefab.projectile), "加入端没有按广播类型本地生成普通长矛");
            Check(Near(ProjectileController.Last.X, 22f) && Near(ProjectileController.Last.Y, 33f) && Near(ProjectileController.Last.xI, 400f) &&
                  ProjectileController.Last.playerNum == 2 && ProjectileController.Last.seed == 4242, "加入端普通长矛没有初始化坐标、速度、玩家编号或种子");
            Check(object.ReferenceEquals(ProjectileController.Last.firedBy, remote) && ProjectileController.Last.Sprite.meshRender.material.ToString() == "Aquabro/trident.png", "加入端没有初始化本地发射者或应用自定义贴图");
            remote.DeliverTrident(true, 24f, 35f, -664f, 0f, 2, 4243);
            Check(ProjectileController.InstantiateCalls == remoteSpawnStart + 2 && object.ReferenceEquals(ProjectileController.LastPrefab, HeroController.Prefab.chargedSpearProectilePrefab) &&
                  Near(ProjectileController.Last.xI, -664f) && Near(ProjectileController.Last.yI, 0f), "加入端没有按广播类型生成蓄力长矛或速度错误");
            Check(object.ReferenceEquals(ProjectileController.Last.firedBy, remote) && ProjectileController.Last.Sprite.meshRender.material.ToString() == "Aquabro/trident.png", "加入端蓄力长矛没有初始化本地发射者或应用自定义贴图");
            remote.Tick(true, 1.4f); remote.Tick(false, 0.01f);
            Check(ProjectileController.InstantiateCalls == remoteSpawnStart + 2 && Networking.Networking.Calls == 0, "加入端自行开火或重复广播了长矛");
            count = ProjectileController.Count;
            p = new AquabroProbe();
            p.Tick(true, 0f); p.Tick(false, 0f);
            Check(ProjectileController.Count == count, "零时间步产生攻击");
            Check(p.BaseCalls == 2, "未保留基类开火逻辑");
            var spear = new PredabroSpear();
            AquabroProbe.Visual(spear, p);
            Check(!spear.Child.enabled, "原版前景武器未隐藏");
            spear.firedBy = p;
            Check(!AquabroProbe.Trail(spear), "海王仍生成原版长矛残影");
            Check((string)spear.Sprite.meshRender.material == "Aquabro/trident.png", "未应用海王武器素材");
            Check(spear.Sprite.Width == 32 && spear.Sprite.Height == 32 && spear.Sprite.PixelWidth == 32 && spear.Sprite.PixelHeight == 32 && spear.Sprite.CellX == 0 && spear.Sprite.CellY == 32 && spear.Sprite.TextureRefreshes == 1, "武器尺寸或采样坐标错误");
            var otherOwnerSpear = new PredabroSpear();
            AquabroProbe.Visual(otherOwnerSpear, new MonoBehaviour());
            Check(otherOwnerSpear.Child.enabled && AquabroProbe.Trail(otherOwnerSpear), "影响了其他角色长矛前景或残影");
            Check(otherOwnerSpear.Sprite.meshRender.material == null, "修改了其他角色的长矛");
            var otherProjectile = new Projectile();
            AquabroProbe.Visual(otherProjectile, p);
            Check(otherProjectile.Sprite.meshRender.material == null, "修改了其他类型的投射物");
            ImpactAuthority();
            Console.WriteLine("PASS: 水花时序、蓄力/连续投掷/打断、双端长矛生成/数据/外观、远端不自行开火、零时间步；近战丢兵/前刺/15-120 FPS；拥有端命中、贴墙顺序、提前销毁、重发去重、双owner序号和逆序结果消费");
            return 0;
        } catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
    }
}
'@
$testSource = Join-Path $env:TEMP ('Aquabro-Charge-' + [Guid]::NewGuid().ToString('N') + '.cs')
$testExe = [System.IO.Path]::ChangeExtension($testSource, '.exe')
try {
    [System.IO.File]::WriteAllText($testSource, $harness.Replace('__CONSTANTS__', $constantText).Replace('__TRIDENT_FIELDS__', $tridentFields).Replace('__METHODS__', $methods).Replace('AquabroProbe', 'Aquabro'), [System.Text.UTF8Encoding]::new($true))
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v3.5\csc.exe'
    & $compiler /nologo /nowarn:0649,0414 /target:exe "/out:$testExe" $testSource
    if ($LASTEXITCODE -ne 0) { throw '蓄力回归测试编译失败' }
    & $testExe
    if ($LASTEXITCODE -ne 0) { throw '蓄力回归测试失败' }
} finally {
    Remove-Item -LiteralPath $testSource,$testExe -Force -ErrorAction SilentlyContinue
}
