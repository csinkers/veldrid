using System;

namespace Veldrid.MetalBindings;

internal readonly struct NSDictionary(IntPtr nativePtr)
{
    public readonly IntPtr NativePtr = nativePtr;

    public UIntPtr count => ObjectiveCRuntime.UIntPtr_objc_msgSend(NativePtr, "count"u8);
}
