namespace Veldrid.OpenGL.EntryList;

internal struct InsertDebugMarkerEntry(Tracked<string> name)
{
    public Tracked<string> Name = name;
}
