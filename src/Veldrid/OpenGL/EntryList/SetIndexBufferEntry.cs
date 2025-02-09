namespace Veldrid.OpenGL.EntryList;

internal struct SetIndexBufferEntry(Tracked<DeviceBuffer> ib, IndexFormat format, uint offset)
{
    public readonly Tracked<DeviceBuffer> Buffer = ib;
    public IndexFormat Format = format;
    public uint Offset = offset;
}
