using System;
using static Veldrid.MetalBindings.ObjectiveCRuntime;

namespace Veldrid.MetalBindings;

public readonly struct NSWindow(IntPtr ptr)
{
    public readonly IntPtr NativePtr = ptr;

    public NSView contentView => new(IntPtr_objc_msgSend(NativePtr, "contentView"u8));
}
