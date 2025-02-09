using System;

namespace Veldrid.MetalBindings;

public struct MTLSamplerState(IntPtr nativePtr)
{
    public readonly IntPtr NativePtr = nativePtr;
}
