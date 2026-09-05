using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace CustomMapMultiplayer
{
    internal static partial class HarmonyDiagnostics
    {
        private static FieldInfo _keyboardSkipNextFrameField;

        private static void PatchChatPauseBoundary()
        {
            var keyboardUpdate = typeof(KeyboardInput).GetMethod(
                "Update", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, Type.EmptyTypes, null);
            var pauseToggle = typeof(PauseController).GetMethod(
                "TogglePause", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, Type.EmptyTypes, null);
            var keyboardPrefix = typeof(HarmonyDiagnostics).GetMethod(
                "KeyboardInputChatBoundaryPrefix", BindingFlags.NonPublic | BindingFlags.Static);
            var pausePostfix = typeof(HarmonyDiagnostics).GetMethod(
                "PauseControllerChatBoundaryPostfix", BindingFlags.NonPublic | BindingFlags.Static);
            if (keyboardUpdate == null || pauseToggle == null ||
                keyboardPrefix == null || pausePostfix == null)
            {
                DiagnosticLog.Warning("Chat Escape boundary patch could not resolve its target methods.");
                return;
            }

            try
            {
                _keyboardSkipNextFrameField = typeof(KeyboardInput).GetField(
                    "skipNextFrame", BindingFlags.NonPublic | BindingFlags.Static);
                _harmony.Patch(keyboardUpdate, new HarmonyMethod(keyboardPrefix), null, null, null);
                _harmony.Patch(pauseToggle, null, new HarmonyMethod(pausePostfix), null, null);
                DiagnosticLog.Info(
                    "Chat Escape boundary patch enabled for KeyboardInput.Update and PauseController.TogglePause.");
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning("Chat Escape boundary patch failed: " + exception);
            }
        }

        private static bool KeyboardInputChatBoundaryPrefix()
        {
            if (!Input.GetKeyDown(KeyCode.Return) || Connect.IsOffline ||
                _keyboardSkipNextFrameField == null ||
                (bool)_keyboardSkipNextFrameField.GetValue(null))
            {
                return true;
            }

            if (PauseController.pauseStatus != PauseStatus.UnPaused)
            {
                return true;
            }

            var campaignMenu = SingletonMono<NewCustomCampaignMenu>.Instance;
            if (campaignMenu != null &&
                campaignMenu.highlightState != NewCustomCampaignMenu.HighlightState.Chat)
            {
                return true;
            }

            KeyboardInput.Toggle();
            return false;
        }

        private static void PauseControllerChatBoundaryPostfix()
        {
            if (PauseController.pauseStatus != PauseStatus.UnPaused)
            {
                return;
            }

            var pauseMenu = PauseMenu.instance;
            if (pauseMenu != null && pauseMenu.MenuActive)
            {
                pauseMenu.MenuActive = false;
                DiagnosticLog.Trace(
                    "Pause menu active flag synchronized after Escape returned to UnPaused.");
            }
        }
    }
}
