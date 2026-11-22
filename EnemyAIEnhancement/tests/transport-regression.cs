// #name Transport And Weapon Authority Regression
// #description Offline actual initialized prefabs, native consumption entries, complete ownership transitions and transport stages.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Networking;
public static class TransportRegression
{
    private static readonly BindingFlags hidden=BindingFlags.Static|BindingFlags.NonPublic;
    private static readonly Type vehicles=typeof(EnemyAIEnhancement.Plugin).Assembly.GetType("EnemyAIEnhancement.VehicleController");
    private static readonly Type transports=typeof(EnemyAIEnhancement.Plugin).Assembly.GetType("EnemyAIEnhancement.TransportController");
    private static readonly List<GameObject> fixtures=new List<GameObject>();
    private static int passed,queries,nearest;private static float spawnX,spawnXI;private static int spawns;
    public static void Main()
    {
        if(!Connect.IsOffline||!HeroController.PlayerIsAlive(0))throw new Exception("Offline healthy hero required.");
        TestVanDammeAnim hero=HeroController.players[0].character;Vector3 original=hero.transform.position;bool active=hero.enabled;int hp=hero.health;
        bool enabled=(bool)typeof(EnemyAIEnhancement.Plugin).GetField("Enabled",hidden).GetValue(null);
        HashSet<Mook> oldMooks=new HashSet<Mook>(UnityEngine.Object.FindObjectsOfType<Mook>());
        HashSet<Projectile> oldShots=new HashSet<Projectile>(UnityEngine.Object.FindObjectsOfType<Projectile>());
        Vector3 site=Vector3.zero;
        for(float y=Map.Height*16f-120;y>200&&site==Vector3.zero;y-=80)
            for(float x=320;x<Map.Width*16f-320;x+=200)
                if(!Physics.CheckBox(new Vector3(x,y,0),new Vector3(260,70,3),Quaternion.identity,Map.groundLayer|Map.fragileLayer)){site=new Vector3(x,y,0);break;}
        if(site==Vector3.zero)throw new Exception("No controlled fixture space.");
        try
        {
            Toggle(true);hero.enabled=false;hero.health=50000;passed=0;
            GameObject floor=Box("Floor",site+Vector3.down*6,new Vector3(520,12,8));
            foreach(string kind in new[]{"CannonTank","MookLauncherTank"})
            {
                Tank tank=Make(kind,site);Type prod=vehicles;hero.SetPosition(site+new Vector3(-100,0,0));Physics.SyncTransforms();
                object memory=Call(prod,"Observe",tank);ScriptContext.Logger(kind+" site="+site+" hero="+hero.X+","+hero.Y+" applicable="+Call(prod,"Applicable",tank)+" owns="+Call(prod,"Owns",tank)+" memory="+(memory==null?"null":"visible="+Get(memory,"Visible")+" npc="+Get(memory,"Npc")));Check(memory!=null&&(bool)Get(memory,"Visible"),kind+" genuine initial observation");
                MethodInfo query=AccessTools.Method(typeof(HeroController),"GetPlayerPos",kind=="CannonTank"?new[]{typeof(int)}:new[]{typeof(int),typeof(float).MakeByRefType(),typeof(float).MakeByRefType()});
                MethodInfo near=AccessTools.Method(typeof(HeroController),"GetActualNearestPlayer");
                ScriptContext.Harmony.Patch(query,new HarmonyMethod(typeof(TransportRegression),"Query"));
                ScriptContext.Harmony.Patch(near,new HarmonyMethod(typeof(TransportRegression),"Nearest"));
                foreach(string state in new[]{"valid","ai-only","restore-ai","weapon-only","restore-weapon","mirror"})
                {
                    if(state=="ai-only")tank.enemyAI.SetOwner(new PID(99));
                    if(state=="restore-ai")tank.enemyAI.SetOwner(PID.MyID);
                    if(state=="weapon-only")tank.weapon.SetOwner(new PID(99));
                    if(state=="restore-weapon")tank.weapon.SetOwner(PID.MyID);
                    if(state=="mirror")
                    {Owners(tank,new PID(99));Check((bool)Call(prod,"AcceptAim",tank,new PID(99),0,site+new Vector3(-100,15,0)),kind+" legitimate owner mirror copy");}
                    else if(state!="ai-only"&&state!="weapon-only")Call(prod,"Observe",tank);
                    queries=nearest=0;Set(tank.weapon,"targetPlayerNum",0);
                    Entry(tank.weapon,kind=="CannonTank"?"Update":"FireWeapon",kind=="CannonTank"?new object[0]:new object[]{0});
                    bool conflict=state=="ai-only"||state=="weapon-only";
                    Check(conflict?queries>0:queries==0,kind+" real consumer "+state+" native query="+queries);
                    if(conflict)Check(Memory(prod,tank)==null,kind+" mismatch invalidates actual old observation");
                    if(kind=="MookLauncherTank")
                    {
                        Set(tank.weapon,"targetPlayerNum",-1);queries=nearest=0;Entry(tank.weapon,"FireWeapon",0);
                        Check(conflict?queries>0&&nearest>0:queries==0&&nearest==0,kind+" actual nearest fallback "+state);
                    }
                }
                ScriptContext.Harmony.UnpatchAll(ScriptContext.Harmony.Id);UnityEngine.Object.DestroyImmediate(tank.gameObject);
            }
            foreach(string kind in new[]{"MookTruck","DumpTruck","Motorbike","MotorbikeNuclear","DrillCarrier"})
            {
                Tank tank=Make(kind,site);hero.SetPosition(site+new Vector3(kind=="MookTruck"?100:-96,0,0));Physics.SyncTransforms();
                object memory=Call(transports,"Observe",tank);
                Check(memory!=null&&(bool)Get(memory,"Visible"),kind+" real prefab visible observation");
                Vector3 last=(Vector3)Get(memory,"Point");GameObject wall=Box("Wall",site+new Vector3((hero.X-site.x)*0.5f,40,0),new Vector3(6,100,8));
                Set(memory,"NextSense",0f);hero.SetPosition(hero.transform.position+Vector3.right*16);Call(transports,"Observe",tank);
                Check(!(bool)Get(memory,"Visible")&&(Vector3)Get(memory,"Point")==last,kind+" hidden hero freezes point");
                Set(memory,"SeenAt",Time.time-6.1f);Set(memory,"NextSense",0f);Call(transports,"Observe",tank);Check((int)Get(memory,"Player")==-1,kind+" memory expiry");
                UnityEngine.Object.DestroyImmediate(wall);hero.SetPosition(site+new Vector3(kind=="MookTruck"?100:-96,0,0));Physics.SyncTransforms();Set(memory,"NextSense",0f);Call(transports,"Observe",tank);
                Check((bool)Call(transports,"SafeDrive",tank,tank.X-64),kind+" actual whole-body supported drive");
                floor.SetActive(false);Check(!(bool)Call(transports,"SafeDrive",tank,tank.X-64),kind+" missing support rejects drive");floor.SetActive(true);Physics.SyncTransforms();
                foreach(string boundary in new[]{"story","player","blind","disabled","editor"})
                {
                    if(boundary=="story")tank.beingControlledByTriggerAction=true;if(boundary=="player")tank.playerNum=0;if(boundary=="blind")Set(tank,"stunTime",1f);if(boundary=="disabled")Toggle(false);if(boundary=="editor")Map.isEditing=true;
                    Check(!(bool)Call(transports,"Applicable",tank),kind+" boundary "+boundary);
                    tank.beingControlledByTriggerAction=false;tank.playerNum=-1;Set(tank,"stunTime",0f);Map.isEditing=false;Toggle(true);
                }
                Owners(tank,new PID(99));Check(!(bool)Call(transports,"Accept",tank,new PID(98),0,site),kind+" wrong sender rejected");
                Check((bool)Call(transports,"Accept",tank,new PID(99),0,site),kind+" valid mirror accepted");Call(transports,"Observe",tank);
                Check((Vector3)Get(Memory(transports,tank),"Point")==site,kind+" mirror does not independently select");
                Owners(tank,PID.MyID);memory=Call(transports,"Observe",tank);
                if(tank is MookTruck&&!(tank is DrillCarrier))
                {
                    Entry(tank.enemyAI,"LookForEnemy");Check(tank.enemyAI.actionQueue.Any(a=>a.type==EnemyActionType.Fire),kind+" actual think deployment opportunity");
                    bool dump=tank is MookDumpTruck;
                    Check(dump?tank.enemyAI.actionQueue.Any(a=>a.type==EnemyActionType.Move)&&((bool)Get(tank.enemyAI,"_hasMoved")):!tank.enemyAI.actionQueue.Any(a=>a.type==EnemyActionType.Move),kind+" parking vs single drive");
                    tank.enemyAI.actionQueue.Clear();tank.enemyAI.AddAction(EnemyActionType.Fire,1E+12f);object action=tank.enemyAI.CurrentAction;
                    Entry(tank.enemyAI,"StopForNearbyPlayer");Check(ReferenceEquals(action,tank.enemyAI.CurrentAction),kind+" committed full wave preserved");
                    int capacity=((MookTruck)tank).mooksToSpawn;float rate=dump?0.22f:0.6f;
                    for(int i=0;i<capacity+2;i++){Set(tank,"t",1f);Entry(tank,"FireWeapon");}
                    Check((int)Get(tank,"mookSpawnCount")==capacity,kind+" native finite capacity");
                    Check(Mathf.Abs((float)Get(tank,"fireDelay")-(rate-2f))<0.01f,kind+" native interval unmodified after exhausted calls");
                    if(dump)Check((bool)Get(tank,"_openUpBackWhenStopped"),kind+" actual rear opening retained");
                }
                else if(tank is MookMotorBike)
                {
                    MookMotorBike bike=(MookMotorBike)tank;bool[] input=Input(bike.enemyAI);
                    Check(input[0]&&!input[1]&&!input[5],kind+" actual GetInput starts native left approach");
                    hero.SetPosition(site+new Vector3(-((MookMotorBikePolyAI)bike.enemyAI).attackXRange-8,0,0));Set(memory,"NextSense",0f);Physics.SyncTransforms();input=Input(bike.enemyAI);
                    Check(input[5],kind+" actual GetInput advances one-shot trigger opportunity");
                    Set(bike,"t",0.02f);Set(bike,"fire",false);Set(bike,"wasFire",false);Entry(bike,"RunAI");
                    Check((bool)Get(bike,"fire")&&(bool)Get(bike,"wasFire"),kind+" actual native RunAI consumes fire edge (offline clone has no RPC NID)");
                    Entry(bike,"FireWeapon");Check((bool)Get(bike,"mookSpawned"),kind+" actual native FireWeapon commits");
                    if(kind=="MotorbikeNuclear")Check((bool)Get(bike,"hasExploded"),kind+" native nuclear difference");
                    Entry(bike,"FireWeapon");Check((bool)Get(bike,"mookSpawned"),kind+" repeated native Fire keeps one-shot gate");
                    Toggle(false);Toggle(true);Check((bool)Get(bike,"mookSpawned"),kind+" toggle retains committed phase");
                }
                else
                {
                    Check(tank.enemyAI==null,kind+" no ordinary AI dependency");
                    Set(tank,"chuckingMooks",true);float x=tank.X;Set(tank,"forceXPos",x);
                    ScriptContext.Harmony.Patch(AccessTools.Method(typeof(MapController),"SpawnMook_Networked"),null,new HarmonyMethod(typeof(TransportRegression),"Spawn"));spawns=0;
                    for(int i=0;i<8;i++)Entry(tank,"FireWeapon");
                    Check(spawns==8&&spawnXI<0&&spawnX<tank.X,kind+" real host spawn consumes observed threat side");
                    Check(!(bool)Get(tank,"chuckingMooks")&&(int)Get(tank,"mookSpawnCount")==8&&tank.X==x,kind+" eight throws and fixed position retained");
                    ScriptContext.Harmony.UnpatchAll(ScriptContext.Harmony.Id);
                }
                UnityEngine.Object.DestroyImmediate(tank.gameObject);ScriptContext.Logger("PASS "+kind+" cumulative="+passed);
            }
            ScriptContext.Logger("PASS "+passed+" transport/authority controlled checks version="+typeof(EnemyAIEnhancement.Plugin).Assembly.GetName().Version);
        }
        finally
        {
            ScriptContext.Harmony.UnpatchAll(ScriptContext.Harmony.Id);
            foreach(GameObject go in fixtures)if(go!=null)UnityEngine.Object.DestroyImmediate(go);fixtures.Clear();
            foreach(Mook m in UnityEngine.Object.FindObjectsOfType<Mook>().Where(m=>!oldMooks.Contains(m)))UnityEngine.Object.DestroyImmediate(m.gameObject);
            foreach(Projectile p in UnityEngine.Object.FindObjectsOfType<Projectile>().Where(p=>!oldShots.Contains(p)))UnityEngine.Object.DestroyImmediate(p.gameObject);
            hero.SetPosition(original);hero.health=hp;hero.enabled=active;Map.isEditing=false;Toggle(enabled);
        }
    }
    private static bool[] Input(PolymorphicAI ai){object[] a=Enumerable.Repeat((object)false,11).ToArray();AccessTools.Method(ai.GetType(),"GetInput").Invoke(ai,a);return a.Cast<bool>().ToArray();}
    private static Tank Make(string kind,Vector3 site)
    {
        Type types=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("RocketLib.Utils.UnitTypes")).First(candidate=>candidate!=null);
        Unit prefab=(Unit)types.GetMethod("GetUnitPrefab").Invoke(null,new object[]{Enum.Parse(types.Assembly.GetType("RocketLib.Utils.UnitType"),kind),-1,false});ScriptContext.Logger("MAKE "+kind);
        Tank t=UnityEngine.Object.Instantiate(prefab.gameObject,site,Quaternion.identity).GetComponent<Tank>();fixtures.Add(t.gameObject);Entry(t,"Start");
        if(t.enemyAI!=null){Entry(t.enemyAI,"Start");Set(t.enemyAI,"firstFrame",false);t.enemyAI.actionQueue.Clear();if(t.enemyAI is MookTruckPolyAI)Set(t.enemyAI,"checkBlocks",false);}
        if(t.weapon!=null)Entry(t.weapon,"Start");Owners(t,PID.MyID);t.SetXY(site.x,site.y);t.transform.position=site;Set(t,"groundHeight",site.y);t.actionState=ActionState.Idle;Physics.SyncTransforms();return t;
    }
    private static GameObject Box(string name,Vector3 p,Vector3 size){GameObject g=new GameObject("EnemyAI.Transport"+name);fixtures.Add(g);g.layer=LayerMask.NameToLayer("Ground");g.transform.position=p;g.AddComponent<BoxCollider>().size=size;Physics.SyncTransforms();return g;}
    private static void Owners(Tank t,PID p){t.SetOwner(p);if(t.enemyAI!=null)t.enemyAI.SetOwner(p);if(t.weapon!=null)t.weapon.SetOwner(p);}
    private static void Entry(object target,string name,params object[] a){MethodInfo m=AccessTools.Method(target.GetType(),name);if(m==null){if(name=="Start")return;throw new Exception("Missing entry "+target.GetType()+"."+name);}try{m.Invoke(target,a);}catch(TargetInvocationException e){throw new Exception(target.GetType()+"."+name+": "+e.InnerException);}}
    private static object Get(object o,string n){return AccessTools.Field(o.GetType(),n).GetValue(o);}
    private static void Set(object o,string n,object v){AccessTools.Field(o.GetType(),n).SetValue(o,v);}
    private static object Call(Type t,string n,params object[] a){return t.GetMethod(n,hidden).Invoke(null,a);}
    private static object Memory(Type t,Tank tank){return ((IDictionary)t.GetField("Observations",hidden).GetValue(null))[tank];}
    private static void Toggle(bool v){typeof(EnemyAIEnhancement.Plugin).GetMethod("Toggle",hidden).Invoke(null,new object[]{null,v});}
    public static void Query(){queries++;}public static void Nearest(){nearest++;}
    public static void Spawn(float x,float y,float xI){spawns++;spawnX=x;spawnXI=xI;}
    private static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL "+name);passed++;}
}
