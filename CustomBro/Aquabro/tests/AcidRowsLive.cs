// Paused production texture comparison; writes every tested RGBA byte for exact before/after comparison.
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using UnityEngine;

public static class AcidRowsLive
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static object effect;
    static object Get(object o, string n) { return o.GetType().GetField(n, F).GetValue(o); }
    static void Set(object o, string n, object v) { o.GetType().GetField(n, F).SetValue(o, v); }
    static object Call(object o, string n, params object[] a) { return o.GetType().GetMethod(n, F).Invoke(o, a); }
    static void Check(bool b, string m) { if (!b) throw new Exception(m); }
    static byte[] Bytes(Texture2D t)
    {
        Color32[] p = t.GetPixels32();
        byte[] b = new byte[p.Length * 4];
        for (int i = 0; i < p.Length; i++) { b[i*4]=p[i].r; b[i*4+1]=p[i].g; b[i*4+2]=p[i].b; b[i*4+3]=p[i].a; }
        return b;
    }
    public static void Main()
    {
        TestVanDammeAnim owner = HeroController.players[0].character;
        Check(owner != null && owner.GetType().FullName == "AquabroMod.Aquabro" && Time.timeScale == 0f, "Paused Aquabro required");
        Assembly asm = owner.GetType().Assembly;
        Type visual = asm.GetType("AquabroMod.AquabroSkillVisual");
        IList active = (IList)asm.GetType("AquabroMod.WaterProjectileBarrier").GetField("Active", F).GetValue(null);
        int initial = active.Count, cases = 0;
        string output = (string)ScriptContext.Args["output"];
        ScriptContext.Logger("MVID=" + asm.ManifestModule.ModuleVersionId + " scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + " character=" + owner.GetType().FullName);
        try
        {
            using (FileStream stream = File.Create(output))
            foreach (int kind in new int[] {0,2,3}) foreach (float facing in new float[] {-1f,1f})
            {
                effect = Activator.CreateInstance(visual, F, null, new object[] {owner,kind,6000f,6000f,facing,5f,0f,.85f}, null);
                object water = Get(effect,"waterBarrier"), acid = Get(water,"acid");
                Call(water,"Absorb",new object()); Call(water,"Absorb",new object());
                int count = kind == 0 ? 16 : kind == 2 ? 32 : 28;
                foreach (float level in new float[] {0f,.5f,1f,1.5f,2f})
                for (int frame = 0; frame < count; frame++)
                {
                    Set(effect,"acidColorLevel",level); Set(acid,"colorLevel",level);
                    Call(effect,"UpdateAcidColor",frame,0f);
                    Texture2D texture = (Texture2D)Get(effect,"acidTexture");
                    Check(texture.width == 128 && texture.height == 128, "Texture dimensions");
                    Check((int)Get(effect,"acidUploadedFrame") == frame && (float)Get(effect,"acidUploadedColorLevel") == level, "Upload state");
                    byte[] bytes = Bytes(texture);
                    stream.Write(bytes,0,bytes.Length);
                    Call(effect,"UpdateAcidColor",frame,0f);
                    Check(System.Object.ReferenceEquals(texture,Get(effect,"acidTexture")), "Cache texture replaced");
                    byte[] repeated = Bytes(texture);
                    for (int i=0;i<bytes.Length;i++) Check(bytes[i] == repeated[i], "Stable frame changed");
                    cases++;
                }
                Call(effect,"Dispose"); effect=null;
                ScriptContext.Logger("CASE kind="+kind+" facing="+facing+" frames="+count+" levels=0,0.5,1,1.5,2 cacheStable=true");
            }
            using (SHA256 sha=SHA256.Create()) using (FileStream input=File.OpenRead(output))
                ScriptContext.Logger("COMPLETE cases="+cases+" bytes="+input.Length+" sha256="+BitConverter.ToString(sha.ComputeHash(input)).Replace("-", ""));
        }
        finally { Unload(); Check(active.Count == initial,"Barrier leak"); }
    }
    public static void Unload() { if (effect != null) { Call(effect,"Dispose"); effect=null; } }
}
