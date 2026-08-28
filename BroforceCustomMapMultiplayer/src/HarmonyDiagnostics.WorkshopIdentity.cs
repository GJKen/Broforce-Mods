using HarmonyLib;
using System;
using System.Reflection;

namespace BroforceCustomMapMultiplayer
{
    // Host-advertised Workshop identity and client-side subscription validation.
    internal static partial class HarmonyDiagnostics
    {
        private const string WorkshopLobbyIdKey = "GJKen_BroforceOnline_WorkshopId";
        private const string WorkshopLobbySceneKey = "GJKen_BroforceOnline_WorkshopScene";
        private const string WorkshopLobbyCampaignKey = "GJKen_BroforceOnline_WorkshopCampaign";
        private const int WorkshopIdentityPollMilliseconds = 500;
        private const int WorkshopSubscriptionRetrySeconds = 5;

        private static bool _sessionWorkshopIdentityAdopted;
        private static string _sessionWorkshopId = string.Empty;
        private static string _sessionWorkshopScene = string.Empty;
        private static string _sessionWorkshopCampaign = string.Empty;
        private static bool _workshopSubscriptionMissing;
        private static WorkshopSubscriptionStatus _workshopSubscriptionStatus;
        private static DateTime _workshopIdentityPollAtUtc;
        private static DateTime _workshopSubscriptionRetryAtUtc;

        private enum WorkshopSubscriptionStatus
        {
            Unknown,
            Subscribed,
            Missing
        }

        private static string GetConfiguredWorkshopId()
        {
            if (_sessionWorkshopIdentityAdopted)
            {
                return _sessionWorkshopId ?? string.Empty;
            }

            // A joining client must never fall back to its saved host configuration while
            // lobby metadata is still arriving. Saved values remain available if this
            // machine later creates its own room.
            if (_networkSessionActive && !_sessionIsHost)
            {
                return string.Empty;
            }

            var settings = Plugin.Settings;
            return settings == null ? string.Empty : (settings.WorkshopId ?? string.Empty).Trim();
        }

        internal static string GetConfiguredWorkshopSceneName()
        {
            if (_sessionWorkshopIdentityAdopted)
            {
                return _sessionWorkshopScene ?? string.Empty;
            }

            if (_networkSessionActive && !_sessionIsHost)
            {
                return string.Empty;
            }

            var settings = Plugin.Settings;
            return settings == null
                ? string.Empty
                : (settings.WorkshopSceneName ?? string.Empty).Trim();
        }

        private static string GetConfiguredWorkshopCampaignName()
        {
            if (_sessionWorkshopIdentityAdopted)
            {
                return _sessionWorkshopCampaign ?? string.Empty;
            }

            if (_networkSessionActive && !_sessionIsHost)
            {
                return string.Empty;
            }

            var settings = Plugin.Settings;
            return settings == null
                ? string.Empty
                : (settings.WorkshopCampaignName ?? string.Empty).Trim();
        }

        private static bool PublishConfiguredWorkshopIdentity(string context)
        {
            if (!_sessionIsHost)
            {
                return false;
            }

            var settings = Plugin.Settings;
            var workshopId = GetConfiguredWorkshopId();
            ulong numericWorkshopId;
            if (settings == null || !settings.EnableOnlineWorkshopInjection ||
                !UInt64.TryParse(workshopId, out numericWorkshopId) || numericWorkshopId == 0)
            {
                SetWorkshopLobbyData(WorkshopLobbyIdKey, string.Empty, "map identity cleared; " + context);
                SetWorkshopLobbyData(WorkshopLobbySceneKey, string.Empty, "map identity cleared; " + context);
                SetWorkshopLobbyData(WorkshopLobbyCampaignKey, string.Empty, "map identity cleared; " + context);
                return false;
            }

            var scene = GetConfiguredWorkshopSceneName();
            var campaign = GetConfiguredWorkshopCampaignName();
            var idWritten = SetWorkshopLobbyData(
                WorkshopLobbyIdKey,
                workshopId,
                "map identity; " + context);
            var sceneWritten = SetWorkshopLobbyData(
                WorkshopLobbySceneKey,
                scene,
                "map identity; " + context);
            var campaignWritten = SetWorkshopLobbyData(
                WorkshopLobbyCampaignKey,
                campaign,
                "map identity; " + context);
            if (idWritten && sceneWritten && campaignWritten)
            {
                DiagnosticLog.Info(
                    "Published host Workshop map identity: id=" + workshopId +
                    "; scene=" + scene + "; campaign=" + campaign +
                    "; context=" + context + ".");
            }

            return idWritten && sceneWritten && campaignWritten;
        }

