using System;

namespace Veldrid.NeoDemo;

public struct RenderItemIndex(RenderOrderKey key, int itemIndex) : IComparable<RenderItemIndex>
{
    public RenderOrderKey Key { get; } = key;
    public int ItemIndex { get; } = itemIndex;

    public int CompareTo(RenderItemIndex other)
    {
        return Key.CompareTo(other.Key);
    }

    public override string ToString()
    {
        return string.Format("Index:{0}, Key:{1}", ItemIndex, Key);
    }
}