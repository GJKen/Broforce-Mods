// Install before entering a level to observe one-time mask decoder calls.
using System;
using System.Reflection;
using HarmonyLib;

public static class MaskInitObserverLive
{
    const string Owner = "aquabro.mask.init.observer";
    static Harmony harmony;
    static int calls;

    public static void Main()
    {
        Assembly assembly = null;
        foreach (Assembly candidate in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (candidate.GetType("AquabroMod.PixelMask", false) != null)
            {
                assembly = candidate;
                break;
            }
        }
        if (assembly == null) throw new Exception("Aquabro PixelMask assembly not loaded");
        Type pixelMask = assembly.GetType("AquabroMod.PixelMask", true);
        MethodInfo decode = pixelMask.GetMethod("DecodeFrames", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
            null, new Type[] { typeof(int), typeof(int), typeof(int), typeof(string) }, null);
        if (decode == null) throw new Exception("Packed DecodeFrames overload not found");

        calls = 0;
        harmony = new Harmony(Owner);
        harmony.Patch(decode, null, new HarmonyMethod(typeof(MaskInitObserverLive).GetMethod("Observe", BindingFlags.Static | BindingFlags.Public)), null, null);
        ScriptContext.Logger("OBSERVER_INSTALLED|mvid=" + assembly.ManifestModule.ModuleVersionId + "|path=" + assembly.Location);
    }

    public static void Observe(int __0, int __1, int __2, string __3)
    {
        calls++;
        ScriptContext.Logger("DECODE|call=" + calls + "|frames=" + __0 + "|bytesPerFrame=" + __1 + "|compressedBytes=" + __2 + "|packedChars=" + __3.Length);
    }

    public static int GetCalls()
    {
        return calls;
    }

    public static void Unload()
    {
        if (harmony != null) harmony.UnpatchAll(Owner);
        harmony = null;
        ScriptContext.Logger("OBSERVER_REMOVED|calls=" + calls);
    }
}
