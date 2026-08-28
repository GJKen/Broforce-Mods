using System;
using System.Reflection;
using HarmonyLib;

namespace CustomMapMultiplayer
{
    internal static class FrpDirectNetworkManager
    {
        private const string HarmonyId = "GJKen.CustomMapMultiplayer.FrpDirectLayer";
        private static readonly FieldInfo ConnectLayerField = AccessTools.Field(typeof(Connect), "layer");
        private static Harmony _harmony;
        private static FrpDirectTransport _transport;
        private static FrpDirectLayer _layer;

        internal static void ApplyConfiguredLayer(FrpDirectTransport transport)
        {
            _transport = transport;
            EnsurePatched();

            var shouldUseFrp = ShouldCreateFrpLayer();
            var current = GetCurrentLayer();
            if (shouldUseFrp)
            {
                if (current == null || current is FrpDirectLayer)
                {
                    return;
                }
                if (current.Room != null)
                {
                    DiagnosticLog.Warning(
                        "FRP_DIRECT game layer change is deferred until the current room is left.");
                    return;
                }

                current.ShutDown();
                SetCurrentLayer(null);
                DiagnosticLog.Info(
                    "FRP_DIRECT game layer enabled; the next Broforce networking access will use FRP.");
                return;
            }

            var frpLayer = current as FrpDirectLayer;
            if (frpLayer != null)
            {
                DisconnectFrpLayer(frpLayer, "FRP Direct game layer disabled");
            }
        }

        internal static void Stop()
        {
            var current = GetCurrentLayer() as FrpDirectLayer;
            if (current != null)
            {
                DisconnectFrpLayer(current, "diagnostics stopped");
            }

            if (_harmony != null)
            {
                try
                {
                    _harmony.UnpatchAll(HarmonyId);
                }
                catch (Exception exception)
                {
                    DiagnosticLog.Warning(
                        "FRP_DIRECT platform patch removal failed; error=" +
                        exception.GetType().Name + ".");
                }
            }

            _harmony = null;
            _layer = null;
            _transport = null;
        }

        internal static void ReleaseLayer(FrpDirectLayer layer)
        {
            if (layer == null)
            {
                return;
            }

            if (ReferenceEquals(_layer, layer))
            {
                _layer = null;
            }
            if (ReferenceEquals(GetCurrentLayer(), layer))
            {
                SetCurrentLayer(null);
            }
        }

        private static bool GetConnectionLayerPrefix(ref ConnectionLayer __result)
        {
            if (!ShouldCreateFrpLayer())
            {
                return true;
            }

            if (_layer == null)
            {
                _layer = new FrpDirectLayer(_transport);
                DiagnosticLog.Info(
                    "FRP_DIRECT platform factory created the Broforce connection layer; role=" +
                    (_transport.IsHost ? "host" : "client") + ".");
            }

            __result = _layer;
            return false;
        }

        private static bool ShouldCreateFrpLayer()
        {
            return Plugin.ShouldUseFrpDirectGameLayer && _transport != null &&
                   _transport.IsEnabled;
        }

        private static void EnsurePatched()
        {
            if (_harmony != null)
            {
                return;
            }

            var target = typeof(Utility.Platforms.Platform).GetMethod(
                "GetConnectionLayer",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                Type.EmptyTypes,
                null);
            var prefix = typeof(FrpDirectNetworkManager).GetMethod(
                "GetConnectionLayerPrefix",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (target == null || prefix == null)
            {
                DiagnosticLog.Error(
                    "FRP_DIRECT could not resolve the platform connection-layer factory.");
                return;
            }

            try
            {
                _harmony = new Harmony(HarmonyId);
                _harmony.Patch(target, new HarmonyMethod(prefix), null, null, null);
                DiagnosticLog.Info(
                    "FRP_DIRECT platform connection-layer selector installed.");
            }
            catch (Exception exception)
            {
                _harmony = null;
                DiagnosticLog.Error(
                    "FRP_DIRECT platform selector installation failed; error=" +
                    exception.GetType().Name + ".");
            }
        }

        private static ConnectionLayer GetCurrentLayer()
        {
            return ConnectLayerField == null
                ? null
                : ConnectLayerField.GetValue(null) as ConnectionLayer;
        }

        private static void SetCurrentLayer(ConnectionLayer layer)
        {
            if (ConnectLayerField != null)
            {
                ConnectLayerField.SetValue(null, layer);
            }
        }

        private static void DisconnectFrpLayer(FrpDirectLayer layer, string reason)
        {
            try
            {
                if (layer.Room != null)
                {
                    Connect.Disconnect();
                }
                else
                {
                    layer.ShutDown();
                }
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning(
                    "FRP_DIRECT layer cleanup failed; error=" +
                    exception.GetType().Name + ".");
                layer.Dispose();
                ReleaseLayer(layer);
            }

            DiagnosticLog.Info("FRP_DIRECT Broforce layer released; reason=" + reason + ".");
        }
    }
}
