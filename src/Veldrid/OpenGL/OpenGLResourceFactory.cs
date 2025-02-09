using System;

namespace Veldrid.OpenGL;

internal sealed class OpenGLResourceFactory(OpenGLGraphicsDevice gd) : ResourceFactory(gd.Features)
{
    readonly StagingMemoryPool _pool = gd.StagingMemoryPool;

    public override GraphicsBackend BackendType => gd.BackendType;

    public override CommandList CreateCommandList(in CommandListDescription description) =>
        new OpenGLCommandList(gd);

    public override Framebuffer CreateFramebuffer(in FramebufferDescription description) =>
        new OpenGLFramebuffer(gd, description);

    public override Pipeline CreateGraphicsPipeline(in GraphicsPipelineDescription description)
    {
        ValidateGraphicsPipeline(description);
        OpenGLPipeline pipeline = new(gd, description);
        gd.EnsureResourceInitialized(pipeline);
        return pipeline;
    }

    public override Pipeline CreateComputePipeline(in ComputePipelineDescription description)
    {
        OpenGLPipeline pipeline = new(gd, description);
        gd.EnsureResourceInitialized(pipeline);
        return pipeline;
    }

    public override ResourceLayout CreateResourceLayout(in ResourceLayoutDescription description) =>
        new OpenGLResourceLayout(description);

    public override ResourceSet CreateResourceSet(in ResourceSetDescription description)
    {
        ValidationHelpers.ValidateResourceSet(gd, description);
        return new OpenGLResourceSet(description);
    }

    public override Sampler CreateSampler(in SamplerDescription description)
    {
        ValidateSampler(description);
        return new OpenGLSampler(gd, description);
    }

    public override Shader CreateShader(in ShaderDescription description)
    {
        ValidateShader(description);
        StagingBlock stagingBlock = _pool.Stage(description.ShaderBytes);
        OpenGLShader shader = new(gd, description.Stage, stagingBlock, description.EntryPoint);
        gd.EnsureResourceInitialized(shader);
        return shader;
    }

    public override Texture CreateTexture(in TextureDescription description)
    {
        ValidateTexture(description);
        return new OpenGLTexture(gd, description);
    }

    public override Texture CreateTexture(ulong nativeTexture, in TextureDescription description) =>
        new OpenGLTexture(gd, (uint)nativeTexture, description);

    public override TextureView CreateTextureView(in TextureViewDescription description)
    {
        ValidateTextureView(description);
        return new OpenGLTextureView(gd, description);
    }

    public override DeviceBuffer CreateBuffer(in BufferDescription description)
    {
        ValidateBuffer(description);
        return new OpenGLBuffer(gd, description);
    }

    public override Fence CreateFence(bool signaled) => new OpenGLFence(signaled);

    public override Swapchain CreateSwapchain(in SwapchainDescription description) =>
        throw new NotSupportedException("OpenGL does not support creating Swapchain objects.");
}
