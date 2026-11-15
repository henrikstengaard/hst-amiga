namespace Hst.Amiga
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Runs async operations synchronously for synchronous APIs, e.g. Stream.Read and Dispose, that have to wait
    /// for async file system operations.
    /// </summary>
    /// <remarks>
    /// Blocking on a task while a synchronization context or non-default task scheduler is active, e.g. on a UI
    /// thread, can deadlock when awaits inside the task try to resume on the blocked thread. In that case the
    /// operation runs on the thread pool, where no synchronization context is captured. Otherwise the task is
    /// awaited directly to avoid the overhead of switching threads.
    /// </remarks>
    internal static class AsyncHelper
    {
        public static void RunSync(Func<Task> operation)
        {
            if (RequiresThreadPool())
            {
                Task.Run(operation).GetAwaiter().GetResult();
                return;
            }

            operation().GetAwaiter().GetResult();
        }

        public static T RunSync<T>(Func<Task<T>> operation)
        {
            return RequiresThreadPool()
                ? Task.Run(operation).GetAwaiter().GetResult()
                : operation().GetAwaiter().GetResult();
        }

        private static bool RequiresThreadPool() =>
            SynchronizationContext.Current != null || TaskScheduler.Current != TaskScheduler.Default;
    }
}
