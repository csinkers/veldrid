using System;

namespace Veldrid.MetalBindings;

public struct MTLOrigin(uint x, uint y, uint z)
{
    public UIntPtr x = (UIntPtr)x;
    public UIntPtr y = (UIntPtr)y;
    public UIntPtr z = (UIntPtr)z;
}