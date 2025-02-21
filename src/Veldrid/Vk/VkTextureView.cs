﻿using TerraFX.Interop.Vulkan;
using static TerraFX.Interop.Vulkan.Vulkan;

namespace Veldrid.Vk;

internal sealed unsafe class VkTextureView : TextureView, IResourceRefCountTarget
{
    readonly VkGraphicsDevice _gd;
    string? _name;

    public VkImageView ImageView { get; }
    public new VkTexture Target => (VkTexture)base.Target;
    public ResourceRefCount RefCount { get; }
    public override bool IsDisposed => RefCount.IsDisposed;

    public VkTextureView(VkGraphicsDevice gd, in TextureViewDescription description)
        : base(description)
    {
        _gd = gd;
        VkTexture tex = Util.AssertSubtype<Texture, VkTexture>(description.Target);

        VkImageAspectFlags aspectFlags;
        aspectFlags =
            (description.Target.Usage & TextureUsage.DepthStencil) != 0
                ? VkImageAspectFlags.VK_IMAGE_ASPECT_DEPTH_BIT
                : VkImageAspectFlags.VK_IMAGE_ASPECT_COLOR_BIT;

        VkImageViewCreateInfo imageViewCI = new()
        {
            sType = VkStructureType.VK_STRUCTURE_TYPE_IMAGE_VIEW_CREATE_INFO,
            image = tex.OptimalDeviceImage,
            format = VkFormats.VdToVkPixelFormat(Format, tex.Usage),
            subresourceRange = new()
            {
                aspectMask = aspectFlags,
                baseMipLevel = description.BaseMipLevel,
                levelCount = description.MipLevels,
                baseArrayLayer = description.BaseArrayLayer,
                layerCount = description.ArrayLayers,
            },
        };

        if ((tex.Usage & TextureUsage.Cubemap) == TextureUsage.Cubemap)
        {
            imageViewCI.viewType =
                description.ArrayLayers == 1
                    ? VkImageViewType.VK_IMAGE_VIEW_TYPE_CUBE
                    : VkImageViewType.VK_IMAGE_VIEW_TYPE_CUBE_ARRAY;
            imageViewCI.subresourceRange.layerCount *= 6;
        }
        else
        {
            switch (tex.Type)
            {
                case TextureType.Texture1D:
                    imageViewCI.viewType =
                        description.ArrayLayers == 1
                            ? VkImageViewType.VK_IMAGE_VIEW_TYPE_1D
                            : VkImageViewType.VK_IMAGE_VIEW_TYPE_1D_ARRAY;
                    break;
                case TextureType.Texture2D:
                    imageViewCI.viewType =
                        description.ArrayLayers == 1
                            ? VkImageViewType.VK_IMAGE_VIEW_TYPE_2D
                            : VkImageViewType.VK_IMAGE_VIEW_TYPE_2D_ARRAY;
                    break;
                case TextureType.Texture3D:
                    imageViewCI.viewType = VkImageViewType.VK_IMAGE_VIEW_TYPE_3D;
                    break;
            }
        }

        VkImageView imageView;
        vkCreateImageView(_gd.Device, &imageViewCI, null, &imageView);
        ImageView = imageView;
        RefCount = new(this);
    }

    public override string? Name
    {
        get => _name;
        set
        {
            _name = value;
            _gd.SetResourceName(this, value);
        }
    }

    public override void Dispose()
    {
        RefCount.DecrementDispose();
    }

    void IResourceRefCountTarget.RefZeroed()
    {
        vkDestroyImageView(_gd.Device, ImageView, null);
    }
}
