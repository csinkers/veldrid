using System;
using static Veldrid.MetalBindings.ObjectiveCRuntime;

namespace Veldrid.MetalBindings;

internal readonly struct NSArray(IntPtr ptr)
{
    public readonly IntPtr NativePtr = ptr;

    public UIntPtr count => UIntPtr_objc_msgSend(NativePtr, sel_count);

    static readonly Selector sel_count = "count"u8;
}
