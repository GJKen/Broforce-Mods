using System;
using System.Drawing;
using Aquabro;

internal static class GroundVisualAlignmentTests
{
    public static void Main(string[] args)
    {
        int checks = 0;
        using (Bitmap atlas = new Bitmap(args[0]))
        {
            for (int cell = 0; cell < 107; cell++)
            {
                if (!(cell == 0 || cell == 6 || (cell >= 32 && cell <= 50) ||
                    (cell >= 96 && cell <= 106))) continue;
                int bottom = -1;
                for (int y = 0; y < 32; y++)
                    for (int x = 0; x < 32; x++)
                        if (atlas.GetPixel(cell % 32 * 32 + x, cell / 32 * 32 + y).A > 0)
                            bottom = Math.Max(bottom, y);
                if (bottom < 0) throw new Exception("Empty ground pose: " + cell);
                // 实际网格上边界是 Y+31；像素行 y 的下边界是 Y+30-y。
                float footHeight = 30 - bottom + TridentMovementAnimation.GroundBodyOffsetY(cell);
                if (footHeight != 0f)
                    throw new Exception("Ground pose " + cell + " floats or sinks by " + footHeight + " pixels.");
                checks++;
            }
        }
        foreach (int cell in new int[] { -1, 4, 5, 17, 22, 51, 63, 64, 75, 76,
            95, 107, 124, 145, 152, 160, 173, 184, 187, 192, 197, 352, 375, 512, 529 })
        {
            if (TridentMovementAnimation.GroundBodyOffsetY(cell) != 0f)
                throw new Exception("Ground alignment changed an unrelated pose: " + cell);
            checks++;
        }
        Console.WriteLine("Ground visual alignment: " + checks + " checks passed (32 atlas poses).");
    }
}
