using static Veldrid.MetalBindings.ObjectiveCRuntime;
using System;

namespace Veldrid.MetalBindings;

public readonly struct MTLFunction(IntPtr ptr)
{
    public readonly IntPtr NativePtr = ptr;

    public NSDictionary functionConstantsDictionary => objc_msgSend<NSDictionary>(NativePtr, sel_functionConstantsDictionary);

    static readonly Selector sel_functionConstantsDictionary = "functionConstantsDictionary"u8;
}