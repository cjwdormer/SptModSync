using System;
using System.Collections.Concurrent;

namespace TCFModSync.Client.UI
{
    public static class MainThreadDispatcher
    {
        private static readonly ConcurrentQueue<Action> Pending = new ConcurrentQueue<Action>();

        public static void Enqueue(Action action)
        {
            if (action != null) Pending.Enqueue(action);
        }

        public static void Drain(Action<string>? logError = null)
        {
            while (Pending.TryDequeue(out var action))
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    logError?.Invoke($"[TCF-ModSync] Main-thread callback failed: {ex}");
                }
            }
        }
    }
}
