using System;

namespace Veldrid;

internal struct BoundResourceSetInfo(ResourceSet set, ReadOnlySpan<uint> offsets)
{
    public readonly ResourceSet Set = set;
    public SmallFixedOrDynamicArray Offsets = new(offsets);

    public bool Equals(ResourceSet set, ReadOnlySpan<uint> offsets)
    {
        if (set != Set || offsets.Length != Offsets.Count)
            return false;

        for (uint i = 0; i < offsets.Length; i++)
            if (offsets[(int)i] != Offsets.Get(i))
                return false;

        return true;
    }
}
