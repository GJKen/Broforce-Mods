using System;
using System.Reflection;
using System.Text;
using UnityEngine;
public static class BoundsContactLive {
 const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 public static void Main(){
 var hero=HeroController.players[0].character;
 if(hero.GetType().FullName!="AquabroMod.Aquabro" || !TestMod.Plugin.IsGamePaused)throw new Exception("Paused Aquabro required");
 var t=hero.GetType().Assembly.GetType("AquabroMod.SkillContactMask");
 var m=t.GetMethod("IntersectsRect",F,null,new Type[]{typeof(Bounds)},null);
 ScriptContext.Logger("MVID="+t.Assembly.ManifestModule.ModuleVersionId);
 ScriptContext.Logger("SCENE="+UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
 ScriptContext.Logger("CHARACTER="+hero.GetType().FullName);
 int total=0,hits=0;
 for(int kind=0;kind<4;kind++)foreach(float face in new float[]{-1,1})foreach(float scale in new float[]{.65f,.85f,.9f})foreach(int frame in new int[]{-1,0,5,17,24,31,58}){
 object mask=Activator.CreateInstance(t,F,null,new object[]{kind,100f,200f,face,scale,frame},null);
 var bits=new StringBuilder();
 for(int i=0;i<160;i++){
 float cy=200f+new float[]{-10,0,1,1.0001f,16,48,96,130}[i%8];
 var b=new Bounds(new Vector3(100f+(i/8-10)*8.125f,cy,9),new Vector3(new float[]{0,1,8,32,80}[i%5],new float[]{0,2,16,64}[i%4],8));
 bool hit=(bool)m.Invoke(mask,new object[]{b}); bits.Append(hit?'1':'0');total++;if(hit)hits++;
 }
 ScriptContext.Logger("CASE|"+kind+"|"+face+"|"+scale+"|"+frame+"|"+bits);
 }
 if(hits==0 || hits==total)throw new Exception("Invalid coverage");
 ScriptContext.Logger("COMPLETE|queries="+total+"|hits="+hits);
 }
}
