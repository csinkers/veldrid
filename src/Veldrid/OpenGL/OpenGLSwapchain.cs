using System;

namespace Veldrid.OpenGL;

internal sealed class OpenGLSwapchain(
    OpenGLGraphicsDevice gd,
    OpenGLSwapchainFramebuffer framebuffer,
    Action<uint, uint>? resizeAction
) : Swapchain
{
    bool _disposed;

    public override Framebuffer Framebuffer => framebuffer;
    public override bool SyncToVerticalBlank
    {
        get => gd.SyncToVerticalBlank;
        set => gd.SyncToVerticalBlank = value;
    }
    public override string? Name { get; set; } = "OpenGL Context Swapchain";
    public override bool IsDisposed => _disposed;

    public override void Resize(uint width, uint height)
    {
        framebuffer.Resize(width, height);
        resizeAction?.Invoke(width, height);
    }

    public override void Dispose()
    {
        _disposed = true;
    }
}
