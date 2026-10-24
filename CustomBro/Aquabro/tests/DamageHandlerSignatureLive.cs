using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
public class SignatureNone : MonoBehaviour { }
public class SignatureNoneDeep : SignatureNone { }
public class SignaturePublic : MonoBehaviour { public void Damage(DamageObject d) { } }
public class SignaturePublicDeep : SignaturePublic { }
public class SignaturePrivate : MonoBehaviour { private void Damage(DamageObject d) { } }
public class SignaturePrivateDeep : SignaturePrivate { }
public class SignatureWrong : MonoBehaviour { public void Damage(int d) { } public static void Damage(DamageObject d) { } }
public class SignatureHiddenWrong : SignaturePrivateDeep { public void Damage(string d) { } }
public static class DamageHandlerSignatureLive {
 const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
 public static void Main() {
 var owner=HeroController.players[0].character;
 if(owner.GetType().FullName!="AquabroMod.Aquabro" || !TestMod.Plugin.IsGamePaused) throw new Exception("Paused Aquabro required");
 var t=owner.GetType().Assembly.GetType("AquabroMod.CollisionDamageTarget");
 var method=t.GetMethod("HasDamageHandler",F);
 var cache=(Dictionary<Type,bool>)t.GetField("DamageHandlers",F).GetValue(null);
 Type[] types={typeof(SignatureNone),typeof(SignatureNoneDeep),typeof(SignaturePublic),typeof(SignaturePublicDeep),typeof(SignaturePrivate),typeof(SignaturePrivateDeep),typeof(SignatureWrong),typeof(SignatureHiddenWrong)};
 bool[] expected={false,false,true,true,true,true,false,true};
 var objects=new List<GameObject>();
 ScriptContext.Logger("MVID="+t.Assembly.ManifestModule.ModuleVersionId);
 ScriptContext.Logger("SCENE="+UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
 ScriptContext.Logger("CHARACTER="+owner.GetType().FullName);
 try {
 for(int i=0;i<types.Length;i++) {
 var go=new GameObject("Aquabro signature probe");objects.Add(go);go.AddComponent(types[i]);
 cache.Remove(types[i]);
 for(int pass=0;pass<2;pass++) {
 bool actual=(bool)method.Invoke(null,new object[]{go});
 if(actual!=expected[i] || cache[types[i]]!=expected[i]) throw new Exception("Signature mismatch "+types[i]+" pass "+pass);
 ScriptContext.Logger("CASE|"+types[i].Name+"|pass="+pass+"|handler="+actual+"|cache="+cache[types[i]]);
 }
 }
 // Preserve component iteration: non-handler followed by an inherited private handler.
 var mixed=new GameObject("Aquabro mixed signature probe");objects.Add(mixed);mixed.AddComponent<SignatureNoneDeep>();mixed.AddComponent<SignaturePrivateDeep>();
 if(!(bool)method.Invoke(null,new object[]{mixed}))throw new Exception("Mixed handler missing");
 ScriptContext.Logger("CASE|mixed|handler=True");
 ScriptContext.Logger("COMPLETE=17");
 } finally {foreach(var go in objects)UnityEngine.Object.DestroyImmediate(go);foreach(var type in types)cache.Remove(type);}
 }
}
