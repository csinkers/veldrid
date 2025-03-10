using Veldrid.MetalBindings;

namespace Veldrid.MTL;

internal abstract class MTLFramebufferBase : Framebuffer
{
    public abstract MTLRenderPassDescriptor CreateRenderPassDescriptor();

    public abstract bool IsRenderable { get; }

    public override string? Name { get; set; }

    public MTLFramebufferBase(in FramebufferDescription description)
        : base(description.DepthTarget, description.ColorTargets) { }

    public MTLFramebufferBase() { }
}
