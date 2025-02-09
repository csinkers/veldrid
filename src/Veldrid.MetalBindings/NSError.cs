using System;
using static Veldrid.MetalBindings.ObjectiveCRuntime;

namespace Veldrid.MetalBindings;

public readonly struct NSError(IntPtr nativePtr)
{
    public readonly IntPtr NativePtr = nativePtr;

    public string domain => string_objc_msgSend(NativePtr, "domain"u8);
    public string localizedDescription => string_objc_msgSend(NativePtr, "localizedDescription"u8);
}
