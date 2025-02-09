using System;
using System.Runtime.InteropServices;
using static Veldrid.MetalBindings.ObjectiveCRuntime;

namespace Veldrid.MetalBindings;

[StructLayout(LayoutKind.Sequential)]
public readonly struct MTLCommandQueue
{
    public readonly IntPtr NativePtr;

    public MTLCommandBuffer commandBuffer() => objc_msgSend<MTLCommandBuffer>(NativePtr, sel_commandBuffer);

    public void insertDebugCaptureBoundary() => objc_msgSend(NativePtr, sel_insertDebugCaptureBoundary);

    static readonly Selector sel_commandBuffer = "commandBuffer"u8;
    static readonly Selector sel_insertDebugCaptureBoundary = "insertDebugCaptureBoundary"u8;
}