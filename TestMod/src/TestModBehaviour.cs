using UnityEngine;

namespace TestMod
{
    internal sealed class TestModBehaviour : MonoBehaviour
    {
        private const float AutoKillRetryInterval = 0.1f;

        private string _sceneName;
        private bool _autoKillDone;
        private float _nextAutoKillAt;

        internal int LastKillCount { get; private set; }

        internal static TestModBehaviour Create()
        {
            var gameObject = new GameObject("TestMod");
            DontDestroyOnLoad(gameObject);
            return gameObject.AddComponent<TestModBehaviour>();
        }

        internal void Stop()
        {
            if (gameObject != null)
            {
                Destroy(gameObject);
            }
        }

        internal void OnAutoKillSettingChanged()
        {
            _autoKillDone = false;
            _nextAutoKillAt = 0f;
        }

        private void Update()
        {
            var sceneName = Application.loadedLevelName;
            if (sceneName != _sceneName)
            {
                _sceneName = sceneName;
                _autoKillDone = false;
                _nextAutoKillAt = 0f;
                LastKillCount = 0;
            }

            if (!Plugin.AutoKillEnabled || _autoKillDone ||
                Time.realtimeSinceStartup < _nextAutoKillAt)
            {
                return;
            }

            var mooks = FindObjectsOfType<Mook>();
            if (mooks.Length == 0)
            {
                _nextAutoKillAt = Time.realtimeSinceStartup + AutoKillRetryInterval;
                return;
            }

            var killedCount = KillVisibleEnemies(mooks);
            _nextAutoKillAt = Time.realtimeSinceStartup + AutoKillRetryInterval;
            if (killedCount > 0)
            {
                _autoKillDone = true;
                Plugin.Log("Automatic cleanup killed " + killedCount +
                    " Mooks in scene " + sceneName + ".");
            }
        }

        internal int KillVisibleEnemies()
        {
            if (!Plugin.AutoKillEnabled)
            {
                LastKillCount = 0;
                return 0;
            }

            return KillVisibleEnemies(FindObjectsOfType<Mook>());
        }

        private int KillVisibleEnemies(Mook[] mooks)
        {
            var killedCount = 0;

            for (var index = 0; index < mooks.Length; index++)
            {
                var mook = mooks[index];
                if (mook == null || mook.destroyed || mook.IsHero || !mook.IsMine || mook.health <= 0)
                {
                    continue;
                }

                if (!SortOfFollow.IsItSortOfVisible(mook.X, mook.Y, 0f, 0f))
                {
                    continue;
                }

                mook.Death();
                killedCount++;
            }

            LastKillCount = killedCount;
            Plugin.Log("Killed " + killedCount + " visible owned Mooks.");
            return killedCount;
        }
    }
}
