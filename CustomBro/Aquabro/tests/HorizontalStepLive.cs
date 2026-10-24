// Paused production horizontal movement comparison; temporary colliders are removed.
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
public static class HorizontalStepLive
{
    public static void Main()
    {
        TestVanDammeAnim owner = HeroController.players[0].character;
        if (owner.GetType().FullName != "AquabroMod.Aquabro" || !TestMod.Plugin.IsGamePaused)
            throw new Exception("Paused Aquabro required");
        MethodInfo move = owner.GetType().GetMethod("MoveSkillHorizontally", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        ScriptContext.Logger("MVID=" + owner.GetType().Assembly.ManifestModule.ModuleVersionId);
        ScriptContext.Logger("SCENE=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        ScriptContext.Logger("CHARACTER=" + owner.GetType().FullName);
        foreach (float origin in new float[] { 0f, 10000f })
        foreach (int direction in new int[] { -1, 1 })
        foreach (int fixture in new int[] { 0, 1, 2, 3, 4 })
        {
            List<GameObject> objects = new List<GameObject>();
            try
            {
                if (fixture != 0)
                {
                    GameObject wall = new GameObject("Aquabro horizontal probe wall"); objects.Add(wall);
                    wall.layer = LayerMask.NameToLayer("Ground");
                    wall.transform.position = new Vector3(origin + direction * 24, origin + (fixture == 2 ? 56 : 12), 0);
                    wall.AddComponent<BoxCollider>().size = new Vector3(8, 8, 16);
                    if (fixture == 3) wall.GetComponent<Collider>().isTrigger = true;
                    if (fixture == 4)
                    {
                        GameObject upper = new GameObject("Aquabro horizontal probe upper"); objects.Add(upper);
                        upper.layer = wall.layer; upper.transform.position = new Vector3(origin + direction * 18, origin + 56, 0);
                        upper.AddComponent<BoxCollider>().size = new Vector3(8, 8, 16);
                    }
                }
                foreach (float distance in new float[] { 0, .000001f, .000009f, .00001f, .000011f, .001f, 4, 8, 12, 16, 24, 64, 240 })
                {
                    float result = (float)move.Invoke(owner, new object[] {origin, origin, origin + direction * distance});
                    if (float.IsNaN(result)) throw new Exception("Invalid position");
                    ScriptContext.Logger("MOVE|" + origin + "|" + direction + "|" + fixture + "|" + distance.ToString("R") + "|" + result.ToString("R"));
                }
            }
            finally { foreach (GameObject obj in objects) UnityEngine.Object.DestroyImmediate(obj); }
        }
        ScriptContext.Logger("HorizontalStepLive completed 260 cases");
    }
}
