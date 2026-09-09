using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace CustomMapMultiplayer
{
    internal enum WorkshopMapLocalState
    {
        NotInstalled,
        CampaignUnreadable,
        InstalledReadable
    }

    internal sealed class WorkshopMapItem
    {
        internal ulong WorkshopId;
        internal string Title;
        internal bool TitleReadPending;
        internal bool TitleReadFailed;
        internal WorkshopMapLocalState LocalState;
    }

    // The settings page keeps this directory in memory. It is never serialized with UMM settings.
    internal static class WorkshopMapDirectory
    {
        private const int TitleQueryTimeoutSeconds = 10;
        private const uint FirstTitleQueryPage = 1;

        private static readonly List<WorkshopMapItem> ItemsInternal = new List<WorkshopMapItem>();
        private static readonly Dictionary<ulong, string> TitleCache = new Dictionary<ulong, string>();

        private static bool _hasRefreshed;
        private static bool _isRefreshing;
        private static DateTime _titleQueryStartedAtUtc;
        private static string _lastError = string.Empty;
        private static int _subscribedCount;
        private static int _unreadableCount;
        private static int _readableCount;
        private static uint _titleQueryPage;
        private static ulong _titleQueryResultsRead;
        private static object _activeTitleQueryHandle;
        private static object _titleQueryCallResult;
        private static object _titleQueryCallback;

        internal static IList<WorkshopMapItem> Items
        {
            get
            {
                UpdateTitleQueryTimeout();
                return ItemsInternal;
            }
        }

        internal static bool IsRefreshing
        {
            get
            {
                UpdateTitleQueryTimeout();
                return _isRefreshing;
            }
        }

        internal static string LastError
        {
            get { return _lastError ?? string.Empty; }
        }

        internal static int SubscribedCount
        {
            get { return _subscribedCount; }
        }

        internal static int UnreadableCount
        {
            get { return _unreadableCount; }
        }

        internal static int ReadableCount
        {
            get { return _readableCount; }
        }

        internal static int TitleFailureCount
        {
            get
            {
                var count = 0;
                for (var index = 0; index < ItemsInternal.Count; index++)
                {
                    if (ItemsInternal[index].TitleReadFailed)
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        internal static void PatchNativeWorkshopCampaignEntries(Harmony harmony)
        {
            var menuType = AccessTools.TypeByName("NewCustomCampaignMenu");
            var postfixMethod = typeof(WorkshopMapDirectory).GetMethod(
                "NativeWorkshopCampaignEntriesPostfix",
                BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo receiveEntriesMethod = null;
            if (menuType != null)
            {
                var methods = menuType.GetMethods(
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.Instance |
                    BindingFlags.Static);
                for (var index = 0; index < methods.Length; index++)
                {
                    var method = methods[index];
                    if (method.Name == "ReceiveUpdatedEntries" &&
                        method.GetParameters().Length == 1)
                    {
                        receiveEntriesMethod = method;
                        break;
                    }
                }
            }

            if (harmony == null || receiveEntriesMethod == null || postfixMethod == null)
            {
                DiagnosticLog.Warning(
                    "Workshop title cache could not resolve NewCustomCampaignMenu.ReceiveUpdatedEntries.");
                return;
            }

            try
            {
                harmony.Patch(
                    receiveEntriesMethod,
                    null,
                    new HarmonyMethod(postfixMethod),
                    null,
                    null);
                DiagnosticLog.Info(
                    "Workshop title cache observes native campaign query results.");
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning(
                    "Workshop title cache native result patch failed: " + exception);
            }
        }

        internal static void EnsureInitialRefresh()
        {
            if (!_hasRefreshed)
            {
                Refresh();
            }
        }

        internal static void Refresh()
        {
            CancelTitleQuery();
            ResetState(false);
            _hasRefreshed = true;
            _isRefreshing = true;
            _titleQueryStartedAtUtc = DateTime.UtcNow;

            List<object> subscribedItems;
            string failure;
            if (!HarmonyDiagnostics.TryGetSubscribedWorkshopItems(out subscribedItems, out failure))
            {
                _lastError = failure ?? string.Empty;
                _isRefreshing = false;
                return;
            }

            _subscribedCount = subscribedItems.Count;
            for (var index = 0; index < subscribedItems.Count; index++)
            {
                var publishedFileId = subscribedItems[index];
                ulong workshopId;
                if (!HarmonyDiagnostics.TryGetPublishedFileId(publishedFileId, out workshopId) || workshopId == 0)
                {
                    _unreadableCount++;
                    DiagnosticLog.Trace(
                        "Workshop map directory skipped subscribed item with unreadable Workshop ID.");
                    continue;
                }

                var localState = HarmonyDiagnostics.GetWorkshopMapLocalState(publishedFileId);
                if (localState == WorkshopMapLocalState.InstalledReadable)
                {
                    _readableCount++;
                }
                else
                {
                    _unreadableCount++;
                }

                var item = new WorkshopMapItem
                {
                    WorkshopId = workshopId,
                    Title = GetFallbackTitle(workshopId),
                    LocalState = localState
                };
                string cachedTitle;
                if (TitleCache.TryGetValue(workshopId, out cachedTitle))
                {
                    item.Title = cachedTitle;
                }
                else
                {
                    item.TitleReadPending = true;
                }
                ItemsInternal.Add(item);
            }

            if (ItemsInternal.Count == 0)
            {
                _isRefreshing = false;
            }
            else
            {
                StartTitleQuery();
            }

            DiagnosticLog.Info(
                "Workshop map directory refresh enumerated: subscribed=" +
                _subscribedCount + "; listed=" + ItemsInternal.Count +
                "; readable=" + _readableCount +
                "; unreadableOrNotInstalled=" + _unreadableCount + ".");
        }

        internal static void Clear()
        {
            CancelTitleQuery();
            ResetState(true);
        }

        private static void ResetState(bool clearTitleCache)
        {
            ItemsInternal.Clear();
            _hasRefreshed = false;
            _isRefreshing = false;
            _lastError = string.Empty;
            _subscribedCount = 0;
            _unreadableCount = 0;
            _readableCount = 0;
            _titleQueryPage = 0;
            _titleQueryResultsRead = 0;
            if (clearTitleCache)
            {
                TitleCache.Clear();
            }
        }

        private static void NativeWorkshopCampaignEntriesPostfix(object __0)
        {
            ObserveNativeWorkshopCampaignEntries(__0);
        }

        internal static void ObserveNativeWorkshopCampaignEntries(object entries)
        {
            var enumerable = entries as IEnumerable;
            if (enumerable == null)
            {
                return;
            }

            var cachedCount = 0;
            foreach (var entry in enumerable)
            {
                if (entry == null)
                {
                    continue;
                }

                ulong workshopId;
                if (!HarmonyDiagnostics.TryGetPublishedFileId(
                        HarmonyDiagnostics.GetFieldOrPropertyValue(entry, "fileid"),
                        out workshopId) ||
                    workshopId == 0)
                {
                    continue;
                }

                var title = Convert.ToString(HarmonyDiagnostics.GetFieldOrPropertyValue(entry, "name"));
                if (MergeTitle(workshopId, title))
                {
                    cachedCount++;
                }
            }

            if (cachedCount > 0)
            {
                DiagnosticLog.Info(
                    "Workshop title cache merged native campaign names: count=" + cachedCount + ".");
            }
        }

        private static void StartTitleQuery()
        {
            _titleQueryPage = FirstTitleQueryPage;
            _titleQueryResultsRead = 0;
            StartTitleQueryPage();
        }

        private static void StartTitleQueryPage()
        {
            object queryHandle = null;
            try
            {
                var steamUgcType = AccessTools.TypeByName("Steamworks.SteamUGC");
                queryHandle = CreateSubscribedQuery(steamUgcType, _titleQueryPage);
                if (queryHandle == null || GetNumericValue(queryHandle) == 0)
                {
                    throw new InvalidOperationException("SteamUGC returned an invalid subscription query handle.");
                }

                var queryResultType = AccessTools.TypeByName(
                    "Steamworks.SteamUGCQueryCompleted_t");
                var callResultOpenType = AccessTools.TypeByName("Steamworks.CallResult`1");
                if (queryResultType == null || callResultOpenType == null)
                {
                    throw new InvalidOperationException("Steam Workshop query callback types are unavailable.");
                }

                var callResultType = callResultOpenType.MakeGenericType(queryResultType);
                var delegateType = callResultType.GetNestedType(
                    "APIDispatchDelegate",
                    BindingFlags.Public | BindingFlags.NonPublic);
                if (delegateType != null && delegateType.ContainsGenericParameters)
                {
                    delegateType = delegateType.MakeGenericType(queryResultType);
                }
                var callbackMethod = typeof(WorkshopMapDirectory).GetMethod(
                    "OnTitleQueryCompleted",
                    BindingFlags.NonPublic | BindingFlags.Static);
                var createMethod = callResultType.GetMethod(
                    "Create",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                var setMethod = FindMethod(callResultType, "Set", 2, BindingFlags.Instance);
                if (delegateType == null || callbackMethod == null ||
                    createMethod == null || setMethod == null)
                {
                    throw new InvalidOperationException("Steam Workshop query callback methods are unavailable.");
                }

                var callback = Delegate.CreateDelegate(
                    delegateType,
                    callbackMethod.MakeGenericMethod(queryResultType));
                var callResult = createMethod.Invoke(null, new object[] { callback });
                var sendMethod = FindMethod(steamUgcType, "SendQueryUGCRequest", 1, BindingFlags.Static);
                var apiCall = sendMethod == null
                    ? null
                    : sendMethod.Invoke(null, new object[] { queryHandle });
                if (apiCall == null || GetNumericValue(apiCall) == 0)
                {
                    throw new InvalidOperationException("SteamUGC subscription query could not be sent.");
                }

                _activeTitleQueryHandle = queryHandle;
                _titleQueryCallResult = callResult;
                _titleQueryCallback = callback;
                setMethod.Invoke(callResult, new object[] { apiCall, null });
            }
            catch (Exception exception)
            {
                DiagnosticLog.Trace(
                    "Workshop subscription title query failed: " + exception.Message);
                if (queryHandle != null && !IsActiveTitleQuery(queryHandle))
                {
                    ReleaseQueryHandle(queryHandle);
                }
                ReleaseActiveTitleQuery();
                FinishTitleQueryWithFailures();
            }
        }

        private static object CreateSubscribedQuery(Type steamUgcType, uint page)
        {
            var steamUserType = AccessTools.TypeByName("Steamworks.SteamUser");
            var steamUtilsType = AccessTools.TypeByName("Steamworks.SteamUtils");
            var getSteamId = FindMethod(steamUserType, "GetSteamID", 0, BindingFlags.Static);
            var getAppId = FindMethod(steamUtilsType, "GetAppID", 0, BindingFlags.Static);
            var createQuery = FindMethod(steamUgcType, "CreateQueryUserUGCRequest", 7, BindingFlags.Static);
            if (getSteamId == null || getAppId == null || createQuery == null)
            {
                throw new InvalidOperationException("Steam Workshop subscription query API is unavailable.");
            }

            var steamId = getSteamId.Invoke(null, null);
            var getAccountId = steamId == null
                ? null
                : steamId.GetType().GetMethod(
                    "GetAccountID",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    Type.EmptyTypes,
                    null);
            if (getAccountId == null)
            {
                throw new InvalidOperationException("Steam user account ID could not be read.");
            }

            var parameters = createQuery.GetParameters();
            var appId = getAppId.Invoke(null, null);
            var arguments = new object[]
            {
                getAccountId.Invoke(steamId, null),
                Enum.Parse(parameters[1].ParameterType, "k_EUserUGCList_Subscribed"),
                Enum.Parse(parameters[2].ParameterType, "k_EUGCMatchingUGCType_UsableInGame"),
                Enum.Parse(parameters[3].ParameterType, "k_EUserUGCListSortOrder_SubscriptionDateDesc"),
                appId,
                appId,
                ConvertPage(page, parameters[6].ParameterType)
            };
            var queryHandle = createQuery.Invoke(null, arguments);
            var setMatchAnyTag = FindMethod(steamUgcType, "SetMatchAnyTag", 2, BindingFlags.Static);
            var setSearchText = FindMethod(steamUgcType, "SetSearchText", 2, BindingFlags.Static);
            if (setMatchAnyTag == null || setSearchText == null)
            {
                throw new InvalidOperationException("Steam Workshop query filter API is unavailable.");
            }

            setMatchAnyTag.Invoke(null, new object[] { queryHandle, true });
            setSearchText.Invoke(null, new object[] { queryHandle, string.Empty });
            return queryHandle;
        }

        private static void OnTitleQueryCompleted<T>(T result, bool ioFailure)
        {
            var boxedResult = (object)result;
            var queryHandle = HarmonyDiagnostics.GetFieldOrPropertyValue(boxedResult, "m_handle");
            if (!IsActiveTitleQuery(queryHandle))
            {
                return;
            }

            var continuePaging = false;
            try
            {
                var querySucceeded = !ioFailure && IsSuccessfulQuery(boxedResult);
                if (querySucceeded)
                {
                    uint returnedCount;
                    uint totalCount;
                    ReadTitleQueryResults(boxedResult, out returnedCount, out totalCount);
                    _titleQueryResultsRead += returnedCount;
                    DiagnosticLog.Trace(
                        "Workshop subscription title query page completed: page=" +
                        _titleQueryPage + "; returned=" + returnedCount +
                        "; total=" + totalCount + ".");
                    continuePaging = returnedCount > 0 &&
                                     totalCount > 0 &&
                                     _titleQueryResultsRead < totalCount;
                }
                else
                {
                    DiagnosticLog.Trace(
                        "Workshop subscription title query was unsuccessful: ioFailure=" +
                        ioFailure + "; result=" +
                        Convert.ToString(HarmonyDiagnostics.GetFieldOrPropertyValue(boxedResult, "m_eResult")) + ".");
                }
            }
            catch (Exception exception)
            {
                DiagnosticLog.Trace(
                    "Workshop subscription title query callback failed: " + exception.Message);
            }
            finally
            {
                ReleaseQueryHandle(queryHandle);
                _activeTitleQueryHandle = null;
                _titleQueryCallResult = null;
                _titleQueryCallback = null;
                if (continuePaging)
                {
                    _titleQueryPage++;
                    StartTitleQueryPage();
                }
                else
                {
                    FinishTitleQueryWithFailures();
                }
            }
        }

        private static void ReadTitleQueryResults(
            object result,
            out uint returnedCount,
            out uint totalCount)
        {
            returnedCount = GetUIntField(result, "m_unNumResultsReturned");
            totalCount = GetUIntField(result, "m_unTotalMatchingResults");
            var steamUgcType = AccessTools.TypeByName("Steamworks.SteamUGC");
            var getResult = FindMethod(steamUgcType, "GetQueryUGCResult", 3, BindingFlags.Static);
            var queryHandle = HarmonyDiagnostics.GetFieldOrPropertyValue(result, "m_handle");
            if (getResult == null || queryHandle == null)
            {
                throw new InvalidOperationException("Steam Workshop query result API is unavailable.");
            }

            var parameters = getResult.GetParameters();
            for (uint index = 0; index < returnedCount; index++)
            {
                var arguments = new object[]
                {
                    queryHandle,
                    ConvertPage(index, parameters[1].ParameterType),
                    null
                };
                getResult.Invoke(null, arguments);
                var details = arguments[2];
                ulong workshopId;
                if (!HarmonyDiagnostics.TryGetPublishedFileId(
                        HarmonyDiagnostics.GetFieldOrPropertyValue(details, "m_nPublishedFileId"),
                        out workshopId) ||
                    workshopId == 0)
                {
                    continue;
                }

                MergeTitle(
                    workshopId,
                    Convert.ToString(HarmonyDiagnostics.GetFieldOrPropertyValue(details, "m_rgchTitle")));
            }
        }

        private static bool IsSuccessfulQuery(object result)
        {
            var resultCode = HarmonyDiagnostics.GetFieldOrPropertyValue(result, "m_eResult");
            if (resultCode == null)
            {
                return false;
            }

            if (string.Equals(resultCode.ToString(), "k_EResultOK", StringComparison.Ordinal))
            {
                return true;
            }

            try
            {
                return Convert.ToInt32(resultCode) == 1;
            }
            catch
            {
                return false;
            }
        }

        private static bool MergeTitle(ulong workshopId, string title)
        {
            title = (title ?? string.Empty).Trim();
            if (workshopId == 0 || title.Length == 0)
            {
                return false;
            }

            TitleCache[workshopId] = title;
            UpdateItemTitle(workshopId, title, false);
            return true;
        }

        private static void FinishTitleQueryWithFailures()
        {
            if (!_isRefreshing)
            {
                return;
            }

            for (var index = 0; index < ItemsInternal.Count; index++)
            {
                if (ItemsInternal[index].TitleReadPending)
                {
                    MarkTitleReadFailed(ItemsInternal[index].WorkshopId);
                }
            }
            _isRefreshing = false;
        }

        private static void MarkTitleReadFailed(ulong workshopId)
        {
            UpdateItemTitle(workshopId, GetFallbackTitle(workshopId), true);
        }

        private static void UpdateItemTitle(ulong workshopId, string title, bool failed)
        {
            for (var index = 0; index < ItemsInternal.Count; index++)
            {
                if (ItemsInternal[index].WorkshopId != workshopId)
                {
                    continue;
                }

                ItemsInternal[index].Title = title;
                ItemsInternal[index].TitleReadPending = false;
                ItemsInternal[index].TitleReadFailed = failed;
                return;
            }
        }

        private static void UpdateTitleQueryTimeout()
        {
            if (!_isRefreshing ||
                DateTime.UtcNow <= _titleQueryStartedAtUtc.AddSeconds(TitleQueryTimeoutSeconds))
            {
                return;
            }

            DiagnosticLog.Warning("Workshop subscription title query timed out.");
            CancelTitleQuery();
            FinishTitleQueryWithFailures();
        }

        private static void CancelTitleQuery()
        {
            if (_titleQueryCallResult != null)
            {
                try
                {
                    var cancelMethod = _titleQueryCallResult.GetType().GetMethod(
                        "Cancel",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (cancelMethod != null)
                    {
                        cancelMethod.Invoke(_titleQueryCallResult, null);
                    }
                }
                catch (Exception exception)
                {
                    DiagnosticLog.Trace(
                        "Workshop subscription title query cancellation failed: " + exception.Message);
                }
            }

            ReleaseActiveTitleQuery();
            _titleQueryCallResult = null;
            _titleQueryCallback = null;
        }

        private static void ReleaseActiveTitleQuery()
        {
            if (_activeTitleQueryHandle != null)
            {
                ReleaseQueryHandle(_activeTitleQueryHandle);
                _activeTitleQueryHandle = null;
            }
        }

        private static void ReleaseQueryHandle(object queryHandle)
        {
            try
            {
                var steamUgcType = AccessTools.TypeByName("Steamworks.SteamUGC");
                var releaseMethod = FindMethod(steamUgcType, "ReleaseQueryUGCRequest", 1, BindingFlags.Static);
                if (releaseMethod != null && queryHandle != null)
                {
                    releaseMethod.Invoke(null, new object[] { queryHandle });
                }
            }
            catch (Exception exception)
            {
                DiagnosticLog.Trace(
                    "Steam Workshop title query handle release failed: " + exception.Message);
            }
        }

        private static bool IsActiveTitleQuery(object queryHandle)
        {
            return queryHandle != null &&
                   _activeTitleQueryHandle != null &&
                   GetNumericValue(queryHandle) == GetNumericValue(_activeTitleQueryHandle);
        }

        private static MethodInfo FindMethod(
            Type type,
            string name,
            int parameterCount,
            BindingFlags bindingFlags)
        {
            if (type == null)
            {
                return null;
            }

            var methods = type.GetMethods(bindingFlags | BindingFlags.Public | BindingFlags.NonPublic);
            for (var index = 0; index < methods.Length; index++)
            {
                if (methods[index].Name == name &&
                    methods[index].GetParameters().Length == parameterCount)
                {
                    return methods[index];
                }
            }
            return null;
        }

        private static uint GetUIntField(object instance, string name)
        {
            var value = HarmonyDiagnostics.GetFieldOrPropertyValue(instance, name);
            try
            {
                return Convert.ToUInt32(value);
            }
            catch
            {
                return 0;
            }
        }

        private static ulong GetNumericValue(object instance)
        {
            var value = HarmonyDiagnostics.GetFieldOrPropertyValue(instance, "m_UGCQueryHandle");
            if (value == null)
            {
                value = HarmonyDiagnostics.GetFieldOrPropertyValue(instance, "m_SteamAPICall");
            }
            if (value == null)
            {
                value = instance;
            }

            try
            {
                return Convert.ToUInt64(value);
            }
            catch
            {
                return 0;
            }
        }

        private static object ConvertPage(uint value, Type parameterType)
        {
            return Convert.ChangeType(value, parameterType);
        }

        private static string GetFallbackTitle(ulong workshopId)
        {
            return "Workshop ID " + workshopId;
        }
    }
}
