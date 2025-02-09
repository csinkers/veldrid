namespace Veldrid.OpenGL;

internal sealed class OpenGLResourceLayout(in ResourceLayoutDescription description)
    : ResourceLayout(description)
{
    bool _disposed;

    public ResourceLayoutElementDescription[] Elements { get; } =
        Util.ShallowClone(description.Elements);

    public override string? Name { get; set; }

    public override bool IsDisposed => _disposed;

    public bool IsDynamicBuffer(uint slot)
    {
        return (Elements[slot].Options & ResourceLayoutElementOptions.DynamicBinding) != 0;
    }

    public override void Dispose()
    {
        _disposed = true;
    }
}
