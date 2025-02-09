namespace Veldrid.OpenGL.EntryList;

internal struct DispatchIndirectEntry(Tracked<DeviceBuffer> indirectBuffer, uint offset)
{
    public Tracked<DeviceBuffer> IndirectBuffer = indirectBuffer;
    public uint Offset = offset;
}
