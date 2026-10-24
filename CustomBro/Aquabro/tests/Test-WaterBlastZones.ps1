$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$source = [IO.File]::ReadAllText((Join-Path $projectRoot 'src\Aquabro.cs'))
function Read-Method([string]$signature) {
$start = $source.IndexOf($signature)
if ($start -lt 0) { throw "未找到方法：$signature" }
$brace = $source.IndexOf('{', $start)
$depth = 1
$end = $brace + 1
while ($depth -gt 0) {
    if ($source[$end] -eq '{') { $depth++ }
    if ($source[$end] -eq '}') { $depth-- }
    $end++
}
return $source.Substring($start, $end - $start)
}
$method = (Read-Method 'private static void ResolveWaterImpact(') + "`n" + (Read-Method 'internal static void ApplyWaterImpactUnits(')
$method += "`n" + (Read-Method 'internal static Unit FindTridentContact(') + "`n" + (Read-Method 'private static bool ClipTridentAxis(')
$method += "`n" + (Read-Method 'private static float GetTridentSweepStartX(')
$method += "`n" + (Read-Method 'private static bool FindChargedWaterContact(')
$method += "`n" + (Read-Method 'private static bool FindChargedWaterContactRect(')
$method += "`n" + (Read-Method 'private static HashSet<object> ApplyWaterImpactContacts(')
$method += "`n" + (Read-Method 'private static bool FindTridentTerrainContact(')
$method += "`n" + (Read-Method 'private static bool FindTridentTerrainContactExcept(')
$method += "`n" + (Read-Method 'private static void ResolveWaterHit(')
$method += "`n" + (Read-Method 'private static bool WaterSceneContactVisible(')
$method += "`n" + (Read-Method 'private static void DamageWaterDoodads(')
$method += "`n" + (Read-Method 'private static bool WaterHitUnits(') + "`n" + (Read-Method 'private static bool WaterHitGround(')
$effectMethods = @(
    Read-Method 'public void BeginBlast('
    Read-Method 'private void Update()'
    Read-Method 'private static int GetAnimationFrame('
) -join "`n"
$effectMethods += "`n" + [regex]::Match($source, 'private static readonly float\[\] ChargedFrameDurations = \{[^}]+\};').Value
$constants = ([regex]::Matches($source, 'private const float (WaterBlast(Range|DamageRatio)|ChargedWaterSize) = [^;]+;') | ForEach-Object Value) -join "`n"
$harness = @'
using System;
using System.Collections.Generic;
using AquabroMod;
using System.Drawing;
class MonoBehaviour { }
class AquabroWaterImpact { }
class AquabroTridentFlight {public bool hasPrevious;public float previousX,previousY;}
class GameObject {
 public Collider Contact;
 public Transform transform=new Transform();
 public bool activeInHierarchy {get {return Contact==null || Contact.active;}}
 private Dictionary<Type,object> components=new Dictionary<Type,object>();
 public T AddComponent<T>() where T : new() { var c = new T(); components[typeof(T)]=c; return c; }
 public void SetComponent(object value) {components[value.GetType()]=value;}
 public object[] GetComponents(Type type) {return new List<object>(components.Values).ToArray();}
 public T GetComponent<T>() where T:class {object value;return components.TryGetValue(typeof(T),out value)?value as T:(Contact==null?null:Contact.relay as T);}
}
class Projectile {
 public float X,Y,xI,yI;
 public int playerNum=-1,damageInternal=13,damage=13;
 public MonoBehaviour firedBy;
 public DamageType damageType;
 public GameObject gameObject=new GameObject();
 public T GetComponent<T>() where T : class { return gameObject.GetComponent<T>(); }
}
class PredabroSpear : Projectile { }
public enum DamageType { Explosion, Melee, Crush, Drill, Unholy, None }
class Block { public int health=100; }
class FallingBlock : Block { }
class Cage : Block { }
class CageBar { }
class Doodad {
 public float centerX,centerY,width=16,height=16;
 public bool immuneToHeroDamage;public bool CanBeDamagedByMooks=true;
 public int health=100,hits;public GameObject gameObject=new GameObject();
 public bool IsPointInRange(float x,float y,float range) {Map.doodadRange=range;return Math.Abs(x-centerX)<=width*.5f+range && Math.Abs(y-centerY)<=height*.5f+range;}
 public bool DamageOptional(DamageObject damage,ref bool hit) {health-=damage.damage;hits++;hit=true;return true;}
}
class DamageObject {public int damage;public DamageObject(int d,DamageType t,float fx,float fy,float x,float y,object sender){damage=d;}}
class Unit : MonoBehaviour {
 public float X,Y,forceX,forceY;
 public float width=8,height=6;
 public int playerNum=-1,health=100,hits;
 public bool invulnerable;
 public GameObject gameObject=new GameObject();
}
class TestVanDammeAnim : Unit { public float headHeight=18; }
class MookBigGuy : Unit { }
class MookGrenadier : Unit { }
struct Vector3 {
 public float x,y,z;
 public Vector3(float x,float y,float z) { this.x=x;this.y=y;this.z=z; }
 public float magnitude {get {return (float)Math.Sqrt(x*x+y*y+z*z);}}
 public Vector3 normalized {get {float n=magnitude;return new Vector3(x/n,y/n,z/n);}}
}
class Collider {
 public bool isTrigger,enabled=true;
 public bool active=true;
 public object parent;
 public DamageRelay relay;
 public float left,right,bottom=-100,top=100;
 public Collider(float a,float b) {left=Math.Min(a,b);right=Math.Max(a,b);gameObject.Contact=this;}
 public GameObject gameObject=new GameObject();
 public Bounds bounds {get {return new Bounds {min=new Vector3(left,bottom,0),max=new Vector3(right,top,0)};}}
 public Transform transform {get {return gameObject.transform;}}
 public bool activeInHierarchy {get {return active;}}
 public T GetComponentInParent<T>() where T:class {return parent as T;}
 public T GetComponent<T>() where T:class {return relay as T ?? gameObject.GetComponent<T>();}
}
class DamageRelay {protected bool isDestroyed;public Unit unit;public object doodad;public BossBlockWeapon bossWeapon;public GameObject damageGameObject;public DamageType immunity=DamageType.Drill,forceDamageType=DamageType.None;public Collider ignoreDamageWhileColliderActive,ignoreDamageWhileOtherColliderActive;}
struct RaycastHit {public Collider collider;public float distance;public Vector3 point,normal;}
struct Bounds {public Vector3 min,max;}
class Physics {
 public static Collider[] OverlapSphere(Vector3 center,float range,int layer) {
  return Walls.FindAll(delegate(Collider c) {
   float x=Mathf.Clamp(center.x,c.left,c.right)-center.x,y=Mathf.Clamp(center.y,c.bottom,c.top)-center.y;
   return c.enabled && c.active && x*x+y*y<=range*range;
  }).ToArray();
 }
 public static List<Collider> Walls=new List<Collider>();
 static bool Slab(float p,float d,float low,float high,ref float enter,ref float leave) {
  if(Math.Abs(d)<.000001f)return p>=low && p<=high;
  float a=(low-p)/d,b=(high-p)/d;
  enter=Math.Max(enter,Math.Min(a,b));leave=Math.Min(leave,Math.Max(a,b));return enter<=leave;
 }
 public static RaycastHit[] RaycastAll(Vector3 origin,Vector3 dir,float length,int layer) {
  var hits=new List<RaycastHit>();
  foreach(var c in Walls) {
   float near=0,far=length;
   if(!Slab(origin.x,dir.x,c.left,c.right,ref near,ref far) ||
      !Slab(origin.y,dir.y,c.bottom,c.top,ref near,ref far))continue;
   hits.Add(new RaycastHit {collider=c,distance=near,point=new Vector3(origin.x+dir.x*near,origin.y+dir.y*near,0),normal=new Vector3(dir.x>0?-1:1,0,0)});
  }
  return hits.ToArray();
 }
}
class Info { public string path=""; }
class Aquabro : MonoBehaviour {
 public bool IsMine=true;
 public int playerNum;
 public float X,Y;
 public Info Info=new Info();
 __CONSTANTS__
 __METHOD__
 public static void Impact(PredabroSpear p, Aquabro a, object direct) { ResolveWaterImpact(p,a,0,0,direct); }
 public static void NativeImpact(PredabroSpear p, Aquabro a) {ResolveWaterHit(p,a,p.X,p.Y);}
 public static void SweepImpact(PredabroSpear p,Aquabro a) {p.firedBy=a;WaterHitUnits(p);}
 public static float SweepStart(float ownerX,float shotX,float direction,bool previous,float lastX) { return GetTridentSweepStartX(ownerX,shotX,direction,previous,lastX); }
}
class Position { public float x,y; }
class Transform {
 public Position position=new Position();public Transform parent;
 public bool IsChildOf(Transform other) {for(Transform node=this;node!=null;node=node.parent)if(node==other)return true;return false;}
}
class Time { public static float deltaTime; }
class SpriteSM { public void SetLowerLeftPixel(int x,int y) { } }
class AquabroWaterEffect {
 public static AquabroWaterEffect Last;
 private bool charged;
 private float elapsed,impactXI;
 private int size,frames;
 private Aquabro impactOwner;
 private readonly HashSet<object> affectedUnits=new HashSet<object>();
 private Transform transform=new Transform();
 public float BlastX {get {return transform.position.x;}}
 private SpriteSM waterSprite=new SpriteSM();
 private object gameObject=new object();
 public bool destroyed;
 private void Destroy(object o) { destroyed=true; }
 public void Tick(float dt) { Time.deltaTime=dt;if(!destroyed)Update(); }
 public static AquabroWaterEffect Play(string p,string t,float x,float y,int size,int frames,bool charged) {
  Last=new AquabroWaterEffect { size=size,frames=frames,charged=charged };
  Last.transform.position.x=x;Last.transform.position.y=y;return Last;
 }
 __EFFECT_METHODS__
}
class Mathf {
 public static float Abs(float x) { return Math.Abs(x); }
 public static float Sqrt(float x) { return (float)Math.Sqrt(x); }
 public static float Sign(float x) { return x < 0 ? -1 : 1; }
 public static float Max(float x,float y) { return Math.Max(x,y); }
 public static float Min(float x,float y) { return Math.Min(x,y); }
 public static float Clamp(float x,float lo,float hi) { return Math.Max(lo,Math.Min(x,hi)); }
 public static int Clamp(int x,int lo,int hi) { return Math.Max(lo,Math.Min(x,hi)); }
}
struct Vector2 {
 public float x,y;
 public Vector2(float x,float y) { this.x=x;this.y=y; }
 public static float Distance(Vector2 a,Vector2 b) { return Mathf.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y)); }
}
namespace UnityEngine { class Object { public static void Destroy(object o) { } } }
class ValueOrchestrator {
 // 模拟伤害倍率的累积舍入，确保外圈的零伤害不会进入伤害修正。
 public static int GetModifiedDamage(int d,int p) { if(d==0) throw new Exception("Zero damage entered modifiers"); return d; }
}
class GameModeController { public static bool DoesPlayerNumDamage(int a,int b) { return a!=b; } }
class SortOfFollow { public static void Shake(float v) { } }
class MapController {
 public static float groundRadius;
 public static void DamageGround(object s,int d,DamageType t,float r,float x,float y,Collider[] ignored,bool b) {
  groundRadius=r;
  foreach(Collider c in Physics.OverlapSphere(new Vector3(x,y,0),r*.5f,Map.groundAndDamageableObjects))
   if(ignored==null || Array.IndexOf(ignored,c)<0) BossDamageTestStubs.Submit(c.gameObject,d,t,0,0);
 }
 public static void Damage_Networked(object owner,GameObject receiver,int damage,DamageType type,float fx,float fy,float x,float y) {
  if(BossDamageTestStubs.Submit(receiver,damage,type,fx,fy))return;
  DamageRelay relay=receiver.GetComponent<DamageRelay>();
  if(relay!=null && relay.unit!=null && type!=relay.immunity)
   Map.KnockAndDamageUnit(owner,relay.unit,damage,type,fx,fy,1,true,x,y,true);
  else if(relay!=null && relay.damageGameObject!=null)
   Damage_Networked(owner,relay.damageGameObject,damage,type,fx,fy,x,y);
 }
}
class Map {
 public const int groundLayer=1,groundAndDamageableObjects=3;
 public static List<Unit> units=new List<Unit>();
 public static List<Projectile> damageableProjectiles=new List<Projectile>();
 public static List<Doodad> destroyableDoodads=new List<Doodad>();
 public static List<Projectile> damagedProjectiles=new List<Projectile>();
 public static float doodadRange;
 public static List<Unit> GetUnitsInRange(float xr,float yr,float x,float y,bool b) { return units.FindAll(delegate(Unit u) { return Math.Abs(u.X-x)<=xr && Math.Abs(u.Y+6-y)<=yr; }); }
 public static void DamageDoodads(int d,DamageType t,float x,float y,float xi,float yi,float r,int p,out bool hit,object s) { hit=false;doodadRange=r; }
 public static void DamageProjectile(Projectile p,int d,DamageType t,float xi,float yi,float delay,int n) { damagedProjectiles.Add(p); }
 public static void ShakeTrees(float x,float y,float xr,float yr,float force) { }
 public static void KnockAndDamageUnit(object s,Unit u,int d,DamageType t,float xi,float yi,int dir,bool knock,float x,float y,bool net) {
  u.health-=d;u.hits++;u.forceX=xi;u.forceY=yi;
 }
}
class Tests {
 static void Check(bool ok,string s) { if(!ok)throw new Exception(s); }
 static Unit At(float x,float y) { return new Unit { X=x,Y=y-6 }; }
  static void Reset() { BossDamageTestStubs.Reset();Map.units.Clear();Map.damageableProjectiles.Clear();Map.damagedProjectiles.Clear();Map.destroyableDoodads.Clear();MapController.groundRadius=0;Physics.Walls.Clear(); }
 static void Charged(int direction) {
  Reset();
  Map.destroyableDoodads.Add(new Doodad {centerX=1000});
  foreach(float offset in new float[]{0,16,20,24,26,28,30,32,34}) {
   var target=new TestVanDammeAnim {X=-offset,Y=-10,width=8,headHeight=18};
   Map.units.Add(target);
  }
  var friendly=new TestVanDammeAnim {X=-16,Y=-10,playerNum=0};Map.units.Add(friendly);
  var invulnerable=new TestVanDammeAnim {X=-16,Y=-10,invulnerable=true};Map.units.Add(invulnerable);
  var dead=new TestVanDammeAnim {X=-16,Y=-10,health=0};Map.units.Add(dead);
  // 重复登记的同一实体仍只能受击一次。
  Map.units.Add(Map.units[4]);
  float inner=26f*.7f;
  foreach(float r in new float[]{inner-.001f,inner+.001f,26f}) Map.damageableProjectiles.Add(new Projectile {X=r});
  var p=new PredabroSpear {xI=664*direction,playerNum=0};
  var owner=new Aquabro();Aquabro.Impact(p,owner,null);
  for(int i=0;i<9;i++) {
   Unit u=Map.units[i];bool damage=i<=4;
   Check(u.hits==1,"Visible water missed body at recorded MCP offset "+u.X);
   Check(u.health==(damage?86:100),"Visible contact damage zone at "+u.X);
   Check(u.forceY>=260 && u.forceY>Math.Abs(u.forceX),"Missing upward/tumble force");
  }
  Check(friendly.hits==0 && invulnerable.hits==0 && dead.hits==0,"Friendly/invulnerable/dead filter");
  Check(Map.damagedProjectiles.Count==1,"Unit coverage unexpectedly expanded projectile destruction");
  Check(Math.Abs(MapController.groundRadius-inner)<.0001f && Math.Abs(Map.doodadRange-inner*.6f)<.0001f,"Destruction radius changed");
  Aquabro.Impact(p,owner,null);Check(Map.units[4].hits==1,"Repeated impact");
 }
 static TestVanDammeAnim PixelTarget(int px,int py) {
  return new TestVanDammeAnim {X=(px+.5f-32)*1.125f,Y=(32-py-.5f)*1.125f-.05f,width=.1f,headHeight=.1f};
 }
 static void MaskAndBody() {
  using(var image=new Bitmap(@"__ATLAS__")) {
   Check(image.Width==384 && image.Height==64,"Unexpected water atlas");
   for(int f=0;f<6;f++) for(int y=0;y<64;y++) for(int x=0;x<64;x++)
    Check(ChargedWaterPixelMask.IsVisible(f,x,y)==(image.GetPixel(f*64+x,y).A>0),"Mask/PNG mismatch");
   // 用实际 PNG 像素作为独立依据，遍历每帧每个像素中心的极小身体。
   var pixelOwner=new Aquabro();
   for(int f=0;f<6;f++) for(int py=0;py<64;py++) for(int px=0;px<64;px++) {
    Reset();var body=PixelTarget(px,py);Map.units.Add(body);
    Aquabro.ApplyWaterImpactUnits(pixelOwner,true,0,0,664,0,null,new HashSet<object>(),f);
    bool visible=image.GetPixel(f*64+px,py).A>0;
    Check(body.hits==(visible?1:0),"Pixel-center body contact disagrees with displayed frame");
    double radial=Math.Sqrt(body.X*body.X+(body.Y+.05f)*(body.Y+.05f));
    if(visible && radial<25) Check(body.health==86,"Visible inner water did not damage");
    if(visible && radial>25.4) Check(body.health==100,"Visible outer water dealt damage");
   }
  }
  Reset();var owner=new Aquabro();var affected=new HashSet<object>();
  var transparent=PixelTarget(32,32);Map.units.Add(transparent);
  Aquabro.ApplyWaterImpactUnits(owner,true,0,0,664,0,null,affected,0);
  Check(transparent.hits==0 && !affected.Contains(transparent),"Transparent center causes phantom hit");
  // 高个单位的脚底采样点远离初筛，但头部正好碰到水花。
  var head=new TestVanDammeAnim {X=-17.4375f,Y=-100,width=.1f,headHeight=99.5f};Map.units.Add(head);
  Aquabro.ApplyWaterImpactUnits(owner,true,0,0,664,0,null,affected,0);
  Check(head.hits==1 && head.health==86,"Head-only contact dropped by broad phase");
  // 同一坐标当前帧已透明，不能沿用上一帧的水体。
  var oldPixel=PixelTarget(16,32);Map.units.Add(oldPixel);
  Aquabro.ApplyWaterImpactUnits(owner,true,0,0,664,0,null,affected,1);
  Check(oldPixel.hits==0,"Previous frame water still hits");
  Aquabro.ApplyWaterImpactUnits(owner,true,0,0,664,0,null,affected,0);
  Check(oldPixel.hits==1,"Opaque frame does not hit body edge");
  Console.WriteLine("PASS pixels: all six masks and 24576 body contacts match PNG; original MCP offsets 0..34; transparent center; current frame; head-only contact beyond old broad phase");
 }
 static void Contact() {
  Reset();float hx,hy;var owner=new Aquabro();
  var big=new TestVanDammeAnim { X=2495.48f,Y=216,width=12,height=12.5f,headHeight=26 };
  Map.units.Add(big);
  Check(Aquabro.FindTridentContact(owner,0,2474.54f,245.5f,2498.54f,245.5f,out hx,out hy)==null,"Above actual spear/head tolerance should miss");
  Check(Aquabro.FindTridentContact(owner,0,2474.54f,243f,2498.54f,243f,out hx,out hy)==big,"Paused scene spear-tip head contact missed");
  Check(hx>2489 && hx<2490 && hy==242,"Explosion did not start at contact point");
  Check(Aquabro.FindTridentContact(owner,0,2518,243,2494,243,out hx,out hy)==big,"Left-facing tip missed");
  Check(Aquabro.FindTridentContact(owner,0,2460,240,2520,240,out hx,out hy)==big,"Fast flight tunneled through target");
  big.invulnerable=true;Check(Aquabro.FindTridentContact(owner,0,2460,240,2520,240,out hx,out hy)==null,"Invulnerable target accepted");big.invulnerable=false;
  Reset();var direct=At(30,0);Map.units.Add(direct);
  Aquabro.Impact(new PredabroSpear { xI=664,playerNum=0 },owner,direct);
  Check(direct.hits==1 && direct.health==86,"Confirmed direct hit suppressed by splash radius");
 }
 static void CloseThrow() {
  foreach(int direction in new[]{-1,1}) foreach(float y in new[]{8f,11f}) foreach(float gap in new[]{0f,2f,6f,12f}) {
   Reset();var owner=new Aquabro();var close=new TestVanDammeAnim { X=100+direction*gap,Y=0,width=8,headHeight=18 };
   var behind=new TestVanDammeAnim { X=100-direction*10,Y=0,width=8,headHeight=18 };
   Map.units.Add(behind);Map.units.Add(close);float hx,hy;
   float shotX=100+direction*14,tipX=shotX+direction*24;
   float from=Aquabro.SweepStart(100,shotX,direction,false,0);
   Check(from==100,"Initial sweep still starts beyond close enemy");
   Check(Aquabro.FindTridentContact(owner,0,from,y,tipX,y,out hx,out hy)==close,"Close/overlap/crouching/left/right miss");
   Check(Aquabro.SweepStart(100,shotX,direction,true,tipX)==tipX,"Subsequent sweep repeats owner-to-tip");
   close.health=0;Check(Aquabro.FindTridentContact(owner,0,from,y,tipX,y,out hx,out hy)==null,"Hit enemy behind player");
   Check(Aquabro.SweepStart(100,100+direction*100,direction,false,0)==100+direction*100,"Remote first observation swept long distance");
  }
 }
 static void LateEntry() {
  Reset();var owner=new Aquabro();var late=PixelTarget(0,32);late.X=100;Map.units.Add(late);
  Aquabro.Impact(new PredabroSpear {xI=664,playerNum=0},owner,null);
  var effect=AquabroWaterEffect.Last;
  Check(late.hits==0,"Outside unit hit at detonation");
  late.X=PixelTarget(0,32).X;
  effect.Tick(0);Check(late.hits==0,"Paused effect still applies shock");
  effect.Tick(.1f);
  Check(late.hits==1 && late.health==100 && late.forceY>=260,"Late visible outer-ring entry");
  late.X=PixelTarget(13,38).X;late.Y=PixelTarget(13,38).Y;
  effect.Tick(.01f);Check(late.hits==1 && late.health==100,"Outer-to-inner repeats damage");
  var inner=PixelTarget(13,38);Map.units.Add(inner);effect.Tick(.01f);
  Check(inner.hits==1 && inner.health==86,"Late visible inner-ring entry");
  var expiry=PixelTarget(13,38);Map.units.Add(expiry);effect.Tick(.481f);
  Check(effect.destroyed && expiry.hits==0,"Expired visual still affects enemies");
  effect.Tick(.1f);Check(inner.hits==1,"Destroyed effect ticks");
  var independent=PixelTarget(16,32);Map.units.Add(independent);
  Aquabro.Impact(new PredabroSpear {xI=664,playerNum=0},owner,null);
  Check(independent.hits==1,"Independent explosion cannot hit target");
 }
 static void WallOcclusion() {
  foreach(int direction in new[]{-1,1}) foreach(float height in new[]{8f,11f}) {
   Reset();var owner=new Aquabro();float hx,hy;
   var behind=new TestVanDammeAnim {X=direction*30,Y=0,width=8,headHeight=18};Map.units.Add(behind);
   // The body can still be before the wall while its 24px tip has crossed it.
   var wall=new Collider(direction*16,direction*24);Physics.Walls.Add(wall);
   Check(Aquabro.FindTridentContact(owner,0,0,height,direction*38,height,out hx,out hy)==null,"Spawn sweep hit through wall");
   Check(Aquabro.FindTridentContact(owner,0,0,height,direction*120,height,out hx,out hy)==null,"Fast sweep hit through wall");
   var front=new TestVanDammeAnim {X=direction*10,Y=0,width=4,headHeight=18};Map.units.Add(front);
   Check(Aquabro.FindTridentContact(owner,0,0,height,direction*120,height,out hx,out hy)==front,"Wall suppressed enemy in front");
   Map.units.Remove(front);
   wall.isTrigger=true;
   Check(Aquabro.FindTridentContact(owner,0,0,height,direction*38,height,out hx,out hy)==behind,"Trigger blocks trident");
   wall.isTrigger=false;wall.parent=new Unit();
   Check(Aquabro.FindTridentContact(owner,0,0,height,direction*38,height,out hx,out hy)==behind,"Unit collider treated as wall");
   wall.parent=null;wall.relay=new DamageRelay {unit=new Unit()};
   Check(Aquabro.FindTridentContact(owner,0,0,height,direction*38,height,out hx,out hy)==behind,"Boss relay treated as terrain");
   wall.relay=null;wall.enabled=false;
   Check(Aquabro.FindTridentContact(owner,0,0,height,direction*38,height,out hx,out hy)==behind,"Destroyed wall blocks trident");
  }
  foreach(bool charged in new[]{false,true}) {
   Reset();var owner=new Aquabro();Unit target=charged ? (Unit)new TestVanDammeAnim {X=-16,Y=-10,width=8,headHeight=18} : At(10,0);Map.units.Add(target);
   var wall=charged ? new Collider(-6,-4) : new Collider(4,6);Physics.Walls.Add(wall);
   Aquabro.Impact(new PredabroSpear {xI=charged?664:400,playerNum=0},owner,null);
   Check(target.hits==0,"Splash damages/knocks through intact wall");
   if(charged) {
    var effect=AquabroWaterEffect.Last;effect.Tick(.01f);Check(target.hits==0,"Late-entry blast passes wall");
    wall.enabled=false;effect.Tick(.01f);
    Check(target.hits==1 && target.health==86,"Removed wall still blocks unspent late-entry hit");
   }
  }
  foreach(int direction in new[]{-1,1}) {
   Reset();var owner=new Aquabro();var target=At(direction*24,0);Map.units.Add(target);
   Physics.Walls.Add(new Collider(direction*10,direction*18));
   Aquabro.NativeImpact(new PredabroSpear {X=direction*14,xI=direction*664,playerNum=0},owner);
   Check(target.hits==0,"Native spawn effects started blast inside wall");
   Check(AquabroWaterEffect.Last.BlastX*direction<10,"Native impact was not moved to near wall face");
   Reset();target=At(direction*30,0);Map.units.Add(target);
   Physics.Walls.Add(new Collider(direction*16,direction*24));
   var shot=new PredabroSpear {X=direction*14,xI=direction*664,playerNum=0};
   Aquabro.SweepImpact(shot,owner);
   Check(shot.GetComponent<AquabroWaterImpact>()!=null,"Tip hit wall but did not resolve impact");
   Check(target.hits==0 && AquabroWaterEffect.Last.BlastX*direction<16,"Tip-wall impact moved splash to far wall face");
  }
  Console.WriteLine("PASS walls: left/right standing/crouched spawn+fast sweeps; front enemy; trigger/unit/boss filters; destroyed walls; normal/charged splash; late entry; native inside-wall spawn; tip-wall impact near face");
 }
 static void SceneOcclusion() {
  foreach(bool charged in new[]{false,true})foreach(int direction in new[]{-1,1})foreach(int obstacle in new[]{0,1,2,3}) {
   Reset();var owner=new Aquabro();var prop=new Doodad {centerX=14*direction};Map.destroyableDoodads.Add(prop);
   var immune=new Doodad {centerX=14*direction,immuneToHeroDamage=true};Map.destroyableDoodads.Add(immune);
   var rocket=new Projectile {X=14*direction};Map.damageableProjectiles.Add(rocket);
   var own=new Projectile {X=14*direction,playerNum=0};Map.damageableProjectiles.Add(own);
   var wall=new Collider(5*direction,6*direction);Physics.Walls.Add(wall);
   if(obstacle==1)wall.isTrigger=true;
   if(obstacle==2)wall.enabled=false;
   if(obstacle==3)wall.parent=new Unit();
   Aquabro.Impact(new PredabroSpear {xI=(charged?664:400)*direction,damageInternal=5,playerNum=0},owner,null);
   Check(prop.hits==(obstacle==0?0:1),"Water damages scenery through wall / loses clear hit");
   Check(prop.health==(obstacle==0?100:charged?86:95) && immune.hits==0,"Scenery damage/eligibility changed");
   Check(Map.damagedProjectiles.Contains(rocket)==(charged && obstacle!=0) && !Map.damagedProjectiles.Contains(own),
    "Charged projectile destruction ignores wall/affiliation");
  }
  Reset();var receiver=new Doodad {centerX=14};Map.destroyableDoodads.Add(receiver);
  var child=new Collider(12,13);child.transform.parent=receiver.gameObject.transform;Physics.Walls.Add(child);
  Aquabro.Impact(new PredabroSpear {xI=664,playerNum=0},new Aquabro(),null);
  Check(receiver.hits==1,"Scenery's own child collider occludes its hit");
  Console.WriteLine("PASS scene walls: normal/charged Doodads and charged rockets; both directions; real wall/trigger/disabled/unit; friendly and immune filters; recipient child collider");
 }
 public static int Main() {
  try {
   Charged(1);Charged(-1);MaskAndBody();LateEntry();Contact();CloseThrow();WallOcclusion();SceneOcclusion();BossWaterDamageTests.Run();
   Reset();Unit normal=At(13,0);Map.units.Add(normal);
   Aquabro.Impact(new PredabroSpear { xI=400,damageInternal=5 },new Aquabro(),normal);
   Check(normal.health==92 && normal.hits==1 && normal.forceY==90,"Normal direct hit changed");
   Reset();Unit remote=At(0,0);Map.units.Add(remote);
   Aquabro.Impact(new PredabroSpear { xI=664 },new Aquabro { IsMine=false },remote);
   Check(remote.hits==0,"Remote owner repeats damage");
   Console.WriteLine("PASS: close+overlap throws / crouch+both directions / behind excluded / tip+head+swept hits / 600ms late entry / pause / once / inner70% damage / outer30% knock / normal / remote");return 0;
  } catch(Exception ex) { Console.Error.WriteLine(ex.Message);return 1; }
 }
}
'@
$testSource = Join-Path $env:TEMP ('Aquabro-WaterZones-' + [Guid]::NewGuid().ToString('N') + '.cs')
$testExe = [IO.Path]::ChangeExtension($testSource,'.exe')
try {
    [IO.File]::WriteAllText($testSource,$harness.Replace('__CONSTANTS__',$constants).Replace('__METHOD__',$method).Replace('__EFFECT_METHODS__',$effectMethods).Replace('__ATLAS__',(Join-Path $projectRoot '_Mod\water-rocket-explosion.png')))
    & (Join-Path $env:WINDIR 'Microsoft.NET\Framework\v3.5\csc.exe') /nologo /nowarn:0649 /reference:System.Drawing.dll /out:$testExe $testSource (Join-Path $projectRoot 'src\PixelMask.cs') (Join-Path $projectRoot 'src\ChargedWaterPixelMask.cs') (Join-Path $projectRoot 'src\CollisionDamageTarget.cs') (Join-Path $PSScriptRoot 'BossDamageTestStubs.cs') (Join-Path $PSScriptRoot 'BossWaterDamageTests.cs')
    if ($LASTEXITCODE -ne 0) { throw '水爆分区测试编译失败' }
    & $testExe
    if ($LASTEXITCODE -ne 0) { throw '水爆分区测试失败' }
} finally {
    Remove-Item -LiteralPath $testSource,$testExe -Force -ErrorAction SilentlyContinue
}
