namespace Veldrid.OpenGL.EntryList;

internal struct DispatchEntry(uint groupCountX, uint groupCountY, uint groupCountZ)
{
    public uint GroupCountX = groupCountX;
    public uint GroupCountY = groupCountY;
    public uint GroupCountZ = groupCountZ;
}
