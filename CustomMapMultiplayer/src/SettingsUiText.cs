using System;
using UnityEngine;

namespace CustomMapMultiplayer
{
    internal sealed class SettingsUiText
    {
        internal string MultiplayerOptions;
        internal string MultiplayerOptionsIntro;
        internal string WorkshopMapSettings;
        internal string FrpDirect;
        internal string Language;
        internal string DiagnosticLogs;
        internal string WorkshopIntro;
        internal string WorkshopHostLabel;
        internal string WorkshopHostHelp;
        internal string WorkshopJoinLabel;
        internal string WorkshopJoinHelp;
        internal string WorkshopEnabled;
        internal string WorkshopDisabled;
        internal string WorkshopEnabledHelp;
        internal string WorkshopDisabledHelp;
        internal string AfkEnabled;
        internal string AfkDisabled;
        internal string AfkHelp;
        internal string OverlapMeleeEnabled;
        internal string OverlapMeleeDisabled;
        internal string OverlapMeleeHelpEnabled;
        internal string OverlapMeleeHelpDisabled;
        internal string WorkshopDropInRespawnFixEnabled;
        internal string WorkshopDropInRespawnFixDisabled;
        internal string WorkshopDropInRespawnFixHelp;
        internal string ManualAfkButton;
        internal string ManualAfkSinglePlayerNotice;
        internal string WorkshopNotice;
        internal string WorkshopSubscriptionMissingNotice;
        internal string WorkshopId;
        internal string WorkshopCampaignName;
        internal string WorkshopCampaignNameHelp;
        internal string WorkshopScene;
        internal string WorkshopSceneHelp;
        internal string WorkshopStatusRefreshingKicker;
        internal string WorkshopStatusRefreshing;
        internal string WorkshopStatusRefreshingDetail;
        internal string WorkshopStatusEmptyKicker;
        internal string WorkshopStatusEmpty;
        internal string WorkshopStatusEmptyDetail;
        internal string WorkshopStatusUnavailableKicker;
        internal string WorkshopStatusUnavailable;
        internal string WorkshopStatusUnavailableDetail;
        internal string WorkshopTitleReadFailed;
        internal string WorkshopRefreshTooltip;
        internal string WorkshopMapSelectLabel;
        internal string WorkshopMapSelectionIntro;
        internal string WorkshopMapCloseTooltip;
        internal string WorkshopMapSelectionCurrent;
        internal string WorkshopMapSelectionManual;
        internal string WorkshopMapSelectionNone;
        internal string WorkshopMapSearch;
        internal string WorkshopMapFilterAll;
        internal string WorkshopMapFilterRecent;
        internal string WorkshopMapFilterFavorites;
        internal string WorkshopMapSelectionCount;
        internal string WorkshopMapSelectionEmpty;
        internal string WorkshopMapFavoriteTooltip;
        internal string WorkshopMapUnfavoriteTooltip;
        internal string WorkshopMapClearFavoritesTooltip;
        internal string WorkshopMapSearchClearTooltip;
        internal string WorkshopMapManualSelection;
        internal string WorkshopMapNoSelection;
        internal string WorkshopMapEnterId;
        internal string WorkshopMapEmpty;
        internal string WorkshopMapTitleLoading;
        internal string WorkshopMapTitleUnavailable;
        internal string WorkshopManualIdHelpReady;
        internal string WorkshopManualIdHelpRefreshing;
        internal string WorkshopManualIdHelpEmpty;
        internal string WorkshopMapStatusInstalledReadable;
        internal string WorkshopMapStatusNotInstalled;
        internal string WorkshopMapStatusCampaignUnreadable;
        internal string WorkshopIdPrefix;
        internal string WorkshopAdvancedTitle;
        internal string FrpIntro;
        internal string FrpEnabled;
        internal string FrpDisabled;
        internal string FrpEnabledHelp;
        internal string FrpDisabledHelp;
        internal string FrpRole;
        internal string FrpRoleHelp;
        internal string Host;
        internal string Client;
        internal string LocalUdpPort;
        internal string FrpPlayerLimit;
        internal string FrpServerEndpoint;
        internal string FrpRoomPassword;
        internal string FrpStatus;
        internal string FrpStatusDisabled;
        internal string FrpStatusListening;
        internal string FrpStatusWaiting;
        internal string LanguageIntro;
        internal string[] LanguageChoices;
        internal string DiagnosticsIntro;
        internal string DiagnosticSessionId;
        internal string DiagnosticLabel;
        internal string OpenLogDirectory;
        internal string PerformanceTelemetryEnabled;
        internal string PerformanceTelemetryDisabled;
        internal string PerformanceTelemetryHelp;
        internal string DiagnosticLogPreset;
        internal string[] DiagnosticPresets;
        internal string DiagnosticCategories;
        internal string[] DiagnosticCategoryLabels;
    }

