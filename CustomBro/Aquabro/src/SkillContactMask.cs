using UnityEngine;

namespace AquabroMod
{
    // World-space contact for the 128px skill sheets, anchored at source (64,112).
    internal struct SkillContactMask
    {
        internal readonly int Kind;
        internal readonly float X, Y, Facing, Scale;
        private readonly int frame;
        private readonly PixelMask mask;

        internal SkillContactMask(int kind, float x, float y, float facing, float scale, int frame)
        {
            Kind = kind; X = x; Y = y; Facing = facing; Scale = scale; this.frame = frame;
            switch (kind)
            {
                case 0: mask = TidalWavePixelMask.Mask; break;
                case 1: mask = SharkPixelMask.Mask; break;
                case 2: mask = MaelstromPixelMask.Mask; break;
                default: mask = SpringPixelMask.Mask; break;
            }
        }

        private float PixelX(float x) { return 64f + (x - X) * Facing / Scale; }
        private float PixelY(float y) { return 112f - (y - Y) / Scale; }

        internal bool Contains(float x, float y, float margin)
        {
            return mask.Contains(frame, PixelX(x), PixelY(y), margin / Scale);
        }

        internal bool IntersectsRect(Bounds bounds)
        {
            return IntersectsRect(bounds.min.x, bounds.min.y, bounds.max.x, bounds.max.y);
        }

        internal bool IntersectsRect(float left, float bottom, float right, float top)
        {
            // Keep the five-pixel margin above the support floor/waterline.
            if (top <= Y + 1f) return false;
            bottom = Mathf.Max(bottom, Y + 1f);
            return mask.IntersectsRect(frame, PixelX(left), PixelY(top), PixelX(right), PixelY(bottom), 5f / Scale);
        }

        internal bool IntersectsBody(Unit target)
        {
            float halfWidth = Mathf.Max(1f, target.width * .5f);
            return IntersectsRect(target.X - halfWidth, target.Y, target.X + halfWidth,
                target.Y + Mathf.Max(target.headHeight, target.height));
        }
    }
}
