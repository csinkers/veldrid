using System;

namespace Veldrid.MetalBindings;

public struct MTLComputePipelineState(IntPtr ptr)
{
    public readonly IntPtr NativePtr = ptr;
    public bool IsNull => NativePtr == IntPtr.Zero;
}