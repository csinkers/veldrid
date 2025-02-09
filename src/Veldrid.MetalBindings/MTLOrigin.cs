using System;

namespace Veldrid.MetalBindings;

public struct MTLOrigin(uint x, uint y, uint z)
{
    public UIntPtr x = this.x;
    public UIntPtr y = this.y;
    public UIntPtr z = this.z;
}
