using System;

namespace Veldrid.MetalBindings;

#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
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
#pragma warning restore CS0649 // Field is never assigned to, and will always have its default value
