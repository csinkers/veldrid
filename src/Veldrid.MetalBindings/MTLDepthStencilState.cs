using System;

namespace Veldrid.MetalBindings;

public struct MTLDepthStencilState(IntPtr nativePtr)
{
    public readonly IntPtr NativePtr = nativePtr;
}