        private static void TrySynchronizeClientWorkshopIdentity(bool force, string context)
        {
            var settings = Plugin.Settings;
            if (_sessionIsHost || !_networkSessionActive || settings == null ||
                !settings.EnableOnlineWorkshopInjection)
            {
                return;
            }

            var now = DateTime.UtcNow;
            if (!force && now < _workshopIdentityPollAtUtc)
            {
                return;
            }

            _workshopIdentityPollAtUtc = now.AddMilliseconds(WorkshopIdentityPollMilliseconds);
            RefreshWorkshopLobbyDataIfNeeded("Workshop map identity synchronization");

            var workshopId = GetWorkshopLobbyData(WorkshopLobbyIdKey).Trim();
            ulong numericWorkshopId;
            if (!UInt64.TryParse(workshopId, out numericWorkshopId) || numericWorkshopId == 0)
            {
                return;
            }

            var scene = GetWorkshopLobbyData(WorkshopLobbySceneKey).Trim();
            var campaign = GetWorkshopLobbyData(WorkshopLobbyCampaignKey).Trim();
            var identityChanged = !_sessionWorkshopIdentityAdopted ||
                !string.Equals(_sessionWorkshopId, workshopId, StringComparison.Ordinal) ||
                !string.Equals(_sessionWorkshopScene, scene, StringComparison.Ordinal) ||
                !string.Equals(_sessionWorkshopCampaign, campaign, StringComparison.Ordinal);

            if (identityChanged)
            {
                var savedWorkshopId = (settings.WorkshopId ?? string.Empty).Trim();
                var savedScene = (settings.WorkshopSceneName ?? string.Empty).Trim();
                var savedCampaign = (settings.WorkshopCampaignName ?? string.Empty).Trim();
                _sessionWorkshopIdentityAdopted = true;
                _sessionWorkshopId = workshopId;
                _sessionWorkshopScene = scene;
                _sessionWorkshopCampaign = campaign;
                _workshopSubscriptionStatus = WorkshopSubscriptionStatus.Unknown;
                _workshopSubscriptionMissing = false;
                _workshopSubscriptionRetryAtUtc = DateTime.MinValue;
                DiagnosticLog.Info(
                    "Adopted host Workshop map identity for this room: id=" + workshopId +
                    "; scene=" + scene + "; campaign=" + campaign +
                    "; context=" + context + ".");
                if (!string.IsNullOrEmpty(savedWorkshopId) &&
                    (!string.Equals(savedWorkshopId, workshopId, StringComparison.Ordinal) ||
                     !string.Equals(savedScene, scene, StringComparison.Ordinal) ||
                     !string.Equals(savedCampaign, campaign, StringComparison.Ordinal)))
                {
                    DiagnosticLog.Info(
                        "Ignored the joining client's saved Workshop map configuration for this room: " +
                        "savedId=" + savedWorkshopId + "; hostId=" + workshopId + ".");
                }
            }

            if (_workshopSubscriptionStatus != WorkshopSubscriptionStatus.Unknown ||
                now < _workshopSubscriptionRetryAtUtc)
            {
                return;
            }

            _workshopSubscriptionRetryAtUtc = now.AddSeconds(WorkshopSubscriptionRetrySeconds);
            _workshopSubscriptionStatus = GetWorkshopSubscriptionStatus(numericWorkshopId);
            if (_workshopSubscriptionStatus == WorkshopSubscriptionStatus.Missing)
            {
                _workshopSubscriptionMissing = true;
                ClearLateJoinState();
                var message =
                    "房主使用的 Steam 创意工坊地图 ID 为 " + workshopId +
                    "，但本机尚未订阅。请先在 Steam 创意工坊订阅并等待下载完成，然后重新加入房间。";
                Plugin.ShowWorkshopNotice(message);
                DiagnosticLog.Warning(
                    "Host Workshop map is not subscribed locally; automatic loading is blocked: id=" +
                    workshopId + ".");
            }
            else if (_workshopSubscriptionStatus == WorkshopSubscriptionStatus.Subscribed)
            {
                Plugin.ClearWorkshopNotice();
                DiagnosticLog.Info(
                    "Verified local Steam Workshop subscription for host map: id=" + workshopId + ".");
            }
        }

