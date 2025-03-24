using System;
using System.Runtime.InteropServices;

namespace Veldrid.MetalBindings;

[StructLayout(LayoutKind.Sequential)]
internal struct MTLSize(uint width, uint height, uint depth)
{
    public UIntPtr Width = width;
    public UIntPtr Height = height;
    public UIntPtr Depth = depth;
}
