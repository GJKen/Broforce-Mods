// Paused live check of every production mask pixel, boundaries and stable storage.
using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Diagnostics;
using HarmonyLib;
using UnityEngine;

public static class MaskStorageLive
{
    const BindingFlags F = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    delegate bool PointQuery(int frame, float x, float y, float margin);
    delegate bool WaterQuery(int frame, int x, int y);
    delegate long AllocationCounter();
    static int decodeCalls;
    public static void DecodeObserved() { decodeCalls++; }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static string Digest(byte[] data) { using (SHA256 sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", ""); }

    public static void Main()
    {
        TestVanDammeAnim owner = HeroController.players[0].character;
        Check(owner != null && owner.GetType().FullName == "AquabroMod.Aquabro" && TestMod.Plugin.IsGamePaused, "Paused Aquabro required");
        Assembly assembly = owner.GetType().Assembly;
        MethodInfo allocated = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", BindingFlags.Static | BindingFlags.Public);
        AllocationCounter counter = allocated == null ? null : (AllocationCounter)Delegate.CreateDelegate(typeof(AllocationCounter), allocated);
        ScriptContext.Logger("MVID=" + assembly.ManifestModule.ModuleVersionId + " character=" + owner.GetType().FullName + " scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        ScriptContext.Logger("RUNTIME version=" + Environment.Version + " allocatedCounter=" + (counter != null));
        string[] names = { "ChargedWaterPixelMask", "MaelstromPixelMask", "SharkPixelMask", "SpringPixelMask", "TidalWavePixelMask" };
        Type pixelMask = assembly.GetType("AquabroMod.PixelMask", true);
        MethodInfo decoder = pixelMask.GetMethod("DecodeContextFrames", F) ?? pixelMask.GetMethod("DecodeFrames", F, null,
            new Type[] { typeof(int), typeof(int), typeof(int), typeof(string) }, null);
        Harmony observer = new Harmony("aquabro.mask.storage.diagnostic");
        decodeCalls = 0;
        if (decoder != null) observer.Patch(decoder, new HarmonyMethod(typeof(MaskStorageLive).GetMethod("DecodeObserved")), null, null, null);
        try
        {
        foreach (string name in names)
        {
            Type type = assembly.GetType("AquabroMod." + name, true);
            bool water = name == "ChargedWaterPixelMask";
            int initialCalls = decodeCalls;
            Stopwatch clock = Stopwatch.StartNew();
            object mask = water ? null : type.GetField("Mask", F).GetValue(null);
            FieldInfo storage = water ? type.GetField("Frames", F) : mask.GetType().GetField("frames", F);
            byte[][] frames = (byte[][])storage.GetValue(mask);
            clock.Stop();
            int firstCalls = decodeCalls - initialCalls;
            if (decoder != null) Check(firstCalls == 1, "Expected one cold initialization for " + name + ", observed " + firstCalls);
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(type.TypeHandle);
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(type.TypeHandle);
            Check(decodeCalls == initialCalls + firstCalls, "Static initialization repeated");
            int size = water ? 64 : (int)mask.GetType().GetField("size", F).GetValue(mask);
            WaterQuery w = water ? (WaterQuery)Delegate.CreateDelegate(typeof(WaterQuery), type.GetMethod("IsVisible", F)) : null;
            PointQuery p = water ? null : (PointQuery)Delegate.CreateDelegate(typeof(PointQuery), mask, mask.GetType().GetMethod("Contains", F));
            int queries = 0, hits = 0;
            byte[] result = new byte[frames.Length * size * size];
            for (int f = 0; f < frames.Length; f++)
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    bool hit = water ? w(f, x, y) : p(f, x + .5f, y + .5f, 0f);
                    int bit = y * size + x;
                    Check(hit == ((frames[f][bit / 8] & (1 << (bit % 8))) != 0), name + " pixel disagreement");
                    result[queries++] = hit ? (byte)1 : (byte)0;
                    if (hit) hits++;
                }
            Check(water ? !w(-1, 0, 0) && !w(frames.Length, 0, 0) && !w(0, -1, 0) && !w(0, size, 0) && !w(0, 0, -1) && !w(0, 0, size) : !p(-1, 0, 0, 5) && !p(frames.Length, 0, 0, 5), "Invalid range changed");
            // Warm the actual delegates, then check repeated calls without reflection/boxing.
            for (int i = 0; i < 128; i++) { if (water) w(0, i % size, i % size); else p(0, i % size + .5f, i % size + .5f, 0); }
            long start = counter == null ? 0 : counter();
            int stableHits = 0;
            for (int i = 0; i < 100000; i++)
                if (water ? w(i % frames.Length, i % size, (i / size) % size) : p(i % frames.Length, i % size + .5f, (i / size) % size + .5f, 0)) stableHits++;
            long allocation = counter == null ? 0 : counter() - start;
            Check(allocation == 0, "Stable queries allocated");
            Check(object.ReferenceEquals(frames, storage.GetValue(mask)), "Frame storage recreated");
            Check(decodeCalls == initialCalls + firstCalls, "Queries decoded data again");
            ScriptContext.Logger("MASK_QUERY|" + name + "|size=" + size + "|frames=" + frames.Length + "|queries=" + queries + "|hits=" + hits + "|sha256=" + Digest(result) + "|stableHits=" + stableHits + "|stableStorage=true|invalidRanges=true");
            ScriptContext.Logger("MEASURE|" + name + "|firstAccessMs=" + clock.Elapsed.TotalMilliseconds + "|stableAllocated=" + (counter == null ? "unsupported" : allocation.ToString()) + "|coldDecoderCalls=" + firstCalls + "|repeatDecoderCalls=0");
        }
        ScriptContext.Logger("COMPLETE every pixel and stable storage PASS; no scene objects created; observer removed in finally");
        }
        finally { observer.UnpatchAll("aquabro.mask.storage.diagnostic"); }
    }
}
