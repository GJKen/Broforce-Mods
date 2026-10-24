// Paused live exhaustiveness check for every production mask query.
using System;
using System.Reflection;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

public static class MaskQueryLive
{
    const BindingFlags F = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    delegate bool PointQuery(int frame, float x, float y, float margin);
    delegate bool WaterQuery(int frame, int x, int y);

    static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }

    static string Digest(byte[] data)
    {
        using (SHA256 sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "");
    }

    public static void Main()
    {
        TestVanDammeAnim owner = HeroController.players[0].character;
        Check(owner != null && owner.GetType().FullName == "AquabroMod.Aquabro" && TestMod.Plugin.IsGamePaused, "Paused Aquabro required");
        Assembly assembly = owner.GetType().Assembly;
        string[] names = { "ChargedWaterPixelMask", "MaelstromPixelMask", "SharkPixelMask", "SpringPixelMask", "TidalWavePixelMask" };
        ScriptContext.Logger("MVID=" + assembly.ManifestModule.ModuleVersionId + " character=" + owner.GetType().FullName + " scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        using (SHA256 sha = SHA256.Create())
            ScriptContext.Logger("LOADED_DLL|path=" + assembly.Location + "|sha256=" + BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(assembly.Location))).Replace("-", ""));

        foreach (string name in names)
        {
            Type type = assembly.GetType("AquabroMod." + name, true);
            bool water = name == "ChargedWaterPixelMask";
            object mask = water ? null : type.GetField("Mask", F).GetValue(null);
            FieldInfo storage = water ? type.GetField("Frames", F) : mask.GetType().GetField("frames", F);
            byte[][] frames = (byte[][])storage.GetValue(mask);
            int size = water ? 64 : (int)mask.GetType().GetField("size", F).GetValue(mask);
            WaterQuery waterQuery = water ? (WaterQuery)Delegate.CreateDelegate(typeof(WaterQuery), type.GetMethod("IsVisible", F)) : null;
            PointQuery pointQuery = water ? null : (PointQuery)Delegate.CreateDelegate(typeof(PointQuery), mask, mask.GetType().GetMethod("Contains", F));
            byte[] result = new byte[frames.Length * size * size];
            int queries = 0;
            int hits = 0;

            for (int frame = 0; frame < frames.Length; frame++)
            {
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        bool actual = water ? waterQuery(frame, x, y) : pointQuery(frame, x + .5f, y + .5f, 0f);
                        int bit = y * size + x;
                        bool expected = (frames[frame][bit / 8] & (1 << (bit % 8))) != 0;
                        Check(actual == expected, name + " pixel mismatch at " + frame + "," + x + "," + y);
                        result[queries++] = actual ? (byte)1 : (byte)0;
                        if (actual) hits++;
                    }
                }
            }

            if (water)
            {
                Check(!waterQuery(-1, 0, 0) && !waterQuery(frames.Length, 0, 0), name + " frame bounds");
                Check(!waterQuery(0, -1, 0) && !waterQuery(0, size, 0), name + " x bounds");
                Check(!waterQuery(0, 0, -1) && !waterQuery(0, 0, size), name + " y bounds");
            }
            else
            {
                Check(!pointQuery(-1, .5f, .5f, 0f) && !pointQuery(frames.Length, .5f, .5f, 0f), name + " frame bounds");
                Check(!pointQuery(0, -2f, .5f, 0f) && !pointQuery(0, size + 1f, .5f, 0f), name + " x bounds");
                Check(!pointQuery(0, .5f, -2f, 0f) && !pointQuery(0, .5f, size + 1f, 0f), name + " y bounds");
            }

            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(type.TypeHandle);
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(type.TypeHandle);
            Check(object.ReferenceEquals(frames, storage.GetValue(mask)), name + " storage changed");
            ScriptContext.Logger("MASK_QUERY|" + name + "|size=" + size + "|frames=" + frames.Length + "|queries=" + queries + "|hits=" + hits + "|sha256=" + Digest(result) + "|invalidRanges=true|stableStorage=true");
        }
        ScriptContext.Logger("COMPLETE all production mask pixels and invalid bounds PASS");
    }
}
