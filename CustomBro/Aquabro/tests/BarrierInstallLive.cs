using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
public static class BarrierInstallLive {
 const BindingFlags F=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static void Check(bool ok,string text) { if(!ok)throw new Exception(text); }
 static string Fingerprint() {
  var rows=new List<string>();
  foreach(var m in Harmony.GetAllPatchedMethods()) {
   var p=Harmony.GetPatchInfo(m);
   foreach(var v in p.Prefixes)rows.Add(m.DeclaringType.FullName+"."+m+"|pre|"+v.owner+"|"+v.PatchMethod+"|"+v.priority+"|"+v.index);
   foreach(var v in p.Postfixes)rows.Add(m.DeclaringType.FullName+"."+m+"|post|"+v.owner+"|"+v.PatchMethod+"|"+v.priority+"|"+v.index);
  }
  rows.Sort(StringComparer.Ordinal);return string.Join("\n",rows.ToArray());
 }
 public static void Main() {
  var hero=HeroController.players[0].character;
  Check(hero.GetType().FullName=="AquabroMod.Aquabro" && TestMod.Plugin.IsGamePaused,"Paused Aquabro required");
  var t=hero.GetType().Assembly.GetType("AquabroMod.WaterProjectileBarrier");
  var acid=(IList)t.GetField("AcidSlime",F).GetValue(null);
  var active=(IList)t.GetField("Active",F).GetValue(null);
  var saved=new object[acid.Count];acid.CopyTo(saved,0);
  var savedActive=new object[active.Count];active.CopyTo(savedActive,0);
  string prior=Fingerprint();
  var go=new GameObject("Barrier install active probe");
  var off=new GameObject("Barrier install inactive probe");off.SetActive(false);
  var slime=go.AddComponent<SlimeGroundCover>();var inactive=off.AddComponent<SlimeGroundCover>();
  string id="aquabro.round6.install.probe";var harmony=new Harmony(id);
  ScriptContext.Logger("MVID="+t.Assembly.ManifestModule.ModuleVersionId);
  ScriptContext.Logger("SCENE="+UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
  ScriptContext.Logger("CHARACTER="+hero.GetType().FullName);
  try {
   for(int pass=0;pass<2;pass++) {
    t.GetMethod("Install",F).Invoke(null,new object[]{harmony});
    var methods=new List<string>();int typeCount=0,missing=0,abstractCount=0;
    foreach(var type in typeof(Projectile).Assembly.GetTypes()) {
     if(!typeof(Projectile).IsAssignableFrom(type))continue;typeCount++;
     var method=type.GetMethod("RunProjectile",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.DeclaredOnly,null,new Type[]{typeof(float)},null);
     if(method==null){missing++;continue;}if(method.IsAbstract){abstractCount++;continue;}
     var patches=Harmony.GetPatchInfo(method);int count=0;
     foreach(var p in patches.Prefixes)if(p.owner==id) {
      count++;Check(p.PatchMethod==t.GetMethod("BeforeRun",F),"prefix method");
      Check(p.priority==Priority.Normal && p.before.Length==0 && p.after.Length==0,"prefix ordering");
     }
     Check(count==1,"prefix count");methods.Add(method.DeclaringType.FullName+"."+method);
    }
    int installed=0;
    foreach(var m in Harmony.GetAllPatchedMethods())foreach(var p in Harmony.GetPatchInfo(m).Prefixes)if(p.owner==id)installed++;
    Check(installed==methods.Count,"unexpected target");methods.Sort(StringComparer.Ordinal);
    foreach(var m in methods)ScriptContext.Logger("METHOD|"+pass+"|"+m);
    int setup=0;foreach(var p in Harmony.GetPatchInfo(typeof(SlimeGroundCover).GetMethod("Setup")).Postfixes)if(p.owner==id){setup++;Check(p.PatchMethod==t.GetMethod("RegisterSlime",F),"setup method");}
    Check(setup==1,"setup count");
    int countSlime=0;foreach(var value in acid)if(object.ReferenceEquals(value,slime))countSlime++;
    Check(countSlime==1 && !acid.Contains(inactive),"slime registration");
    Check(active.Count==savedActive.Length,"water count");for(int i=0;i<active.Count;i++)Check(object.ReferenceEquals(active[i],savedActive[i]),"water order");
    for(int i=0;i<saved.Length;i++)Check(object.ReferenceEquals(acid[i],saved[i]),"acid prior order");
    ScriptContext.Logger("CASE|"+pass+"|types="+typeCount+"|patched="+installed+"|missing="+missing+"|abstract="+abstractCount+"|setup="+setup+"|activeSlime="+countSlime+"|inactiveSlime=0|waterPreserved=True|priorOrder=True");
    foreach(var m in new List<MethodBase>(Harmony.GetAllPatchedMethods()))harmony.Unpatch(m,HarmonyPatchType.All,id);
    Check(Fingerprint()==prior,"original patches changed");
   }
   ScriptContext.Logger("COMPLETE=2; originalPatchesRestored=True");
  } finally {
   foreach(var m in new List<MethodBase>(Harmony.GetAllPatchedMethods()))harmony.Unpatch(m,HarmonyPatchType.All,id);
   acid.Clear();foreach(var value in saved)acid.Add(value);
   UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(off);
  }
 }
}
