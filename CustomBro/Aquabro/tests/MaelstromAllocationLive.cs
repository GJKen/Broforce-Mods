using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
public static class MaelstromAllocationLive {
 const BindingFlags F=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
 public static void Main() {
 var owner=HeroController.players[0].character;
 if(owner.GetType().FullName!="AquabroMod.Aquabro" || !TestMod.Plugin.IsGamePaused) throw new Exception("Paused Aquabro required");
 var pull=owner.GetType().GetMethod("SkillMaelstromPullAtTime",F);
 var attract=owner.GetType().GetMethod("MaelstromAttractor",F);
 Mook source=null; foreach(var m in UnityEngine.Object.FindObjectsOfType<Mook>()) if(m is MookTrooper) {source=m;break;}
 if(source==null) throw new Exception("Native Mook required");
 ScriptContext.Logger("MVID="+owner.GetType().Assembly.ManifestModule.ModuleVersionId);
 ScriptContext.Logger("SCENE="+UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
 int count=0;
 foreach(int facing in new int[]{-1,1}) foreach(bool pulse in new bool[]{false,true}) foreach(float delta in new float[]{0f,1f/120f,1f/60f,1f/30f}) foreach(int frame in new int[]{-1,10,23,24}) {
 GameObject go=null; Mook target=null;
 try {
 go=UnityEngine.Object.Instantiate(source.gameObject); target=go.GetComponent<Mook>();
 target.SetXY(6000+facing*20,6030);target.SetPosition();target.xI=13;target.yI=-7;target.health=100;target.invulnerable=false;target.playerNum=-1;go.SetActive(true);
 if(!Map.units.Contains(target)) Map.RegisterUnit(target,false);
 var caught=new HashSet<object>();caught.Add(target);
 Vector2 destination=(Vector2)attract.Invoke(null,new object[]{target,6000f,6000f,.85f,.5f});
 float vx=Mathf.MoveTowards(13,Mathf.Clamp((destination.x-target.X)*6,-170,170),delta*1200);
 float vy=Mathf.MoveTowards(-7,Mathf.Clamp((destination.y-target.Y-6)*7,-160,260),delta*2400);
 pull.Invoke(owner,new object[]{6000f,6000f,(float)facing,.85f,frame,delta,pulse,caught,.5f});
 bool active=delta>0 && frame>=0 && frame<24;
 bool velocity=active ? Mathf.Abs(target.xI-vx)<.001f && Mathf.Abs(target.yI-vy)<.001f : target.xI==13 && target.yI==-7;
 if(!velocity) throw new Exception("Velocity mismatch "+target.xI+","+target.yI+" expected "+vx+","+vy);
 ScriptContext.Logger("CASE|"+facing+"|"+pulse+"|"+delta.ToString("R")+"|"+frame+"|hp="+target.health+"|caught="+caught.Contains(target)+"|velocity="+velocity);count++;
 } finally {if(target!=null) Map.RemoveUnit(target);if(go!=null) UnityEngine.Object.DestroyImmediate(go);}
 }
 ScriptContext.Logger("COMPLETE="+count);
 }
}
