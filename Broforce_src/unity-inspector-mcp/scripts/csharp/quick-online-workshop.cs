// #name Quick Online Configured Workshop
// #description Enter Arcade Online, host a room, and start the Workshop map configured in CustomMapMultiplayer.
// #tags testing, workshop, online, arcade
// #args workshopId: Expected numeric Workshop ID from the Mod configuration (default: 3715087178)
// #args playerLimit: Online room capacity (default: 3)
// #args sceneName: Expected configured Workshop scene (default: Test Evan2)

public static class QuickOnlineWorkshop
{
    public static void Main()
    {
        var workshopId = ScriptContext.GetArg("workshopId", "3715087178").Trim();
        var configuredSceneName = ScriptContext.GetArg("sceneName", "Test Evan2").Trim();
        var playerLimitText = ScriptContext.GetArg("playerLimit", "3");
        int playerLimit;
        if (!int.TryParse(playerLimitText, out playerLimit))
        {
            playerLimit = 3;
        }
        playerLimit = UnityEngine.Mathf.Clamp(playerLimit, 2, 4);

        if (string.IsNullOrEmpty(workshopId))
        {
            ScriptContext.Logger("Workshop ID is empty.");
            return;
        }

        var configuration = ReadWorkshopConfiguration();
        if (configuration.Found)
        {
            if (!configuration.Enabled)
            {
                ScriptContext.Logger(
                    "CustomMapMultiplayer Workshop injection is disabled; enable it before running this helper.");
                return;
            }

            if (!string.Equals(configuration.WorkshopId, workshopId, System.StringComparison.Ordinal))
            {
                ScriptContext.Logger(
                    "Workshop ID mismatch: helper=" + workshopId + "; Mod=" +
                    configuration.WorkshopId + ". Use the ID configured in CustomMapMultiplayer.");
                return;
            }

            if (!string.IsNullOrEmpty(configuration.SceneName))
            {
                configuredSceneName = configuration.SceneName;
            }
        }
        else
        {
            ScriptContext.Logger(
                "CustomMapMultiplayer configuration was not found; continuing without a configuration check.");
        }

        if (Connect.IsHost && !Connect.IsOffline)
        {
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            var initialStep = activeScene == LevelSelectionController.JoinScene ? 5 : 4;
            CreateRunner(workshopId, configuredSceneName, playerLimit, initialStep);
            ScriptContext.Logger("An online host session already exists; continuing the native game-start flow.");
            return;
        }

        if (MainMenu.instance == null)
        {
            ScriptContext.Logger("MainMenu is not available.");
            return;
        }

        Connect.CurrentMultiplayerMode = Utility.MultiplayerPlayMode.Online;
        Connect.PlayerLimit = playerLimit;
        OnlineOrOfflineMenu.gotToCustomCampaignMenu = false;
        CreateRunner(workshopId, configuredSceneName, playerLimit, 0);
    }

    private static void CreateRunner(
        string workshopId,
        string sceneName,
        int playerLimit,
        int initialStep)
    {
        var runnerObject = new UnityEngine.GameObject("UnityInspector.QuickOnlineWorkshopRunner");
        UnityEngine.Object.DontDestroyOnLoad(runnerObject);
        ScriptContext.GameObjects.Add(runnerObject);
        var runner = runnerObject.AddComponent<QuickOnlineWorkshopRunner>();
        runner.workshopId = workshopId;
        runner.sceneName = sceneName;
        runner.playerLimit = playerLimit;
        runner.step = initialStep;
    }

    private static void InvokePrivate(object target, string methodName, object[] arguments)
    {
        var method = target.GetType().GetMethod(
            methodName,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic);
        if (method == null)
        {
            throw new System.MissingMethodException(target.GetType().FullName, methodName);
        }

        method.Invoke(target, arguments);
    }

    private static void RequestLocalHostJoin()
    {
        var primaryController = SingletonMono<Utility.Platforms.Platform>.Instance.GetPrimaryUserController();
        if (primaryController < 0)
        {
            primaryController = 0;
        }

        HeroController.AddLocalPlayer(-1, primaryController);
    }

    private static void BindNativeHostTransition()
    {
        var onlineMenu = OnlineOrOfflineMenu.instance;
        var makeOnlineMenu = MakeOnlineMenu.Instance;
        if (onlineMenu == null || makeOnlineMenu == null)
        {
            throw new System.InvalidOperationException("Native online host menus are not available.");
        }

        var transitionMethod = onlineMenu.GetType().GetMethod(
            "TrasitionToScene",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic);
        if (transitionMethod == null)
        {
            throw new System.MissingMethodException(
                onlineMenu.GetType().FullName,
                "TrasitionToScene");
        }

        makeOnlineMenu.onHostGame = (ConnectionDelegate)System.Delegate.CreateDelegate(
            typeof(ConnectionDelegate),
            onlineMenu,
            transitionMethod);
    }

