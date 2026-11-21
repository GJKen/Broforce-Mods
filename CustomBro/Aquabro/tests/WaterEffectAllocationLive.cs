using System;
using System.Reflection;
using UnityEngine;

public static class WaterEffectAllocationLive
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    public static void Main()
    {
        var owner = HeroController.players[0].character;
        if (!TestMod.Plugin.IsGamePaused || owner.GetType().FullName != "AquabroMod.Aquabro")
            throw new Exception("Paused Aquabro required");
        var type = owner.GetType().Assembly.GetType("AquabroMod.AquabroWaterEffect", true);
        foreach (bool charged in new bool[] { false, true })
        {
            var effect = (MonoBehaviour)type.GetMethod("Play", F).Invoke(null, new object[] {
                ((BroMakerLib.CustomObjects.Bros.CustomHero)owner).Info.path,
                charged ? "water-rocket-explosion.png" : "water-hit-small.png", 6000f, 6000f,
                charged ? 64 : 48, charged ? 6 : 12, charged });
            try
            {
                var field = type.GetField("affectedUnits", F);
                object before = field.GetValue(effect);
                type.GetMethod("Update", F).Invoke(effect, null);
                if (!object.ReferenceEquals(before, field.GetValue(effect)) ||
                    (float)type.GetField("elapsed", F).GetValue(effect) != 0f)
                    throw new Exception("Paused water effect advanced or allocated");
                if (charged)
                {
                    var begin = type.GetMethod("BeginBlast", F);
                    begin.Invoke(effect, new object[] { owner, 664f, 14, null });
                    object hits = field.GetValue(effect);
                    begin.Invoke(effect, new object[] { owner, 664f, 14, null });
                    if (hits == null || !object.ReferenceEquals(hits, field.GetValue(effect)))
                        throw new Exception("Charged hit set missing or replaced");
                }
                ScriptContext.Logger("WATER charged=" + charged + " initialSet=" + (before != null) +
                    " finalSet=" + (field.GetValue(effect) != null) + " paused=True");
            }
            finally { UnityEngine.Object.DestroyImmediate(effect.gameObject); }
        }
        ScriptContext.Logger("WATER_ALLOCATION_LIVE PASS; effects disposed");
    }
}
