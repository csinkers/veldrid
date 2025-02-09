namespace Veldrid.OpenGL.EntryList;

internal struct DrawIndexedEntry(
    uint indexCount,
    uint instanceCount,
    uint indexStart,
    int vertexOffset,
    uint instanceStart)
{
    public readonly uint IndexCount = indexCount;
    public readonly uint InstanceCount = instanceCount;
    public readonly uint IndexStart = indexStart;
    public readonly int VertexOffset = vertexOffset;
    public readonly uint InstanceStart = instanceStart;
}