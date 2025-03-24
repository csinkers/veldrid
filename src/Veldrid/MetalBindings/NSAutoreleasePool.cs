using System;

namespace Veldrid.MetalBindings;

internal readonly struct NSAutoreleasePool(IntPtr ptr) : IDisposable
{
    static readonly ObjCClass s_class = new("NSAutoreleasePool"u8);

    public readonly IntPtr NativePtr = ptr;

    public static NSAutoreleasePool Begin()
    {
        return s_class.AllocInit<NSAutoreleasePool>();
    }

    public void Dispose()
    {
        ObjectiveCRuntime.release(NativePtr);
    }
}
