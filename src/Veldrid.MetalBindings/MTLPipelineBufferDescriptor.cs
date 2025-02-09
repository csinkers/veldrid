using System;
using static Veldrid.MetalBindings.ObjectiveCRuntime;

namespace Veldrid.MetalBindings;

public readonly struct MTLPipelineBufferDescriptor(IntPtr ptr)
{
    public readonly IntPtr NativePtr = ptr;

    public MTLMutability mutability
    {
        get => (MTLMutability)uint_objc_msgSend(NativePtr, sel_mutability);
        set => objc_msgSend(NativePtr, sel_setMutability, (uint)value);
    }

    static readonly Selector sel_mutability = "mutability"u8;
    static readonly Selector sel_setMutability = "setMutability:"u8;
}