using System;
using System.Reflection;
using UnityEngine;
public static class MaelstromSightLengthLive
{
 public static void Main() {
  TestVanDammeAnim owner=HeroController.players[0].character;
  if(owner.GetType().FullName!="AquabroMod.Aquabro" || !TestMod.Plugin.IsGamePaused) throw new Exception("Paused Aquabro required");
  MethodInfo method=owner.GetType().GetMethod("MaelstromLineOfSight",BindingFlags.NonPublic|BindingFlags.Instance);
  ScriptContext.Logger("MVID="+owner.GetType().Assembly.ManifestModule.ModuleVersionId);
  ScriptContext.Logger("SCENE="+UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
  ScriptContext.Logger("CHARACTER="+owner.GetType().FullName);
  float oldX=owner.X,oldY=owner.Y; try { owner.X=0f; owner.Y=6000f; int count=0;
  foreach(int direction in new int[]{-1,1})
  foreach(float slope in new float[]{0f,.5f,1f,-1f})
  foreach(int fixture in new int[]{0,1,2,3,4}) {
   GameObject wall=new GameObject("Sight length fixture");
   try {
    wall.layer=LayerMask.NameToLayer("Ground"); wall.transform.position=new Vector3(0f,6000f,0f);
    BoxCollider col=wall.AddComponent<BoxCollider>(); col.size=new Vector3(.25f,100f,16f);
    if(fixture==0) wall.SetActive(false);
    if(fixture==2) col.isTrigger=true;
    if(fixture==3) col.enabled=false;
    if(fixture==4) wall.AddComponent<DamageRelay>().unit=owner;
    foreach(float distance in new float[]{0f,1f,7.99f,8f,8.01f,11.99f,12f,12.01f,16f,64f}) {
     float x=owner.X-direction*distance, y=owner.Y+6f-slope*distance;
     wall.transform.position=new Vector3(x+direction*4f,y,0f);
     bool result=(bool)method.Invoke(owner,new object[]{x,y,owner});
     if(fixture!=1 && !result) throw new Exception("Ignored fixture obstructed");
     if(fixture==1 && distance==64f && result) throw new Exception("Solid wall missed");
     if(fixture==1 && distance==1f && !result) throw new Exception("Short bypass lost");
     ScriptContext.Logger("CASE|"+direction+"|"+slope+"|"+fixture+"|"+distance.ToString("R")+"|"+result); count++;
    }
   } finally { UnityEngine.Object.DestroyImmediate(wall); }
  }
  ScriptContext.Logger("COMPLETE="+count); } finally { owner.X=oldX; owner.Y=oldY; }
 }
}
