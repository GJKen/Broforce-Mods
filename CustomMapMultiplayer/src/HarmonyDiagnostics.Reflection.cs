using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CustomMapMultiplayer
{
    // 反射兼容层：字段/属性读写、类型转换、游戏对象与房间信息访问。
    internal static partial class HarmonyDiagnostics
    {
        private static Type _connectTypeCache;
        private static MethodInfo _connectIsHostGetterCache;
        private static FieldInfo _playersPlayingFieldCache;

        private struct ConnectionOnlineState
        {
            internal bool IsOnline;
            internal bool ConnectIsOffline;
            internal bool LayerIsOffline;
            internal bool HasLayer;
            internal bool HasRoom;
            internal bool IsHost;
            internal bool IsOnlineRoomReady;
            internal bool MyIdHasBeenSet;
            internal bool ServerHasBeenSet;
            internal string ConnectType;
            internal string ConnectIsOfflineGetter;
            internal string LayerType;
            internal string RoomGetter;
            internal string Error;
        }

        private static MethodInfo GetPatchMethod(string name)
        {
            return typeof(HarmonyDiagnostics).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
        }

        internal static MethodInfo GetInstanceMethod(Type type, string name)
        {
            return type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, Type.EmptyTypes, null);
        }

        private static Type GetConnectTypeCached()
        {
            if (_connectTypeCache == null)
            {
                _connectTypeCache = AccessTools.TypeByName("Connect");
            }

            return _connectTypeCache;
        }

        private static MethodInfo GetConnectIsHostGetterCached(Type connectType)
        {
            if (_connectIsHostGetterCache == null && connectType != null)
            {
                _connectIsHostGetterCache = AccessTools.PropertyGetter(connectType, "IsHost");
            }

            return _connectIsHostGetterCache;
        }

                private static bool TryConvertHeroType(object value, out HeroType heroType)
        {
            heroType = HeroType.None;
            if (value == null)
            {
                return false;
            }

            if (value is HeroType)
            {
                heroType = (HeroType)value;
                return true;
            }

            try
            {
                heroType = (HeroType)Enum.Parse(typeof(HeroType), value.ToString(), true);
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal static object GetFieldOrPropertyValue(object instance, string name)
        {
            if (instance == null)
            {
                return null;
            }

            var type = instance.GetType();
            var field = type.GetField(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (field != null)
            {
                return field.GetValue(instance);
            }

            var property = type.GetProperty(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            return property == null || !property.CanRead
                ? null
                : property.GetValue(instance, null);
        }

        private static RoomInfo GetCurrentRoom()
        {
            try
            {
                return Connect.Layer.Room;
            }
            catch
            {
                return null;
            }
        }

        private static object GetCurrentConnectionLayer()
        {
            try
            {
                return Connect.Layer;
            }
            catch
            {
                return null;
            }
        }

        private static bool[] GetPlayersPlayingArray()
        {
            try
            {
                var field = _playersPlayingFieldCache;
                if (field == null)
                {
                    field = typeof(HeroController).GetField(
                        "playersPlaying",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    _playersPlayingFieldCache = field;
                }
                return field == null ? null : field.GetValue(null) as bool[];
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning(
                    "Reading HeroController.playersPlaying failed: " + exception);
                return null;
            }
        }

        private static object GetGameStateInstance(Type gameStateType)
        {
            if (gameStateType == null)
            {
                return null;
            }

            var instanceProperty = gameStateType.GetProperty(
                "Instance",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            return instanceProperty == null ? null : instanceProperty.GetValue(null, null);
        }

        private static string TrimRoomInfoString(string value)
        {
            return value == null ? string.Empty : value.Trim();
        }

        private static int GetRoomLevelNumber(RoomInfo room, int fallback)
        {
            try
            {
                return room.levelNumber;
            }
            catch
            {
                return fallback;
            }
        }

        private static void SetStaticFieldOrProperty(Type type, string name, object value)
        {
            if (type == null)
            {
                throw new MissingMemberException(name);
            }

            var field = type.GetField(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (field != null)
            {
                field.SetValue(null, value);
                return;
            }

            var property = type.GetProperty(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (property != null && property.CanWrite)
            {
                property.SetValue(null, value, null);
                return;
            }

            throw new MissingMemberException(type.FullName, name);
        }

        private static bool IsOnlineHost()
        {
            var connectType = GetConnectTypeCached();
            if (connectType == null || !IsOnline())
            {
                return false;
            }

            var hostGetter = GetConnectIsHostGetterCached(connectType);
            return hostGetter != null && Convert.ToBoolean(hostGetter.Invoke(null, null));
        }

        private static bool IsOnline()
        {
            return ReadConnectionOnlineState(false).IsOnline;
        }

        private static ConnectionOnlineState ReadConnectionOnlineState(bool includeReflectionMetadata)
        {
            var state = new ConnectionOnlineState
            {
                ConnectType = includeReflectionMetadata ? typeof(Connect).FullName : "<omitted>",
                ConnectIsOfflineGetter = includeReflectionMetadata
                    ? DescribeGetter(typeof(Connect), "IsOffline")
                    : "<omitted>"
            };

            try
            {
                var layer = Connect.Layer;
                state.HasLayer = layer != null;
                state.LayerType = layer == null ? "<null>" : layer.GetType().FullName;
                state.RoomGetter = !includeReflectionMetadata || layer == null
                    ? "<unavailable>"
                    : DescribeGetter(layer.GetType(), "Room");
                state.ConnectIsOffline = Connect.IsOffline;
                state.LayerIsOffline = layer == null || layer.IsOffline;
                state.HasRoom = layer != null && layer.Room != null;
                state.IsHost = layer != null && layer.IsHost;
                state.IsOnlineRoomReady = layer != null && layer.IsOnlineRoomReady;
                state.MyIdHasBeenSet = PID.MyIdHasBeenSet;
                state.ServerHasBeenSet = PID.ServerHasBeenSet;
                state.IsOnline = state.HasLayer && state.HasRoom &&
                    !state.ConnectIsOffline && !state.LayerIsOffline;
            }
            catch (Exception exception)
            {
                state.Error = exception.GetType().Name + ":" + exception.Message;
                state.IsOnline = false;
            }

            return state;
        }

        private static string DescribeGetter(Type type, string propertyName)
        {
            if (type == null)
            {
                return "<null-type>";
            }

            try
            {
                var property = type.GetProperty(
                    propertyName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                var getter = property == null ? null : property.GetGetMethod(true);
                return getter == null
                    ? "<unavailable>"
                    : getter.DeclaringType.FullName + "." + getter.Name + "()->" +
                        getter.ReturnType.FullName;
            }
            catch (Exception exception)
            {
                return "<error:" + exception.GetType().Name + ">";
            }
        }

        private static string FormatConnectionOnlineState(ConnectionOnlineState state)
        {
            return "connectType=" + state.ConnectType +
                ";connectIsOffline=" + state.ConnectIsOffline +
                ";connectIsOfflineGetter=" + state.ConnectIsOfflineGetter +
                ";layerType=" + state.LayerType +
                ";layerIsOffline=" + state.LayerIsOffline +
                ";roomPresent=" + state.HasRoom +
                ";roomGetter=" + state.RoomGetter +
                ";isHost=" + state.IsHost +
                ";roomReady=" + state.IsOnlineRoomReady +
                ";myIdSet=" + state.MyIdHasBeenSet +
                ";serverIdSet=" + state.ServerHasBeenSet +
                ";networkSessionActive=" + _networkSessionActive +
                ";error=" + (string.IsNullOrEmpty(state.Error) ? "none" : state.Error);
        }

        private static void SetFieldOrProperty(object instance, string name, object value)
        {
            var type = instance.GetType();
            var field = type.GetField(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (field != null)
            {
                field.SetValue(instance, value);
                return;
            }

            var property = type.GetProperty(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (property != null && property.CanWrite)
            {
                property.SetValue(instance, value, null);
                return;
            }

            throw new MissingMemberException(type.FullName, name);
        }

        private static int GetIntFieldOrProperty(object instance, string name)
        {
            return Convert.ToInt32(GetFieldOrPropertyValue(instance, name));
        }

        private static bool GetBoolFieldOrProperty(object instance, string name)
        {
            if (instance == null)
            {
                return false;
            }

            var type = instance.GetType();
            var field = type.GetField(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (field != null)
            {
                try
                {
                    return Convert.ToBoolean(field.GetValue(instance));
                }
                catch
                {
                    return false;
                }
            }

            var property = type.GetProperty(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (property != null && property.CanRead)
            {
                try
                {
                    return Convert.ToBoolean(property.GetValue(instance, null));
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        private static string GetStringFieldOrProperty(object instance, string name)
        {
            if (instance == null)
            {
                return string.Empty;
            }

            var type = instance.GetType();
            var field = type.GetField(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (field != null)
            {
                try
                {
                    var value = field.GetValue(instance);
                    return value == null ? string.Empty : Convert.ToString(value);
                }
                catch
                {
                    return string.Empty;
                }
            }

            var property = type.GetProperty(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (property != null && property.CanRead)
            {
                try
                {
                    var value = property.GetValue(instance, null);
                    return value == null ? string.Empty : Convert.ToString(value);
                }
                catch
                {
                    return string.Empty;
                }
            }

            return string.Empty;
        }
    }
}
