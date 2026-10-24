using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
public static class SpringStepLive {
 public static void Main(){
 var hero=HeroController.players[0].character;
 if(hero.GetType().FullName!="AquabroMod.Aquabro" || !TestMod.Plugin.IsGamePaused)throw new Exception("Paused Aquabro required");
 var method=hero.GetType().GetMethod("FindSpringColumn",BindingFlags.Instance|BindingFlags.NonPublic);
 float oldX=hero.X,oldY=hero.Y;int total=0,accepted=0,rejected=0;
 ScriptContext.Logger("MVID="+hero.GetType().Assembly.ManifestModule.ModuleVersionId);
 ScriptContext.Logger("SCENE="+UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
 ScriptContext.Logger("CHARACTER="+hero.GetType().FullName);
 try{
 foreach(float origin in new float[]{0,10000})foreach(int dir in new int[]{-1,1})for(int fixture=0;fixture<7;fixture++){
 var objects=new List<GameObject>();
 try{
 hero.SetXY(origin,origin);hero.SetPosition();
 if(fixture>0){
 var ground=new GameObject("Spring probe floor");objects.Add(ground);ground.layer=LayerMask.NameToLayer("Ground");ground.transform.position=new Vector3(origin,origin-4,0);ground.AddComponent<BoxCollider>().size=new Vector3(220,8,16);
 if(fixture>=2 && fixture<=5){var wall=new GameObject("Spring probe obstacle");objects.Add(wall);wall.layer=ground.layer;float height=fixture==2?16:fixture==3?16.125f:40;wall.transform.position=new Vector3(origin+dir*36,origin+height/2,0);var c=wall.AddComponent<BoxCollider>();c.size=new Vector3(24,height,16);c.isTrigger=fixture==5;}
 if(fixture==6){ground.transform.position=new Vector3(origin-dir*55,origin-4,0);ground.GetComponent<BoxCollider>().size=new Vector3(110,8,16);var lower=new GameObject("Spring probe lower floor");objects.Add(lower);lower.layer=ground.layer;lower.transform.position=new Vector3(origin+dir*55,origin-20,0);lower.AddComponent<BoxCollider>().size=new Vector3(110,8,16);}
 }
 foreach(float distance in new float[]{0,.000001f,.000009f,.00001f,.000011f,.001f,4,8,16,24,30,60,90}){
 object[] args={origin+dir*distance,new Vector2()};bool ok=(bool)method.Invoke(hero,args);Vector2 p=(Vector2)args[1];
 if(float.IsNaN(p.x)||float.IsNaN(p.y))throw new Exception("NaN");total++;if(ok)accepted++;else rejected++;
 ScriptContext.Logger("CASE|"+origin+"|"+dir+"|"+fixture+"|"+distance.ToString("R")+"|"+ok+"|"+p.x.ToString("R")+"|"+p.y.ToString("R"));
 }
 }finally{foreach(var go in objects)UnityEngine.Object.DestroyImmediate(go);}
 }
 if(accepted==0||rejected==0||total!=364)throw new Exception("Invalid coverage");
 ScriptContext.Logger("COMPLETE|queries="+total+"|accepted="+accepted+"|rejected="+rejected);
 }finally{hero.SetXY(oldX,oldY);hero.SetPosition();}
 }
}
