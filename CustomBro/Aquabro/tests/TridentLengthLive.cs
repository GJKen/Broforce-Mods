using System;
using System.Reflection;
using UnityEngine;
public static class TridentLengthLive
{
    static string V(Vector3 v) { return v.x.ToString("R")+","+v.y.ToString("R")+","+v.z.ToString("R"); }
    public static void Main()
    {
        TestVanDammeAnim owner=HeroController.players[0].character;
        if(owner.GetType().FullName!="AquabroMod.Aquabro" || !TestMod.Plugin.IsGamePaused) throw new Exception("Paused Aquabro required");
        MethodInfo method=owner.GetType().GetMethod("FindTridentTerrainContactExcept",BindingFlags.NonPublic|BindingFlags.Static);
        ScriptContext.Logger("MVID="+owner.GetType().Assembly.ManifestModule.ModuleVersionId);
        ScriptContext.Logger("SCENE="+UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        ScriptContext.Logger("CHARACTER="+owner.GetType().FullName);
        int count=0;
        foreach(int direction in new int[]{-1,1})
        foreach(float slope in new float[]{0f,.5f,1f,-1f})
        foreach(int fixture in new int[]{0,1,2,3,4,5,6,7,8})
        {
            GameObject wall=new GameObject("Trident near"), far=new GameObject("Trident far");
            try {
                wall.layer=far.layer=LayerMask.NameToLayer("Ground");
                wall.transform.position=new Vector3(direction*4f,6000f,0f);
                far.transform.position=new Vector3(direction*8f,6000f,0f);
                BoxCollider col=wall.AddComponent<BoxCollider>(); col.size=new Vector3(.25f,100f,16f);
                BoxCollider second=far.AddComponent<BoxCollider>(); second.size=new Vector3(.25f,100f,16f);
                far.SetActive(fixture==7 || fixture==8);
                if(fixture==0) wall.SetActive(false);
                if(fixture==2) col.isTrigger=true;
                if(fixture==3) col.enabled=false;
                if(fixture==5) wall.AddComponent<TridentLengthReceiver>();
                if(fixture==6) wall.AddComponent<DamageRelay>().unit=owner;
                Collider receiver=fixture==4 || fixture==8 ? col : null;
                if(fixture==5) { SphereCollider r=wall.AddComponent<SphereCollider>(); r.enabled=false; receiver=r; }
                foreach(float distance in new float[]{0f,.000009f,.00001f,.000011f,.009f,.01f,.011f,1f,3.874f,3.875f,3.88f,3.885f,3.886f,4f,7.874f,7.875f,8f,12f,64f}) {
                    object[] args={0f,6000f,direction*distance,6000f+slope*distance,receiver,new RaycastHit()};
                    bool result=(bool)method.Invoke(null,args); RaycastHit hit=(RaycastHit)args[5];
                    if((fixture==0 || fixture==2 || fixture==3 || fixture==4 || fixture==5 || fixture==6) && result) throw new Exception("Ignored fixture hit");
                    if((fixture==1 || fixture==7 || fixture==8) && distance==64f && !result) throw new Exception("Missing wall");
                    if(result && fixture==7 && hit.collider!=col) throw new Exception("Nearest lost");
                    if(result && fixture==8 && hit.collider!=second) throw new Exception("Receiver exemption lost");
                    ScriptContext.Logger("CASE|"+direction+"|"+slope+"|"+fixture+"|"+distance.ToString("R")+"|"+result+"|"+(hit.collider==null?"none":hit.collider==col?"near":"far")+"|"+hit.distance.ToString("R")+"|"+V(hit.point)+"|"+V(hit.normal)); count++;
                }
            } finally { UnityEngine.Object.DestroyImmediate(wall); UnityEngine.Object.DestroyImmediate(far); }
        }
        ScriptContext.Logger("COMPLETE="+count);
    }
}
public class TridentLengthReceiver:MonoBehaviour { public void Damage(DamageObject d) {} }
