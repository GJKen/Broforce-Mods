using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
public static class HitRegistrationLive {
 const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
 public static void Main(){
 var owner=HeroController.players[0].character;
 if(owner.GetType().FullName!="AquabroMod.Aquabro" || !TestMod.Plugin.IsGamePaused) throw new Exception("Paused Aquabro required");
 var t=owner.GetType().Assembly.GetType("AquabroMod.CollisionDamageTarget");
 var already=t.GetMethod("AlreadyHit",F); var register=t.GetMethod("Register",F); var keys=t.GetMethod("DamageKeys",F);
 ScriptContext.Logger("MVID="+t.Assembly.ManifestModule.ModuleVersionId);
 ScriptContext.Logger("SCENE="+UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
 ScriptContext.Logger("CHARACTER="+owner.GetType().FullName);
 var objects=new List<GameObject>();
 try {
 var a=new GameObject("Aquabro registration A");objects.Add(a);a.AddComponent<MeshRenderer>();var p=a.AddComponent<BossPiece>();
 var b=new GameObject("Aquabro registration B");objects.Add(b);b.AddComponent<MeshRenderer>();var q=b.AddComponent<BossPiece>();
 var dead=new GameObject("Aquabro registration destroyed"); var destroyed=dead.AddComponent<BoxCollider>();UnityEngine.Object.DestroyImmediate(dead);
 object[] targets={null,new object(),owner,destroyed,p,p,p};
 int count=0;
 for(int scenario=0;scenario<targets.Length;scenario++) {
 p.connectedPieces=scenario==4?null:new List<BossPiece>();
 if(scenario==6){p.connectedPieces.Add(q);p.connectedPieces.Add(null);p.connectedPieces.Add(q);p.connectedPieces.Add(p);}
 object target=targets[scenario]; var expectedKeys=(object[])keys.Invoke(null,new object[]{target});
 for(int seed=0;seed<3;seed++) {
 var hits=new HashSet<object>(); if(seed==1)hits.Add(target);if(seed==2)hits.Add(q);
 bool expected=false; foreach(var key in expectedKeys)if(hits.Contains(key))expected=true;
 bool actual=(bool)already.Invoke(null,new object[]{target,hits});if(actual!=expected)throw new Exception("AlreadyHit mismatch");
 var expectedSet=new HashSet<object>(hits);foreach(var key in expectedKeys)expectedSet.Add(key);
 register.Invoke(null,new object[]{target,hits});if(!hits.SetEquals(expectedSet))throw new Exception("Register mismatch");
 bool repeat=(bool)already.Invoke(null,new object[]{target,hits});if(!repeat)throw new Exception("Repeat mismatch");
 register.Invoke(null,new object[]{target,hits});if(!hits.SetEquals(expectedSet))throw new Exception("Duplicate mismatch");
 ScriptContext.Logger("CASE|"+scenario+"|"+seed+"|keys="+expectedKeys.Length+"|before="+actual+"|count="+hits.Count+"|repeat="+repeat);count++;
 }
 }
 ScriptContext.Logger("COMPLETE="+count);
 }finally{foreach(var go in objects)UnityEngine.Object.DestroyImmediate(go);}
 }
}
