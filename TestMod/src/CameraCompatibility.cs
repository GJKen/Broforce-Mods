using System.Collections.Generic;
using UnityEngine;
using Net = Networking.Networking;

namespace TestMod
{
    internal sealed class CameraCompatibility
    {
        private sealed class RemoteTarget
        {
            internal PID Peer;
            internal IDWrapper Connection;
            internal Player Source;
            internal TestVanDammeAnim SourceCharacter;
            internal Transform Target;
            internal bool Wanted;
        }

        private readonly List<RemoteTarget> _targets = new List<RemoteTarget>();
        private HeroController _heroController;
        private int _sessionId = -1;

        internal void Update()
        {
            // SID is mutable: retain its value rather than the game's SID instance.
            if (_sessionId != Connect.SessionID.AsInt ||
                !object.ReferenceEquals(_heroController, HeroController.Instance))
            {
                _targets.Clear();
                _sessionId = Connect.SessionID.AsInt;
                _heroController = HeroController.Instance;
            }

            if (Connect.IsOffline || !Plugin.IsFocusedCameraEnabled || _heroController == null ||
                Map.MapData == null || Map.isEditing || GameModeController.LevelFinished ||
                SortOfFollow.ControlledByTriggerAction || CutsceneController.InCutscene)
            {
                Release();
                return;
            }

            var playerNumber = Plugin.GetCameraFollowPlayer();
            var source = playerNumber < 0 ? null : HeroController.players[playerNumber];
            if (source == null || source.character == null || source.character.health <= 0 ||
                source.playerTemporaryTarget != null)
            {
                Release();
                return;
            }

            foreach (var binding in _targets) binding.Wanted = false;
            for (var index = 0; index < 4; index++)
            {
                var peer = HeroController.PIDS[index];
                var player = HeroController.players[index];
                var connection = GetReadyConnection(peer);
                if (connection == null || player == null || player.character == null ||
                    player.character.health <= 0)
                    continue;

                var binding = FindTarget(peer);
                // A peer may control several players, but has only one shared camera.
                if (binding != null && binding.Wanted) continue;
                // Native temporary targets are projectiles. Another focused peer can instead
                // point its slot at one of our heroes; reusing that would create a feedback loop.
                var target = player.playerTemporaryTarget;
                if (target == null || target.GetComponent<Projectile>() == null)
                    target = player.character.transform;
                if (Registry.GetNID(target) == NID.NoID) continue;
                if (binding == null)
                {
                    binding = new RemoteTarget { Peer = peer };
                    _targets.Add(binding);
                }
                else if (binding.Source != source)
                {
                    Restore(binding);
                }

                if (binding.Source != source || binding.SourceCharacter != source.character ||
                    binding.Target != target || binding.Connection != connection)
                {
                    // Only replace our contribution on this peer. Its real player positions stay intact.
                    Net.RPC<int, Transform>(peer, HeroController.AddTemporaryPlayerTarget,
                        playerNumber, target, false);
                    binding.Source = source;
                    binding.SourceCharacter = source.character;
                    binding.Target = target;
                    binding.Connection = connection;
                }
                binding.Wanted = true;
            }

            for (var index = _targets.Count - 1; index >= 0; index--)
            {
                if (_targets[index].Wanted) continue;
                Restore(_targets[index]);
                _targets.RemoveAt(index);
            }
        }

        internal void Release()
        {
            foreach (var binding in _targets) Restore(binding);
            _targets.Clear();
        }

        private RemoteTarget FindTarget(PID peer)
        {
            foreach (var binding in _targets)
                if (binding.Peer == peer) return binding;
            return null;
        }

        private static IDWrapper GetReadyConnection(PID peer)
        {
            if (peer == null || peer.IsMine || Connect.IsOffline || Net.PauseStream)
                return null;

            IDWrapper connection;
            if (!Connect.Layer.PlayerIDPairs.TryGetValue(peer, out connection) ||
                !connection.HaveSentSyncStateToPlayer)
                return null;
            return connection;
        }

        private void Restore(RemoteTarget binding)
        {
            if (_sessionId != Connect.SessionID.AsInt ||
                !object.ReferenceEquals(_heroController, HeroController.Instance) ||
                binding.Source == null || !binding.Source.IsMine ||
                HeroController.players[binding.Source.playerNum] != binding.Source ||
                GetReadyConnection(binding.Peer) != binding.Connection)
                return;

            // A native skill may have just broadcast a new target. Do not erase it on cleanup.
            var nativeTarget = binding.Source.playerTemporaryTarget;
            if (nativeTarget != null)
                Net.RPC<int, Transform>(binding.Peer, HeroController.AddTemporaryPlayerTarget,
                    binding.Source.playerNum, nativeTarget, false);
            else
                Net.RPC<int>(binding.Peer, HeroController.RemoveTemporaryPlayerTarget,
                    binding.Source.playerNum, false);
        }
    }
}
