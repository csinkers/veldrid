namespace Veldrid.OpenGL.EntryList;

internal struct CopyBufferEntry(
    Tracked<DeviceBuffer> source,
    uint sourceOffset,
    Tracked<DeviceBuffer> destination,
    uint destinationOffset,
    uint sizeInBytes
)
{
    public readonly Tracked<DeviceBuffer> Source = source;
    public readonly uint SourceOffset = sourceOffset;
    public readonly Tracked<DeviceBuffer> Destination = destination;
    public readonly uint DestinationOffset = destinationOffset;
    public readonly uint SizeInBytes = sizeInBytes;
}
