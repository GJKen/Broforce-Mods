// Paused before/after comparison using the loaded production assembly.
using System;
using System.Reflection;
using UnityEngine;

public static class SizeTrimLive
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static uint hash;
    static void Add(int value) { unchecked { hash = (hash ^ (uint)value) * 16777619u; } }
    static void Add(float value) { Add(BitConverter.ToInt32(BitConverter.GetBytes(value), 0)); }
    static object Get(object value, string name) { return value.GetType().GetField(name, F).GetValue(value); }
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static uint Pixels(Texture2D texture)
    {
        uint result = 2166136261u;
        foreach (Color32 p in texture.GetPixels32())
            unchecked { result = (result ^ p.r) * 16777619u; result = (result ^ p.g) * 16777619u;
                result = (result ^ p.b) * 16777619u; result = (result ^ p.a) * 16777619u; }
        return result;
    }
    public static void Main()
    {
        TestVanDammeAnim owner = HeroController.players[0].character;
        Check(owner != null && owner.GetType().FullName == "AquabroMod.Aquabro" && TestMod.Plugin.IsGamePaused,
            "Paused Aquabro required");
        Assembly assembly = owner.GetType().Assembly;
        Type hero = owner.GetType(), visual = assembly.GetType("AquabroMod.AquabroSkillVisual", true);
        ScriptContext.Logger("MVID=" + assembly.ManifestModule.ModuleVersionId);
        MethodInfo water = assembly.GetType("AquabroMod.AquabroWaterEffect", true).GetMethod("GetAnimationFrame", F);
        MethodInfo idle = hero.GetMethod("GetChargeAnimationFrame", F);
        MethodInfo running = hero.GetMethod("GetRunningChargeAnimationFrame", F);
        hash = 2166136261u;
        int samples = 0;
        for (int i = 0; i <= 10000; i++)
        {
            float t = i / 1000f;
            Add((int)water.Invoke(null, new object[] {t, false}));
            Add((int)water.Invoke(null, new object[] {t, true}));
            Add((int)idle.Invoke(null, new object[] {t}));
            Add((int)running.Invoke(null, new object[] {t}));
            samples++;
        }
        float boundary = 0f;
        for (int i = 0; i < 6; i++)
        {
            boundary += .1f;
            int bits = BitConverter.ToInt32(BitConverter.GetBytes(boundary), 0);
            for (int offset = -1; offset <= 1; offset++)
            {
                float t = BitConverter.ToSingle(BitConverter.GetBytes(bits + offset), 0);
                Add((int)water.Invoke(null, new object[] {t, true})); samples++;
            }
        }
        ScriptContext.Logger("ANIMATION samples=" + samples + " hash=" + hash.ToString("X8"));
        Type atlas = assembly.GetType("AquabroMod.TridentAtlas", true);
        hash = 2166136261u;
        int values = 0;
        foreach (string name in new string[] {"StandingLegs", "RunningLegs", "RunningBob", "JumpingLegs",
            "JumpingRecovery", "LadderLegs", "LadderBob", "ZiplineLegs", "MovementFrames", "UpperFrames"})
            foreach (object value in (Array)atlas.GetField(name, F).GetValue(null)) { Add(Convert.ToInt32(value)); values++; }
        ScriptContext.Logger("ATLAS values=" + values + " hash=" + hash.ToString("X8"));
        System.Collections.IList active = (System.Collections.IList)visual.GetField("ActiveVisuals", F).GetValue(null);
        int initial = active.Count;
        foreach (int kind in new int[] {0, 1, 2, 3}) foreach (int fps in new int[] {30, 60}) foreach (float facing in new float[] {-1f, 1f})
        {
            object effect = Activator.CreateInstance(visual, F, null,
                new object[] {owner, kind, 6000f, 6000f, facing, kind == 0 ? 2f : kind == 1 ? .2f : kind == 2 ? 4.2f : 1.4f, 0f, .85f, null}, null);
            try
            {
                object blood = Get(effect, "blood");
                blood.GetType().GetField("Touched", F).SetValue(blood, true);
                MethodInfo tick = visual.GetMethod("Tick", F);
                SpriteSM sprite = (SpriteSM)Get(effect, "sprite");
                hash = 2166136261u;
                int steps = 0;
                for (; steps < fps * 6; steps++)
                {
                    bool alive = (bool)tick.Invoke(effect, new object[] {1f / fps});
                    Add((float)Get(effect, "x")); Add((float)Get(effect, "y"));
                    Add(sprite.lowerLeftPixel.x); Add(sprite.lowerLeftPixel.y); Add(alive ? 1 : 0);
                    if (!alive) break;
                }
                Texture2D texture = (Texture2D)Get(effect, "acidTexture");
                ScriptContext.Logger("VISUAL kind=" + kind + " fps=" + fps + " facing=" + facing + " steps=" + steps +
                    " hash=" + hash.ToString("X8") + " rgba=" + (texture == null ? "none" : Pixels(texture).ToString("X8")));
            }
            finally { visual.GetMethod("Dispose", F).Invoke(effect, null); }
        }
        Check(active.Count == initial, "Visual leaked");
        ScriptContext.Logger("SIZE_TRIM_LIVE PASS; temporary effects disposed");
    }
}
