using System;
using Veldrid.MetalBindings;

namespace Veldrid.MTL;

internal sealed class MTLFramebuffer(in FramebufferDescription description)
    : MTLFramebufferBase(description)
{
    public override bool IsRenderable => true;
    bool _disposed;

    public override MTLRenderPassDescriptor CreateRenderPassDescriptor()
    {
        MTLRenderPassDescriptor ret = MTLRenderPassDescriptor.New();

        ReadOnlySpan<FramebufferAttachment> colorTargets = ColorTargets;
        for (int i = 0; i < colorTargets.Length; i++)
        {
            FramebufferAttachment colorTarget = colorTargets[i];
            MTLTexture mtlTarget = Util.AssertSubtype<Texture, MTLTexture>(colorTarget.Target);
            MTLRenderPassColorAttachmentDescriptor colorDescriptor = ret.colorAttachments[(uint)i];
            colorDescriptor.texture = mtlTarget.DeviceTexture;
            colorDescriptor.loadAction = MTLLoadAction.Load;
            colorDescriptor.slice = colorTarget.ArrayLayer;
            colorDescriptor.level = colorTarget.MipLevel;
        }

        if (DepthTarget != null)
        {
            FramebufferAttachment depthTarget = DepthTarget.GetValueOrDefault();

            MTLTexture mtlDepthTarget = Util.AssertSubtype<Texture, MTLTexture>(depthTarget.Target);
            MTLRenderPassDepthAttachmentDescriptor depthDescriptor = ret.depthAttachment;
            depthDescriptor.loadAction = MTLLoadAction.Load;
            depthDescriptor.storeAction = MTLStoreAction.Store;
            depthDescriptor.texture = mtlDepthTarget.DeviceTexture;
            depthDescriptor.slice = depthTarget.ArrayLayer;
            depthDescriptor.level = depthTarget.MipLevel;

            if (FormatHelpers.IsStencilFormat(mtlDepthTarget.Format))
            {
                MTLRenderPassStencilAttachmentDescriptor stencilDescriptor = ret.stencilAttachment;
                stencilDescriptor.loadAction = MTLLoadAction.Load;
                stencilDescriptor.storeAction = MTLStoreAction.Store;
                stencilDescriptor.texture = mtlDepthTarget.DeviceTexture;
                stencilDescriptor.slice = depthTarget.ArrayLayer;
            }
        }

        return ret;
    }

    public override bool IsDisposed => _disposed;

    public override void Dispose()
    {
        _disposed = true;
    }
}
