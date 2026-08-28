using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BroforceOnlineDiagnostics
{
    // 反射兼容层：字段/属性读写、类型转换、游戏对象与房间信息访问。
    internal static partial class HarmonyDiagnostics
    {
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

        private static object GetFieldOrPropertyValue(object instance, string name)
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

        private static MethodInfo FindSteamMatchmakingMethod(Type type, string name, int parameterCount)
        {
            if (type == null)
            {
                return null;
            }

            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            foreach (var method in methods)
            {
                if (method.Name == name && method.GetParameters().Length == parameterCount)
                {
                    return method;
                }
            }

            return null;
        }

        private static RoomInfo GetCurrentRoom()
        {
            try
            {
                var connectType = AccessTools.TypeByName("Connect");
                var layerGetter = connectType == null
                    ? null
                    : AccessTools.PropertyGetter(connectType, "Layer");
                var layer = layerGetter == null ? null : layerGetter.Invoke(null, null);
                var connectionType = AccessTools.TypeByName("ConnectionLayer");
                var roomGetter = connectionType == null
                    ? null
                    : AccessTools.PropertyGetter(connectionType, "Room");
                return roomGetter == null ? null : roomGetter.Invoke(layer, null) as RoomInfo;
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
                var connectType = AccessTools.TypeByName("Connect");
                var layerGetter = connectType == null
                    ? null
                    : AccessTools.PropertyGetter(connectType, "Layer");
                return layerGetter == null ? null : layerGetter.Invoke(null, null);
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
                var field = typeof(HeroController).GetField(
                    "playersPlaying",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
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

        private static string GetRoomInfoString(RoomInfo room, string fieldName)
        {
            var field = typeof(RoomInfo).GetField(
                fieldName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                var value = field.GetValue(room) as string;
                return value == null ? string.Empty : value.Trim();
            }

            var property = typeof(RoomInfo).GetProperty(
                fieldName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null && property.CanRead)
            {
                var value = property.GetValue(room, null) as string;
                return value == null ? string.Empty : value.Trim();
            }

            return string.Empty;
        }

        private static int GetRoomInfoInt(RoomInfo room, string fieldName, int fallback)
        {
            var field = typeof(RoomInfo).GetField(
                fieldName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                try
                {
                    return Convert.ToInt32(field.GetValue(room));
                }
                catch
                {
                    return fallback;
                }
            }

            return fallback;
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
            var connectType = AccessTools.TypeByName("Connect");
            if (connectType == null || !IsOnline())
            {
                return false;
            }

            var hostGetter = AccessTools.PropertyGetter(connectType, "IsHost");
            return hostGetter != null && Convert.ToBoolean(hostGetter.Invoke(null, null));
        }

        private static bool IsOnline()
        {
            var connectType = AccessTools.TypeByName("Connect");
            if (connectType == null)
            {
                return false;
            }

            var offlineGetter = AccessTools.PropertyGetter(connectType, "IsOffline");
            return offlineGetter == null || !Convert.ToBoolean(offlineGetter.Invoke(null, null));
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
            if (instance == null)
            {
                return 0;
            }

            var type = instance.GetType();
            var field = type.GetField(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (field != null)
            {
                return Convert.ToInt32(field.GetValue(instance));
            }

            var property = type.GetProperty(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (property != null && property.CanRead)
            {
                return Convert.ToInt32(property.GetValue(instance, null));
            }

            return 0;
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
