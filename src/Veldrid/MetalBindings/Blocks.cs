using System;

namespace Veldrid.MetalBindings;

internal unsafe struct BlockLiteral
{
    public IntPtr isa;
    public int flags;
    public int reserved;
    public IntPtr invoke;
    public BlockDescriptor* descriptor;
};

internal struct BlockDescriptor
{
    public ulong reserved;
    public ulong Block_size;
}
