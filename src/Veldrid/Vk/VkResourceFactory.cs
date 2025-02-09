using TerraFX.Interop.Vulkan;

namespace Veldrid.Vulkan;

internal sealed class VkResourceFactory(VkGraphicsDevice vkGraphicsDevice)
    : ResourceFactory(vkGraphicsDevice.Features)
{
    public override GraphicsBackend BackendType => GraphicsBackend.Vulkan;

    public override CommandList CreateCommandList(in CommandListDescription description)
    {
        return new VkCommandList(vkGraphicsDevice, description);
    }

    public override Framebuffer CreateFramebuffer(in FramebufferDescription description)
    {
        return new VkFramebuffer(vkGraphicsDevice, description, false);
    }

    public override Pipeline CreateGraphicsPipeline(in GraphicsPipelineDescription description)
    {
        ValidateGraphicsPipeline(description);
        return new VkPipeline(vkGraphicsDevice, description);
    }

    public override Pipeline CreateComputePipeline(in ComputePipelineDescription description)
    {
        return new VkPipeline(vkGraphicsDevice, description);
    }

    public override ResourceLayout CreateResourceLayout(in ResourceLayoutDescription description)
    {
        return new VkResourceLayout(vkGraphicsDevice, description);
    }

    public override ResourceSet CreateResourceSet(in ResourceSetDescription description)
    {
        ValidationHelpers.ValidateResourceSet(vkGraphicsDevice, description);
        return new VkResourceSet(vkGraphicsDevice, description);
    }

    public override Sampler CreateSampler(in SamplerDescription description)
    {
        ValidateSampler(description);
        return new VkSampler(vkGraphicsDevice, description);
    }

    public override Shader CreateShader(in ShaderDescription description)
    {
        ValidateShader(description);
        return new VkShader(vkGraphicsDevice, description);
    }

    public override Texture CreateTexture(in TextureDescription description)
    {
        ValidateTexture(description);
        return new VkTexture(vkGraphicsDevice, description);
    }

    public override Texture CreateTexture(ulong nativeTexture, in TextureDescription description)
    {
        return new VkTexture(
            vkGraphicsDevice,
            description.Width,
            description.Height,
            description.MipLevels,
            description.ArrayLayers,
            VkFormats.VdToVkPixelFormat(description.Format, description.Usage),
            description.Usage,
            description.SampleCount,
            new VkImage(nativeTexture),
            false,
            true
        );
    }

    public override TextureView CreateTextureView(in TextureViewDescription description)
    {
        ValidateTextureView(description);
        return new VkTextureView(vkGraphicsDevice, description);
    }

    public override DeviceBuffer CreateBuffer(in BufferDescription description)
    {
        ValidateBuffer(description);
        return new VkBuffer(vkGraphicsDevice, description);
    }

    public override Fence CreateFence(bool signaled)
    {
        return new VkFence(vkGraphicsDevice, signaled);
    }

    public override Swapchain CreateSwapchain(in SwapchainDescription description)
    {
        return new VkSwapchain(vkGraphicsDevice, description);
    }
}
