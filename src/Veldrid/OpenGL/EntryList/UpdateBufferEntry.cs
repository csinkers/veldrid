namespace Veldrid.OpenGL.EntryList;

internal struct UpdateBufferEntry(
    Tracked<DeviceBuffer> buffer,
    uint bufferOffsetInBytes,
    StagingBlock stagingBlock,
    uint stagingBlockSize
)
{
    public readonly Tracked<DeviceBuffer> Buffer = buffer;
    public readonly uint BufferOffsetInBytes = bufferOffsetInBytes;
    public readonly StagingBlock StagingBlock = stagingBlock;
    public readonly uint StagingBlockSize = stagingBlockSize;
}
