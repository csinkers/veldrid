using System;
using static Veldrid.MetalBindings.ObjectiveCRuntime;

namespace Veldrid.MetalBindings;

internal readonly struct CALayer(IntPtr ptr)
{
    public readonly IntPtr NativePtr = ptr;

    public static implicit operator IntPtr(CALayer c) => c.NativePtr;

    public void addSublayer(IntPtr layer)
    {
        objc_msgSend(NativePtr, "addSublayer:"u8, layer);
    }
}
