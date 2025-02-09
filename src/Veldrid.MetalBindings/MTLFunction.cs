using System;
using static Veldrid.MetalBindings.ObjectiveCRuntime;

namespace Veldrid.MetalBindings;

public readonly struct MTLFunction(IntPtr ptr)
{
    public readonly IntPtr NativePtr = ptr;

    public NSDictionary functionConstantsDictionary =>
        new(IntPtr_objc_msgSend(NativePtr, sel_functionConstantsDictionary));

    static readonly Selector sel_functionConstantsDictionary = "functionConstantsDictionary"u8;
}
