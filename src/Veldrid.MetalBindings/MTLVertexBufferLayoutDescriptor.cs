using System;
using static Veldrid.MetalBindings.ObjectiveCRuntime;

namespace Veldrid.MetalBindings;

public readonly struct MTLVertexBufferLayoutDescriptor(IntPtr ptr)
{
    public readonly IntPtr NativePtr = ptr;

    public MTLVertexStepFunction stepFunction
    {
        get => (MTLVertexStepFunction)uint_objc_msgSend(NativePtr, sel_stepFunction);
        set => objc_msgSend(NativePtr, sel_setStepFunction, (uint)value);
    }

    public UIntPtr stride
    {
        get => UIntPtr_objc_msgSend(NativePtr, sel_stride);
        set => objc_msgSend(NativePtr, sel_setStride, value);
    }

    public UIntPtr stepRate
    {
        get => UIntPtr_objc_msgSend(NativePtr, sel_stepRate);
        set => objc_msgSend(NativePtr, sel_setStepRate, value);
    }

    static readonly Selector sel_stepFunction = "stepFunction"u8;
    static readonly Selector sel_setStepFunction = "setStepFunction:"u8;
    static readonly Selector sel_stride = "stride"u8;
    static readonly Selector sel_setStride = "setStride:"u8;
    static readonly Selector sel_stepRate = "stepRate"u8;
    static readonly Selector sel_setStepRate = "setStepRate:"u8;
}