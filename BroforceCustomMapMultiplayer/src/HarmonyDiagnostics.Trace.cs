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
    // 方法追踪：追踪消息构建、参数格式化、敏感信息脱敏、去重缓存。
    internal static partial class HarmonyDiagnostics
    {
                private static string BuildTraceMessage(
            MethodBase method,
            object instance,
            object[] arguments)
        {
            var builder = new StringBuilder();
            builder.Append(DescribeMethod(method));
            builder.Append("(");

            var parameters = method.GetParameters();
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
                    return FormatFields(value, new[]
                    {
                        "gameMode", "campaignName", "CurrentSceneName", "capacity", "_playerCount",
                        "returnToWorldMap", "levelNumber", "totalLevels", "worldMapProgress",
                        "liberatedAreas", "invalidInfo", "hardMode", "hardcoreMode"
                    });
                case "GameState":
                    return FormatFields(value, new[]
                    {
                        "_sceneToLoad", "_campaignName", "levelNumber", "customLevelID",
                        "loadCustomCampaign", "loadMode", "gameMode", "levelEditorActive",
                        "returnToWorldMap", "arcadeHardMode", "persistPastLevelLoad"
                    });
                case "LevelSelectionController":
                    return FormatFields(value, new[]
                    {
                        "_levelFileNameToLoad", "JoinScene", "CampaignScene", "OnlineCampaign",
                        "OfflineCampaign", "DefaultCampaign", "loadPublishedCampaign", "isOnlineCampaign",
                        "currentWorkshopLevel"
                    });
                case "GameModeController":
                    return FormatFields(value, new[]
                    {
                        "switchingLevel", "nextScene", "levelHasStarted", "levelFinished",
                        "waitingForAllPlayersToReady", "switchSilently"
                    });
                case "HeroController":
                    return FormatHeroControllerState(value);
                case "Player":
                    return FormatPlayerState(value);
                case "PID":
                    return FormatPid(value);
                case "WorkshopLevelDetails":
                    return FormatFields(value, new[]
                    {
                        "name", "fileid", "fileName", "tags", "isWWBLevel", "wasCompletedSuccessfully"
                    });
                case "Campaign":
                    return FormatFields(value, new[] { "name", "levels", "brodownLevel" });
                case "CampaignHeader":
                    return FormatFields(value, new[]
                    {
                        "name", "length", "md5", "isPublished", "gameMode"
                    });
                case "MakeOnlineMenu":
                    return FormatFields(value, new[] { "state", "playerLimit", "canChangePassword", "canChangeName" });
                default:
                    return string.Empty;
            }
        }

        private static string FormatHeroControllerState(object value)
        {
            return FormatFields(value, new[]
            {
                "playersPlaying", "players", "PIDS", "playerControllerIDs",
                "heroesHaveBeenReleasedFromTransport", "brosHaveBeenReleased",
                "WaitForAllPlayersToSpawnBeforeStarting", "AllPlayersHaveJoined"
            });
        }

        private static string FormatPlayerState(object value)
        {
            var builder = new StringBuilder();
            builder.Append(FormatFields(value, new[]
            {
                "playerNum", "lives", "firstDeployment", "_awaitingHeroTypeFromServer", "heroType"
            }));
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
                var property = value.GetType().GetProperty(
                    propertyName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
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
                var field = value.GetType().GetField(
                    fieldName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
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
            var typeName = method.DeclaringType == null ? "<unknown>" : method.DeclaringType.FullName;
            return typeName + "." + method.Name;
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
