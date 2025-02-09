using System;

namespace Veldrid.MetalBindings;

public struct MTLScissorRect(uint x, uint y, uint width, uint height)
{
    public UIntPtr x = x;
    public UIntPtr y = y;
    public UIntPtr width = width;
    public UIntPtr height = height;
}
