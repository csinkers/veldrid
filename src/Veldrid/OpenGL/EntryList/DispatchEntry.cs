namespace Veldrid.OpenGL.EntryList;

internal struct DispatchEntry(uint groupCountX, uint groupCountY, uint groupCountZ)
{
    public readonly uint GroupCountX = groupCountX;
    public readonly uint GroupCountY = groupCountY;
    public readonly uint GroupCountZ = groupCountZ;
}
