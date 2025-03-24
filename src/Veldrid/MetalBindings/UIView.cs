using System;
using static Veldrid.MetalBindings.ObjectiveCRuntime;

namespace Veldrid.MetalBindings;

internal readonly struct UIView(IntPtr ptr)
{
    public readonly IntPtr NativePtr = ptr;

    public CALayer layer => new(IntPtr_objc_msgSend(NativePtr, "layer"u8));

    public CGRect frame => CGRect_objc_msgSend(NativePtr, "frame"u8);
}
