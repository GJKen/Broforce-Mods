using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace CustomMapMultiplayer
{
    // 检查 Workshop 本地地图状态、记录最近地图，并保留退出期间的加载保护。
    internal static partial class HarmonyDiagnostics
    {
        private const int WorkshopCacheFolderBufferSize = 4096;
        private const long WorkshopCacheMaxFileBytes = 512L * 1024L * 1024L;

        private static bool _workshopLoadRequestPending;
        private static ulong _workshopLoadRequestId;

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
                if (_nativeMainMenuExitPending)
                {
                    ClearWorkshopLoadRequest();
                    DiagnosticLog.InfoFileOnly(
                        "Blocked stale SteamController.LoadLevel after online Host departure; native MainMenu is pending.");
                    return false;
                }

                // 联机 Workshop 必须保留原生房间确认和地图加载阶段，不能直接完成加载。
                if (IsWorkshopOnlineSession())
                {
                    ulong workshopId;
                    if (TryGetPublishedFileId(__0, out workshopId) && workshopId != 0)
                    {
                        _workshopLoadRequestPending = true;
                        _workshopLoadRequestId = workshopId;
                    }
                    else
                    {
                        ClearWorkshopLoadRequest();
                    }

                    DiagnosticLog.InfoFileOnly(
                        "Preserved native SteamController.LoadLevel for online Workshop session; " +
                        "native player confirmation and loading animation remain active.");
                    return true;
                }

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
                if (_nativeMainMenuExitPending)
                {
                    ClearWorkshopLoadRequest();
                    DiagnosticLog.InfoFileOnly(
                        "Blocked stale Workshop UGC details callback after online Host departure.");
                    return false;
                }

                // UGC details must remain on the native path; never substitute a cached campaign here.
                return true;
            }
            catch (Exception exception)
            {
                DiagnosticLog.Trace("Legacy Workshop UGC cache probe failed: " + exception.Message);
                return true;
            }
        }

        private static void WorkshopLevelLoadCompletePostfix()
        {
            var workshopId = _workshopLoadRequestPending
                ? _workshopLoadRequestId
                : 0;
            ClearWorkshopLoadRequest();
            if (workshopId != 0)
            {
                Plugin.RecordWorkshopMapUsage(workshopId);
            }
        }

        internal static WorkshopMapLocalState GetWorkshopMapLocalState(object publishedFileId)
        {
            string folder;
            if (!TryGetInstalledWorkshopFolder(publishedFileId, out folder))
            {
                return WorkshopMapLocalState.NotInstalled;
            }

            Campaign campaign;
            return TryLoadCampaignFromFolder(folder, out campaign)
                ? WorkshopMapLocalState.InstalledReadable
                : WorkshopMapLocalState.CampaignUnreadable;
        }

        private static bool TryGetInstalledWorkshopFolder(object publishedFileId, out string folder)
        {
            folder = string.Empty;
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

                folder = arguments[2] as string;
                return !string.IsNullOrEmpty(folder) && Directory.Exists(folder);
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

        internal static bool TryGetPublishedFileId(object value, out ulong workshopId)
        {
            workshopId = 0;
            if (value == null)
            {
                return false;
            }

            var id = GetFieldOrPropertyValue(value, "m_PublishedFileId") ?? value;
            var numericId = GetFieldOrPropertyValue(id, "m_PublishedFileId") ?? id;
            try
            {
                workshopId = Convert.ToUInt64(numericId);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void ClearWorkshopLoadRequest()
        {
            _workshopLoadRequestPending = false;
            _workshopLoadRequestId = 0;
        }
    }
}
