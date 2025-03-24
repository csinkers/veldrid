using System;
using static Veldrid.MetalBindings.ObjectiveCRuntime;

namespace Veldrid.MetalBindings;

internal readonly struct MTLComputePipelineDescriptor(IntPtr nativePtr)
{
    public readonly IntPtr NativePtr = nativePtr;

    public MTLFunction computeFunction
    {
        get => new(IntPtr_objc_msgSend(NativePtr, sel_computeFunction));
        set => objc_msgSend(NativePtr, sel_setComputeFunction, value.NativePtr);
    }

    public MTLPipelineBufferDescriptorArray buffers =>
        new(IntPtr_objc_msgSend(NativePtr, sel_buffers));

    static readonly Selector sel_computeFunction = "computeFunction"u8;
    static readonly Selector sel_setComputeFunction = "setComputeFunction:"u8;
    static readonly Selector sel_buffers = "buffers"u8;
}
