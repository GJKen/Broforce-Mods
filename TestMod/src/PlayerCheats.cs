using System;
using HarmonyLib;

namespace TestMod
{
    public static partial class Plugin
    {
        private static int _invinciblePlayers;
        private static int _infiniteSpecialPlayers;
        private static int _autoRevivePlayers;

        public static bool IsPlayerInvincibleEnabled(int playerNumber)
        {
            return _behaviour != null && playerNumber >= 0 && playerNumber < 4 &&
                (_invinciblePlayers & (1 << playerNumber)) != 0;
        }

        public static bool IsPlayerInfiniteSpecialEnabled(int playerNumber)
        {
            return _behaviour != null && playerNumber >= 0 && playerNumber < 4 &&
                (_infiniteSpecialPlayers & (1 << playerNumber)) != 0;
        }

        public static void SetPlayerInvincible(int playerNumber, bool enabled)
        {
            SetPlayerCheat(playerNumber, PlayerCheat.Invincible, enabled);
        }

        // Keep the 0.3.5 API usable by existing scripts; the UI calls this ability invincibility.
        public static bool IsPlayerInfiniteLivesEnabled(int playerNumber)
        {
            return IsPlayerInvincibleEnabled(playerNumber);
        }

        public static void SetPlayerInfiniteLives(int playerNumber, bool enabled)
        {
            SetPlayerInvincible(playerNumber, enabled);
        }

        public static bool IsPlayerAutoReviveEnabled(int playerNumber)
        {
            return _behaviour != null && playerNumber >= 0 && playerNumber < 4 &&
                (_autoRevivePlayers & (1 << playerNumber)) != 0;
        }

        public static void SetPlayerAutoRevive(int playerNumber, bool enabled)
        {
            SetPlayerCheat(playerNumber, PlayerCheat.AutoRevive, enabled);
        }

        public static void SetPlayerInfiniteSpecial(int playerNumber, bool enabled)
        {
            SetPlayerCheat(playerNumber, PlayerCheat.Special, enabled);
        }

        private static void SetPlayerCheat(int playerNumber, PlayerCheat cheat, bool enabled)
        {
            if (playerNumber < 0 || playerNumber > 3)
                throw new ArgumentOutOfRangeException("playerNumber", "Use a player number from 0 to 3.");
            if (_behaviour == null) return;
            RefreshPlayerCheatSession();
            SendPlayerCheatCommand(playerNumber, cheat, enabled);
        }

        internal static void UpdatePlayerCheats()
        {
            UpdatePlayerCheatNetwork();
            UpdatePlayerRevival();
            for (var playerNumber = 0; playerNumber < 4; playerNumber++)
            {
                var player = HeroController.players[playerNumber];
                if (player == null || !player.IsMine || !HeroController.IsPlaying(playerNumber)) continue;
                if ((IsPlayerInvincibleEnabled(playerNumber) || IsPlayerAutoReviveEnabled(playerNumber)) && player.Lives < 1)
                    player.Lives = 1;

                var character = player.character;
                if (!IsPlayerInfiniteSpecialEnabled(playerNumber) || character == null ||
                    !character.IsMine || character.health <= 0 || character.destroyed) continue;
                // Pocketed ammo has its own stack, preserved by the patch below.
                var bro = character as BroBase;
                if (bro != null && bro.pockettedSpecialAmmo.Count > 0) continue;
                var ammo = System.Math.Max(1, character.originalSpecialAmmo);
                if (character.SpecialAmmo < ammo) character.SpecialAmmo = ammo;
            }
        }

        internal static bool ProtectPlayerFromDamage(TestVanDammeAnim character)
        {
            return character.IsHero && character.health > 0 && !character.destroyed &&
                IsPlayerInvincibleEnabled(character.playerNum);
        }
    }

    [HarmonyPatch(typeof(Player), "RemoveLife")]
    internal static class InfinitePlayerLivesPatch
    {
        private static bool Prefix(Player __instance)
        {
            return !Plugin.IsPlayerInvincibleEnabled(__instance.playerNum) &&
                !Plugin.IsPlayerAutoReviveEnabled(__instance.playerNum);
        }
    }

    [HarmonyPatch(typeof(TestVanDammeAnim), "Damage")]
    internal static class InfinitePlayerHealthPatch
    {
        private static bool Prefix(TestVanDammeAnim __instance, DamageType damageType)
        {
            return damageType == DamageType.OutOfBounds || !Plugin.ProtectPlayerFromDamage(__instance);
        }
    }

    [HarmonyPatch(typeof(TestVanDammeAnim), "Gib")]
    internal static class InfinitePlayerGibPatch
    {
        private static bool Prefix(TestVanDammeAnim __instance, DamageType damageType)
        {
            // Let the native out-of-bounds flow respawn the player instead of trapping them below the map.
            var allow = damageType == DamageType.OutOfBounds || !Plugin.ProtectPlayerFromDamage(__instance);
            if (allow) Plugin.CapturePlayerDeath(__instance);
            return allow;
        }
    }

    [HarmonyPatch(typeof(BroBase), "ReducePockettedSpecialAmmo")]
    internal static class InfinitePocketedSpecialPatch
    {
        private static bool Prefix(BroBase __instance)
        {
            return !Plugin.IsPlayerInfiniteSpecialEnabled(__instance.playerNum);
        }
    }
}