    internal static class SettingsUiLocalization
    {
        private static readonly SettingsUiText English = new SettingsUiText
        {
            MultiplayerOptions = "Multiplayer Options",
            MultiplayerOptionsIntro = "Configure player behavior in online games.",
            WorkshopMapSettings = "Workshop Map Settings",
            FrpDirect = "FRP Direct",
            Language = "Language",
            DiagnosticLogs = "Diagnostic Logs",
            WorkshopIntro = "Configure Workshop maps for online games.",
            WorkshopHostLabel = "Host:",
            WorkshopHostHelp = "Use the selected Workshop map when creating the room.",
            WorkshopJoinLabel = "Joining player:",
            WorkshopJoinHelp = "Use the host's published map when joining; the local ID does not override it.",
            WorkshopEnabled = "Enabled Workshop map injection",
            WorkshopDisabled = "Disabled Workshop map injection",
            WorkshopEnabledHelp = "Arcade online games use the configured Workshop map.",
            WorkshopDisabledHelp = "Official online map selection remains unchanged.",
            AfkEnabled = "Enabled automatic AFK spectator mode",
            AfkDisabled = "Disabled automatic AFK spectator mode",
            AfkHelp = "When disabled, online players are not sent to spectator mode by inactivity; manual AFK remains available.",
            OverlapMeleeEnabled = "Enabled melee overrides player-overlap high-five",
            OverlapMeleeDisabled = "Disabled player-overlap high-five",
            OverlapMeleeHelpEnabled = "Pressing melee while players overlap starts the selected bro's melee instead of an automatic high-five.",
            OverlapMeleeHelpDisabled = "Pressing melee while players overlap keeps Broforce's automatic high-five behavior.",
            WorkshopDropInRespawnFixEnabled = "Enabled Workshop drop-in respawn position fix",
            WorkshopDropInRespawnFixDisabled = "Disabled Workshop drop-in respawn position fix",
            WorkshopDropInRespawnFixHelp = "Corrects abnormal DropInDuringGame positions only after a stable direct Workshop spawn; transport, parachute, checkpoint, rescue, and cage spawns remain native.",
            ManualAfkButton = "Enter AFK now",
            ManualAfkSinglePlayerNotice = "AFK is unavailable while you are the only player in the room.",
            WorkshopNotice = "When using a Workshop map, all players must use the same Mod build and subscribe to the same map.\nThe host enters the numeric Workshop ID.\nAfter joining, the joining player uses the host's Workshop ID; this setting can be ignored.",
            WorkshopSubscriptionMissingNotice = "The host is using Steam Workshop map ID {id}, but the map is not subscribed on this local machine. Please subscribe to the map in the Steam Workshop, restart the game, and then rejoin the room.",
            WorkshopId = "Workshop ID",
            WorkshopCampaignName = "Workshop campaign name (optional)",
            WorkshopCampaignNameHelp = "Optional override for the campaign name sent with the Workshop map. Usually leave it empty.",
            WorkshopScene = "Custom level scene",
            WorkshopSceneHelp = "Usually leave Test Evan2 unchanged.\nChange it only when the map author provides a different Unity scene name.",
            WorkshopStatusRefreshingKicker = "Refreshing subscribed items",
            WorkshopStatusRefreshing = "Checking subscribed items and local campaign files...",
            WorkshopStatusRefreshingDetail = "Workshop titles can be filled in after Steam details finish loading.",
            WorkshopStatusEmptyKicker = "No subscribed Workshop items",
            WorkshopStatusEmpty = "{subscribed} subscribed items returned.",
            WorkshopStatusEmptyDetail = "",
            WorkshopStatusUnavailableKicker = "Workshop data unavailable",
            WorkshopStatusUnavailable = "The local Workshop list could not be read.",
            WorkshopStatusUnavailableDetail = "Manual Workshop ID input remains available.",
            WorkshopTitleReadFailed = "Some Workshop titles could not be read; their full IDs remain available.",
            WorkshopRefreshTooltip = "Refresh subscribed Workshop maps",
            WorkshopMapSelectLabel = "Choose a subscribed map",
            WorkshopMapSelectionIntro = "Choose a subscribed Workshop map. Selecting a map only changes the Workshop ID.",
            WorkshopMapCloseTooltip = "Back to map settings",
            WorkshopMapSelectionCurrent = "Selected map: {title} (Workshop ID: {id})",
            WorkshopMapSelectionManual = "Current Workshop ID: {id} (not in the subscribed list)",
            WorkshopMapSelectionNone = "No Workshop map selected yet.",
            WorkshopMapSearch = "Search",
            WorkshopMapFilterAll = "All",
            WorkshopMapFilterRecent = "Recent online maps",
            WorkshopMapFilterFavorites = "Starred",
            WorkshopMapSelectionCount = "{count} results",
            WorkshopMapSelectionEmpty = "No matching Workshop maps.",
            WorkshopMapFavoriteTooltip = "Star this map",
            WorkshopMapUnfavoriteTooltip = "Remove star",
            WorkshopMapClearFavoritesTooltip = "Clear all starred maps",
            WorkshopMapSearchClearTooltip = "Clear search",
            WorkshopMapManualSelection = "Manual Workshop ID",
            WorkshopMapNoSelection = "Workshop ID is empty",
            WorkshopMapEnterId = "",
            WorkshopMapEmpty = "No subscribed Workshop items. Enter a Workshop ID manually above.",
            WorkshopMapTitleLoading = "Loading Workshop title",
            WorkshopMapTitleUnavailable = "Workshop title unavailable",
            WorkshopManualIdHelpReady = "Enter a numeric ID directly, or choose a subscribed map.",
            WorkshopManualIdHelpRefreshing = "The subscribed list is refreshing; manual ID input remains available.",
            WorkshopManualIdHelpEmpty = "The list is empty; manual numeric ID input remains available.",
            WorkshopMapStatusInstalledReadable = "Installed and readable.",
            WorkshopMapStatusNotInstalled = "Subscribed, but the map has not finished downloading locally.",
            WorkshopMapStatusCampaignUnreadable = "Downloaded, but no readable campaign file was found locally.",
            WorkshopIdPrefix = "Workshop ID:",
            WorkshopAdvancedTitle = "Other map settings",
            FrpIntro = "Configure the optional direct transport and its connection role.",
            FrpEnabled = "Enabled FRP Direct networking",
            FrpDisabled = "Disabled FRP Direct networking",
            FrpEnabledHelp = "FRP Direct transport is active.",
            FrpDisabledHelp = "Native Steam networking is active.",
            FrpRole = "FRP Direct role",
            FrpRoleHelp = "Select your FRP Direct role using the buttons below.",
            Host = "Host",
            Client = "Client",
            LocalUdpPort = "Local UDP listen port",
            FrpPlayerLimit = "FRP room player limit (applies immediately)",
            FrpServerEndpoint = "FRP server endpoint (host:port)",
            FrpRoomPassword = "FRP room password (optional)",
            FrpStatus = "FRP Direct status: ",
            FrpStatusDisabled = "Disabled",
            FrpStatusListening = "Listening on UDP ",
            FrpStatusWaiting = "Waiting to connect",
            LanguageIntro = "Choose how the UMM settings text is displayed.",
            LanguageChoices = new[] { "Follow system", "English", "中文" },
            DiagnosticsIntro = "Choose the session identity and diagnostic output categories.",
            DiagnosticSessionId = "Diagnostic session ID (use the same value on both clients; optional)",
            DiagnosticLabel = "Diagnostic label (optional; only used in log names)",
            OpenLogDirectory = "Open diagnostic log directory",
            PerformanceTelemetryEnabled = "Enabled performance telemetry",
            PerformanceTelemetryDisabled = "Disabled performance telemetry",
            PerformanceTelemetryHelp = "When enabled, writes a two-second aggregate summary of frame time and Mod hot paths to the diagnostic file. It does not change online behavior.",
            DiagnosticLogPreset = "Diagnostic log preset",
            DiagnosticPresets = new[] { "Basic", "Join / Rejoin", "AFK / Failure", "Workshop", "Full" },
            DiagnosticCategories = "Diagnostic categories (log output only; online behavior is unchanged.)",
            DiagnosticCategoryLabels = new[]
            {
                "Lobby and network session",
                "Workshop download/load/scenes",
                "Player join/spawn/dropout",
                "AFK and Dropout",
                "Lives/failure/level outcome",
                "Workshop items and object sync",
                "FRP Direct transport",
                "Optional Mod compatibility",
                "Harmony detailed tracing"
            },
        };

