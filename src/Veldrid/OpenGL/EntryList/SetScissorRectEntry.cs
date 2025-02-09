namespace Veldrid.OpenGL.EntryList;

internal struct SetScissorRectEntry(uint index, uint x, uint y, uint width, uint height)
{
    public readonly uint Index = index;
    public readonly uint X = x;
    public readonly uint Y = y;
    public readonly uint Width = width;
    public readonly uint Height = height;
}