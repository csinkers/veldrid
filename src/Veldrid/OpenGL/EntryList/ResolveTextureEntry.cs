namespace Veldrid.OpenGL.EntryList;

internal struct ResolveTextureEntry(Tracked<Texture> source, Tracked<Texture> destination)
{
    public readonly Tracked<Texture> Source = source;
    public readonly Tracked<Texture> Destination = destination;
}
