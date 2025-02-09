namespace Veldrid.OpenGL.EntryList;

internal struct DrawEntry(uint vertexCount, uint instanceCount, uint vertexStart, uint instanceStart)
{
    public uint VertexCount = vertexCount;
    public uint InstanceCount = instanceCount;
    public uint VertexStart = vertexStart;
    public uint InstanceStart = instanceStart;
}