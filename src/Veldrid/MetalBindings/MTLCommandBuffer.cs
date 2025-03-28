using System;
using System.Runtime.InteropServices;
using static Veldrid.MetalBindings.ObjectiveCRuntime;

namespace Veldrid.MetalBindings;

[StructLayout(LayoutKind.Sequential)]
internal readonly struct MTLCommandBuffer(IntPtr nativePtr) : IEquatable<MTLCommandBuffer>
{
    public readonly IntPtr NativePtr = nativePtr;

    public MTLRenderCommandEncoder renderCommandEncoderWithDescriptor(MTLRenderPassDescriptor desc)
    {
        return new(
            IntPtr_objc_msgSend(NativePtr, sel_renderCommandEncoderWithDescriptor, desc.NativePtr)
        );
    }

    public void presentDrawable(IntPtr drawable) =>
        objc_msgSend(NativePtr, sel_presentDrawable, drawable);

    public void commit() => objc_msgSend(NativePtr, sel_commit);

    public MTLBlitCommandEncoder blitCommandEncoder() =>
        new(IntPtr_objc_msgSend(NativePtr, sel_blitCommandEncoder));

    public MTLComputeCommandEncoder computeCommandEncoder() =>
        new(IntPtr_objc_msgSend(NativePtr, sel_computeCommandEncoder));

    public void waitUntilCompleted() => objc_msgSend(NativePtr, sel_waitUntilCompleted);

    public unsafe void addCompletedHandler(
        delegate* unmanaged[Cdecl]<IntPtr, MTLCommandBuffer, void> block
    ) => objc_msgSend(NativePtr, sel_addCompletedHandler, (IntPtr)block);

    public void addCompletedHandler(IntPtr block) =>
        objc_msgSend(NativePtr, sel_addCompletedHandler, block);

    public MTLCommandBufferStatus status =>
        (MTLCommandBufferStatus)uint_objc_msgSend(NativePtr, sel_status);

    static readonly Selector sel_renderCommandEncoderWithDescriptor =
        "renderCommandEncoderWithDescriptor:"u8;
    static readonly Selector sel_presentDrawable = "presentDrawable:"u8;
    static readonly Selector sel_commit = "commit"u8;
    static readonly Selector sel_blitCommandEncoder = "blitCommandEncoder"u8;
    static readonly Selector sel_computeCommandEncoder = "computeCommandEncoder"u8;
    static readonly Selector sel_waitUntilCompleted = "waitUntilCompleted"u8;
    static readonly Selector sel_addCompletedHandler = "addCompletedHandler:"u8;
    static readonly Selector sel_status = "status"u8;

    public bool Equals(MTLCommandBuffer other) => NativePtr == other.NativePtr;

    public override bool Equals(object? obj) => obj is MTLCommandBuffer other && Equals(other);

    public override int GetHashCode() => NativePtr.GetHashCode();
}
