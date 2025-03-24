using System;

namespace Veldrid.MetalBindings;

internal struct MTLDepthStencilState(IntPtr nativePtr)
{
    public readonly IntPtr NativePtr = nativePtr;
}
