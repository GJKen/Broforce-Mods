using System;
using System.Reflection;
using UnityEngine;
public static class MaelstromFallbackLive {
 public static void Main(){
 var hero=HeroController.players[0].character;
 if(hero.GetType().FullName!="AquabroMod.Aquabro" || !TestMod.Plugin.IsGamePaused)throw new Exception("Paused Aquabro required");
 var method=hero.GetType().GetMethod("FindMaelstromDestination",BindingFlags.Instance|BindingFlags.NonPublic);
 float oldX=hero.X,oldY=hero.Y;int count=0;
 ScriptContext.Logger("MVID="+hero.GetType().Assembly.ManifestModule.ModuleVersionId);
 ScriptContext.Logger("SCENE="+UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
 ScriptContext.Logger("CHARACTER="+hero.GetType().FullName);
 try {hero.SetXY(0,6000);hero.SetPosition();
 foreach(float facing in new float[]{-1,1,0,-.5f,.5f})foreach(int fixture in new int[]{0,1,2,3,4})foreach(float distance in new float[]{1,8,16,35,69,70,71})foreach(int ground in new int[]{0,1,2}){
 var floor=new GameObject("Fallback probe floor");var wall=new GameObject("Fallback probe wall");
 try {
 floor.layer=wall.layer=LayerMask.NameToLayer("Ground");
 floor.transform.position=new Vector3(0,ground==2?5900:5960,0);floor.AddComponent<BoxCollider>().size=new Vector3(500,8,16);
 if(ground==0)floor.SetActive(false);
 float dir=facing<0?-1:1;
 wall.transform.position=new Vector3(dir*distance,6000,0);
 var col=wall.AddComponent<BoxCollider>();col.size=new Vector3(.125f,220,16);
 if(fixture==0)wall.SetActive(false);
 if(fixture==2)col.isTrigger=true;
 if(fixture==3)col.enabled=false;
 if(fixture==4)wall.AddComponent<DamageRelay>().unit=hero;
 Vector2 p=(Vector2)method.Invoke(hero,new object[]{facing});
 if(fixture==0 || fixture>=2 || facing==0){
 if(p.x!=facing*70 || p.y!=(ground==1?5964:6002))throw new Exception("Unobstructed fallback/floor mismatch "+p);
 }
 if(fixture==1 && Mathf.Abs(facing)==1 && distance==35 && ground==1 && (Mathf.Abs(p.x-dir*(35-.0625f-8))>.001f || p.y!=5964))throw new Exception("Wall setback mismatch "+p);
 ScriptContext.Logger("CASE|"+facing+"|"+fixture+"|"+distance+"|"+ground+"|"+p.x.ToString("R")+"|"+p.y.ToString("R"));count++;
 }finally{UnityEngine.Object.DestroyImmediate(wall);UnityEngine.Object.DestroyImmediate(floor);}
 }
 if(count!=525)throw new Exception("Coverage failed "+count);
 ScriptContext.Logger("COMPLETE|queries="+count);
 }finally{hero.SetXY(oldX,oldY);hero.SetPosition();}
 }
}
