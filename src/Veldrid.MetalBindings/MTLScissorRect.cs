using System;

namespace Veldrid.MetalBindings;

public struct MTLScissorRect(uint x, uint y, uint width, uint height)
{
    public UIntPtr x = this.x;
    public UIntPtr y = this.y;
    public UIntPtr width = this.width;
    public UIntPtr height = this.height;
}
