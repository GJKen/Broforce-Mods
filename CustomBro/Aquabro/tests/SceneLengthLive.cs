using System;
using System.Reflection;
using UnityEngine;
public static class SceneLengthLive
{
    public static void Main()
    {
        TestVanDammeAnim owner = HeroController.players[0].character;
        if (owner.GetType().FullName != "AquabroMod.Aquabro" || !TestMod.Plugin.IsGamePaused) throw new Exception("Paused Aquabro required");
        MethodInfo method = owner.GetType().GetMethod("WaterSceneContactVisible", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
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
                if (fixture == 6) wall.AddComponent<DamageRelay>().unit = owner;
                foreach (float distance in new float[] {0f,.000009f,.00001f,.000011f,.009f,.01f,.011f,1f,3.874f,3.875f,3.88f,3.885f,3.886f,4f,8f,64f})
                {
                    GameObject receiver = fixture == 4 ? wall : null;
                    if (fixture == 5) { receiver = new GameObject("Receiver parent"); wall.transform.parent = receiver.transform; }
                    bool result = (bool)method.Invoke(null,new object[] {0f,6000f,direction*distance,6000f+slope*distance,receiver});
                    if ((fixture == 0 || fixture == 2 || fixture == 3 || fixture == 4 || fixture == 5 || fixture == 6) && !result) throw new Exception("Expected ignored fixture");
                    if (fixture == 1 && distance == 64f && result) throw new Exception("Expected wall occlusion");
                    ScriptContext.Logger("VISIBLE|"+direction+"|"+slope+"|"+fixture+"|"+distance.ToString("R")+"|"+result);
                    count++;
                    if (fixture == 5) { wall.transform.parent = null; UnityEngine.Object.DestroyImmediate(receiver); }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(wall); }
        }
        ScriptContext.Logger("COMPLETE="+count);
    }
}
