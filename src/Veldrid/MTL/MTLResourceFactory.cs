using System;
using Veldrid.MetalBindings;

namespace Veldrid.MTL;

internal sealed class MTLResourceFactory(MTLGraphicsDevice gd) : ResourceFactory(gd.Features)
{
    public override GraphicsBackend BackendType => GraphicsBackend.Metal;

    public override CommandList CreateCommandList(in CommandListDescription description)
    {
        return new MTLCommandList(description, gd);
    }

    public override Pipeline CreateComputePipeline(in ComputePipelineDescription description)
    {
        return new MTLPipeline(description, gd);
    }

    public override Framebuffer CreateFramebuffer(in FramebufferDescription description)
    {
        return new MTLFramebuffer(gd, description);
    }

    public override Pipeline CreateGraphicsPipeline(in GraphicsPipelineDescription description)
    {
        ValidateGraphicsPipeline(description);
        return new MTLPipeline(description, gd);
    }

    public override ResourceLayout CreateResourceLayout(in ResourceLayoutDescription description)
    {
        return new MTLResourceLayout(description, gd);
    }

    public override ResourceSet CreateResourceSet(in ResourceSetDescription description)
    {
        ValidationHelpers.ValidateResourceSet(gd, description);
        return new MTLResourceSet(description, gd);
    }

    public override Sampler CreateSampler(in SamplerDescription description)
    {
        ValidateSampler(description);
        return new MTLSampler(description, gd);
    }

    public override Shader CreateShader(in ShaderDescription description)
    {
        ValidateShader(description);
        return new MTLShader(description, gd);
    }

    public override DeviceBuffer CreateBuffer(in BufferDescription description)
    {
        ValidateBuffer(description);
        return new MTLBuffer(description, gd);
    }

    public override Texture CreateTexture(in TextureDescription description)
    {
        ValidateTexture(description);
        return new MTLTexture(description, gd);
    }

    public override Texture CreateTexture(ulong nativeTexture, in TextureDescription description)
    {
        return new MTLTexture(nativeTexture, description);
    }

    public override TextureView CreateTextureView(in TextureViewDescription description)
    {
        ValidateTextureView(description);
        return new MTLTextureView(description, gd);
    }

    public override Fence CreateFence(bool signaled)
    {
        return new MTLFence(signaled);
    }

    public override Swapchain CreateSwapchain(in SwapchainDescription description)
    {
        return new MTLSwapchain(gd, description);
    }
}