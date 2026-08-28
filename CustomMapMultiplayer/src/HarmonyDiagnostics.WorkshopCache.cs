using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace CustomMapMultiplayer
{
    // 优先复用 Steam 已安装或已缓存的 Workshop 内容，并阻止加载动画中的重复请求。
    internal static partial class HarmonyDiagnostics
    {
        private const int WorkshopCacheFolderBufferSize = 4096;
        private const long WorkshopCacheMaxFileBytes = 512L * 1024L * 1024L;
        private const int WorkshopLoadRequestTimeoutSeconds = 120;

        private static bool _workshopLoadRequestPending;
        private static ulong _workshopLoadRequestId;
        private static DateTime _workshopLoadRequestStartedAtUtc;
        private static bool _cachedWorkshopCompletionPending;
        private static Campaign _cachedWorkshopCampaign;

        private static void PatchWorkshopLoadCache()
        {
            var steamControllerType = AccessTools.TypeByName("SteamController");
            if (steamControllerType == null)
            {
                DiagnosticLog.Warning("Workshop cache patch skipped: SteamController type not found.");
                return;
            }

            var loadLevel = FindStaticMethod(steamControllerType, "LoadLevel", 1);
            var loadPrefix = typeof(HarmonyDiagnostics).GetMethod(
                "WorkshopSteamLoadLevelPrefix",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (loadLevel == null || loadPrefix == null)
            {
                DiagnosticLog.Warning("Workshop cache patch skipped: SteamController.LoadLevel target not found.");
                return;
            }

            try
            {
                _harmony.Patch(loadLevel, new HarmonyMethod(loadPrefix), null, null, null);
                DiagnosticLog.Info("Workshop cache and duplicate-load guard enabled.");
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning("Workshop cache LoadLevel patch failed: " + exception);
                return;
            }

            var detailsMethod = FindStaticMethod(
                steamControllerType,
                "Cloud_CloudGetPublishedFileDetailsResult",
                2);
            var detailsPrefix = typeof(HarmonyDiagnostics).GetMethod(
                "WorkshopPublishedFileDetailsPrefix",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (detailsMethod != null && detailsPrefix != null)
            {
                try
                {
                    _harmony.Patch(detailsMethod, new HarmonyMethod(detailsPrefix), null, null, null);
                    DiagnosticLog.Info("Legacy Workshop UGC cache probe enabled.");
                }
                catch (Exception exception)
                {
                    DiagnosticLog.Warning("Legacy Workshop UGC cache patch failed: " + exception);
                }
            }

            var completionMethod = FindStaticMethod(
                steamControllerType,
                "OnLevelLoadComplete",
                1);
            var completionPostfix = typeof(HarmonyDiagnostics).GetMethod(
                "WorkshopLevelLoadCompletePostfix",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (completionMethod != null && completionPostfix != null)
            {
                try
                {
                    _harmony.Patch(completionMethod, null, new HarmonyMethod(completionPostfix), null, null);
                }
                catch (Exception exception)
                {
                    DiagnosticLog.Warning("Workshop load completion cleanup patch failed: " + exception);
                }
            }
        }

        private static MethodInfo FindStaticMethod(Type type, string name, int parameterCount)
        {
            if (type == null)
            {
                return null;
            }

            var methods = type.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            foreach (var method in methods)
            {
                if (method.Name == name && method.GetParameters().Length == parameterCount)
                {
                    return method;
                }
            }

            return null;
        }

        private static bool WorkshopSteamLoadLevelPrefix(object __0)
        {
            try
            {
                if (!IsWorkshopOnlineSession())
                {
                    return true;
                }

                ulong workshopId;
                if (!TryGetPublishedFileId(__0, out workshopId) || workshopId == 0)
                {
                    return true;
                }

                if (_workshopLoadRequestPending)
                {
                    if (_workshopLoadRequestId == workshopId &&
                        DateTime.UtcNow <= _workshopLoadRequestStartedAtUtc.AddSeconds(
                            WorkshopLoadRequestTimeoutSeconds))
                    {
                        DiagnosticLog.Trace(
                            "Skipped duplicate SteamController.LoadLevel while Workshop map is pending: id=" +
                            workshopId + ".");
                        return false;
                    }

                    DiagnosticLog.Warning(
                        "Workshop map load request timed out or changed; allowing a new request: previousId=" +
                        _workshopLoadRequestId + "; currentId=" + workshopId + ".");
                    ClearWorkshopLoadRequest();
                }

                _workshopLoadRequestPending = true;
                _workshopLoadRequestId = workshopId;
                _workshopLoadRequestStartedAtUtc = DateTime.UtcNow;

                Campaign cachedCampaign;
                string cacheSource;
                if (TryLoadInstalledWorkshopCampaign(__0, out cachedCampaign, out cacheSource))
                {
                    QueueCachedWorkshopCompletion(cachedCampaign, cacheSource, workshopId);
                    return false;
                }

                DiagnosticLog.Info(
                    "Workshop cache miss; falling back to Steam UGC download: id=" + workshopId + ".");
                return true;
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning("Workshop cache LoadLevel prefix failed; using original load: " + exception);
                ClearWorkshopLoadRequest();
                return true;
            }
        }

        private static bool WorkshopPublishedFileDetailsPrefix(object __0, bool __1)
        {
            try
            {
                if (!IsWorkshopOnlineSession() || __1 || !_workshopLoadRequestPending ||
                    _cachedWorkshopCompletionPending || __0 == null)
                {
                    return true;
                }

                var resultId = GetFieldOrPropertyValue(__0, "m_nPublishedFileId");
                ulong resultWorkshopId;
                if (resultId == null || !TryGetPublishedFileId(resultId, out resultWorkshopId) ||
                    resultWorkshopId != _workshopLoadRequestId)
                {
                    return true;
                }

                var fileSize = GetIntFieldOrProperty(__0, "m_nFileSize");
                var fileHandle = GetFieldOrPropertyValue(__0, "m_hFile");
                Campaign cachedCampaign;
                if (!TryReadCachedUgcCampaign(fileHandle, fileSize, out cachedCampaign))
                {
                    return true;
                }

                QueueCachedWorkshopCompletion(cachedCampaign, "legacy-ugc", _workshopLoadRequestId);
                return false;
            }
            catch (Exception exception)
            {
                DiagnosticLog.Trace("Legacy Workshop UGC cache probe failed: " + exception.Message);
                return true;
            }
        }

        private static void WorkshopLevelLoadCompletePostfix()
        {
            ClearWorkshopLoadRequest();
        }

        private static void QueueCachedWorkshopCompletion(
            Campaign campaign,
            string cacheSource,
            ulong workshopId)
        {
            if (campaign == null)
            {
                ClearWorkshopLoadRequest();
                return;
            }

            _cachedWorkshopCampaign = campaign;
            _cachedWorkshopCompletionPending = true;
            DiagnosticLog.Info(
                "Workshop cache hit; reusing local campaign before Steam download: id=" +
                workshopId + "; source=" + cacheSource + ".");
        }

        private static void TryCompleteCachedWorkshopLoad()
        {
            if (!_cachedWorkshopCompletionPending || _cachedWorkshopCampaign == null)
            {
                return;
            }

            var campaign = _cachedWorkshopCampaign;
            _cachedWorkshopCampaign = null;
            _cachedWorkshopCompletionPending = false;
            try
            {
                var steamControllerType = AccessTools.TypeByName("SteamController");
                var completionMethod = FindStaticMethod(steamControllerType, "OnLevelLoadComplete", 1);
                if (completionMethod == null)
                {
                    DiagnosticLog.Warning(
                        "Workshop cache completion could not find SteamController.OnLevelLoadComplete.");
                    ClearWorkshopLoadRequest();
                    return;
                }

                completionMethod.Invoke(null, new object[] { campaign });
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning("Workshop cache completion failed: " + exception);
                ClearWorkshopLoadRequest();
            }
        }

        private static bool TryLoadInstalledWorkshopCampaign(
            object publishedFileId,
            out Campaign campaign,
            out string cacheSource)
        {
            campaign = null;
            cacheSource = string.Empty;
            var steamUgcType = AccessTools.TypeByName("Steamworks.SteamUGC");
            var getInstallInfo = steamUgcType == null
                ? null
                : FindStaticMethod(steamUgcType, "GetItemInstallInfo", 5);
            if (getInstallInfo == null || publishedFileId == null)
            {
                return false;
            }

            try
            {
                var arguments = new object[]
                {
                    publishedFileId,
                    (ulong)0,
                    string.Empty,
                    (uint)WorkshopCacheFolderBufferSize,
                    false
                };
                if (!Convert.ToBoolean(getInstallInfo.Invoke(null, arguments)))
                {
                    return false;
                }

                var folder = arguments[2] as string;
                if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                {
                    return false;
                }

                if (!TryLoadCampaignFromFolder(folder, out campaign))
                {
                    DiagnosticLog.Trace(
                        "Steam reported an installed Workshop folder without a readable campaign: " +
                        folder + ".");
                    return false;
                }

                cacheSource = "installed-folder";
                return true;
            }
            catch (Exception exception)
            {
                DiagnosticLog.Trace("Installed Workshop cache probe failed: " + exception.Message);
                return false;
            }
        }

        private static bool TryLoadCampaignFromFolder(string folder, out Campaign campaign)
        {
            campaign = null;
            var candidates = new List<string>();
            AddCampaignFiles(folder, "*.bfg", candidates);
            AddCampaignFiles(folder, "*.bfd", candidates);
            AddCampaignFiles(folder, "*.bfc", candidates);
            AddCampaignFiles(folder, "*.bytes", candidates);
            candidates.Sort(StringComparer.OrdinalIgnoreCase);

            foreach (var candidate in candidates)
            {
                try
                {
                    var fileInfo = new FileInfo(candidate);
                    if (!fileInfo.Exists || fileInfo.Length <= 0 ||
                        fileInfo.Length > WorkshopCacheMaxFileBytes)
                    {
                        continue;
                    }

                    var bytes = File.ReadAllBytes(candidate);
                    var parsed = FileIO.LoadCampaignBytes(bytes, true, true);
                    if (parsed != null)
                    {
                        campaign = parsed;
                        DiagnosticLog.Trace("Loaded Workshop campaign from local file: " + candidate + ".");
                        return true;
                    }
                }
                catch (Exception exception)
                {
                    DiagnosticLog.Trace(
                        "Skipped unreadable local Workshop campaign candidate " + candidate +
                        ": " + exception.Message);
                }
            }

            return false;
        }

        private static void AddCampaignFiles(string folder, string pattern, List<string> candidates)
        {
            try
            {
                candidates.AddRange(Directory.GetFiles(folder, pattern, SearchOption.AllDirectories));
            }
            catch (Exception exception)
            {
                DiagnosticLog.Trace(
                    "Workshop cache file enumeration failed for " + folder + ": " + exception.Message);
            }
        }

        private static bool TryReadCachedUgcCampaign(
            object fileHandle,
            int fileSize,
            out Campaign campaign)
        {
            campaign = null;
            if (fileHandle == null || fileSize <= 0 || fileSize > WorkshopCacheMaxFileBytes)
            {
                return false;
            }

            var steamStorageType = AccessTools.TypeByName("Steamworks.SteamRemoteStorage");
            var ugcRead = steamStorageType == null
                ? null
                : FindStaticMethod(steamStorageType, "UGCRead", 5);
            if (ugcRead == null)
            {
                return false;
            }

            try
            {
                var readActionType = ugcRead.GetParameters()[4].ParameterType;
                var readAction = Enum.Parse(
                    readActionType,
                    "k_EUGCReadAction_ContinueReadingUntilFinished");
                var bytes = new byte[fileSize];
                var bytesRead = Convert.ToInt32(ugcRead.Invoke(
                    null,
                    new object[] { fileHandle, bytes, fileSize, (uint)0, readAction }));
                if (bytesRead <= 0)
                {
                    return false;
                }

                if (bytesRead < bytes.Length)
                {
                    Array.Resize(ref bytes, bytesRead);
                }

                campaign = FileIO.LoadCampaignBytes(bytes, true, true);
                return campaign != null;
            }
            catch
            {
                campaign = null;
                return false;
            }
        }

        private static bool TryGetPublishedFileId(object value, out ulong workshopId)
        {
            workshopId = 0;
            if (value == null)
            {
                return false;
            }

            var id = GetFieldOrPropertyValue(value, "m_PublishedFileId");
            if (id == null)
            {
                return false;
            }

            var numericId = GetFieldOrPropertyValue(id, "m_PublishedFileId");
            if (numericId == null)
            {
                return false;
            }

            workshopId = Convert.ToUInt64(numericId);
            return true;
        }

        private static void ClearWorkshopLoadRequest()
        {
            _workshopLoadRequestPending = false;
            _workshopLoadRequestId = 0;
            _workshopLoadRequestStartedAtUtc = DateTime.MinValue;
            _cachedWorkshopCompletionPending = false;
            _cachedWorkshopCampaign = null;
        }
    }
}
