namespace Veldrid.OpenGL;

internal sealed class OpenGLResourceSet(in ResourceSetDescription description)
    : ResourceSet(description)
{
    bool _disposed;

    public new OpenGLResourceLayout Layout { get; } =
        Util.AssertSubtype<ResourceLayout, OpenGLResourceLayout>(description.Layout);
    public new BindableResource[] Resources { get; } =
        Util.ShallowClone(description.BoundResources);
    public override string? Name { get; set; }
    public override bool IsDisposed => _disposed;

    public override void Dispose() => _disposed = true;
}
