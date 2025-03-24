using System;

namespace Veldrid.MetalBindings;

internal struct MTLRenderPipelineState(IntPtr ptr)
{
    public readonly IntPtr NativePtr = ptr;
}
