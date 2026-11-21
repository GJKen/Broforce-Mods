using System;
using System.Reflection;
using UnityEngine;

public static class AcidAllocationLive
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    public static void Main()
    {
        var owner = HeroController.players[0].character;
        Check(owner != null && owner.GetType().FullName == "AquabroMod.Aquabro" && TestMod.Plugin.IsGamePaused,
            "Paused Aquabro required");
        var type = owner.GetType().Assembly.GetType("AquabroMod.WaterAcidState", true);
        var sources = type.GetField("sources", F);
        var absorb = type.GetMethod("Absorb", F);
        var stage = type.GetProperty("Stage", F);
        var advance = type.GetMethod("AdvanceColor", F);
        object state = Activator.CreateInstance(type, true);
        bool initial = sources.GetValue(state) != null;
        absorb.Invoke(state, new object[] { null });
        Check(initial == (sources.GetValue(state) != null), "Null source allocated");
        object first = new object();
        absorb.Invoke(state, new object[] { first });
        object collection = sources.GetValue(state);
        Check(collection != null && (int)stage.GetValue(state, null) == 1, "Missing first source");
        absorb.Invoke(state, new object[] { first });
        Check((int)stage.GetValue(state, null) == 1, "Duplicate advanced stage");
        float color = (float)advance.Invoke(state, new object[] { .15f, .15f });
        float repeat = (float)advance.Invoke(state, new object[] { .15f, .15f });
        Check(color == repeat, "Repeated shared clock advanced");
        absorb.Invoke(state, new object[] { new object() });
        absorb.Invoke(state, new object[] { new object() });
        Check((int)stage.GetValue(state, null) == 2 && object.ReferenceEquals(collection, sources.GetValue(state)),
            "Cap or collection reuse changed");
        object next = Activator.CreateInstance(type, true);
        Check((int)stage.GetValue(next, null) == 0 && initial == (sources.GetValue(next) != null), "Next cast leaked state");
        ScriptContext.Logger("ACID_ALLOCATION initialSet=" + initial + " nullSourceUnchanged=True duplicateIgnored=True cap=2 reused=True color=" + color + " sharedClock=True nextCastFresh=True");
    }
}
