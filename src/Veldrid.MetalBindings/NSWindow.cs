using System;
using static Veldrid.MetalBindings.ObjectiveCRuntime;

namespace Veldrid.MetalBindings;

public readonly struct NSWindow(IntPtr ptr)
{
    public readonly IntPtr NativePtr = ptr;

    public NSView contentView => objc_msgSend<NSView>(NativePtr, "contentView"u8);
}