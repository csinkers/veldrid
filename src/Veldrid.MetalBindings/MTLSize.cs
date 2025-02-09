using System;
using System.Runtime.InteropServices;

namespace Veldrid.MetalBindings;

[StructLayout(LayoutKind.Sequential)]
public struct MTLSize(uint width, uint height, uint depth)
{
    public UIntPtr Width = (UIntPtr)width;
    public UIntPtr Height = (UIntPtr)height;
    public UIntPtr Depth = (UIntPtr)depth;
}