using System;
using System.Collections;
using System.Reflection;
using System.Text;
using UnityEngine;
public static class BarrierMotionLive {
 const BindingFlags F=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
 public static void Main() {
  var hero=HeroController.players[0].character;
  if(hero.GetType().FullName!="AquabroMod.Aquabro" || !TestMod.Plugin.IsGamePaused)throw new Exception("Paused Aquabro required");
  var t=hero.GetType().Assembly.GetType("AquabroMod.WaterProjectileBarrier");
  var active=(IList)t.GetField("Active",F).GetValue(null);int prior=active.Count;
  var intersect=t.GetMethod("Intersects",F);var wall=new GameObject("Barrier motion probe wall");
  wall.layer=LayerMask.NameToLayer("Ground");wall.transform.position=new Vector3(10020,10048,0);
  var collider=wall.AddComponent<BoxCollider>();collider.size=new Vector3(4,240,16);
  ScriptContext.Logger("MVID="+t.Assembly.ManifestModule.ModuleVersionId);
  ScriptContext.Logger("SCENE="+UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
  ScriptContext.Logger("CHARACTER="+hero.GetType().FullName);
  int total=0,hits=0;
  try {
   for(int kind=0;kind<4;kind++)foreach(float face in new float[]{-1,1})foreach(float scale in new float[]{.65f,.85f})
   for(int fixture=0;fixture<3;fixture++)for(int motion=0;motion<5;motion++) {
    collider.enabled=fixture!=0;collider.isTrigger=fixture==2;
    float mx=new float[]{0,4,-4,.00001f,48.125f}[motion],my=new float[]{0,-4,4,.00001f,-16.25f}[motion];
    object water=Activator.CreateInstance(t,F,null,new object[]{hero,kind,10000f-mx,10000f-my,face,scale},null);
    try {
     t.GetMethod("MoveTo",F).Invoke(water,new object[]{10000f,10000f});
     var bits=new StringBuilder();
     foreach(int frame in new int[]{-1,0,5,17,24,31,58}) {
      t.GetMethod("SetFrame",F).Invoke(water,new object[]{frame});
      for(int i=0;i<32;i++) {
       float px=10000f+(i%8-4)*16.125f,py=10000f+(i/8)*32.125f;
       float dx=new float[]{0,.000001f,120,-120}[i%4];float dy=new float[]{0,80,-80,1f/120f}[i%4];
       float radius=new float[]{0,.5f,4,8}[i%4];
       bool hit=(bool)intersect.Invoke(water,new object[]{px,py,dx,dy,radius});bits.Append(hit?'1':'0');total++;if(hit)hits++;
      }
     }
     ScriptContext.Logger("CASE|"+kind+"|"+face+"|"+scale+"|"+fixture+"|"+motion+"|"+bits);
    } finally {t.GetMethod("Dispose",F).Invoke(water,null);}
   }
   if(active.Count!=prior || hits==0 || hits==total)throw new Exception("Invalid coverage or leaked barrier");
   ScriptContext.Logger("COMPLETE|queries="+total+"|hits="+hits+"|barriersRestored=True");
  } finally {UnityEngine.Object.DestroyImmediate(wall);}
 }
}