        private static readonly SettingsUiText Chinese = new SettingsUiText
        {
            MultiplayerOptions = "多人游戏选项",
            MultiplayerOptionsIntro = "配置联机时的玩家行为。",
            WorkshopMapSettings = "Workshop 地图配置",
            FrpDirect = "FRP 直连",
            Language = "语言",
            DiagnosticLogs = "诊断日志",
            WorkshopIntro = "配置联机时使用的 Workshop 地图。",
            WorkshopHostLabel = "房主：",
            WorkshopHostHelp = "创建房间时使用选中的 Workshop 地图。",
            WorkshopJoinLabel = "加入方：",
            WorkshopJoinHelp = "加入房间时使用房主发布的地图，本地 ID 不会覆盖房主配置。",
            WorkshopEnabled = "已启用 Workshop 地图注入",
            WorkshopDisabled = "已禁用 Workshop 地图注入",
            WorkshopEnabledHelp = "使用街机模式创建线上游戏自动使用配置的 Workshop 地图。",
            WorkshopDisabledHelp = "官方联机选图流程保持不变。",
            AfkEnabled = "已启用 自动 AFK 旁观模式",
            AfkDisabled = "已禁用 自动 AFK 旁观模式",
            AfkHelp = "关闭后，联机玩家不会因长时间无操作自动进入观战；仍可手动进入 AFK。",
            OverlapMeleeEnabled = "已启用 玩家重叠时优先执行近战",
            OverlapMeleeDisabled = "已禁用 玩家重叠时仍会击掌",
            OverlapMeleeHelpEnabled = "玩家重叠时按近战会执行当前角色的近战动作，不再自动变成击掌。",
            OverlapMeleeHelpDisabled = "玩家重叠时按近战会保留 Broforce 原生的自动击掌行为。",
            WorkshopDropInRespawnFixEnabled = "已启用 Workshop 复活位置修复",
            WorkshopDropInRespawnFixDisabled = "已禁用 Workshop 复活位置修复",
            WorkshopDropInRespawnFixHelp = "修复部分 Workshop 地图中角色死亡后复活位置错误的问题；运输、降落伞、检查点、救援和笼子出生保持原生行为。",
            ManualAfkButton = "立即进入 AFK",
            ManualAfkSinglePlayerNotice = "房间只有你一人时无法进入 AFK。",
            WorkshopNotice = "使用 Workshop 地图时，所有玩家必须使用相同的 Mod 构建，并订阅同一张地图。\n房主填写数字 Workshop ID；\n进入房间后，加入方则会使用房主的 Workshop ID，此项无需留意。",
            WorkshopSubscriptionMissingNotice = "房主使用的 Steam 创意工坊地图 ID 为 {id}，但本机尚未订阅。请先在 Steam 创意工坊订阅地图，重启游戏后，再重新加入房间。",
            WorkshopId = "Workshop ID",
            WorkshopCampaignName = "Workshop 战役名称（可选）",
            WorkshopCampaignNameHelp = "可选的战役名覆盖设置，通常留空即可。只有地图需要指定战役名时才填写。",
            WorkshopScene = "自定义关卡场景",
            WorkshopSceneHelp = "通常保持 Test Evan2。\n只有地图作者明确提供其他 Unity 场景名时才修改。",
            WorkshopStatusRefreshingKicker = "正在刷新已订阅项目",
            WorkshopStatusRefreshing = "正在检查订阅项目和本地战役文件……",
            WorkshopStatusRefreshingDetail = "Steam 详情加载完成后可以补充 Workshop 标题。",
            WorkshopStatusEmptyKicker = "没有已订阅 Workshop 项目",
            WorkshopStatusEmpty = "已订阅 {subscribed} 项。",
            WorkshopStatusEmptyDetail = "",
            WorkshopStatusUnavailableKicker = "Workshop 数据不可用",
            WorkshopStatusUnavailable = "无法读取本机 Workshop 列表。",
            WorkshopStatusUnavailableDetail = "仍可手工输入 Workshop ID。",
            WorkshopTitleReadFailed = "部分 Workshop 标题读取失败，仍可使用完整 ID。",
            WorkshopRefreshTooltip = "刷新已订阅 Workshop 地图",
            WorkshopMapSelectLabel = "选择已订阅地图",
            WorkshopMapSelectionIntro = "选择一张已订阅的 Workshop 地图。选择地图只会修改 Workshop ID。",
            WorkshopMapCloseTooltip = "返回配置",
            WorkshopMapSelectionCurrent = "当前已选择：{title}（Workshop ID：{id}）",
            WorkshopMapSelectionManual = "当前 Workshop ID：{id}（不在已订阅列表中）",
            WorkshopMapSelectionNone = "尚未选择 Workshop 地图。",
            WorkshopMapSearch = "搜索",
            WorkshopMapFilterAll = "全部",
            WorkshopMapFilterRecent = "最近联机地图",
            WorkshopMapFilterFavorites = "已标星",
            WorkshopMapSelectionCount = "{count} 个结果",
            WorkshopMapSelectionEmpty = "没有匹配的 Workshop 地图。",
            WorkshopMapFavoriteTooltip = "标星此地图",
            WorkshopMapUnfavoriteTooltip = "取消标星",
            WorkshopMapClearFavoritesTooltip = "取消全部星标",
            WorkshopMapSearchClearTooltip = "清除搜索",
            WorkshopMapManualSelection = "手工 Workshop ID",
            WorkshopMapNoSelection = "Workshop ID 当前为空",
            WorkshopMapEnterId = "",
            WorkshopMapEmpty = "没有已订阅 Workshop 项目。请在上方手工输入 Workshop ID。",
            WorkshopMapTitleLoading = "正在读取 Workshop 标题",
            WorkshopMapTitleUnavailable = "Workshop 标题不可用",
            WorkshopManualIdHelpReady = "直接输入数字 ID，或从已订阅地图中选择。",
            WorkshopManualIdHelpRefreshing = "已订阅项目正在刷新，仍可手工输入 ID。",
            WorkshopManualIdHelpEmpty = "列表为空，仍可手工输入数字 ID。",
            WorkshopMapStatusInstalledReadable = "已安装，可以读取地图文件。",
            WorkshopMapStatusNotInstalled = "已订阅，但地图尚未完整下载到本机。",
            WorkshopMapStatusCampaignUnreadable = "已下载，但本机找不到或无法读取有效战役文件。",
            WorkshopIdPrefix = "Workshop ID：",
            WorkshopAdvancedTitle = "其它地图设置",
            FrpIntro = "配置可选的直连传输方式和连接角色。",
            FrpEnabled = "已启用 FRP 直连网络",
            FrpDisabled = "已禁用 FRP 直连网络",
            FrpEnabledHelp = "当前使用 FRP 直连传输。",
            FrpDisabledHelp = "当前使用原生 Steam 联机。",
            FrpRole = "FRP 直连角色",
            FrpRoleHelp = "使用下面的按钮选择 FRP 直连角色。",
            Host = "房主",
            Client = "加入方",
            LocalUdpPort = "本地 UDP 监听端口",
            FrpPlayerLimit = "FRP 房间人数上限（立即生效）",
            FrpServerEndpoint = "FRP 服务器地址（host:port）",
            FrpRoomPassword = "FRP 房间密码（可选）",
            FrpStatus = "FRP 直连状态：",
            FrpStatusDisabled = "已关闭",
            FrpStatusListening = "正在监听 UDP ",
            FrpStatusWaiting = "等待连接",
            LanguageIntro = "选择 UMM 设置界面的显示语言。",
            LanguageChoices = new[] { "跟随系统", "English", "中文" },
            DiagnosticsIntro = "设置会话标识和诊断日志输出分类。",
            DiagnosticSessionId = "诊断会话 ID（双方使用相同值；可选）",
            DiagnosticLabel = "诊断标签（可选；仅用于日志文件名）",
            OpenLogDirectory = "打开诊断日志目录",
            PerformanceTelemetryEnabled = "已启用性能观测",
            PerformanceTelemetryDisabled = "已禁用性能观测",
            PerformanceTelemetryHelp = "启用后每两秒向诊断文件写入一次帧时间和 Mod 热路径聚合摘要，不改变联机行为。",
            DiagnosticLogPreset = "诊断日志预设",
            DiagnosticPresets = new[] { "基础", "加入/重新加入", "AFK/失败", "Workshop", "完整" },
            DiagnosticCategories = "诊断分类（只筛选日志输出，不改变联机行为。）",
            DiagnosticCategoryLabels = new[]
            {
                "大厅和联机会话",
                "Workshop 下载/加载/场景",
                "玩家加入/生成/掉线",
                "AFK 和掉线",
                "生命/失败/关卡结果",
                "Workshop 道具和物件同步",
                "FRP 直连传输",
                "可选 Mod 兼容性",
                "Harmony 详细跟踪"
            },
        };

        internal static SettingsUiText Get(string preference)
        {
            if (string.Equals(preference, "zh", StringComparison.OrdinalIgnoreCase))
            {
                return Chinese;
            }

            if (string.Equals(preference, "en", StringComparison.OrdinalIgnoreCase))
            {
                return English;
            }

            return Application.systemLanguage.ToString().StartsWith(
                "Chinese",
                StringComparison.OrdinalIgnoreCase)
                ? Chinese
                : English;
        }
    }
}
