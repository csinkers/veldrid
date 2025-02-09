namespace Veldrid.OpenGL.EntryList;

internal struct SetVertexBufferEntry(uint index, Tracked<DeviceBuffer> buffer, uint offset)
{
    public readonly uint Index = index;
    public readonly Tracked<DeviceBuffer> Buffer = buffer;
    public uint Offset = offset;
}