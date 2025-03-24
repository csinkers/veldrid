using System;

namespace Veldrid.MetalBindings;

internal struct MTLSamplerState(IntPtr nativePtr)
{
    public readonly IntPtr NativePtr = nativePtr;
}
