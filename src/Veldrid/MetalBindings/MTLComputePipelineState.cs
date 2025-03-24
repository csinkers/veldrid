using System;

namespace Veldrid.MetalBindings;

internal struct MTLComputePipelineState(IntPtr ptr)
{
    public readonly IntPtr NativePtr = ptr;

    public bool IsNull => NativePtr == IntPtr.Zero;
}
