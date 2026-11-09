using HarmonyLib;
using System.Reflection;
using UnityEngine.SceneManagement;

namespace BroforceBugFix
{
    internal static class LobbyJoinStreamFix
    {
        private const string HarmonyId = "GJKen.BroforceBugFix.LobbyJoinStream";
        private static Harmony _harmony;

        internal static void Apply()
        {
            if (_harmony != null) return;
            _harmony = new Harmony(HarmonyId);
            _harmony.Patch(
                typeof(ConnectionLayer).GetMethod("JoinLobby"), null,
                new HarmonyMethod(typeof(LobbyJoinStreamFix).GetMethod(
                    "JoinLobbyPostfix", BindingFlags.Static | BindingFlags.NonPublic)),
                null, null);
        }

        internal static void Remove()
        {
            if (_harmony == null) return;
            _harmony.UnpatchAll(HarmonyId);
            _harmony = null;
        }

        private static void JoinLobbyPostfix()
        {
            if (!Networking.Networking.PauseStream ||
                SceneManager.GetActiveScene().name != LevelSelectionController.MainMenuScene)
                return;

            // Native GameState.LoadLevel pauses before the asynchronous Workshop download.
            // If that load never reaches OnStart, Disconnect leaves the stream paused.
            // JoinLobby has now discarded the old room; discard its RPCs before unpausing.
            RPCController.Instance.Reset();
            Networking.Networking.PauseStream = false;
            Plugin.Log("Cleared the previous room's paused RPC stream before joining a new lobby.");
        }
    }
}