        private static WorkshopSubscriptionStatus GetWorkshopSubscriptionStatus(ulong workshopId)
        {
            try
            {
                var steamControllerType = AccessTools.TypeByName("SteamController");
                var isSteamEnabled = steamControllerType == null
                    ? null
                    : steamControllerType.GetMethod(
                        "IsSteamEnabled",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (isSteamEnabled != null &&
                    !Convert.ToBoolean(isSteamEnabled.Invoke(null, null)))
                {
                    return WorkshopSubscriptionStatus.Unknown;
                }

                var steamUgcType = AccessTools.TypeByName("Steamworks.SteamUGC");
                var publishedFileIdType = AccessTools.TypeByName("Steamworks.PublishedFileId_t");
                var getCount = steamUgcType == null
                    ? null
                    : steamUgcType.GetMethod(
                        "GetNumSubscribedItems",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                var getItems = steamUgcType == null
                    ? null
                    : steamUgcType.GetMethod(
                        "GetSubscribedItems",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (publishedFileIdType == null || getCount == null || getItems == null)
                {
                    return WorkshopSubscriptionStatus.Unknown;
                }

                var count = Convert.ToUInt32(getCount.Invoke(null, null));
                if (count == 0)
                {
                    return WorkshopSubscriptionStatus.Missing;
                }
                if (count > 100000)
                {
                    DiagnosticLog.Warning(
                        "Steam Workshop subscription count was unexpectedly large; subscription check skipped: " +
                        count + ".");
                    return WorkshopSubscriptionStatus.Unknown;
                }

                var subscribedItems = Array.CreateInstance(publishedFileIdType, (int)count);
                var returnedCount = Convert.ToUInt32(getItems.Invoke(
                    null,
                    new object[] { subscribedItems, count }));
                var idField = publishedFileIdType.GetField(
                    "m_PublishedFileId",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (idField == null)
                {
                    return WorkshopSubscriptionStatus.Unknown;
                }

                var safeReturnedCount = global::System.Math.Min((int)returnedCount, subscribedItems.Length);
                for (var index = 0; index < safeReturnedCount; index++)
                {
                    var item = subscribedItems.GetValue(index);
                    if (item != null && Convert.ToUInt64(idField.GetValue(item)) == workshopId)
                    {
                        return WorkshopSubscriptionStatus.Subscribed;
                    }
                }

                return WorkshopSubscriptionStatus.Missing;
            }
            catch (Exception exception)
            {
                DiagnosticLog.Trace(
                    "Steam Workshop subscription check was unavailable: " + exception.Message);
                return WorkshopSubscriptionStatus.Unknown;
            }
        }

        private static bool ShouldBlockMissingWorkshopLoad(string nextScene)
        {
            if (!_workshopSubscriptionMissing || _sessionIsHost || !IsOnline())
            {
                return false;
            }

            var state = GetGameStateInstance(AccessTools.TypeByName("GameState"));
            var stateWorkshopId = state == null
                ? string.Empty
                : GetStringFieldOrProperty(state, "customLevelID").Trim();
            var targetsHostWorkshop = string.Equals(
                stateWorkshopId,
                GetConfiguredWorkshopId(),
                StringComparison.Ordinal);
            if (!targetsHostWorkshop && !string.IsNullOrEmpty(nextScene))
            {
                targetsHostWorkshop = string.Equals(
                    nextScene,
                    GetConfiguredWorkshopSceneName(),
                    StringComparison.OrdinalIgnoreCase);
            }

            if (!targetsHostWorkshop)
            {
                return false;
            }

            Plugin.ShowWorkshopNotice(
                "房主使用的 Steam 创意工坊地图 ID 为 " + GetConfiguredWorkshopId() +
                "，但本机尚未订阅。请先在 Steam 创意工坊订阅并等待下载完成，然后重新加入房间。");
            return true;
        }

        private static void ClearWorkshopIdentityState()
        {
            _sessionWorkshopIdentityAdopted = false;
            _sessionWorkshopId = string.Empty;
            _sessionWorkshopScene = string.Empty;
            _sessionWorkshopCampaign = string.Empty;
            _workshopSubscriptionMissing = false;
            _workshopSubscriptionStatus = WorkshopSubscriptionStatus.Unknown;
            _workshopIdentityPollAtUtc = DateTime.MinValue;
            _workshopSubscriptionRetryAtUtc = DateTime.MinValue;
            Plugin.ClearWorkshopNotice();
        }
    }
}