    private static WorkshopConfiguration ReadWorkshopConfiguration()
    {
        var configuration = new WorkshopConfiguration();
        var pluginType = FindType("CustomMapMultiplayer.Plugin");
        if (pluginType == null)
        {
            return configuration;
        }

        var settings = ReadStaticMember(pluginType, "Settings");
        if (settings == null)
        {
            return configuration;
        }

        configuration.Found = true;
        configuration.Enabled = ReadBoolean(settings, "EnableOnlineWorkshopInjection");
        configuration.WorkshopId = ReadMember(settings, "WorkshopId");
        configuration.SceneName = ReadMember(settings, "WorkshopSceneName");
        return configuration;
    }

    private static System.Type FindType(string fullName)
    {
        var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
        for (var index = 0; index < assemblies.Length; index++)
        {
            var type = assemblies[index].GetType(fullName, false);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }

    private sealed class WorkshopConfiguration
    {
        internal bool Found;
        internal bool Enabled;
        internal string WorkshopId = string.Empty;
        internal string SceneName = string.Empty;
    }

    private sealed class QuickOnlineWorkshopRunner : UnityEngine.MonoBehaviour
    {
        internal string workshopId;
        internal string sceneName;
        internal int playerLimit;
        internal int step;
        private float deadline;
        private float nextActionAt;
        private float joinRetryAt;
        private bool campaignMenuRequested;
        private bool gameTransitionRequested;
        private bool joinRequested;

        private void Start()
        {
            deadline = UnityEngine.Time.realtimeSinceStartup + 120f;
            nextActionAt = UnityEngine.Time.realtimeSinceStartup + 0.5f;
        }

        private void Update()
        {
            try
            {
                var now = UnityEngine.Time.realtimeSinceStartup;
                if (now >= deadline)
                {
                    LogAndDestroy("Quick online configured Workshop flow timed out at step " + step + ".");
                    return;
                }

                if (IsTargetMapReady())
                {
                    ScriptContext.Logger(
                        "Native online flow reached Workshop map " + workshopId +
                        " in scene " + sceneName + "; local player count=" +
                        HeroController.GetLocalPlayerCount() + ".");
                    UnityEngine.Object.Destroy(gameObject);
                    return;
                }

                if (step == 0)
                {
                    if (MainMenu.instance == null ||
                        !ReadBoolean(MainMenu.instance, "hasInitialized") ||
                        now < nextActionAt)
                    {
                        return;
                    }

                    if (!campaignMenuRequested)
                    {
                        MainMenu.instance.GoToCampaignMenu();
                        campaignMenuRequested = true;
                        nextActionAt = now + 0.75f;
                        ScriptContext.Logger("Opened the Arcade campaign menu.");
                        return;
                    }

                    var arcadeMenu = MainMenu.instance.worldMapOrArcadeMenu as WorldMapOrArcadeMenu;
                    if (arcadeMenu == null || !arcadeMenu.MenuActive)
                    {
                        return;
                    }

                    InvokePrivate(arcadeMenu, "StartArcade", null);
                    step = 1;
                    nextActionAt = now + 0.75f;
                    ScriptContext.Logger("Selected Arcade mode.");
                    return;
                }

                if (step == 1)
                {
                    var difficultyMenu = DifficultyMenu.instance;
                    if (difficultyMenu == null || !difficultyMenu.MenuActive || now < nextActionAt)
                    {
                        return;
                    }

                    if (PlayerProgress.Instance.lastFinishedLevelOffline > 0)
                    {
                        PlayerProgress.Instance.lastFinishedLevelOffline = 0;
                    }

                    InvokePrivate(difficultyMenu, "StartArcade", new object[] { false, true });
                    step = 2;
                    nextActionAt = now + 0.75f;
                    ScriptContext.Logger("Selected Normal difficulty and opened the Online menu.");
                    return;
                }

                if (step == 2)
                {
                    var onlineMenu = OnlineOrOfflineMenu.instance;
                    if (onlineMenu == null || !onlineMenu.onlineAvalable || now < nextActionAt)
                    {
                        return;
                    }

                    Connect.CurrentMultiplayerMode = Utility.MultiplayerPlayMode.Online;
                    Connect.PlayerLimit = playerLimit;
                    OnlineOrOfflineMenu.gotToCustomCampaignMenu = false;
                    InvokePrivate(onlineMenu, "Close", null);
                    MakeOnlineMenu.Open();
                    step = 3;
                    nextActionAt = now + 1f;
                    ScriptContext.Logger("Selected Online and opened the native host-room menu.");
                    return;
                }

                if (step == 3)
                {
                    var onlineMenu = MakeOnlineMenu.Instance;
                    if (onlineMenu == null || !ReadBoolean(onlineMenu, "open") || now < nextActionAt)
                    {
                        return;
                    }

                    // Use the same callback installed by the native Online menu. It transitions
                    // to newJoin immediately after CreateMatch, while the room becomes ready.
                    BindNativeHostTransition();
                    InvokePrivate(onlineMenu, "DoHostGame", null);
                    gameTransitionRequested = true;
                    step = 4;
                    ScriptContext.Logger("Requested a " + playerLimit + "-player online room.");
                    return;
                }

                if (step == 4)
                {
                    if (!IsOnlineHostReady())
                    {
                        return;
                    }

                    if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name ==
                        LevelSelectionController.JoinScene)
                    {
                        step = 5;
                        return;
                    }

                    if (!gameTransitionRequested)
                    {
                        ContinueThroughNativeOnlineTransition();
                        gameTransitionRequested = true;
                        step = 5;
                        ScriptContext.Logger("Online room is ready; entered the native newJoin flow.");
                    }
                    return;
                }

                if (step == 5 || step == 6)
                {
                    EnsureLocalHostPlayer(now);
                }
            }
            catch (System.Exception exception)
            {
                LogAndDestroy(
                    "Quick online configured Workshop flow failed at step " + step + ": " + exception);
            }
        }

        private void EnsureLocalHostPlayer(float now)
        {
            if (!IsOnlineHostReady())
            {
                return;
            }

            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name !=
                LevelSelectionController.JoinScene)
            {
                return;
            }

            if (HeroController.GetLocalPlayerCount() > 0)
            {
                if (!joinRequested)
                {
                    ScriptContext.Logger("The native newJoin flow already contains the local P1.");
                }
                step = 6;
                return;
            }

            if (now < joinRetryAt)
            {
                return;
            }

            RequestLocalHostJoin();
            joinRequested = true;
            joinRetryAt = now + 3f;
            ScriptContext.Logger("Requested the local host P1 through HeroController.AddLocalPlayer.");
        }

