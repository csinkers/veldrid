namespace Veldrid.OpenGL.EntryList;

internal struct PushDebugGroupEntry(Tracked<string> name)
{
    public Tracked<string> Name = name;
}