namespace AquabroMod
{
    // Shared query implementation; generated files contain only their frame data.
    internal sealed class PixelMask
    {
        private readonly int size;
        private readonly byte[][] frames;

        internal PixelMask(int size, byte[][] frames)
        {
            this.size = size;
            this.frames = frames;
        }

        internal bool Contains(int frame, float x, float y, float margin)
        {
            return IntersectsRect(frame, x, y, x, y, margin);
        }

        // Exact pixel-square to rectangle distance, including collider edges.
        internal bool IntersectsRect(int frame, float left, float top, float right, float bottom, float margin)
        {
            if (frame < 0 || frame >= frames.Length) return false;
            if (left > right) { float swap = left; left = right; right = swap; }
            if (top > bottom) { float swap = top; top = bottom; bottom = swap; }
            int minX = System.Math.Max(0, (int)System.Math.Floor(left - margin) - 1);
            int maxX = System.Math.Min(size - 1, (int)System.Math.Floor(right + margin));
            int minY = System.Math.Max(0, (int)System.Math.Floor(top - margin) - 1);
            int maxY = System.Math.Min(size - 1, (int)System.Math.Floor(bottom + margin));
            byte[] mask = frames[frame];
            for (int py = minY; py <= maxY; py++)
            {
                float dy = System.Math.Max(0f, System.Math.Max(py - bottom, top - (py + 1)));
                for (int px = minX; px <= maxX; px++)
                {
                    int bit = py * size + px;
                    if ((mask[bit / 8] & (1 << (bit % 8))) == 0) continue;
                    float dx = System.Math.Max(0f, System.Math.Max(px - right, left - (px + 1)));
                    if (dx * dx + dy * dy <= margin * margin) return true;
                }
            }
            return false;
        }
    }
}
