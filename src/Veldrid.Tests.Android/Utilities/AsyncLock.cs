using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Veldrid.Tests.Android.Utilities;

internal class AsyncLock
{
    readonly SemaphoreSlim semaphore;
    readonly Task<Releaser> releaser;

    public AsyncLock()
    {
        semaphore = new SemaphoreSlim(1);
        releaser = Task.FromResult(new Releaser(this));
    }

    public readonly struct Releaser : IDisposable
    {
        readonly AsyncLock toRelease;

        internal Releaser(AsyncLock toRelease)
        {
            this.toRelease = toRelease;
        }

        public void Dispose()
        {
            toRelease?.semaphore.Release();
        }
    }

    public Task<Releaser> LockAsync(
        [CallerMemberName] string? callingMethod = null,
        [CallerFilePath] string? path = null,
        [CallerLineNumber] int line = 0
    )
    {
#if DEBUG
        Debug.WriteLine($"AsyncLock.LockAsync called by: {callingMethod} in file: {path} : {line}");
#endif

        Task wait = semaphore.WaitAsync();
        if (wait.IsCompleted)
        {
            return releaser;
        }

        return wait.ContinueWith(
            (_, state) => new Releaser((AsyncLock)state!),
            this,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default
        );
    }
}
