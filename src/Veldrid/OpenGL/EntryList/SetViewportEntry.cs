namespace Veldrid.OpenGL.EntryList;

internal struct SetViewportEntry(uint index, in Viewport viewport)
{
    public readonly uint Index = index;
    public Viewport Viewport = viewport;
}