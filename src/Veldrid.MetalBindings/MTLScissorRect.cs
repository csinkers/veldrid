using System;

namespace Veldrid.MetalBindings;

public struct MTLScissorRect(uint x, uint y, uint width, uint height)
{
    public UIntPtr x = (UIntPtr)x;
    public UIntPtr y = (UIntPtr)y;
    public UIntPtr width = (UIntPtr)width;
    public UIntPtr height = (UIntPtr)height;
}
