using System;

namespace Veldrid.MetalBindings;

public struct MTLRenderPipelineState(IntPtr ptr)
{
    public readonly IntPtr NativePtr = ptr;
}
