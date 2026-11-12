using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using UnityEngine;

namespace Unity_Inspector_Mod
{
    public class MainThreadDispatcher : MonoBehaviour
    {
        private sealed class WorkItem
        {
            public Action Action;
            public Func<bool> Cancelled;
            public readonly object Gate = new object();
            public readonly Stopwatch Age = Stopwatch.StartNew();
            public int Timeout;
            public int State; // 0 queued, 1 executing, 2 completed, 3 cancelled
            public Exception Error;
            public bool Waited;
        }

        private static MainThreadDispatcher instance;
        private static readonly object queueGate = new object();
        private static readonly Queue<WorkItem> executionQueue = new Queue<WorkItem>();
        private static int mainThreadId;
        private static bool accepting;
        [ThreadStatic] public static Func<bool> RequestCancelled;
        public static bool IsMainThread { get { return Thread.CurrentThread.ManagedThreadId == mainThreadId; } }
        public static MainThreadDispatcher Instance { get { return instance; } }

        // Only Load/OnToggle may create the Unity object, never a TCP thread.
        public static void Initialize()
        {
            mainThreadId = Thread.CurrentThread.ManagedThreadId;
            if (instance == null)
            {
                var go = new GameObject("MainThreadDispatcher");
                instance = go.AddComponent<MainThreadDispatcher>();
                DontDestroyOnLoad(go);
            }
            lock (queueGate) accepting = true;
        }

        public void Update()
        {
            var budget = Stopwatch.StartNew();
            for (int count = 0; count < 32 && budget.ElapsedMilliseconds < 4; count++)
            {
                WorkItem item;
                lock (queueGate)
                {
                    if (!accepting || executionQueue.Count == 0) break;
                    item = executionQueue.Dequeue();
                }
                lock (item.Gate)
                {
                    if (item.State != 0) continue;
                    if (IsCancelled(item) || item.Age.ElapsedMilliseconds >= item.Timeout)
                    {
                        Cancel(item, new TimeoutException("Main thread task cancelled before execution"));
                        continue;
                    }
                    item.State = 1;
                }
                Exception error = null;
                try { item.Action(); }
                catch (Exception ex) { error = ex; }
                lock (item.Gate)
                {
                    item.Error = error;
                    item.State = 2;
                    Monitor.PulseAll(item.Gate);
                }
                if (error != null && !item.Waited) Main.Log("[MainThreadDispatcher] " + error);
            }
        }

        private static bool IsCancelled(WorkItem item)
        {
            return item.Cancelled != null && item.Cancelled();
        }

        private static void Cancel(WorkItem item, Exception error)
        {
            item.Error = error;
            item.State = 3;
            Monitor.PulseAll(item.Gate);
        }

        private static WorkItem Queue(Action action, int timeout, bool waited)
        {
            var item = new WorkItem { Action = action, Timeout = timeout, Waited = waited, Cancelled = RequestCancelled };
            lock (queueGate)
            {
                if (!accepting) throw new OperationCanceledException("Unity Inspector is disabled");
                if (executionQueue.Count >= 256) throw new InvalidOperationException("Main thread queue is full");
                executionQueue.Enqueue(item);
            }
            return item;
        }

        public static void Enqueue(Action action)
        {
            if (action != null) Queue(action, 5000, false);
        }

        public static void EnqueueAndWait(Action action, int timeoutMs = 5000)
        {
            if (action == null) return;
            if (IsMainThread) { action(); return; }
            var item = Queue(action, timeoutMs, true);
            lock (item.Gate)
            {
                while (item.State < 2)
                {
                    if (IsCancelled(item) && item.State == 0)
                    {
                        Cancel(item, new OperationCanceledException("Request disconnected before execution"));
                        break;
                    }
                    long remaining = timeoutMs - item.Age.ElapsedMilliseconds;
                    if (remaining <= 0)
                    {
                        if (item.State == 0)
                        {
                            Cancel(item, new TimeoutException("Main thread task timed out and was cancelled before execution"));
                            break;
                        }
                        throw new TimeoutException("Main thread task already started and is still running; check game state before retrying");
                    }
                    Monitor.Wait(item.Gate, (int)System.Math.Min(remaining, 25));
                }
                if (item.Error != null) throw item.Error;
            }
        }

        public static void StopAccepting()
        {
            WorkItem[] pending;
            lock (queueGate)
            {
                accepting = false;
                pending = executionQueue.ToArray();
                executionQueue.Clear();
            }
            foreach (var item in pending)
            {
                lock (item.Gate)
                    if (item.State == 0) Cancel(item, new OperationCanceledException("Unity Inspector disabled before execution"));
            }
        }

        public static void Shutdown()
        {
            StopAccepting();
            if (instance != null) Destroy(instance.gameObject);
            instance = null;
        }

        void OnDestroy()
        {
            if (instance == this) { StopAccepting(); instance = null; }
        }
    }
}
