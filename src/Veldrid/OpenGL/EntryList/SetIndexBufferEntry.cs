namespace Veldrid.OpenGL.EntryList;

internal struct SetIndexBufferEntry(Tracked<DeviceBuffer> ib, IndexFormat format, uint offset)
{
    public readonly Tracked<DeviceBuffer> Buffer = ib;
    public readonly IndexFormat Format = format;
    public readonly uint Offset = offset;
}
