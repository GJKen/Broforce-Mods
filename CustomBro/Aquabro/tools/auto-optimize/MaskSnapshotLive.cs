// Diagnostic only; never included in Aquabro.dll. Dump the loaded production masks exactly.
using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using UnityEngine;

public static class MaskSnapshotLive
{
    const BindingFlags F = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    public static void Main()
    {
        TestVanDammeAnim owner = HeroController.players[0].character;
        if (owner == null || owner.GetType().FullName != "AquabroMod.Aquabro" || !TestMod.Plugin.IsGamePaused)
            throw new Exception("Paused Aquabro required");
        Assembly assembly = owner.GetType().Assembly;
        string output = (string)ScriptContext.Args["output"];
        string[] names = {"ChargedWaterPixelMask", "MaelstromPixelMask", "SharkPixelMask", "SpringPixelMask", "TidalWavePixelMask"};
        using (BinaryWriter writer = new BinaryWriter(File.Create(output)))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("AQUABRO_MASKS_V1\n"));
            writer.Write(names.Length);
            foreach (string name in names)
            {
                Type type = assembly.GetType("AquabroMod." + name, true);
                byte[][] frames;
                int size;
                if (name == "ChargedWaterPixelMask")
                {
                    frames = (byte[][])type.GetField("Frames", F).GetValue(null);
                    size = 64;
                }
                else
                {
                    object mask = type.GetField("Mask", F).GetValue(null);
                    frames = (byte[][])mask.GetType().GetField("frames", F).GetValue(mask);
                    size = (int)mask.GetType().GetField("size", F).GetValue(mask);
                }
                writer.Write(name); writer.Write(size); writer.Write(frames.Length);
                foreach (byte[] frame in frames) { writer.Write(frame.Length); writer.Write(frame); }
                ScriptContext.Logger("MASK " + name + " size=" + size + " frames=" + frames.Length);
            }
        }
        using (SHA256 sha = SHA256.Create()) using (FileStream input = File.OpenRead(output))
            ScriptContext.Logger("MASK_SNAPSHOT bytes=" + input.Length + " sha256=" + BitConverter.ToString(sha.ComputeHash(input)).Replace("-", ""));
        ScriptContext.Logger("MVID=" + assembly.ManifestModule.ModuleVersionId + " character=" + owner.GetType().FullName + " scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }
}
