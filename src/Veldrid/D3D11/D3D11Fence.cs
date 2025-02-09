using System;
using System.Threading;

namespace Veldrid.D3D11;

internal sealed class D3D11Fence(bool signaled) : Fence
{
    readonly ManualResetEvent _mre = new(signaled);
    bool _disposed;

    public override string? Name { get; set; }
    public ManualResetEvent ResetEvent => _mre;

    public void Set() => _mre.Set();

    public override void Reset() => _mre.Reset();

    public override bool Signaled => _mre.WaitOne(0);
    public override bool IsDisposed => _disposed;

    public override void Dispose()
    {
        if (!_disposed)
        {
            _mre.Dispose();
            _disposed = true;
        }
    }

    internal bool Wait(ulong nanosecondTimeout)
    {
        ulong timeout = Math.Min(int.MaxValue, nanosecondTimeout / 1_000_000);
        return _mre.WaitOne((int)timeout);
    }
}
