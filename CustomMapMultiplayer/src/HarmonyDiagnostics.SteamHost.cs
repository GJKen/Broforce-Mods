using HarmonyLib;
using System.Reflection;
using Steamworks;
using UnityEngine;

namespace CustomMapMultiplayer
{
    internal static partial class HarmonyDiagnostics
    {
        private static SteamLayer _steamHostLayer;
        private static CSteamID _steamHostLobby;
        private static int _steamHostFrame = -1;
        private static bool _steamHostResult;

        private static void PatchSteamHostQueryCache()
        {
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            _harmony.Patch(AccessTools.PropertyGetter(typeof(SteamLayer), "IsHost"),
                new HarmonyMethod(typeof(HarmonyDiagnostics).GetMethod("SteamHostQueryPrefix", flags)),
                new HarmonyMethod(typeof(HarmonyDiagnostics).GetMethod("SteamHostQueryPostfix", flags)),
                null, null);

            // Ownership changes arrive through Steam callbacks. Clear before each
            // lobby event as well so migration handlers cannot reuse an old role.
            var clear = new HarmonyMethod(typeof(HarmonyDiagnostics).GetMethod("ClearSteamHostQueryCache", flags));
            _harmony.Patch(typeof(SteamAPI).GetMethod("RunCallbacks"), clear, clear, null, null);
            _harmony.Patch(typeof(SteamLayer).GetMethod("LobbyChatUpdate_Callback", flags),
                clear, null, null, null);
            _harmony.Patch(typeof(SteamLayer).GetMethod("LobbyDataUpdate_Callback", flags),
                clear, null, null, null);
        }

        private static bool SteamHostQueryPrefix(SteamLayer __instance, ref bool __result,
            out bool __state)
        {
            __state = false;
            // The offline getter already avoids Steam API calls.
            if (__instance.IsOffline) return true;

            if (ReferenceEquals(_steamHostLayer, __instance) &&
                _steamHostLobby == __instance.LobbySteamId && _steamHostFrame == Time.frameCount)
            {
                __result = _steamHostResult;
                return false;
            }

            __state = true;
            return true;
        }

        private static void SteamHostQueryPostfix(SteamLayer __instance, bool __result,
            bool __state)
        {
            if (!__state) return;
            // Every cache miss uses the original Steam query, including guest false.
            _steamHostLayer = __instance;
            _steamHostLobby = __instance.LobbySteamId;
            _steamHostFrame = Time.frameCount;
            _steamHostResult = __result;
        }

        private static void ClearSteamHostQueryCache()
        {
            _steamHostLayer = null;
            _steamHostFrame = -1;
        }
    }
}
