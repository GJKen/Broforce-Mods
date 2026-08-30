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
    // 方法追踪：追踪消息构建、参数格式化、敏感信息脱敏、去重缓存。
    internal static partial class HarmonyDiagnostics
    {
        private static readonly string[] RoomInfoTraceFields =
        {
            "gameMode", "campaignName", "CurrentSceneName", "capacity", "_playerCount",
            "returnToWorldMap", "levelNumber", "totalLevels", "worldMapProgress",
            "liberatedAreas", "invalidInfo", "hardMode", "hardcoreMode"
        };
        private static readonly string[] GameStateTraceFields =
        {
            "_sceneToLoad", "_campaignName", "levelNumber", "customLevelID",
            "loadCustomCampaign", "loadMode", "gameMode", "levelEditorActive",
            "returnToWorldMap", "arcadeHardMode", "persistPastLevelLoad"
        };
        private static readonly string[] LevelSelectionControllerTraceFields =
        {
            "_levelFileNameToLoad", "JoinScene", "CampaignScene", "OnlineCampaign",
            "OfflineCampaign", "DefaultCampaign", "loadPublishedCampaign", "isOnlineCampaign",
            "currentWorkshopLevel"
        };
        private static readonly string[] GameModeControllerTraceFields =
        {
            "switchingLevel", "nextScene", "levelHasStarted", "levelFinished",
            "waitingForAllPlayersToReady", "switchSilently"
        };
        private static readonly string[] WorkshopLevelDetailsTraceFields =
        {
            "name", "fileid", "fileName", "tags", "isWWBLevel", "wasCompletedSuccessfully"
        };
        private static readonly string[] CampaignTraceFields = { "name", "levels", "brodownLevel" };
        private static readonly string[] CampaignHeaderTraceFields =
        {
            "name", "length", "md5", "isPublished", "gameMode"
        };
        private static readonly string[] MakeOnlineMenuTraceFields =
        {
            "state", "playerLimit", "canChangePassword", "canChangeName"
        };
        private static readonly string[] HeroControllerTraceFields =
        {
            "playersPlaying", "players", "PIDS", "playerControllerIDs",
            "heroesHaveBeenReleasedFromTransport", "brosHaveBeenReleased",
            "WaitForAllPlayersToSpawnBeforeStarting", "AllPlayersHaveJoined"
        };
        private static readonly string[] PlayerTraceFields =
        {
            "playerNum", "lives", "firstDeployment", "_awaitingHeroTypeFromServer", "heroType"
        };
        private static readonly Dictionary<MethodBase, ParameterInfo[]> TraceParametersCache =
            new Dictionary<MethodBase, ParameterInfo[]>();
        private static readonly Dictionary<MethodBase, string> TraceMethodDescriptionCache =
            new Dictionary<MethodBase, string>();
        private static readonly Dictionary<Type, Dictionary<string, FieldInfo>> TraceFieldCache =
            new Dictionary<Type, Dictionary<string, FieldInfo>>();
        private static readonly Dictionary<Type, Dictionary<string, PropertyInfo>> TracePropertyCache =
            new Dictionary<Type, Dictionary<string, PropertyInfo>>();

        private static string BuildTraceMessage(
            MethodBase method,
            object instance,
            object[] arguments)
        {
            var builder = new StringBuilder();
            builder.Append(DescribeMethod(method));
            builder.Append("(");

            var parameters = GetTraceParameters(method);
            for (var index = 0; index < parameters.Length; index++)
            {
                if (index > 0)
                {
                    builder.Append(", ");
                }

                var parameter = parameters[index];
                builder.Append(parameter.Name);
                builder.Append("=");
                var value = arguments != null && index < arguments.Length ? arguments[index] : null;
                builder.Append(FormatArgument(parameter.Name, value));
            }

            builder.Append(")");
            var state = BuildSafeObjectSummary(instance);
            if (!string.IsNullOrEmpty(state))
            {
                builder.Append("; state=");
                builder.Append(state);
            }

            return builder.ToString();
        }

        private static string FormatArgument(string parameterName, object value)
        {
            if (IsSensitiveName(parameterName))
            {
                return "<redacted>";
            }

            if (value == null)
            {
                return "null";
            }

            if (value is Vector3)
            {
                return FormatVector3((Vector3)value);
            }

            var summary = BuildSafeObjectSummary(value);
            if (!string.IsNullOrEmpty(summary))
            {
                return summary;
            }

            var component = value as Component;
            if (component != null)
            {
                try
                {
                    return "<" + value.GetType().Name +
                           " position=" + FormatVector3(component.transform.position) + ">";
                }
                catch
                {
                    return "<" + value.GetType().Name + ">";
                }
            }

            var type = value.GetType();
            if (type.IsEnum || value is bool || value is byte || value is short ||
                value is int || value is long || value is float || value is double || value is decimal)
            {
                return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
            }

            var text = value as string;
            if (text != null)
            {
                return "\"" + Sanitize(text, 160) + "\"";
            }

            return "<" + type.FullName + ">";
        }

        private static ParameterInfo[] GetTraceParameters(MethodBase method)
        {
            lock (Sync)
            {
                ParameterInfo[] parameters;
                if (!TraceParametersCache.TryGetValue(method, out parameters))
                {
                    parameters = method.GetParameters();
                    TraceParametersCache[method] = parameters;
                }

                return parameters;
            }
        }

        private static FieldInfo GetTraceField(Type type, string fieldName)
        {
            lock (Sync)
            {
                Dictionary<string, FieldInfo> fields;
                if (!TraceFieldCache.TryGetValue(type, out fields))
                {
                    fields = new Dictionary<string, FieldInfo>(StringComparer.Ordinal);
                    TraceFieldCache[type] = fields;
                }

                FieldInfo field;
                if (!fields.TryGetValue(fieldName, out field))
                {
                    field = type.GetField(
                        fieldName,
                        BindingFlags.Public | BindingFlags.NonPublic |
                        BindingFlags.Instance | BindingFlags.Static);
                    fields[fieldName] = field;
                }

                return field;
            }
        }

        private static PropertyInfo GetTraceProperty(Type type, string propertyName)
        {
            lock (Sync)
            {
                Dictionary<string, PropertyInfo> properties;
                if (!TracePropertyCache.TryGetValue(type, out properties))
                {
                    properties = new Dictionary<string, PropertyInfo>(StringComparer.Ordinal);
                    TracePropertyCache[type] = properties;
                }

                PropertyInfo property;
                if (!properties.TryGetValue(propertyName, out property))
                {
                    property = type.GetProperty(
                        propertyName,
                        BindingFlags.Public | BindingFlags.NonPublic |
                        BindingFlags.Instance | BindingFlags.Static);
                    properties[propertyName] = property;
                }

                return property;
            }
        }

        private static string FormatVector3(Vector3 value)
        {
            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "({0:0.###},{1:0.###},{2:0.###})",
                value.x,
                value.y,
                value.z);
        }

        private static string BuildSafeObjectSummary(object value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            var typeName = value.GetType().FullName;
            switch (typeName)
            {
                case "RoomInfo":
                    return FormatFields(value, RoomInfoTraceFields);
                case "GameState":
                    return FormatFields(value, GameStateTraceFields);
                case "LevelSelectionController":
                    return FormatFields(value, LevelSelectionControllerTraceFields);
                case "GameModeController":
                    return FormatFields(value, GameModeControllerTraceFields);
                case "HeroController":
                    return FormatHeroControllerState(value);
                case "Player":
                    return FormatPlayerState(value);
                case "PID":
                    return FormatPid(value);
                case "WorkshopLevelDetails":
                    return FormatFields(value, WorkshopLevelDetailsTraceFields);
                case "Campaign":
                    return FormatFields(value, CampaignTraceFields);
                case "CampaignHeader":
                    return FormatFields(value, CampaignHeaderTraceFields);
                case "MakeOnlineMenu":
                    return FormatFields(value, MakeOnlineMenuTraceFields);
                default:
                    return string.Empty;
            }
        }

        private static string FormatHeroControllerState(object value)
        {
            return FormatFields(value, HeroControllerTraceFields);
        }

        private static string FormatPlayerState(object value)
        {
            var builder = new StringBuilder();
            builder.Append(FormatFields(value, PlayerTraceFields));
            builder.Length--;
            builder.Append(", IsMine=");
            builder.Append(FormatReadableProperty(value, "IsMine"));
            builder.Append(", controllerNum=");
            builder.Append(FormatReadableProperty(value, "controllerNum"));
            builder.Append(", character=");
            var player = value as Player;
            if (player != null && player.character != null)
            {
                try
                {
                    builder.Append("<" + player.character.GetType().Name +
                                   " position=" + FormatVector3(player.character.transform.position) + ">");
                }
                catch
                {
                    builder.Append(FormatReadableProperty(value, "character"));
                }
            }
            else
            {
                builder.Append(FormatReadableProperty(value, "character"));
            }
            builder.Append("}");
            return builder.ToString();
        }

        private static string FormatPid(object value)
        {
            return "PID{IsMine=" + FormatReadableProperty(value, "IsMine") + "}";
        }

        private static string FormatReadableProperty(object value, string propertyName)
        {
            try
            {
                var property = GetTraceProperty(value.GetType(), propertyName);
                if (property == null || !property.CanRead || property.GetIndexParameters().Length != 0)
                {
                    return "<missing>";
                }

                return FormatFieldValue(property.GetValue(value, null));
            }
            catch (Exception exception)
            {
                return "<error:" + exception.GetType().Name + ">";
            }
        }

        private static string FormatFields(object value, string[] fieldNames)
        {
            var builder = new StringBuilder();
            builder.Append(value.GetType().Name);
            builder.Append("{");
            var wroteValue = false;

            foreach (var fieldName in fieldNames)
            {
                var field = GetTraceField(value.GetType(), fieldName);
                if (field == null)
                {
                    continue;
                }

                object fieldValue;
                try
                {
                    fieldValue = field.GetValue(value);
                }
                catch
                {
                    continue;
                }

                if (wroteValue)
                {
                    builder.Append(", ");
                }

                builder.Append(fieldName);
                builder.Append("=");
                builder.Append(FormatFieldValue(fieldValue));
                wroteValue = true;
            }

            builder.Append("}");
            return builder.ToString();
        }

        private static string FormatFieldValue(object value)
        {
            if (value == null)
            {
                return "null";
            }

            var nestedSummary = BuildSafeObjectSummary(value);
            if (!string.IsNullOrEmpty(nestedSummary))
            {
                return nestedSummary;
            }

            var array = value as Array;
            if (array != null)
            {
                return FormatArrayValue(array);
            }

            var type = value.GetType();
            if (type.IsEnum || type.IsPrimitive || value is decimal)
            {
                return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
            }

            var text = value as string;
            if (text != null)
            {
                return "\"" + Sanitize(text, 160) + "\"";
            }

            return "<" + type.Name + ">";
        }

        private static string FormatArrayValue(Array array)
        {
            var builder = new StringBuilder();
            var elementType = array.GetType().GetElementType();
            builder.Append(elementType == null ? "Array" : elementType.Name);
            builder.Append("[");
            builder.Append(array.Length);
            builder.Append("]{");

            var maxItems = System.Math.Min(array.Length, 8);
            for (var index = 0; index < maxItems; index++)
            {
                if (index > 0)
                {
                    builder.Append(",");
                }

                builder.Append(FormatFieldValue(array.GetValue(index)));
            }

            if (array.Length > maxItems)
            {
                builder.Append(",...");
            }

            builder.Append("}");
            return builder.ToString();
        }

        private static bool IsSensitiveName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            var lowered = name.ToLowerInvariant();
            return lowered.Contains("password") || lowered.Contains("token") ||
                   lowered.Contains("secret") || lowered.Contains("credential");
        }

        private static string Sanitize(string value, int maxLength)
        {
            var builder = new StringBuilder(value.Length);
            for (var index = 0; index < value.Length; index++)
            {
                var current = value[index];
                if (current == '\r')
                {
                    builder.Append("\\r");
                }
                else if (current == '\n')
                {
                    builder.Append("\\n");
                }
                else if (char.IsHighSurrogate(current))
                {
                    if (index + 1 < value.Length && char.IsLowSurrogate(value[index + 1]))
                    {
                        builder.Append(current);
                        builder.Append(value[++index]);
                    }
                    else
                    {
                        builder.Append("\\u");
                        builder.Append(((int)current).ToString("X4"));
                    }
                }
                else if (char.IsLowSurrogate(current))
                {
                    builder.Append("\\u");
                    builder.Append(((int)current).ToString("X4"));
                }
                else
                {
                    builder.Append(current);
                }
            }

            var result = builder.ToString();
            if (result.Length > maxLength)
            {
                result = result.Substring(0, maxLength) + "...";
            }

            return result;
        }

        private static bool ShouldWrite(string key, string message, out string suppressionSummary)
        {
            suppressionSummary = string.Empty;
            lock (Sync)
            {
                var now = DateTime.UtcNow;
                var cacheKey = ShouldCoalesceByMethod(key) ? key : key + "\n" + message;
                TraceCacheEntry previous;
                if (TraceCache.TryGetValue(cacheKey, out previous) &&
                    now - previous.Timestamp < TimeSpan.FromSeconds(DuplicateWindowSeconds))
                {
                    previous.SuppressedCount++;
                    previous.LastMessage = message;
                    return false;
                }

                if (previous != null && previous.SuppressedCount > 0)
                {
                    suppressionSummary =
                        "TRACE_SUPPRESSED method=" + key +
                        "; count=" + previous.SuppressedCount +
                        "; latest=" + Sanitize(message, 500);
                }

                TraceCache[cacheKey] = new TraceCacheEntry(message, now);
                PruneTraceCache(now);
                return true;
            }
        }

        private static bool ShouldCoalesceByMethod(string key)
        {
            return key.EndsWith("ConnectionLayer.UpdateOnlinePlayerList", StringComparison.Ordinal) ||
                   key.EndsWith("RoomInfo.RefreshInfo", StringComparison.Ordinal) ||
                   key.EndsWith("RoomInfo.PushUpdatedInfo", StringComparison.Ordinal) ||
                   key.EndsWith("RoomInfo.PullUpdatedInfo", StringComparison.Ordinal) ||
                   key.EndsWith("HeroController.UpdatePlayerData", StringComparison.Ordinal) ||
                   key.EndsWith("HeroController.UpdatePlayerUserData", StringComparison.Ordinal) ||
                   key.EndsWith("Player.RespawnBro", StringComparison.Ordinal);
        }

        private static void PruneTraceCache(DateTime now)
        {
            if (TraceCache.Count <= MaxTraceCacheEntries)
            {
                return;
            }

            var cutoff = now - TimeSpan.FromSeconds(TraceCacheExpirySeconds);
            var staleKeys = new List<string>();
            foreach (var pair in TraceCache)
            {
                if (pair.Value.Timestamp < cutoff)
                {
                    staleKeys.Add(pair.Key);
                }
            }

            foreach (var staleKey in staleKeys)
            {
                TraceCache.Remove(staleKey);
            }

            while (TraceCache.Count > MaxTraceCacheEntries)
            {
                string oldestKey = null;
                DateTime oldestTimestamp = DateTime.MaxValue;
                foreach (var pair in TraceCache)
                {
                    if (pair.Value.Timestamp < oldestTimestamp)
                    {
                        oldestKey = pair.Key;
                        oldestTimestamp = pair.Value.Timestamp;
                    }
                }

                if (oldestKey == null)
                {
                    break;
                }

                TraceCache.Remove(oldestKey);
            }
        }

        private static string DescribeMethod(MethodBase method)
        {
            lock (Sync)
            {
                string description;
                if (!TraceMethodDescriptionCache.TryGetValue(method, out description))
                {
                    var typeName = method.DeclaringType == null
                        ? "<unknown>"
                        : method.DeclaringType.FullName;
                    description = typeName + "." + method.Name;
                    TraceMethodDescriptionCache[method] = description;
                }

                return description;
            }
        }

        private sealed class TraceTarget
        {
            public TraceTarget(string typeName, string methodName)
            {
                TypeName = typeName;
                MethodName = methodName;
            }

            public string TypeName { get; private set; }
            public string MethodName { get; private set; }

            public override string ToString()
            {
                return TypeName + "." + MethodName;
            }
        }

        private sealed class TraceCacheEntry
        {
            public TraceCacheEntry(string message, DateTime timestamp)
            {
                LastMessage = message;
                Timestamp = timestamp;
            }

            public string LastMessage { get; set; }
            public DateTime Timestamp { get; private set; }
            public int SuppressedCount { get; set; }
        }
    }
}
