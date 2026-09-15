namespace Aquabro
{
    internal static class TridentStrikeGeometry
    {
        internal static bool IsInEnvelope(float targetDeltaX, float direction,
            float reach, float targetRadius)
        {
            float forwardDistance = targetDeltaX * direction;
            return forwardDistance >= -targetRadius &&
                forwardDistance <= reach + targetRadius;
        }
    }
}
