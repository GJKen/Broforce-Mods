using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace CustomMapMultiplayer
{
    internal static partial class HarmonyDiagnostics
    {
        private const float ChatInputRepeatDelaySeconds = 0.4f;
        private const float ChatInputRepeatIntervalSeconds = 0.06f;
        private static FieldInfo _keyboardSkipNextFrameField;
        private static bool _chatInputRepeatActive;
        private static KeyCode _chatInputRepeatKey;
        private static float _chatInputRepeatAt;

        private static void PatchChatPauseBoundary()
        {
            var keyboardUpdate = typeof(KeyboardInput).GetMethod(
                "Update", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, Type.EmptyTypes, null);
            var pauseToggle = typeof(PauseController).GetMethod(
                "TogglePause", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, Type.EmptyTypes, null);
            var submitMessage = typeof(MessageController).GetMethod(
                "SubmitMessage", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, new[] { typeof(PID), typeof(int) }, null);
            var keyboardPrefix = typeof(HarmonyDiagnostics).GetMethod(
                "KeyboardInputChatBoundaryPrefix", BindingFlags.NonPublic | BindingFlags.Static);
            var keyboardPostfix = typeof(HarmonyDiagnostics).GetMethod(
                "KeyboardInputChatRepeatPostfix", BindingFlags.NonPublic | BindingFlags.Static);
            var pausePostfix = typeof(HarmonyDiagnostics).GetMethod(
                "PauseControllerChatBoundaryPostfix", BindingFlags.NonPublic | BindingFlags.Static);
            var submitPostfix = typeof(HarmonyDiagnostics).GetMethod(
                "MessageControllerChatInputRepeatCleanupPostfix", BindingFlags.NonPublic | BindingFlags.Static);
            if (keyboardUpdate == null || pauseToggle == null || submitMessage == null ||
                keyboardPrefix == null || keyboardPostfix == null ||
                pausePostfix == null || submitPostfix == null)
            {
                DiagnosticLog.Warning("Chat boundary patch could not resolve its target methods.");
                return;
            }

            try
            {
                ClearChatInputRepeatState();
                _keyboardSkipNextFrameField = typeof(KeyboardInput).GetField(
                    "skipNextFrame", BindingFlags.NonPublic | BindingFlags.Static);
                _harmony.Patch(
                    keyboardUpdate,
                    new HarmonyMethod(keyboardPrefix),
                    new HarmonyMethod(keyboardPostfix),
                    null,
                    null);
                _harmony.Patch(pauseToggle, null, new HarmonyMethod(pausePostfix), null, null);
                _harmony.Patch(submitMessage, null, new HarmonyMethod(submitPostfix), null, null);
                DiagnosticLog.Info(
                    "Chat boundary patch enabled for KeyboardInput.Update, MessageController.SubmitMessage, " +
                    "and PauseController.TogglePause.");
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

        private static void KeyboardInputChatRepeatPostfix()
        {
            if (Connect.IsOffline || !KeyboardInput.open ||
                PauseController.pauseStatus != PauseStatus.UnPaused)
            {
                ClearChatInputRepeatState();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                StartChatInputRepeat(KeyCode.Backspace);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Delete))
            {
                StartChatInputRepeat(KeyCode.Delete);
                return;
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                StartChatInputRepeat(KeyCode.LeftArrow);
                return;
            }

            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                StartChatInputRepeat(KeyCode.RightArrow);
                return;
            }

            if (!_chatInputRepeatActive)
            {
                return;
            }

            if (!Input.GetKey(_chatInputRepeatKey))
            {
                ClearChatInputRepeatState();
                return;
            }

            if (Time.unscaledTime < _chatInputRepeatAt)
            {
                return;
            }

            if (_chatInputRepeatKey == KeyCode.Backspace)
            {
                SingletonMono<MessageController>.Instance.BackSpace();
            }
            else if (_chatInputRepeatKey == KeyCode.Delete)
            {
                SingletonMono<MessageController>.Instance.Delete();
            }
            else if (_chatInputRepeatKey == KeyCode.LeftArrow)
            {
                SingletonMono<MessageController>.Instance.MoveCursorLeft();
            }
            else
            {
                SingletonMono<MessageController>.Instance.MoveCursorRight();
            }
            _chatInputRepeatAt = Time.unscaledTime + ChatInputRepeatIntervalSeconds;
        }

        private static void StartChatInputRepeat(KeyCode key)
        {
            _chatInputRepeatActive = true;
            _chatInputRepeatKey = key;
            _chatInputRepeatAt = Time.unscaledTime + ChatInputRepeatDelaySeconds;
        }

        private static void MessageControllerChatInputRepeatCleanupPostfix()
        {
            ClearChatInputRepeatState();
        }

        private static void ClearChatInputRepeatState()
        {
            _chatInputRepeatActive = false;
            _chatInputRepeatKey = KeyCode.None;
            _chatInputRepeatAt = 0f;
        }

        private static void PauseControllerChatBoundaryPostfix()
        {
            ClearChatInputRepeatState();
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
