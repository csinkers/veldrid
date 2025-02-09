namespace Veldrid.OpenGL.EntryList;

internal struct GenerateMipmapsEntry(Tracked<Texture> texture)
{
    public readonly Tracked<Texture> Texture = texture;
}
