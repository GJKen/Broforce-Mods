using System;
using UnityEngine;

namespace CustomMapMultiplayer
{
    internal sealed class SettingsUiText
    {
        internal string MultiplayerOptions;
        internal string FrpDirect;
        internal string Language;
        internal string DiagnosticLogs;
        internal string WorkshopIntro;
        internal string WorkshopEnabled;
        internal string WorkshopDisabled;
        internal string WorkshopEnabledHelp;
        internal string WorkshopDisabledHelp;
        internal string AfkEnabled;
        internal string AfkDisabled;
        internal string ManualAfkButton;
        internal string ManualAfkHelp;
        internal string WorkshopNotice;
        internal string WorkshopId;
        internal string WorkshopCampaignName;
        internal string WorkshopScene;
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
            FrpDirect = "FRP Direct",
            Language = "Language",
            DiagnosticLogs = "Diagnostic Logs",
            WorkshopIntro = "Configure Workshop map injection and AFK behavior for online games.",
            WorkshopEnabled = "Enabled: Workshop map injection",
            WorkshopDisabled = "Disabled: Workshop map injection",
            WorkshopEnabledHelp = "Arcade online games use the configured Workshop map.",
            WorkshopDisabledHelp = "Official online map selection remains unchanged.",
            AfkEnabled = "Enabled: automatic AFK spectator mode",
            AfkDisabled = "Disabled: automatic AFK spectator mode",
            ManualAfkButton = "Enter AFK now",
            ManualAfkHelp = "Immediately put your local player into AFK spectator mode during an online game.",
            WorkshopNotice = "For a third-party Workshop map, all players must use the same Mod build, subscribe to and finish downloading the same map. The host enters the numeric Workshop ID below; joining players leave their local ID blank and follow the host's published map.",
            WorkshopId = "Workshop ID (host only; joining players leave this blank and follow the host's map)",
            WorkshopCampaignName = "Workshop campaign name (optional)",
            WorkshopScene = "Custom level scene",
            FrpIntro = "Configure the optional direct transport and its connection role.",
            FrpEnabled = "Enabled: FRP Direct networking",
            FrpDisabled = "Disabled: FRP Direct networking",
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
            PerformanceTelemetryEnabled = "Enabled: performance telemetry",
            PerformanceTelemetryDisabled = "Disabled: performance telemetry",
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
            FrpDirect = "FRP 直连",
            Language = "语言",
            DiagnosticLogs = "诊断日志",
            WorkshopIntro = "配置 Workshop 地图注入和联机时的 AFK 行为。",
            WorkshopEnabled = "已启用 Workshop 地图注入",
            WorkshopDisabled = "已禁用 Workshop 地图注入",
            WorkshopEnabledHelp = "使用街机模式创建线上游戏自动使用配置的 Workshop 地图。",
            WorkshopDisabledHelp = "官方联机选图流程保持不变。",
            AfkEnabled = "已启用自动 AFK 旁观模式",
            AfkDisabled = "已禁用自动 AFK 旁观模式",
            ManualAfkButton = "立即进入 AFK",
            ManualAfkHelp = "在联机游戏中立即让本地玩家进入 AFK 旁观模式。",
            WorkshopNotice = "使用第三方 Workshop 地图时，所有玩家必须使用相同的 Mod 构建，订阅并完成下载同一张地图。房主在下面填写数字 Workshop ID；加入方将本地 ID 留空，并跟随房主发布的地图。",
            WorkshopId = "Workshop ID（仅房主填写；加入方将本地 ID 留空并跟随房主发布的地图）",
            WorkshopCampaignName = "Workshop 战役名称（可选）",
            WorkshopScene = "自定义关卡场景",
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
