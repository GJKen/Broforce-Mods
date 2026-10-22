using System;
using AquabroMod;

internal static class PixelMaskTests
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }

    internal static void Run()
    {
        // One known square [3,4] x [4,5], with a 3-4-5 diagonal boundary.
        byte[] data = new byte[8]; data[4] = 1 << 3;
        PixelMask mask = new PixelMask(8, new byte[][] { data, new byte[8] });
        Check(mask.Contains(0, 3.5f, 4.5f, 0f), "Opaque pixel centre missed");
        Check(mask.Contains(0, 4f, 5f, 0f), "Pixel corner must be inclusive");
        Check(!mask.Contains(0, 4.001f, 5f, 0f), "Transparent point hit");
        Check(mask.Contains(0, 7f, 9f, 5f), "Exact diagonal margin missed");
        Check(!mask.Contains(0, 7f, 9f, 4.999f), "Diagonal margin became a square");
        Check(mask.IntersectsRect(0, 7f, 9f, 10f, 12f, 5f), "Rectangle corner margin missed");
        Check(mask.IntersectsRect(0, 10f, 12f, 7f, 9f, 5f), "Reversed rectangle edges differ");
        Check(!mask.IntersectsRect(0, 7f, 9f, 10f, 12f, 4.999f), "Rectangle margin too wide");
        Check(mask.IntersectsRect(0, -20f, -20f, 20f, 20f, 0f), "Clipped rectangle lost pixel");
        Check(!mask.Contains(-1, 3.5f, 4.5f, 100f) && !mask.Contains(2, 3.5f, 4.5f, 100f), "Invalid frame hit");
        Check(!mask.IntersectsRect(1, -20f, -20f, 20f, 20f, 100f), "Empty frame hit");

        PixelMask[] masks = { TidalWavePixelMask.Mask, SharkPixelMask.Mask, MaelstromPixelMask.Mask, SpringPixelMask.Mask };
        foreach (float facing in new float[] { -1f, 1f })
        foreach (float scale in new float[] { .65f, 1f, 1.1f })
        for (int kind = 0; kind < masks.Length; kind++)
        {
            SkillContactMask contact = new SkillContactMask(kind, 213f, -91f, facing, scale, 10);
            for (int py = 0; py < 128; py += 3)
            for (int px = 0; px < 128; px += 3)
            {
                float x = 213f + (px + .5f - 64f) * facing * scale;
                float y = -91f + (112f - py - .5f) * scale;
                Check(contact.Contains(x, y, 0f) == masks[kind].Contains(10, px + .5f, py + .5f, 0f),
                    "Translated/mirrored/scaled point changed mask");
                Check(!contact.IntersectsRect(x - 2f, -1000f, x + 2f, -90f), "Rectangle hit below waterline");
                if (y > -89f)
                    Check(contact.IntersectsRect(x, y, x, y) == masks[kind].Contains(10, px + .5f, py + .5f, 5f / scale),
                        "World rectangle margin changed");
            }
        }
        Console.WriteLine("PASS shared mask: point/rectangle edges; diagonal margin; reversed bounds; clipping; invalid/empty frames; world translation/mirror/scale/waterline");
    }
}
