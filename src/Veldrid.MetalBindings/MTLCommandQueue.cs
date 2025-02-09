using System;
using System.Runtime.InteropServices;
using static Veldrid.MetalBindings.ObjectiveCRuntime;

namespace Veldrid.MetalBindings;

[StructLayout(LayoutKind.Sequential)]
public readonly struct MTLCommandQueue(IntPtr nativePtr)
{
    public readonly IntPtr NativePtr = nativePtr;

    public MTLCommandBuffer commandBuffer() =>
        new(IntPtr_objc_msgSend(NativePtr, sel_commandBuffer));

    public void insertDebugCaptureBoundary() =>
        objc_msgSend(NativePtr, sel_insertDebugCaptureBoundary);

    static readonly Selector sel_commandBuffer = "commandBuffer"u8;
    static readonly Selector sel_insertDebugCaptureBoundary = "insertDebugCaptureBoundary"u8;
}
