// #name Aquabro Logic Optimization Comparison
// #description Paused, disposable before/after checks of production acid rendering, wave cooldown and HUD.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;

public static class LogicOptimizationLive
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    public static readonly List<string> Rows = new List<string>();
    static object effect;
    static object Get(object value, string name) { return value.GetType().GetField(name, F).GetValue(value); }
    static object Call(object value, string name, params object[] args) { return value.GetType().GetMethod(name, F).Invoke(value, args); }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Record(string value) { Rows.Add(value); ScriptContext.Logger(value); }
    static string Pixels(Texture2D texture)
    {
        uint hash = 2166136261;
        foreach (Color32 pixel in texture.GetPixels32())
            unchecked { hash = (hash ^ pixel.r) * 16777619; hash = (hash ^ pixel.g) * 16777619;
                hash = (hash ^ pixel.b) * 16777619; hash = (hash ^ pixel.a) * 16777619; }
        return hash.ToString("X8");
    }
    public static void Main()
    {
        Rows.Clear();
        TestVanDammeAnim owner = HeroController.players[0].character;
        Check(owner != null && owner.GetType().FullName == "AquabroMod.Aquabro" && Time.timeScale == 0f, "Paused Aquabro required");
        Assembly assembly = owner.GetType().Assembly;
        Type visual = assembly.GetType("AquabroMod.AquabroSkillVisual");
        Record("MVID=" + assembly.ManifestModule.ModuleVersionId + " health=" + owner.health + " ammo=" + owner.SpecialAmmo + " position=" + owner.transform.position);
        int[] selections = (int[])owner.GetType().GetField("SkillSelections", F).GetValue(null);
        int originalSelection = selections[0];
        int initialBarriers = ((IList)assembly.GetType("AquabroMod.WaterProjectileBarrier").GetField("Active", F).GetValue(null)).Count;
        try
        {
            foreach (int kind in new int[] { 0, 2, 3 }) foreach (float facing in new float[] { -1f, 1f })
            {
                effect = Activator.CreateInstance(visual, F, null, new object[] { owner, kind, 6000f, 6000f, facing, 5f, 0f, .85f }, null);
                object water = Get(effect, "waterBarrier");
                Call(water, "Absorb", new object());
                List<string> hashes = new List<string>();
                foreach (float delta in new float[] { .15f, .15f })
                {
                    Call(effect, "Tick", delta);
                    hashes.Add(Pixels((Texture2D)Get(effect, "acidTexture")));
                }
                Texture2D texture = (Texture2D)Get(effect, "acidTexture");
                int frame = (int)Get(water, "frame");
                MethodInfo update = visual.GetMethod("UpdateAcidColor", F);
                string settled = Pixels(texture);
                Stopwatch clock = Stopwatch.StartNew();
                for (int i = 0; i < 240; i++) update.Invoke(effect, new object[] { frame, .001f });
                clock.Stop();
                Check(settled == Pixels(texture), "Stable frame changed");
                hashes.Add(settled);
                Call(water, "Absorb", new object());
                foreach (float delta in new float[] { .15f, .15f, .05f })
                {
                    Call(effect, "Tick", delta);
                    hashes.Add(Pixels(texture));
                }
                Check(Mathf.Abs((float)Get(effect, "acidColorLevel") - 2f) < .0001f, "Second stage not reached");
                Record("ACID kind=" + kind + " facing=" + facing + " hashes=" + string.Join(",", hashes.ToArray()) + " stable240Ms=" + clock.Elapsed.TotalMilliseconds.ToString("F3"));
                Call(effect, "Dispose"); effect = null;
            }
            Type wave = assembly.GetType("AquabroMod.WaveDamageState");
            object budget = Activator.CreateInstance(wave, true), target = new object();
            int first = (int)Call(budget, "TakeDamage", target, 0f);
            int blocked = (int)Call(budget, "TakeDamage", target, .119f);
            int boundary = (int)Call(budget, "TakeDamage", target, .12f);
            int sum = first + blocked + boundary;
            for (int i = 2; i < 20; i++) sum += (int)Call(budget, "TakeDamage", target, i * .12f);
            bool independent = (bool)Call(budget, "TryHit", new object(), 0f);
            Check(first == 2 && blocked == 0 && boundary == 2 && sum == 26 && independent, "Wave cooldown/budget changed");
            Record("WAVE first=" + first + " early=" + blocked + " boundary=" + boundary + " total=" + sum + " independent=" + independent);
            int ammo = owner.SpecialAmmo, selection = (int)owner.GetType().GetProperty("SelectedSkill").GetValue(owner, null);
            bool[] visibility = Array.ConvertAll(HeroController.players[0].hud.grenadeIcons, icon => icon.meshRender.enabled);
            for (int i = 0; i < 4; i++)
            {
                owner.SetGestureAnimation(GestureElement.Gestures.Flex);
                Call(owner, "RefreshSkillHud");
                string selected = ((string[])owner.GetType().GetField("SkillAssets", F).GetValue(null))[(int)owner.GetType().GetProperty("SelectedSkill").GetValue(owner, null)];
                int index = 0;
                foreach (SpriteSM icon in HeroController.players[0].hud.grenadeIcons)
                {
                    Check(icon.meshRender.sharedMaterial == (Material)Get(owner, "skillHudMaterial"), "HUD material mismatch");
                    Check(icon.meshRender.enabled == visibility[index++], "HUD visibility changed");
                }
                Record("HUD selection=" + selected + " ammo=" + owner.SpecialAmmo);
            }
            owner.SetGestureAnimation(GestureElement.Gestures.None);
            Check(ammo == owner.SpecialAmmo && selection == (int)owner.GetType().GetProperty("SelectedSkill").GetValue(owner, null), "HUD changed ammo/selection");
            Record("RESULT six acid comparisons; wave cooldown and 26 budget; four HUD selections; PASS");
        }
        finally
        {
            Unload();
            selections[0] = originalSelection;
            owner.SetGestureAnimation(GestureElement.Gestures.None);
            Call(owner, "RefreshSkillHud");
            int remaining = ((IList)assembly.GetType("AquabroMod.WaterProjectileBarrier").GetField("Active", F).GetValue(null)).Count;
            Check(remaining == initialBarriers, "Temporary barriers leaked");
        }
    }
    public static void Unload() { if (effect != null) { Call(effect, "Dispose"); effect = null; } }
}
