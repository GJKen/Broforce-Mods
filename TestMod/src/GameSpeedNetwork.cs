using System;
using Networking;
using UnityEngine;

namespace TestMod
{
    public static partial class Plugin
    {
        private enum SpeedCommand { Enable, Speed, Pause, Step, Multiplier }
        private static PID _speedNetworkHost;
        private static byte _speedNetworkSession;
        private static float _nextSpeedSyncAt;

        internal static bool CanStepGameFrame
        {
            get
            {
                return PauseController.pauseStatus == PauseStatus.UnPaused ||
                    (!Connect.IsOffline && PauseController.pauseStatus == PauseStatus.MenuPause);
            }
        }

        private static bool SendGameSpeedCommand(SpeedCommand command, float value)
        {
            if (Connect.IsOffline || _speedController == null) return false;
            RefreshGameSpeedSession();
            if (PID.IamServer())
                ApplyGameSpeedCommand(command, value);
            else
            {
                global::Networking.Networking.RPC<int, float>(PID.TargetServer, true, false,
                    new RpcSignature<int, float>(RequestGameSpeedRPC), (int)command, value);
                RPCBatcher.FlushQueue();
            }
            return true;
        }

        [AllowedRPC]
        private static void RequestGameSpeedRPC(int command, float value)
        {
            if (Connect.IsOffline || !PID.IamServer() || _speedController == null ||
                RPCController.CurrentRPC == null ||
                !Connect.Layer.PlayerIDPairs.ContainsKey(RPCController.CurrentRPC.messageInfo.Sender) ||
                command < (int)SpeedCommand.Enable || command > (int)SpeedCommand.Multiplier ||
                float.IsNaN(value) || float.IsInfinity(value) || value < 0f || value > 4f) return;
            RefreshGameSpeedSession();
            ApplyGameSpeedCommand((SpeedCommand)command, value);
        }

        private static void ApplyGameSpeedCommand(SpeedCommand command, float value)
        {
            var enabled = Settings.EnableGameSpeed;
            var speed = Settings.GameSpeed;
            var paused = IsGamePaused;
            switch (command)
            {
                case SpeedCommand.Enable: enabled = value != 0f; break;
                case SpeedCommand.Speed:
                    enabled = true;
                    paused = value == 0f;
                    if (value > 0f) speed = Mathf.Max(0.01f, value);
                    break;
                case SpeedCommand.Pause: enabled = true; paused = value != 0f; break;
                case SpeedCommand.Multiplier: speed = Mathf.Max(0.01f, value); break;
                case SpeedCommand.Step:
                    if (!CanStepGameFrame) return;
                    enabled = true;
                    paused = true;
                    break;
            }
            ApplyGameSpeedState(enabled, speed, paused, command == SpeedCommand.Step);
            BroadcastGameSpeed(command == SpeedCommand.Step);
        }

        private static void ApplyGameSpeedState(bool enabled, float speed, bool paused, bool step)
        {
            var changed = Settings.EnableGameSpeed != enabled || Settings.GameSpeed != speed;
            Settings.EnableGameSpeed = enabled;
            Settings.GameSpeed = speed;
            if (IsGamePaused != (enabled && paused))
                _speedController.SetPaused(enabled && paused, true);
            else
                _speedController.ApplySettings();
            if (enabled && paused && step) _speedController.QueueNetworkFrameStep();
            if (changed) SaveSettings(ModEntry);
        }

        internal static void SetPlaybackSpeed(float speed)
        {
            if (SendGameSpeedCommand(SpeedCommand.Multiplier, speed)) return;
            Settings.GameSpeed = speed;
            if (_speedController != null) _speedController.ApplySettings();
        }

        private static void BroadcastGameSpeed(bool step)
        {
            global::Networking.Networking.RPC<bool, float, bool, bool>(PID.TargetOthers, true, false,
                new RpcSignature<bool, float, bool, bool>(ReceiveGameSpeedRPC),
                Settings.EnableGameSpeed, Settings.GameSpeed, IsGamePaused, step);
            RPCBatcher.FlushQueue();
        }

        [AllowedRPC]
        private static void ReceiveGameSpeedRPC(bool enabled, float speed, bool paused, bool step)
        {
            if (Connect.IsOffline || _speedController == null || RPCController.CurrentRPC == null ||
                RPCController.CurrentRPC.messageInfo.Sender != PID.ServerID ||
                float.IsNaN(speed) || float.IsInfinity(speed) || speed < 0.01f || speed > 4f) return;
            RefreshGameSpeedSession();
            ApplyGameSpeedState(enabled, speed, paused, step);
        }

        internal static void UpdateGameSpeedNetwork()
        {
            if (Connect.IsOffline)
            {
                if (_speedNetworkHost != null)
                {
                    _speedController.SetPaused(false);
                    ResetGameSpeedNetwork();
                }
                return;
            }
            RefreshGameSpeedSession();
            if (PID.IamServer() && Time.realtimeSinceStartup >= _nextSpeedSyncAt)
            {
                _nextSpeedSyncAt = Time.realtimeSinceStartup + 1f;
                // Also initializes late joiners. A snapshot never replays a step.
                BroadcastGameSpeed(false);
            }
        }

        private static void RefreshGameSpeedSession()
        {
            if (_speedNetworkHost == PID.ServerID && _speedNetworkSession == Connect.SessionID.AsByte) return;
            // Resume on scene/session changes and host migration; never replay an old queued step.
            _speedController.SetPaused(false);
            _speedNetworkHost = PID.ServerID;
            _speedNetworkSession = Connect.SessionID.AsByte;
            _nextSpeedSyncAt = 0f;
        }

        internal static void UpdateFrozenNetworkObjects()
        {
            if (Connect.IsOffline) return;
            // State packets arrive after the simulation frame. Apply remote interpolation while
            // frozen too, so a single step becomes visible on the other screen without another step.
            foreach (var networkObject in Registry.ComponentsToSync.Values)
                if (networkObject != null && !networkObject.IsMine) networkObject.RunSync();
        }

        private static void ResetGameSpeedNetwork()
        {
            _speedNetworkHost = null;
            _nextSpeedSyncAt = 0f;
        }
    }
}
