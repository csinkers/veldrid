namespace Veldrid.OpenGL.EntryList;

internal struct CopyTextureEntry(
    Tracked<Texture> source,
    uint srcX,
    uint srcY,
    uint srcZ,
    uint srcMipLevel,
    uint srcBaseArrayLayer,
    Tracked<Texture> destination,
    uint dstX,
    uint dstY,
    uint dstZ,
    uint dstMipLevel,
    uint dstBaseArrayLayer,
    uint width,
    uint height,
    uint depth,
    uint layerCount
)
{
    public readonly Tracked<Texture> Source = source;
    public readonly uint SrcX = srcX;
    public readonly uint SrcY = srcY;
    public readonly uint SrcZ = srcZ;
    public readonly uint SrcMipLevel = srcMipLevel;
    public readonly uint SrcBaseArrayLayer = srcBaseArrayLayer;
    public readonly Tracked<Texture> Destination = destination;
    public readonly uint DstX = dstX;
    public readonly uint DstY = dstY;
    public readonly uint DstZ = dstZ;
    public readonly uint DstMipLevel = dstMipLevel;
    public readonly uint DstBaseArrayLayer = dstBaseArrayLayer;
    public readonly uint Width = width;
    public readonly uint Height = height;
    public readonly uint Depth = depth;
    public readonly uint LayerCount = layerCount;
}
