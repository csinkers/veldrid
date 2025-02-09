namespace Veldrid.OpenGL.EntryList;

internal struct SetFramebufferEntry(Tracked<Framebuffer> fb)
{
    public readonly Tracked<Framebuffer> Framebuffer = fb;
}
