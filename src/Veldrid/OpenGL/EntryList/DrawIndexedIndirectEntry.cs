namespace Veldrid.OpenGL.EntryList;

internal struct DrawIndexedIndirectEntry(
    Tracked<DeviceBuffer> indirectBuffer,
    uint offset,
    uint drawCount,
    uint stride
)
{
    public Tracked<DeviceBuffer> IndirectBuffer = indirectBuffer;
    public uint Offset = offset;
    public uint DrawCount = drawCount;
    public uint Stride = stride;
}
