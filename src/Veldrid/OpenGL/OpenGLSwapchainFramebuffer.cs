namespace Veldrid.OpenGL;

internal sealed class OpenGLSwapchainFramebuffer : Framebuffer
{
    bool _disposed;

    public override string? Name { get; set; }

    public override bool IsDisposed => _disposed;

    readonly OpenGLPlaceholderTexture _colorTexture;
    readonly OpenGLPlaceholderTexture? _depthTexture;

    public bool DisableSrgbConversion { get; }

    internal OpenGLSwapchainFramebuffer(
        uint width,
        uint height,
        PixelFormat colorFormat,
        PixelFormat? depthFormat,
        bool disableSrgbConversion
    )
    {
        // This is wrong, but it's not really used.
        OutputAttachmentDescription? depthDesc =
            depthFormat != null ? new OutputAttachmentDescription(depthFormat.Value) : null;

        OutputDescription = new(depthDesc, new OutputAttachmentDescription(colorFormat));

        _colorTexture = new(
            width,
            height,
            colorFormat,
            TextureUsage.RenderTarget,
            TextureSampleCount.Count1
        );
        _colorTargets = [new(_colorTexture, 0)];

        if (depthFormat != null)
        {
            _depthTexture = new(
                width,
                height,
                depthFormat.Value,
                TextureUsage.DepthStencil,
                TextureSampleCount.Count1
            );
            _depthTarget = new FramebufferAttachment(_depthTexture, 0);
        }

        OutputDescription = OutputDescription.CreateFromFramebuffer(this);
        Width = width;
        Height = height;

        DisableSrgbConversion = disableSrgbConversion;
    }

    public void Resize(uint width, uint height)
    {
        _colorTexture.Resize(width, height);
        _depthTexture?.Resize(width, height);
        Width = width;
        Height = height;
    }

    public override void Dispose() => _disposed = true;
}