        private void ContinueThroughNativeOnlineTransition()
        {
            var onlineMenu = OnlineOrOfflineMenu.instance;
            if (onlineMenu != null)
            {
                InvokePrivate(onlineMenu, "TrasitionToScene", null);
                return;
            }

            GameState.FadeToNextScene(string.Empty);
        }

        private bool IsOnlineHostReady()
        {
            return Connect.Layer != null &&
                   Connect.IsHost &&
                   !Connect.IsOffline &&
                   Connect.Layer.IsOnlineRoomReady;
        }

        private bool IsTargetMapReady()
        {
            if (!string.Equals(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                    sceneName,
                    System.StringComparison.OrdinalIgnoreCase) ||
                HeroController.GetLocalPlayerCount() < 1)
            {
                return false;
            }

            var state = GameState.Instance;
            var stateWorkshopId = ReadMember(state, "customLevelID");
            return string.Equals(stateWorkshopId, workshopId, System.StringComparison.Ordinal);
        }

        private void LogAndDestroy(string message)
        {
            ScriptContext.Logger(message);
            UnityEngine.Object.Destroy(gameObject);
        }
    }

    private static bool ReadBoolean(object target, string name)
    {
        var value = ReadMemberValue(target, name);
        return value is bool && (bool)value;
    }

    private static string ReadMember(object target, string name)
    {
        var value = ReadMemberValue(target, name);
        return value == null ? string.Empty : value.ToString().Trim();
    }

    private static object ReadStaticMember(System.Type targetType, string name)
    {
        if (targetType == null)
        {
            return null;
        }

        var flags =
            System.Reflection.BindingFlags.Static |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic;
        var field = targetType.GetField(name, flags);
        if (field != null)
        {
            return field.GetValue(null);
        }

        var property = targetType.GetProperty(name, flags);
        return property == null ? null : property.GetValue(null, null);
    }

    private static object ReadMemberValue(object target, string name)
    {
        if (target == null)
        {
            return null;
        }

        var type = target.GetType();
        var flags =
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic;
        var field = type.GetField(name, flags);
        if (field != null)
        {
            return field.GetValue(target);
        }

        var property = type.GetProperty(name, flags);
        return property == null ? null : property.GetValue(target, null);
    }
}
