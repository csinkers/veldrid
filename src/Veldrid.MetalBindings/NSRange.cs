using System;

namespace Veldrid.MetalBindings;

public struct NSRange(UIntPtr location, UIntPtr length)
{
    public UIntPtr location = location;
    public UIntPtr length = length;

    public NSRange(uint location, uint length)
        : this(location, (UIntPtr)length) { }
}
