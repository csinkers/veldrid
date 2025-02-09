namespace Veldrid.OpenGL.EntryList;

internal struct DrawIndexedIndirectEntry(
    Tracked<DeviceBuffer> indirectBuffer,
    uint offset,
    uint drawCount,
    uint stride
)
{
    public Tracked<DeviceBuffer> IndirectBuffer = indirectBuffer;
    public readonly uint Offset = offset;
    public readonly uint DrawCount = drawCount;
    public readonly uint Stride = stride;
}
