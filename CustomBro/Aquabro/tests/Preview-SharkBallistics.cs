// #name Aquabro Shark Ballistics Preview
// #description Holds one production shark at a requested flight time for a screenshot; unload removes it.
using System;
using System.Reflection;
using UnityEngine;

public static class PreviewSharkBallistics
{
    private const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static object effect;
    private static float Arg(string key, float fallback)
    { return float.Parse(ScriptContext.GetArg(key, fallback.ToString(System.Globalization.CultureInfo.InvariantCulture)), System.Globalization.CultureInfo.InvariantCulture); }
    public static void Main()
    {
        if (!TestMod.Plugin.IsGamePaused) throw new Exception("Pause game first");
        Unload();
        TestVanDammeAnim owner = HeroController.players[0].character;
        Type visual = owner.GetType().Assembly.GetType("AquabroMod.AquabroSkillVisual");
        float x = Arg("x", owner.X), y = Arg("y", owner.Y);
        float facing = Arg("facing", 1f), age = Arg("age", .3f);
        effect = Activator.CreateInstance(visual, F, null, new object[] { owner, 1, x, y, facing, 12f, 0f, 1f }, null);
        visual.GetMethod("BreachShark", F).Invoke(effect, null);
        if (bool.Parse(ScriptContext.GetArg("entry", "false")))
        {
            // Start one pixel above a requested real landing surface, then let
            // production collision and animation perform the entry normally.
            visual.GetField("y", F).SetValue(effect, y + 1f);
            visual.GetField("sharkVelocityX", F).SetValue(effect, 0f);
            visual.GetField("sharkVelocityY", F).SetValue(effect, -240f);
            visual.GetField("sharkBreachElapsed", F).SetValue(effect, .7f);
        }
        MethodInfo tick = visual.GetMethod("Tick", F); float time = 0;
        while (time < age - .00001f)
        {
            float dt = Mathf.Min(1f / 120f, age - time);
            if (!(bool)tick.Invoke(effect, new object[] { dt })) break;
            time += dt;
        }
        foreach (string name in new string[] { "x", "y", "sharkVelocityY", "sharkLandingElapsed" })
            ScriptContext.Logger(name + "=" + visual.GetField(name, F).GetValue(effect));
    }
    public static void Unload()
    {
        if (effect != null) effect.GetType().GetMethod("Dispose", F).Invoke(effect, null);
        effect = null;
    }
}
