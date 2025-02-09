using System;
using static Veldrid.MetalBindings.ObjectiveCRuntime;

namespace Veldrid.MetalBindings;

public readonly struct UIView(IntPtr ptr)
{
    public readonly IntPtr NativePtr = ptr;

    public CALayer layer => objc_msgSend<CALayer>(NativePtr, "layer"u8);

    public CGRect frame => CGRect_objc_msgSend(NativePtr, "frame"u8);
}
