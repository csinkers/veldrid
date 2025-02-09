namespace Veldrid.OpenGL.EntryList;

internal struct ClearDepthTargetEntry(float depth, byte stencil)
{
    public readonly float Depth = depth;
    public readonly byte Stencil = stencil;
}
