namespace CustomMapMultiplayer
{
    // Keeps the existing diagnostics call site separate from the Swap Bros adapter.
    internal static class OptionalBroModDiagnostics
    {
        internal static bool TryGetAlwaysChosenHeroType(int playerNum, out HeroType heroType)
        {
            return SwapBrosCompatibility.TryGetAlwaysChosenHeroType(playerNum, out heroType);
        }

        internal static void LogCompatibilitySnapshot(string trigger)
        {
            SwapBrosCompatibility.LogCompatibilitySnapshot(trigger);
        }
    }
}
