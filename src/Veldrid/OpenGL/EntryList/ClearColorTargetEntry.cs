namespace Veldrid.OpenGL.EntryList;

internal struct ClearColorTargetEntry(uint index, RgbaFloat clearColor)
{
    public readonly uint Index = index;
    public readonly RgbaFloat ClearColor = clearColor;
}