namespace Veldrid.D3D11;

internal sealed class D3D11ResourceSet(in ResourceSetDescription description)
    : ResourceSet(description)
{
    bool _disposed;

    public new BindableResource[] Resources { get; } =
        Util.ShallowClone(description.BoundResources);
    public new D3D11ResourceLayout Layout { get; } =
        Util.AssertSubtype<ResourceLayout, D3D11ResourceLayout>(description.Layout);

    public override string? Name { get; set; }
    public override bool IsDisposed => _disposed;

    public override void Dispose() => _disposed = true;
}
