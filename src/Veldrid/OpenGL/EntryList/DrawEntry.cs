namespace Veldrid.OpenGL.EntryList;

internal struct DrawEntry(
    uint vertexCount,
    uint instanceCount,
    uint vertexStart,
    uint instanceStart
)
{
    public readonly uint VertexCount = vertexCount;
    public readonly uint InstanceCount = instanceCount;
    public readonly uint VertexStart = vertexStart;
    public readonly uint InstanceStart = instanceStart;
}
