using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Unity_Inspector_Mod
{
    public static class InputSimulator
    {
        private static readonly object gate = new object();
        private static readonly Dictionary<string, bool> simulatedInputs = new Dictionary<string, bool>();
        private static readonly Dictionary<string, Timer> activeHolds = new Dictionary<string, Timer>();
        private static bool startButtonHandled;
        private static bool accepting;
        private static int generation;

        public static void Initialize()
        {
            lock (gate)
            {
                ClearLocked();
                generation++;
                accepting = true;
                foreach (string key in new[] { "up", "down", "left", "right", "fire", "jump", "special", "highfive", "gesture", "sprint", "start", "escape" })
                    simulatedInputs[key] = false;
            }
        }

        public static object SimulateInput(string action, int? duration = null, int? playerNum = null, int? count = null, int? interval = null)
        {
            try
            {
                string key = (action ?? "").ToLowerInvariant();
                int presses = count ?? 1, spacing = interval ?? 200, hold = duration ?? 50;
                if (hold <= 0) hold = 50;
                if (presses < 1 || presses > 1000 || spacing < 1 || spacing > 10000 || hold > 120000 ||
                    (long)(presses - 1) * spacing > 120000)
                    throw new ArgumentException("Input count/interval/duration is outside the supported range (maximum 120 seconds)");
                int current;
                var cancelled = MainThreadDispatcher.RequestCancelled;
                lock (gate)
                {
                    if (!simulatedInputs.ContainsKey(key)) throw new ArgumentException("Unknown input action: " + action);
                    current = generation;
                }
                for (int i = 0; i < presses; i++)
                {
                    lock (gate)
                    {
                        CheckActive(current, cancelled);
                        HoldLocked(key, presses > 1 ? 50 : hold);
                    }
                    if (i < presses - 1)
                    {
                        var elapsed = Stopwatch.StartNew();
                        while (elapsed.ElapsedMilliseconds < spacing)
                        {
                            lock (gate) CheckActive(current, cancelled);
                            long remaining = spacing - elapsed.ElapsedMilliseconds;
                            if (remaining > 0) Thread.Sleep((int)System.Math.Min(25, remaining));
                        }
                    }
                }
                return new { success = true, action = action, count = presses, interval = spacing, duration = hold, player = playerNum ?? 0 };
            }
            catch (Exception ex)
            {
                return new { success = false, error = ex.Message };
            }
        }

        private static void CheckActive(int current, Func<bool> cancelled)
        {
            if (!accepting || current != generation || (cancelled != null && cancelled()))
                throw new OperationCanceledException("Simulated input cancelled");
        }

        private static void HoldLocked(string key, int duration)
        {
            Timer previous;
            if (activeHolds.TryGetValue(key, out previous)) previous.Dispose();
            simulatedInputs[key] = true;
            Timer timer = null;
            timer = new Timer(_ =>
            {
                lock (gate)
                {
                    Timer active;
                    if (!activeHolds.TryGetValue(key, out active) || !ReferenceEquals(active, timer)) return;
                    activeHolds.Remove(key);
                    simulatedInputs[key] = false;
                    if (key == "start") startButtonHandled = false;
                    timer.Dispose();
                }
            }, null, Timeout.Infinite, Timeout.Infinite);
            activeHolds[key] = timer;
            timer.Change(duration, Timeout.Infinite);
        }

        public static bool IsInputSimulated(string key)
        {
            lock (gate)
            {
                bool value;
                return accepting && simulatedInputs.TryGetValue(key, out value) && value;
            }
        }

        public static bool HasAnySimulatedInput()
        {
            lock (gate)
            {
                if (!accepting) return false;
                foreach (var value in simulatedInputs.Values) if (value) return true;
                return false;
            }
        }

        public static bool ShouldTriggerStart()
        {
            lock (gate)
            {
                bool value;
                if (!accepting || startButtonHandled || !simulatedInputs.TryGetValue("start", out value) || !value) return false;
                startButtonHandled = true;
                return true;
            }
        }

        private static void ClearLocked()
        {
            foreach (var timer in activeHolds.Values) timer.Dispose();
            activeHolds.Clear();
            foreach (var key in new List<string>(simulatedInputs.Keys)) simulatedInputs[key] = false;
            startButtonHandled = false;
        }

        public static void ClearAllInputs() { lock (gate) ClearLocked(); }
        public static void Cleanup()
        {
            lock (gate) { accepting = false; generation++; ClearLocked(); }
        }
    }
}
