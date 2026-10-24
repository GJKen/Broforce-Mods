using System;
using System.Reflection;
using UnityEngine;
public static class SkillLengthLive
{
    public static void Main()
    {
        TestVanDammeAnim owner = HeroController.players[0].character;
        if (owner.GetType().FullName != "AquabroMod.Aquabro" || !TestMod.Plugin.IsGamePaused) throw new Exception("Paused Aquabro required");
        MethodInfo method = owner.GetType().GetMethod("SkillContactVisible", BindingFlags.Static | BindingFlags.NonPublic, null, new Type[] {typeof(float),typeof(float),typeof(float),typeof(float),typeof(bool),typeof(Collider)}, null);
        ScriptContext.Logger("MVID=" + owner.GetType().Assembly.ManifestModule.ModuleVersionId);
        ScriptContext.Logger("SCENE=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        ScriptContext.Logger("CHARACTER=" + owner.GetType().FullName);
        int count = 0;
        foreach (int direction in new int[] {-1,1})
        foreach (float slope in new float[] {0f,.5f,1f,-1f})
        foreach (int fixture in new int[] {0,1,2,3,4,5,6})
        {
            GameObject wall = new GameObject("Aquabro length probe");
            try
            {
                wall.layer = LayerMask.NameToLayer("Ground");
                wall.transform.position = new Vector3(direction*4f,6000f,0f);
                BoxCollider col = wall.AddComponent<BoxCollider>(); col.size = new Vector3(.25f,100f,16f);
                if (fixture == 0) wall.SetActive(false);
                if (fixture == 2) col.isTrigger = true;
                if (fixture == 3) col.enabled = false;
                if (fixture == 6) wall.AddComponent<DamageRelay>();
                if (fixture == 5) wall.AddComponent<SkillLengthReceiver>();
                foreach (bool hasReceiver in new bool[] {false,true})
                foreach (bool ignoreProps in new bool[] {false,true})
                foreach (float distance in new float[] {0f,.000009f,.00001f,.000011f,.009f,.01f,.011f,1f,3.874f,3.875f,3.88f,3.885f,3.886f,4f,7.999f,8f,8.001f,12f,64f})
                {
                    Collider receiver = fixture == 4 ? col : (hasReceiver ? owner.GetComponent<Collider>() : null);
                    if (fixture == 5) receiver = wall.AddComponent<SphereCollider>();
                    bool result = (bool)method.Invoke(null,new object[] {0f,6000f,direction*distance,6000f+slope*distance,ignoreProps,receiver});
                    if ((fixture == 0 || fixture == 2 || fixture == 3 || fixture == 4 || fixture == 5 || fixture == 6) && !result) throw new Exception("Expected ignored fixture");
                    if (fixture == 1 && distance == 64f && result) throw new Exception("Expected wall occlusion");
                    ScriptContext.Logger("VISIBLE|"+hasReceiver+"|"+ignoreProps+"|"+direction+"|"+slope+"|"+fixture+"|"+distance.ToString("R")+"|"+result);
                    count++;
                    if (fixture == 5) UnityEngine.Object.DestroyImmediate(receiver);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(wall); }
        }
        ScriptContext.Logger("COMPLETE="+count);
    }
}

public class SkillLengthReceiver : MonoBehaviour { public void Damage(DamageObject damage) {} }
