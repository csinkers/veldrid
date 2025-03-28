using System;
using static Veldrid.MetalBindings.ObjectiveCRuntime;

namespace Veldrid.MetalBindings;

internal readonly struct NSObject(IntPtr ptr)
{
    public readonly IntPtr NativePtr = ptr;

    public Bool8 IsKindOfClass(IntPtr @class) =>
        bool8_objc_msgSend(NativePtr, sel_isKindOfClass, @class);

    static readonly Selector sel_isKindOfClass = "isKindOfClass:"u8;
}
