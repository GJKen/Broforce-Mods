// #name Quick Join Workshop Lobby
// #description Join an existing Steam lobby by ID and wait for its configured Workshop map.
// #tags testing, workshop, online, join
// #args lobbyId: Required Steam lobby ID published by the host
// #args workshopId: Expected Workshop ID (default: 3715087178)
// #args sceneName: Expected Workshop scene (default: Test Evan2)
// #args hostName: Optional expected Steam host name

public static class QuickJoinWorkshopLobby
{
    private const string WorkshopIdKey = "GJKen_BroforceOnline_WorkshopId";
    private const string WorkshopSceneKey = "GJKen_BroforceOnline_WorkshopScene";
    private const string WorkshopReadyKey = "GJKen_BroforceOnline_WorkshopReady";
    private const string WorkshopPhaseKey = "GJKen_BroforceOnline_WorkshopPhase";

    public static void Main()
    {
        var lobbyIdText = ScriptContext.GetArg("lobbyId", string.Empty).Trim();
        var workshopId = ScriptContext.GetArg("workshopId", "3715087178").Trim();
        var sceneName = ScriptContext.GetArg("sceneName", "Test Evan2").Trim();
        var hostName = ScriptContext.GetArg("hostName", string.Empty).Trim();
        ulong lobbyId;

        if (!ulong.TryParse(lobbyIdText, out lobbyId) || lobbyId == 0UL)
        {
            ScriptContext.Logger("A valid lobbyId is required.");
            return;
        }

        if (string.IsNullOrEmpty(workshopId) || string.IsNullOrEmpty(sceneName))
        {
            ScriptContext.Logger("workshopId and sceneName are required.");
            return;
        }

        if (SteamLayer.Instance == null || !(Connect.Layer is SteamLayer))
        {
            ScriptContext.Logger("The Steam connection layer is not available.");
            return;
        }

        if (Connect.IsHost && !Connect.IsOffline)
        {
            ScriptContext.Logger("This game is already hosting an online room; leave it before joining another lobby.");
            return;
        }

        if (!Connect.IsHost && !Connect.IsOffline && IsTargetMapReady(workshopId, sceneName))
        {
            ScriptContext.Logger("This client is already in the requested Workshop lobby map.");
            return;
        }

        if (ConnectionLayer.connectionState == ConnectionState.Connecting)
        {
            ScriptContext.Logger("A lobby connection is already in progress.");
            return;
        }

        var runnerObject = new UnityEngine.GameObject("UnityInspector.QuickJoinWorkshopLobbyRunner");
        UnityEngine.Object.DontDestroyOnLoad(runnerObject);
        ScriptContext.GameObjects.Add(runnerObject);
        var runner = runnerObject.AddComponent<QuickJoinWorkshopLobbyRunner>();
        runner.lobbyId = lobbyId;
        runner.workshopId = workshopId;
        runner.sceneName = sceneName;
        runner.hostName = hostName;
        ScriptContext.Logger("Started the direct Steam Workshop lobby join flow.");
    }

    private static bool IsTargetMapReady(string workshopId, string sceneName)
    {
        if (!string.Equals(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                sceneName,
                System.StringComparison.OrdinalIgnoreCase) ||
            HeroController.GetLocalPlayerCount() < 1)
        {
            return false;
        }

        return string.Equals(
            ReadMember(GameState.Instance, "customLevelID"),
            workshopId,
            System.StringComparison.Ordinal);
    }

    private static string ReadMember(object target, string name)
    {
        if (target == null)
        {
            return string.Empty;
        }

        var flags =
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic;
        var field = target.GetType().GetField(name, flags);
        if (field != null)
        {
            var value = field.GetValue(target);
            return value == null ? string.Empty : value.ToString().Trim();
        }

        var property = target.GetType().GetProperty(name, flags);
        if (property == null)
        {
            return string.Empty;
        }

        var propertyValue = property.GetValue(target, null);
        return propertyValue == null ? string.Empty : propertyValue.ToString().Trim();
    }

    private sealed class QuickJoinWorkshopLobbyRunner : UnityEngine.MonoBehaviour
    {
        internal ulong lobbyId;
        internal string workshopId;
        internal string sceneName;
        internal string hostName;

