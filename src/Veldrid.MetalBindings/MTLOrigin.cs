using System;

namespace Veldrid.MetalBindings;

public struct MTLOrigin(uint x, uint y, uint z)
{
    public UIntPtr x = x;
    public UIntPtr y = y;
    public UIntPtr z = z;
}
