// #name Boss Fallback Regression
// #description Offline actual injected query entries and Goliath action branches; no natural evidence.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Networking;
using UnityEngine;
public static class BossFallbackRegression
{
    private static readonly BindingFlags flags=BindingFlags.Static|BindingFlags.NonPublic;
    private static readonly Type prod=typeof(EnemyAIEnhancement.Plugin).Assembly.GetType("EnemyAIEnhancement.BossController");
    private static readonly List<GameObject> fixtures=new List<GameObject>();
    private static readonly Dictionary<string,int> queries=new Dictionary<string,int>();
    private static int passed;private static bool refResult;private static int refPlayer;
    public static void Main()
    {
        if(!Connect.IsOffline||!HeroController.PlayerIsAlive(0))throw new Exception("Offline healthy hero required");
        TestVanDammeAnim hero=HeroController.players[0].character;Player old=HeroController.players[1];
        Vector3 original=hero.transform.position;bool active=hero.enabled;int hp=hero.health;
        Vector3 cameraMin=(Vector3)AccessTools.Field(typeof(SortOfFollow),"minWorldPoint").GetValue(null),cameraMax=(Vector3)AccessTools.Field(typeof(SortOfFollow),"maxWorldPoint").GetValue(null);
        bool enabled=(bool)typeof(EnemyAIEnhancement.Plugin).GetField("Enabled",flags).GetValue(null);
        HashSet<Projectile> prior=new HashSet<Projectile>(UnityEngine.Object.FindObjectsOfType<Projectile>());
        Vector3 site=new Vector3(480,Map.Height*16f-200,0);passed=0;
        try
        {
            hero.enabled=false;hero.health=500;
            GameObject pg=new GameObject("EnemyAI.BossPlayerFixture");fixtures.Add(pg);Player second=pg.AddComponent<Player>();
            TestVanDammeAnim other=UnityEngine.Object.Instantiate(hero,site+Vector3.right*260,Quaternion.identity);fixtures.Add(other.gameObject);
            foreach(MonoBehaviour b in other.GetComponentsInChildren<MonoBehaviour>(true))b.enabled=false;
            other.playerNum=1;other.health=500;second.playerNum=1;second.character=other;HeroController.players[1]=second;
            hero.SetPosition(site+Vector3.left*200);other.SetPosition(site+Vector3.right*260);Physics.SyncTransforms();
            foreach(MethodInfo m in typeof(HeroController).GetMethods().Where(m=>new[]{"GetPlayerPos","GetPlayerPosition","GetNearestPlayerPos","GetNearestPlayer","GetActualNearestPlayer"}.Contains(m.Name)))
                ScriptContext.Harmony.Patch(m,new HarmonyMethod(typeof(BossFallbackRegression),"Query"));
            ScriptContext.Harmony.Patch(AccessTools.Method(typeof(HeroController),"GetNearestPlayer",new[]{typeof(float),typeof(float),typeof(float),typeof(float),typeof(int).MakeByRefType()}),null,new HarmonyMethod(typeof(BossFallbackRegression),"NearestReturned"));
            Unit satan=Make("SatanMiniboss",site);PolymorphicAI sai=satan.GetComponent<PolymorphicAI>();
            Toggle(false);Set(sai,"seenEnemyNum",1);Set(sai,"pattern",SatanBossPattern.Jumping);queries.Clear();Entry(sai,"DoAlertedThink");
            Check(Count("GetNearestPlayerPos:2")>0&&sai.actionQueue.Any(a=>a.type==EnemyActionType.Jump&&a.gridPoint.collumn==Map.GetCollumn(hero.X)),"disabled actual Satan cycle chooses nearest by call origin, not seen slot");
            Unit dolf=Make("CR666",site);PolymorphicAI dai=dolf.GetComponent<PolymorphicAI>();
            foreach(string state in new[]{"disabled","ai-conflict","npc"})
            {
                Toggle(state!="disabled");Owners(dolf,PID.MyID);Call("Forget",dolf);
                if(state=="ai-conflict")dai.SetOwner(new PID(99));
                if(state=="npc")
                {
                    // Actual native NPC selection uses this non-player combatant and camera visibility.
                    TestVanDammeAnim npc=UnityEngine.Object.Instantiate(hero,site+Vector3.right*40,Quaternion.identity);fixtures.Add(npc.gameObject);
                    foreach(MonoBehaviour b in npc.GetComponentsInChildren<MonoBehaviour>(true))b.enabled=false;
                    npc.playerNum=5;npc.health=500;npc.SetPosition(site+Vector3.right*40);Map.units.Add(npc);
                    AccessTools.Field(typeof(SortOfFollow),"minWorldPoint").SetValue(null,site-Vector3.one*500);AccessTools.Field(typeof(SortOfFollow),"maxWorldPoint").SetValue(null,site+Vector3.one*500);dolf.transform.localScale=new Vector3(1,1,1);Physics.SyncTransforms();
                    object memory=Call("Observe",dolf);Check(memory!=null&&(bool)Get(memory,"Npc"),"native NPC priority actually detected");
                }
                Set(dai,"seenEnemyNum",0);queries.Clear();Entry(dai,"StartBombardment");
                Check(Count("GetPlayerPos:1")>0&&Count("GetNearestPlayer:5")>0&&!refResult&&refPlayer==0&&((IList)Get(dai,"bombardmentPositions")).Count==0,state+" actual Dolf ref nearest preserves current nearest and false return");
                if(state=="npc")foreach(GameObject f in fixtures.Where(f=>f!=null&&f.GetComponent<TestVanDammeAnim>()!=null&&f.GetComponent<TestVanDammeAnim>().playerNum==5).ToArray())UnityEngine.Object.DestroyImmediate(f);
            }
            Owners(dolf,PID.MyID);Toggle(false);Set(dai,"seenEnemyNum",1);Entry(dai,"StartBombardment");
            ScriptContext.Logger("Dolf replacement refResult="+refResult+" refPlayer="+refPlayer+" seen="+Get(dai,"seenEnemyNum")+" count="+((IList)Get(dai,"bombardmentPositions")).Count+" heroes="+hero.X+","+other.X);
            Check(refResult&&refPlayer==0&&((IList)Get(dai,"bombardmentPositions")).Count==11,"Dolf actual ref query changes farther current target and continues native bombardment");
            hero.health=0;Set(dai,"seenEnemyNum",0);queries.Clear();Entry(dai,"StartBombardment");
            Check(Count("GetPlayerPos:1")>0&&((IList)Get(dai,"bombardmentPositions")).Count==0&&(Vector3)Call("Point",0,dai)==Vector3.one*-1000,"dead target actual entry calls GetPlayerPos and retains invalid Vector3 semantics");hero.health=500;
            Unit kopter=Make("Terrorkopter",site);PolymorphicAI kai=kopter.GetComponent<PolymorphicAI>();
            hero.gameObject.SetActive(false);other.gameObject.SetActive(false);Set(kai,"seenEnemyNum",-1);Set(kai,"kopterPattern",KopterPattern.MovingLeft);Set(kai,"xMaxDescending",site.x+100);Set(kai,"xMin",site.x);queries.Clear();Entry(kai,"DoAlertedThink");
            Check(Count("GetActualNearestPlayer:4")>0&&Count("GetPlayerPos:3")>0&&kai.actionQueue.Any(a=>a.type==EnemyActionType.FacePoint&&a.gridPoint.collumn==Map.GetCollumn(0)),"actual kopter ref-point consumer keeps initialized zero refs for invalid target");
            object[] sentinel={-1,123f,456f,kai};CallArgs("PointRef",sentinel);Check((float)sentinel[1]==123f&&(float)sentinel[2]==456f,"invalid ref query preserves nonzero sentinel values");
            hero.gameObject.SetActive(true);other.gameObject.SetActive(true);
            Unit mech=Make("Terrorbot",site);GoliathMechAI mai=mech.GetComponent<GoliathMechAI>();
            hero.SetPosition(site+new Vector3(0,40,0));other.SetPosition(site+new Vector3(260,0,0));queries.Clear();
            Check(!(bool)Entry(mai,"IsPlayerOnTopOfMe")&&Count("GetNearestPlayer:4")>0&&Count("GetActualNearestPlayer:4")==0,"disabled actual Goliath on-top uses native last qualifying slot, not actual nearest");
            Toggle(true);hero.SetPosition(site+Vector3.left*100);other.SetPosition(site+Vector3.right*500);Physics.SyncTransforms();
            foreach(string state in new[]{"valid","weapon-conflict","restored","mirror"})
            {
                Owners(mech,PID.MyID);Call("Forget",mech);
                if(state=="weapon-conflict")((Mech)mech).weapon.SetOwner(new PID(99));
                if(state=="mirror"){Owners(mech,new PID(98));Check((bool)Call("Accept",mech,new PID(98),0,site+new Vector3(0,40,0),true),"legal mirror lock point accepted");}
                else Call("Observe",mech);
                queries.Clear();bool top=(bool)Entry(mai,"IsPlayerOnTopOfMe");
                Check(state=="weapon-conflict"?Count("GetNearestPlayer:4")>0:Count("GetNearestPlayer:4")==0,"actual Goliath query original-api use "+state);
                if(state=="mirror")Check(top,"actual mirror consumes copied lock point");
            }
            Owners(mech,PID.MyID);Call("Forget",mech);hero.SetPosition(site+Vector3.left*96);Physics.SyncTransforms();
            GameObject floor=Box(site+new Vector3(-40,-8,0),new Vector3(400,16,8));
            Set(mai,"originalGroundHeight",site.y);Set(mai,"minCollumn",mech.collumn-20);Set(mai,"maxCollumn",mech.collumn+20);
            object mem=Call("Observe",mech);Check(mem!=null&&(bool)Get(mem,"Visible"),"movement fixture has actual visible observation");
            Cycle(mai);ScriptContext.Logger("Goliath geometry bounds="+Call("BodyBounds",mech)+" site="+site+" actual="+mech.X+","+mech.Y+" grid="+mech.collumn+","+mech.row+" aim="+Get(mem,"Aim")+" queue="+string.Join(",",mai.actionQueue.Select(a=>a.type.ToString()).ToArray()));Check(mai.actionQueue.Any(a=>a.type==EnemyActionType.Hover)&&mai.actionQueue.Any(a=>a.type==EnemyActionType.Stomp),"safe actual Goliath cycle queues Hover/Stomp");
            bool[] input=Input(mai);Check(input[2]&&input[9],"native GetInput consumes real Hover as up plus special4");
            GameObject obstacle=Box(site+new Vector3(-48,70,0),new Vector3(12,12,8));
            foreach(bool support in new[]{true,false})
            {
                floor.SetActive(support);Physics.SyncTransforms();Set(mem,"HasCommit",false);Cycle(mai);
                Check(!mai.actionQueue.Any(a=>a.type==EnemyActionType.Hover||a.type==EnemyActionType.Stomp)&&mai.actionQueue.Any(a=>a.type==EnemyActionType.Fire&&a.duration==mai.attackTime)&&(MechAIPattern)Get(mai,"patternType")==MechAIPattern.Stomping&&(int)Get(mai,"patternIndex")==2,"unsafe movement chooses full stationary fire without consuming pattern/index support="+support);
                mai.actionQueue.RemoveAll(a=>a.type!=EnemyActionType.Fire);Check(Input(mai)[5],"native GetInput consumes safe alternative Fire support="+support);
            }
            hero.SetPosition(site+new Vector3(0,40,0));Set(mem,"NextSense",0f);Set(mem,"HasCommit",false);Call("Observe",mech);Cycle(mai);
            Check(mai.actionQueue.Any(a=>a.type==EnemyActionType.UseSpecial2)&&(int)Get(mai,"patternIndex")==2,"same unsafe geometry allows native stationary top defence without advancing pattern");
            mai.actionQueue.RemoveAll(a=>a.type!=EnemyActionType.UseSpecial2);Check(Input(mai)[7],"native GetInput consumes top defence special2");
            hero.SetPosition(site+Vector3.left*96);Set(mem,"NextSense",0f);Call("Observe",mech);Set(mai,"minCollumn",mech.collumn);Set(mai,"startY",site.y);Cycle(mai);
            Check(!mai.actionQueue.Any(a=>a.type==EnemyActionType.Hover)&&(int)Get(mai,"patternIndex")==3,"same unsafe geometry permits actual native arena-edge bombardment branch");
            ScriptContext.Logger("PASS "+passed+" focused fallback/action checks version="+typeof(EnemyAIEnhancement.Plugin).Assembly.GetName().Version);
        }
        finally
        {
            ScriptContext.Harmony.UnpatchAll(ScriptContext.Harmony.Id);HeroController.players[1]=old;
            AccessTools.Field(typeof(SortOfFollow),"minWorldPoint").SetValue(null,cameraMin);AccessTools.Field(typeof(SortOfFollow),"maxWorldPoint").SetValue(null,cameraMax);
            foreach(GameObject f in fixtures)if(f!=null)UnityEngine.Object.DestroyImmediate(f);fixtures.Clear();
            foreach(Projectile p in UnityEngine.Object.FindObjectsOfType<Projectile>().Where(p=>!prior.Contains(p)))UnityEngine.Object.DestroyImmediate(p.gameObject);
            hero.gameObject.SetActive(true);hero.SetPosition(original);hero.health=hp;hero.enabled=active;Toggle(enabled);Call("Reset");
        }
    }
    private static void Cycle(GoliathMechAI ai){Set(ai,"patternType",MechAIPattern.Stomping);Set(ai,"patternIndex",2);ai.actionQueue.Clear();Entry(ai,"DoAlertedThink");}
    private static Unit Make(string kind,Vector3 p)
    {
        Type t=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("RocketLib.Utils.UnitTypes")).First(v=>v!=null);
        Unit prefab=(Unit)t.GetMethod("GetUnitPrefab").Invoke(null,new object[]{Enum.Parse(t.Assembly.GetType("RocketLib.Utils.UnitType"),kind),-1,false});
        Unit b=UnityEngine.Object.Instantiate(prefab,p,Quaternion.identity);fixtures.Add(b.gameObject);foreach(MonoBehaviour c in b.GetComponentsInChildren<MonoBehaviour>(true))c.enabled=false;
        Owners(b,PID.MyID);Entry(b,"Start");foreach(Unit w in (IEnumerable<Unit>)Call("Weapons",b))Entry(w,"Start");b.transform.position=p;b.SetXY(b.transform);b.collumn=Map.GetCollumn(p.x);b.row=Map.GetRow(p.y);b.enabled=true;b.health=b.maxHealth=500;b.actionState=ActionState.Idle;b.yI=0;Set(b,"groundHeight",p.y);
        PolymorphicAI ai=b.GetComponent<PolymorphicAI>();ai.enabled=true;Set(ai,"firstFrame",false);ai.actionQueue.Clear();ai.mentalState=MentalState.Alerted;return b;
    }
    private static GameObject Box(Vector3 p,Vector3 size){GameObject g=new GameObject("EnemyAI.BossGeometry");fixtures.Add(g);g.layer=LayerMask.NameToLayer("Ground");g.transform.position=p;g.AddComponent<BoxCollider>().size=size;Physics.SyncTransforms();return g;}
    private static bool[] Input(PolymorphicAI ai){object[] a=Enumerable.Repeat((object)false,11).ToArray();Entry(ai,"GetInput",a);return a.Cast<bool>().ToArray();}
    private static void Owners(Unit b,PID owner){foreach(NetworkObject o in b.GetComponentsInChildren<NetworkObject>(true))o.SetOwner(owner);}
    private static object Entry(object o,string n,params object[] a){MethodInfo m=AccessTools.Method(o.GetType(),n);return m==null?null:m.Invoke(o,a);}
    private static object Call(string n,params object[] a){return CallArgs(n,a);}
    private static object CallArgs(string n,object[] a){return prod.GetMethod(n,flags).Invoke(null,a);}
    private static object Get(object o,string n){return AccessTools.Field(o.GetType(),n).GetValue(o);}
    private static void Set(object o,string n,object v){AccessTools.Field(o.GetType(),n).SetValue(o,v);}
    private static void Toggle(bool b){typeof(EnemyAIEnhancement.Plugin).GetMethod("Toggle",flags).Invoke(null,new object[]{null,b});}
    public static void Query(MethodBase __originalMethod){string key=__originalMethod.Name+":"+__originalMethod.GetParameters().Length;queries[key]=Count(key)+1;}
    public static void NearestReturned(bool __result,int __4){refResult=__result;refPlayer=__4;}
    private static int Count(string key){int n;return queries.TryGetValue(key,out n)?n:0;}
    private static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL "+name);passed++;ScriptContext.Logger("PASS "+name);}
}