        private Steamworks.CSteamID steamLobbyId;
        private float deadline;
        private float nextLobbyDataRequestAt;
        private bool joinRequested;
        private bool connectedLogged;

        private void Start()
        {
            steamLobbyId = new Steamworks.CSteamID(lobbyId);
            deadline = UnityEngine.Time.realtimeSinceStartup + 120f;
            nextLobbyDataRequestAt = UnityEngine.Time.realtimeSinceStartup;
        }

        private void Update()
        {
            try
            {
                var now = UnityEngine.Time.realtimeSinceStartup;
                if (now >= deadline)
                {
                    LogAndDestroy("Quick Workshop lobby join timed out.");
                    return;
                }

                if (IsTargetMapReady(workshopId, sceneName))
                {
                    LogAndDestroy(
                        "Joined Steam lobby " + lobbyId + " and reached Workshop scene " +
                        sceneName + "; local player count=" +
                        HeroController.GetLocalPlayerCount() + ".");
                    return;
                }

                if (!joinRequested)
                {
                    TryRequestAndJoin(now);
                    return;
                }

                if (!connectedLogged &&
                    ConnectionLayer.connectionState == ConnectionState.Connected &&
                    !Connect.IsHost &&
                    !Connect.IsOffline)
                {
                    connectedLogged = true;
                    ScriptContext.Logger(
                        "Joined the Steam lobby; waiting for CustomMapMultiplayer late-join loading.");
                }
            }
            catch (System.Exception exception)
            {
                LogAndDestroy("Quick Workshop lobby join failed: " + exception);
            }
        }

        private void TryRequestAndJoin(float now)
        {
            if (now < nextLobbyDataRequestAt)
            {
                return;
            }
            nextLobbyDataRequestAt = now + 1f;

            Steamworks.SteamMatchmaking.RequestLobbyData(steamLobbyId);
            var actualWorkshopId = Steamworks.SteamMatchmaking.GetLobbyData(
                steamLobbyId,
                WorkshopIdKey).Trim();
            if (string.IsNullOrEmpty(actualWorkshopId))
            {
                return;
            }

            var actualSceneName = Steamworks.SteamMatchmaking.GetLobbyData(
                steamLobbyId,
                WorkshopSceneKey).Trim();
            if (!string.Equals(actualWorkshopId, workshopId, System.StringComparison.Ordinal) ||
                !string.Equals(actualSceneName, sceneName, System.StringComparison.OrdinalIgnoreCase))
            {
                LogAndDestroy(
                    "Lobby metadata mismatch: expected Workshop " + workshopId + " / " +
                    sceneName + "; actual " + actualWorkshopId + " / " + actualSceneName + ".");
                return;
            }

            var room = new SteamLayer.SteamGameInfo(steamLobbyId);
            room.PullUpdatedInfo();
            if (!string.IsNullOrEmpty(hostName) &&
                !string.Equals(room.HostName, hostName, System.StringComparison.OrdinalIgnoreCase))
            {
                LogAndDestroy(
                    "Lobby host mismatch: expected " + hostName + "; actual " +
                    room.HostName + ".");
                return;
            }

            if (!room.HasSameVersion())
            {
                LogAndDestroy("The host and client Broforce network versions do not match.");
                return;
            }
            if (!room.HasSpace)
            {
                LogAndDestroy("The target Steam lobby is full.");
                return;
            }

            var ready = Steamworks.SteamMatchmaking.GetLobbyData(
                steamLobbyId,
                WorkshopReadyKey).Trim();
            var phase = Steamworks.SteamMatchmaking.GetLobbyData(
                steamLobbyId,
                WorkshopPhaseKey).Trim();

            Connect.CurrentMultiplayerMode = Utility.MultiplayerPlayMode.Online;
            ConnectionLayer.queryCancelled = true;
            SteamLayer.Instance.JoinLobby(room, -1, null, null);
            joinRequested = true;
            ScriptContext.Logger(
                "Requested native Steam lobby join: lobby=" + lobbyId +
                "; host=" + room.HostName +
                "; game=" + room.GameName +
                "; workshopReady=" + ready +
                "; workshopPhase=" + phase + ".");
        }

        private void LogAndDestroy(string message)
        {
            ScriptContext.Logger(message);
            UnityEngine.Object.Destroy(gameObject);
        }
    }
}
