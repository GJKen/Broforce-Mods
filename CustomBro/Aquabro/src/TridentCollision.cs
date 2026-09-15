using UnityEngine;

namespace Aquabro
{
    internal static class TridentCollision
    {
        internal static int SolidMask(int playerNum)
        {
            int mask = (1 << LayerMask.NameToLayer("Ground")) |
                (1 << LayerMask.NameToLayer("LargeObjects")) |
                (1 << LayerMask.NameToLayer("FLUI")) |
                (1 << LayerMask.NameToLayer("MobileBarriers")) |
                (1 << LayerMask.NameToLayer("IndestructibleGround")) |
                (1 << LayerMask.NameToLayer("DirtyHippie"));
            if (playerNum < 0) mask |= 1 << LayerMask.NameToLayer("FriendlyBarriers");
            return mask;
        }

        // 中央戟尖与两侧戟尖共同检测，取最近的遮挡。
        internal static bool Trace(float x, float y, int direction, float distance,
            int mask, out RaycastHit nearest)
        {
            nearest = new RaycastHit();
            bool found = false;
            float bestDistance = distance + 1f;
            for (int i = -1; i <= 1; i++)
            {
                RaycastHit hit;
                if (Physics.Raycast(new Vector3(x, y + i * 3f, 0f),
                    new Vector3(direction, 0f, 0f), out hit, distance, mask) &&
                    hit.distance < bestDistance)
                {
                    nearest = hit;
                    bestDistance = hit.distance;
                    found = true;
                }
            }
            return found;
        }

        internal static bool CanReachUnit(float x, float y, Unit unit, int mask)
        {
            float distance = Mathf.Abs(unit.X - x);
            if (distance < 0.1f) return true;
            RaycastHit hit;
            if (!Physics.Raycast(new Vector3(x, y, 0f),
                new Vector3(Mathf.Sign(unit.X - x), 0f, 0f), out hit, distance, mask))
                return true;
            return hit.collider.transform == unit.transform ||
                hit.collider.transform.IsChildOf(unit.transform);
        }

        internal static bool IsInStrikeEnvelope(float targetDeltaX, float direction,
            float reach, float targetRadius)
        { return TridentStrikeGeometry.IsInEnvelope(targetDeltaX, direction, reach, targetRadius); }
    }
}
