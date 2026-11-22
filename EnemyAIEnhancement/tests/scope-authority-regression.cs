// #name Scope And Authority Regression
// #description Focused installed-DLL checks with actual native queries; controlled fixtures are not online combat.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

public static class ScopeAuthorityRegression
{
    private static readonly BindingFlags hidden=BindingFlags.Static|BindingFlags.NonPublic;
    private static readonly Type controller=typeof(EnemyAIEnhancement.Plugin).Assembly.GetType("EnemyAIEnhancement.Controller");
    private static readonly List<GameObject> fixtures=new List<GameObject>();
    private static int passed,spawns;
    public static void Main()
    {
        passed=0;spawns=0;
        if(!Connect.IsOffline||!HeroController.PlayerIsAlive(0))throw new Exception("Offline healthy player required.");
        TestVanDammeAnim hero=HeroController.players[0].character;bool enabled=(bool)typeof(EnemyAIEnhancement.Plugin).GetField("Enabled",hidden).GetValue(null);
        try
        {
            Toggle(true);
            AlienMosquito mosquito=(AlienMosquito)Make("ZAlienMosquito",new Vector3(hero.X-64f,hero.Y+64f,0f));
            mosquito.flyingInvisibleParachute.gameObject.SetActive(true);Physics.SyncTransforms();
            Collider support=mosquito.flyingInvisibleParachute.GetComponent<Collider>();
            float h=Mathf.Max(mosquito.height,mosquito.headHeight);Vector3 origin=new Vector3(mosquito.X,mosquito.Y+1f+h*0.5f,0f),extents=new Vector3(mosquito.HalfWidth,h*0.5f,1f);
            Collider[] overlaps=Physics.OverlapBox(origin,extents,Quaternion.identity,Map.groundLayer|Map.fragileLayer);
            ScriptContext.Logger("SUPPORT reference="+support.name+" parentIsSource="+(support.transform.parent==mosquito.transform)+" overlap="+overlaps.Contains(support)+" all="+string.Join("|",overlaps.Select(c=>c.name+":"+c.gameObject.layer).ToArray()));
            Ray ray=new Ray(new Vector3(mosquito.X,mosquito.Y+8f,0f),new Vector3(1,-1,0).normalized);
            ScriptContext.Logger("DIAGONAL hits="+string.Join("|",Physics.SphereCastAll(ray,mosquito.HalfWidth,120f,Map.groundLayer|Map.fragileLayer).Select(hit=>hit.collider.name+":own="+(hit.collider==support)+":distance="+hit.distance).ToArray()));
            if(ScriptContext.GetArg("mode","")=="diagnose")return;
            Check(overlaps.Contains(support),"actual native support occupies unchanged full body");
            Check((bool)Call("SoulAirSpace",mosquito,0f,16f),"own flight support no longer blocks full body climb");
            Check((bool)Call("MosquitoDiveClear",mosquito,ray.direction,8f),"own flight support no longer blocks diagonal opportunity");
            Check(!(bool)Call("MosquitoDiveClear",mosquito,ray.direction,120f),"native terrain still blocks diagonal opportunity");
            GameObject obstacle=new GameObject("EnemyAI.ScopeObstacle");fixtures.Add(obstacle);obstacle.layer=support.gameObject.layer;
            BoxCollider box=obstacle.AddComponent<BoxCollider>();box.size=new Vector3(2,2,4);obstacle.transform.position=origin+Vector3.up*24f;Physics.SyncTransforms();
            Check(!(bool)Call("SoulAirSpace",mosquito,0f,16f),"same layer real obstacle still blocks full body sweep");
            obstacle.transform.position=origin;Physics.SyncTransforms();Check(!(bool)Call("SoulAirSpace",mosquito,0f,0f),"real initial overlap still blocks");
            obstacle.transform.position=ray.origin+ray.direction*8f;Physics.SyncTransforms();Check(!(bool)Call("MosquitoDiveClear",mosquito,ray.direction,8f),"same layer real obstacle still blocks diagonal opportunity");
            UnityEngine.Object.DestroyImmediate(obstacle);UnityEngine.Object.DestroyImmediate(mosquito.gameObject);

            ScriptContext.Harmony.Patch(AccessTools.Method(typeof(MapController),"SpawnMook_Networked"),new HarmonyMethod(typeof(ScopeAuthorityRegression),"CountSpawn"));
            MookGeneral general=(MookGeneral)Make("ZMookGeneral",new Vector3(hero.X-64f,hero.Y,0));
            Mook trooper=Make("ZMook",new Vector3(hero.X-64f,hero.Y,0));
            Mook alarmist=Make("ZMook Alarmist",new Vector3(hero.X-80f,hero.Y,0));
            Mook prefab=InstantiationController.PrefabList.OfType<GameObject>().First(g=>g.name=="ZMook").GetComponent<Mook>();
            foreach(string boundary in new[]{"legal","mirror","unitOwner","aiOwner","player","trigger","editor","disabled","alarmist"})
            {
                MookGeneral source=boundary=="alarmist"?(MookGeneral)alarmist:general;source.SetOwner(PID.MyID);source.enemyAI.SetOwner(PID.MyID);
                if(boundary=="mirror"||boundary=="unitOwner")source.SetOwner(new PID(99));
                if(boundary=="mirror"||boundary=="aiOwner")source.enemyAI.SetOwner(new PID(99));
                source.playerNum=boundary=="player"?0:-1;source.beingControlledByTriggerAction=boundary=="trigger";
                bool editing=Map.isEditing;Map.isEditing=boundary=="editor";Toggle(boundary!="disabled");int before=spawns;
                try{AccessTools.Method(typeof(MookGeneral),"SpawnMook").Invoke(source,new object[]{prefab,source.X,source.Y+100f});}
                finally{Map.isEditing=editing;}
                Check(spawns-before==((boundary=="mirror"||boundary=="unitOwner"||boundary=="aiOwner")?0:1),"native SpawnMook authority "+boundary);
                source.SetOwner(PID.MyID);source.enemyAI.SetOwner(PID.MyID);source.playerNum=-1;source.beingControlledByTriggerAction=false;Toggle(true);
            }
            foreach(bool mine in new[]{true,false})
            {
                general.SetOwner(mine?PID.MyID:new PID(99));general.enemyAI.SetOwner(mine?PID.MyID:new PID(99));Set(general,"lastSpawnTime",Time.time-4f);int before=spawns;
                AccessTools.Method(typeof(MookGeneral),"UseSpecial").Invoke(general,null);float time=(float)Get(general,"lastSpawnTime");
                AccessTools.Method(typeof(MookGeneral),"UseSpecial").Invoke(general,null);
                Check(spawns-before==(mine?3:0)&&Mathf.Abs(time-Time.time)<0.01f,"native three-child/3s gate and mirror presentation mine="+mine);
            }
            general.SetOwner(PID.MyID);general.enemyAI.SetOwner(PID.MyID);
            foreach(Mook source in new[]{(Mook)general,trooper})
            {
                Query(source,hero);Check(!(bool)Call("PreferNativeNPC",source.enemyAI,source,false,Vector2.zero),"real hero excluded "+source.GetType().Name);
                foreach(int unknown in new[]{-10,-1})
                {
                    object memory=Call("GetMemory",source.enemyAI,source);Set(memory,"SharedPending",true);Set(memory,"Player",0);Set(memory,"SeenAt",Time.time);
                    object[] args={source.enemyAI,source,unknown};controller.GetMethod("SharedPlayer",hidden).Invoke(null,args);Check((int)args[2]==0,"shared target recovery "+unknown+" "+source.GetType().Name);
                }
            }
            GameObject villagerPrefab=InstantiationController.PrefabList.OfType<GameObject>().First(g=>g.name=="Denizen1");
            Villager villager=UnityEngine.Object.Instantiate(villagerPrefab,new Vector3(hero.X-32f,hero.Y,0),Quaternion.identity).GetComponent<Villager>();fixtures.Add(villager.gameObject);villager.enabled=false;
            AccessTools.Method(typeof(Villager),"Start").Invoke(villager,null);
            foreach(bool armed in new[]{false,true})
            {
                if(armed)AccessTools.Method(typeof(Villager),"ArmVillagerRPC").Invoke(villager,new object[]{hero.playerNum});
                Check(!villager.isHero&&villager.playerNum==(armed?hero.playerNum:5)&&villager.hasGun==armed,"actual native villager faction/gun armed="+armed);
                foreach(Mook source in new[]{(Mook)general,trooper})
                {
                    Query(source,villager);Check((bool)Call("PreferNativeNPC",source.enemyAI,source,true,(Vector2)hero.transform.position),"real NPC retained "+source.GetType().Name+" armed="+armed);
                    source.enemyAI.StopAllActions();source.enemyAI.SetMentalState(MentalState.Alerted);object memory=Call("GetMemory",source.enemyAI,source);Set(memory,"Player",0);Set(memory,"SeenAt",Time.time);Set(memory,"Plan",Enum.Parse(memory.GetType().GetField("Plan",BindingFlags.Instance|BindingFlags.NonPublic).FieldType,"Search"));
                    object[] tick={source.enemyAI,source,0,true};bool handled=(bool)controller.GetMethod(source==general?"CommandTick":"GroundTick",hidden).Invoke(null,tick);
                    Check(!handled&&((IDictionary)controller.GetField("memories",hidden).GetValue(null))[source.enemyAI]==null,"entrypoint returns actual NPC selection to native "+source.GetType().Name+" armed="+armed);
                }
            }
            ScriptContext.Logger("PASS "+passed+" scope/authority assertions on "+typeof(EnemyAIEnhancement.Plugin).Assembly.GetName().Version);
        }
        finally
        {
            ScriptContext.Harmony.UnpatchAll("script.scope-authority-regression");Call("Reset",false);
            foreach(GameObject go in fixtures)if(go!=null)UnityEngine.Object.DestroyImmediate(go);fixtures.Clear();Toggle(enabled);
        }
    }
    public static bool CountSpawn(ref Mook __result){spawns++;__result=null;return false;}
    private static Mook Make(string name,Vector3 position)
    {
        GameObject prefab=InstantiationController.PrefabList.OfType<GameObject>().First(g=>g.name==name);Mook m=UnityEngine.Object.Instantiate(prefab,position,Quaternion.identity).GetComponent<Mook>();fixtures.Add(m.gameObject);m.enabled=false;
        AccessTools.Method(m.GetType(),"Start").Invoke(m,null);m.SetOwner(PID.MyID);m.enemyAI.SetOwner(PID.MyID);m.collumn=Map.GetCollumn(m.X);m.row=Map.GetRow(m.Y+8f);m.groundHeight=m.Y;m.yI=0;m.actionState=ActionState.Idle;
        m.enemyAI.AddAction(EnemyActionType.Wait,100f);AccessTools.Method(typeof(PolymorphicAI),"Update").Invoke(m.enemyAI,null);m.enemyAI.StopAllActions();m.enemyAI.ForceFacingDirection(1);Check(!(bool)Get(m.enemyAI,"firstFrame"),"actual initialization "+name);return m;
    }
    private static void Query(Mook source,Unit expected){int faction=int.MinValue;float x=0,y=0;bool found=Map.CanSeeNPCEnemyUnit(source.X,source.Y,1,source.enemyAI.sightRangeX,source.enemyAI.sightRangeY,source.playerNum,ref faction,ref x,ref y);Check(found&&faction==expected.playerNum&&x==expected.X&&y==expected.Y,"unmodified native query returns "+expected.GetType().Name+" faction="+faction+" pos="+x+","+y);}
    private static object Get(object value,string name){return AccessTools.Field(value.GetType(),name).GetValue(value);}
    private static void Set(object value,string name,object field){AccessTools.Field(value.GetType(),name).SetValue(value,field);}
    private static object Call(string method,params object[] args){return controller.GetMethod(method,hidden).Invoke(null,args);}
    private static void Toggle(bool enabled){typeof(EnemyAIEnhancement.Plugin).GetMethod("Toggle",hidden).Invoke(null,new object[]{null,enabled});}
    private static void Check(bool ok,string message){if(!ok)throw new Exception("FAIL "+message);passed++;ScriptContext.Logger("PASS "+message);}
}
