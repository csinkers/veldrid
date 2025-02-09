using System.Diagnostics;
using Veldrid.MetalBindings;

namespace Veldrid.MTL;

internal sealed class MTLSwapchainFramebuffer : MTLFramebufferBase
{
    private readonly MTLGraphicsDevice _gd;
    private readonly MTLPlaceholderTexture _placeholderTexture;
    private MTLTexture? _depthTexture;
    private readonly MTLSwapchain _parentSwapchain;
    private bool _disposed;

    private readonly PixelFormat? _depthFormat;

    public override bool IsDisposed => _disposed;

    public MTLSwapchainFramebuffer(
        MTLGraphicsDevice gd,
        MTLSwapchain parent,
        uint width,
        uint height,
        PixelFormat? depthFormat,
        PixelFormat colorFormat)
        : base()
    {
        _gd = gd;
        _parentSwapchain = parent;

        OutputAttachmentDescription? depthAttachment = null;
        if (depthFormat != null)
        {
            _depthFormat = depthFormat;
            depthAttachment = new OutputAttachmentDescription(depthFormat.Value);
            RecreateDepthTexture(width, height);
            _depthTarget = new FramebufferAttachment(_depthTexture!, 0);
        }
        OutputAttachmentDescription colorAttachment = new(colorFormat);

        OutputDescription = new OutputDescription(depthAttachment, colorAttachment);
        _placeholderTexture = new MTLPlaceholderTexture(colorFormat);
        _placeholderTexture.Resize(width, height);
        _colorTargets = new[] { new FramebufferAttachment(_placeholderTexture, 0) };

        Width = width;
        Height = height;
    }

    private void RecreateDepthTexture(uint width, uint height)
    {
        Debug.Assert(_depthFormat.HasValue);
        if (_depthTexture != null)
        {
            _depthTexture.Dispose();
        }

        _depthTexture = Util.AssertSubtype<Texture, MTLTexture>(
            _gd.ResourceFactory.CreateTexture(TextureDescription.Texture2D(
                width, height, 1, 1, _depthFormat.Value, TextureUsage.DepthStencil)));
    }

    public void Resize(uint width, uint height)
    {
        _placeholderTexture.Resize(width, height);

        if (_depthFormat.HasValue)
        {
            RecreateDepthTexture(width, height);
        }

        Width = width;
        Height = height;
    }

    public override bool IsRenderable => !_parentSwapchain.CurrentDrawable.IsNull;

    public override MTLRenderPassDescriptor CreateRenderPassDescriptor()
    {
        MTLRenderPassDescriptor ret = MTLRenderPassDescriptor.New();
        MTLRenderPassColorAttachmentDescriptor color0 = ret.colorAttachments[0];
        color0.texture = _parentSwapchain.CurrentDrawable.texture;
        color0.loadAction = MTLLoadAction.Load;

        if (_depthTarget != null)
        {
            MTLRenderPassDepthAttachmentDescriptor depthAttachment = ret.depthAttachment;
            depthAttachment.texture = _depthTexture!.DeviceTexture;
            depthAttachment.loadAction = MTLLoadAction.Load;
        }

        return ret;
    }

    public override void Dispose()
    {
        _depthTexture?.Dispose();
        _disposed = true;
    }
}