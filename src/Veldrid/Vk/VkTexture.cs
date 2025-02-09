﻿using System;
using System.Diagnostics;
using TerraFX.Interop.Vulkan;
using static TerraFX.Interop.Vulkan.Vulkan;
using static Veldrid.Vulkan.VulkanUtil;
using VulkanBuffer = TerraFX.Interop.Vulkan.VkBuffer;

namespace Veldrid.Vulkan
{
    internal sealed unsafe class VkTexture : Texture, IResourceRefCountTarget
    {
        private readonly VkGraphicsDevice _gd;
        private readonly VkImage _optimalImage;
        private readonly VkMemoryBlock _memoryBlock;
        private readonly VulkanBuffer _stagingBuffer;
        private readonly uint _actualImageArrayLayers;
        
        public uint ActualArrayLayers => _actualImageArrayLayers;
        public override bool IsDisposed => RefCount.IsDisposed;

        public VkImage OptimalDeviceImage => _optimalImage;
        public VulkanBuffer StagingBuffer => _stagingBuffer;
        public VkMemoryBlock Memory => _memoryBlock;

        public VkFormat VkFormat { get; }
        public VkSampleCountFlags VkSampleCount { get; }

        private VkImageLayout[] _imageLayouts;
        private bool _isSwapchainTexture;
        private bool _leaveOpen;
        private string? _name;

        public ResourceRefCount RefCount { get; }
        public bool IsSwapchainTexture => _isSwapchainTexture;

        internal VkTexture(VkGraphicsDevice gd, in TextureDescription description)
        {
            _gd = gd;
            Width = description.Width;
            Height = description.Height;
            Depth = description.Depth;
            MipLevels = description.MipLevels;
            ArrayLayers = description.ArrayLayers;
            bool isCubemap = (description.Usage & TextureUsage.Cubemap) == TextureUsage.Cubemap;
            _actualImageArrayLayers = isCubemap
                ? 6 * ArrayLayers
                : ArrayLayers;
            Format = description.Format;
            Usage = description.Usage;
            Type = description.Type;
            SampleCount = description.SampleCount;
            VkSampleCount = VkFormats.VdToVkSampleCount(SampleCount);
            VkFormat = VkFormats.VdToVkPixelFormat(Format, description.Usage);

            bool isStaging = (Usage & TextureUsage.Staging) == TextureUsage.Staging;

            if (!isStaging)
            {
                VkImageCreateInfo imageCI = new()
                {
                    sType = VkStructureType.VK_STRUCTURE_TYPE_IMAGE_CREATE_INFO,
                    mipLevels = MipLevels,
                    arrayLayers = _actualImageArrayLayers,
                    imageType = VkFormats.VdToVkTextureType(Type),
                    extent = new VkExtent3D()
                    {
                        width = Width,
                        height = Height,
                        depth = Depth
                    },
                    initialLayout = VkImageLayout.VK_IMAGE_LAYOUT_PREINITIALIZED,
                    usage = VkFormats.VdToVkTextureUsage(Usage),
                    tiling = isStaging ? VkImageTiling.VK_IMAGE_TILING_LINEAR : VkImageTiling.VK_IMAGE_TILING_OPTIMAL,
                    format = VkFormat,
                    flags = VkImageCreateFlags.VK_IMAGE_CREATE_MUTABLE_FORMAT_BIT,
                    samples = VkSampleCount
                };

                if (isCubemap)
                {
                    imageCI.flags |= VkImageCreateFlags.VK_IMAGE_CREATE_CUBE_COMPATIBLE_BIT;
                }

                uint subresourceCount = MipLevels * _actualImageArrayLayers * Depth;
                VkImage optimalImage;
                VkResult result = vkCreateImage(gd.Device, &imageCI, null, &optimalImage);
                CheckResult(result);
                _optimalImage = optimalImage;

                VkMemoryRequirements memoryRequirements;
                VkBool32 prefersDedicatedAllocation;
                if (_gd.GetImageMemoryRequirements2 != null)
                {
                    VkImageMemoryRequirementsInfo2 memReqsInfo2 = new()
                    {
                        sType = VkStructureType.VK_STRUCTURE_TYPE_IMAGE_MEMORY_REQUIREMENTS_INFO_2,
                        image = _optimalImage
                    };
                    VkMemoryDedicatedRequirements dedicatedReqs = new()
                    {
                        sType = VkStructureType.VK_STRUCTURE_TYPE_MEMORY_DEDICATED_REQUIREMENTS
                    };
                    VkMemoryRequirements2 memReqs2 = new()
                    {
                        sType = VkStructureType.VK_STRUCTURE_TYPE_MEMORY_REQUIREMENTS_2,
                        pNext = &dedicatedReqs
                    };
                    _gd.GetImageMemoryRequirements2(_gd.Device, &memReqsInfo2, &memReqs2);
                    memoryRequirements = memReqs2.memoryRequirements;
                    prefersDedicatedAllocation = dedicatedReqs.prefersDedicatedAllocation | dedicatedReqs.requiresDedicatedAllocation;
                }
                else
                {
                    vkGetImageMemoryRequirements(gd.Device, _optimalImage, &memoryRequirements);
                    prefersDedicatedAllocation = false;
                }

                VkMemoryBlock memoryToken = gd.MemoryManager.Allocate(
                    gd.PhysicalDeviceMemProperties,
                    memoryRequirements.memoryTypeBits,
                    VkMemoryPropertyFlags.VK_MEMORY_PROPERTY_DEVICE_LOCAL_BIT,
                    false,
                    memoryRequirements.size,
                    memoryRequirements.alignment,
                    prefersDedicatedAllocation,
                    _optimalImage,
                    default);
                _memoryBlock = memoryToken;
                result = vkBindImageMemory(gd.Device, _optimalImage, _memoryBlock.DeviceMemory, _memoryBlock.Offset);
                CheckResult(result);

                _imageLayouts = new VkImageLayout[subresourceCount];
                _imageLayouts.AsSpan().Fill(VkImageLayout.VK_IMAGE_LAYOUT_PREINITIALIZED);
            }
            else // isStaging
            {
                uint depthPitch = FormatHelpers.GetDepthPitch(
                    FormatHelpers.GetRowPitch(Width, Format),
                    Height,
                    Format);
                uint stagingSize = depthPitch * Depth;
                for (uint level = 1; level < MipLevels; level++)
                {
                    Util.GetMipDimensions(this, level, out uint mipWidth, out uint mipHeight, out uint mipDepth);

                    depthPitch = FormatHelpers.GetDepthPitch(
                        FormatHelpers.GetRowPitch(mipWidth, Format),
                        mipHeight,
                        Format);

                    stagingSize += depthPitch * mipDepth;
                }
                stagingSize *= ArrayLayers;

                VkBufferCreateInfo bufferCI = new()
                {
                    sType = VkStructureType.VK_STRUCTURE_TYPE_BUFFER_CREATE_INFO,
                    usage = VkBufferUsageFlags.VK_BUFFER_USAGE_TRANSFER_SRC_BIT | VkBufferUsageFlags.VK_BUFFER_USAGE_TRANSFER_DST_BIT,
                    size = stagingSize
                };
                VulkanBuffer stagingBuffer;
                VkResult result = vkCreateBuffer(_gd.Device, &bufferCI, null, &stagingBuffer);
                CheckResult(result);
                _stagingBuffer = stagingBuffer;

                VkMemoryRequirements bufferMemReqs;
                VkBool32 prefersDedicatedAllocation;
                if (_gd.GetBufferMemoryRequirements2 != null)
                {
                    VkBufferMemoryRequirementsInfo2 memReqInfo2 = new()
                    {
                        sType = VkStructureType.VK_STRUCTURE_TYPE_BUFFER_MEMORY_REQUIREMENTS_INFO_2,
                        buffer = _stagingBuffer
                    };
                    VkMemoryDedicatedRequirements dedicatedReqs = new()
                    {
                        sType = VkStructureType.VK_STRUCTURE_TYPE_MEMORY_DEDICATED_REQUIREMENTS
                    };
                    VkMemoryRequirements2 memReqs2 = new()
                    {
                        sType = VkStructureType.VK_STRUCTURE_TYPE_MEMORY_REQUIREMENTS_2,
                        pNext = &dedicatedReqs
                    };
                    _gd.GetBufferMemoryRequirements2(_gd.Device, &memReqInfo2, &memReqs2);
                    bufferMemReqs = memReqs2.memoryRequirements;
                    prefersDedicatedAllocation = dedicatedReqs.prefersDedicatedAllocation | dedicatedReqs.requiresDedicatedAllocation;
                }
                else
                {
                    vkGetBufferMemoryRequirements(gd.Device, _stagingBuffer, &bufferMemReqs);
                    prefersDedicatedAllocation = false;
                }

                // Use "host cached" memory when available, for better performance of GPU -> CPU transfers
                VkMemoryPropertyFlags propertyFlags =
                    VkMemoryPropertyFlags.VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT |
                    VkMemoryPropertyFlags.VK_MEMORY_PROPERTY_HOST_COHERENT_BIT |
                    VkMemoryPropertyFlags.VK_MEMORY_PROPERTY_HOST_CACHED_BIT;

                if (!TryFindMemoryType(_gd.PhysicalDeviceMemProperties, bufferMemReqs.memoryTypeBits, propertyFlags, out _))
                {
                    propertyFlags ^= VkMemoryPropertyFlags.VK_MEMORY_PROPERTY_HOST_CACHED_BIT;
                }

                _memoryBlock = _gd.MemoryManager.Allocate(
                    _gd.PhysicalDeviceMemProperties,
                    bufferMemReqs.memoryTypeBits,
                    propertyFlags,
                    true,
                    bufferMemReqs.size,
                    bufferMemReqs.alignment,
                    prefersDedicatedAllocation,
                    default,
                    _stagingBuffer);

                result = vkBindBufferMemory(_gd.Device, _stagingBuffer, _memoryBlock.DeviceMemory, _memoryBlock.Offset);
                CheckResult(result);

                _imageLayouts = Array.Empty<VkImageLayout>();
            }

            RefCount = new ResourceRefCount(this);
            ClearIfRenderTarget();
            TransitionIfSampled();
        }

        // Used to construct Swapchain textures.
        internal VkTexture(
            VkGraphicsDevice gd,
            uint width,
            uint height,
            uint mipLevels,
            uint arrayLayers,
            VkFormat vkFormat,
            TextureUsage usage,
            TextureSampleCount sampleCount,
            VkImage existingImage,
            bool isSwapchainTexture,
            bool leaveOpen)
        {
            Debug.Assert(width > 0 && height > 0);
            _gd = gd;
            MipLevels = mipLevels;
            Width = width;
            Height = height;
            Depth = 1;
            VkFormat = vkFormat;
            Format = VkFormats.VkToVdPixelFormat(VkFormat);
            ArrayLayers = arrayLayers;
            bool isCubemap = (usage & TextureUsage.Cubemap) == TextureUsage.Cubemap;
            _actualImageArrayLayers = isCubemap
                ? 6 * ArrayLayers
                : ArrayLayers;
            Usage = usage;
            Type = TextureType.Texture2D;
            SampleCount = sampleCount;
            VkSampleCount = VkFormats.VdToVkSampleCount(sampleCount);
            _optimalImage = existingImage;
            _imageLayouts = new[] { VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED };
            _isSwapchainTexture = isSwapchainTexture;
            _leaveOpen = leaveOpen;

            RefCount = new ResourceRefCount(this);
            ClearIfRenderTarget();
        }

        private void ClearIfRenderTarget()
        {
            // If the image is going to be used as a render target, we need to clear the data before its first use.
            if ((Usage & TextureUsage.RenderTarget) != 0)
            {
                VkCommandList cl = _gd.GetAndBeginCommandList();
                cl.ClearColorTexture(this, new VkClearColorValue());
                _gd.EndAndSubmitCommands(cl);
            }
            else if ((Usage & TextureUsage.DepthStencil) != 0)
            {
                VkCommandList cl = _gd.GetAndBeginCommandList();
                cl.ClearDepthTexture(this, new VkClearDepthStencilValue());
                _gd.EndAndSubmitCommands(cl);
            }
        }

        private void TransitionIfSampled()
        {
            if ((Usage & TextureUsage.Sampled) != 0)
            {
                VkCommandList cl = _gd.GetAndBeginCommandList();
                cl.TransitionImageLayout(this, VkImageLayout.VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL);
                _gd.EndAndSubmitCommands(cl);
            }
        }

        internal VkSubresourceLayout GetSubresourceLayout(uint mipLevel, uint arrayLevel)
        {
            VkSubresourceLayout layout;
            bool staging = _stagingBuffer != VulkanBuffer.NULL;
            if (!staging)
            {
                VkImageAspectFlags aspect = (Usage & TextureUsage.DepthStencil) == TextureUsage.DepthStencil
                    ? (VkImageAspectFlags.VK_IMAGE_ASPECT_DEPTH_BIT | VkImageAspectFlags.VK_IMAGE_ASPECT_STENCIL_BIT)
                    : VkImageAspectFlags.VK_IMAGE_ASPECT_COLOR_BIT;
                VkImageSubresource imageSubresource = new()
                {
                    arrayLayer = arrayLevel,
                    mipLevel = mipLevel,
                    aspectMask = aspect
                };

                vkGetImageSubresourceLayout(_gd.Device, _optimalImage, &imageSubresource, &layout);
            }
            else
            {
                base.GetSubresourceLayout(mipLevel, arrayLevel, out uint rowPitch, out uint depthPitch);

                layout.offset = Util.ComputeSubresourceOffset(this, mipLevel, arrayLevel);
                layout.rowPitch = rowPitch;
                layout.depthPitch = depthPitch;
                layout.arrayPitch = depthPitch;
                layout.size = depthPitch;
            }
            return layout;
        }

        internal override void GetSubresourceLayout(uint mipLevel, uint arrayLevel, out uint rowPitch, out uint depthPitch)
        {
            VkSubresourceLayout layout = GetSubresourceLayout(mipLevel, arrayLevel);
            rowPitch = (uint)layout.rowPitch;
            depthPitch = (uint)layout.depthPitch;
        }

        public override uint GetSizeInBytes(uint subresource)
        {
            Util.GetMipLevelAndArrayLayer(this, subresource, out uint mipLevel, out uint arrayLayer);
            VkSubresourceLayout layout = GetSubresourceLayout(mipLevel, arrayLayer);
            return (uint)layout.size;
        }

        internal void TransitionImageLayout(
            VkCommandBuffer cb,
            uint baseMipLevel,
            uint levelCount,
            uint baseArrayLayer,
            uint layerCount,
            VkImageLayout newLayout)
        {
            if (_stagingBuffer != VulkanBuffer.NULL)
            {
                return;
            }

            Debug.Assert(baseMipLevel + levelCount <= MipLevels);
            Debug.Assert(baseArrayLayer + layerCount <= ActualArrayLayers);

            VkImageLayout oldLayout = GetImageLayout(baseMipLevel, baseArrayLayer);
#if DEBUG
            for (uint layer = 0; layer < layerCount; layer++)
            {
                for (uint level = 0; level < levelCount; level++)
                {
                    if (GetImageLayout(baseMipLevel + level, baseArrayLayer + layer) != oldLayout)
                    {
                        throw new VeldridException("Unexpected image layout.");
                    }
                }
            }
#endif
            if (oldLayout != newLayout)
            {
                VkImageAspectFlags aspectMask;
                if ((Usage & TextureUsage.DepthStencil) != 0)
                {
                    aspectMask = FormatHelpers.IsStencilFormat(Format)
                        ? VkImageAspectFlags.VK_IMAGE_ASPECT_DEPTH_BIT | VkImageAspectFlags.VK_IMAGE_ASPECT_STENCIL_BIT
                        : VkImageAspectFlags.VK_IMAGE_ASPECT_DEPTH_BIT;
                }
                else
                {
                    aspectMask = VkImageAspectFlags.VK_IMAGE_ASPECT_COLOR_BIT;
                }
                VulkanUtil.TransitionImageLayout(
                    cb,
                    OptimalDeviceImage,
                    baseMipLevel,
                    levelCount,
                    baseArrayLayer,
                    layerCount,
                    aspectMask,
                    oldLayout,
                    newLayout);

                for (uint layer = 0; layer < layerCount; layer++)
                {
                    for (uint level = 0; level < levelCount; level++)
                    {
                        SetImageLayout(baseMipLevel + level, baseArrayLayer + layer, newLayout);
                    }
                }
            }
        }

        internal void TransitionImageLayoutNonmatching(
            VkCommandBuffer cb,
            uint baseMipLevel,
            uint levelCount,
            uint baseArrayLayer,
            uint layerCount,
            VkImageLayout newLayout)
        {
            if (_stagingBuffer != VulkanBuffer.NULL)
            {
                return;
            }

            for (uint layer = baseArrayLayer; layer < baseArrayLayer + layerCount; layer++)
            {
                for (uint level = baseMipLevel; level < baseMipLevel + levelCount; level++)
                {
                    VkImageLayout oldLayout = GetImageLayout(level, layer);
                    if (oldLayout != newLayout)
                    {
                        VkImageAspectFlags aspectMask;
                        if ((Usage & TextureUsage.DepthStencil) != 0)
                        {
                            aspectMask = FormatHelpers.IsStencilFormat(Format)
                                ? VkImageAspectFlags.VK_IMAGE_ASPECT_DEPTH_BIT | VkImageAspectFlags.VK_IMAGE_ASPECT_STENCIL_BIT
                                : VkImageAspectFlags.VK_IMAGE_ASPECT_DEPTH_BIT;
                        }
                        else
                        {
                            aspectMask = VkImageAspectFlags.VK_IMAGE_ASPECT_COLOR_BIT;
                        }

                        VulkanUtil.TransitionImageLayout(
                            cb,
                            OptimalDeviceImage,
                            level,
                            1,
                            layer,
                            1,
                            aspectMask,
                            oldLayout,
                            newLayout);

                        SetImageLayout(level, layer, newLayout);
                    }
                }
            }
        }

        internal VkImageLayout GetImageLayout(uint mipLevel, uint arrayLayer)
        {
            return _imageLayouts[CalculateSubresource(mipLevel, arrayLayer)];
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

        internal void SetStagingDimensions(uint width, uint height, uint depth, PixelFormat format)
        {
            Debug.Assert(_stagingBuffer != VulkanBuffer.NULL);
            Debug.Assert(Usage == TextureUsage.Staging);
            Width = width;
            Height = height;
            Depth = depth;
            Format = format;
        }

        private protected override void DisposeCore()
        {
            RefCount.DecrementDispose();
        }

        void IResourceRefCountTarget.RefZeroed()
        {
            if (_leaveOpen)
            {
                return;
            }

            bool isStaging = (Usage & TextureUsage.Staging) == TextureUsage.Staging;
            if (isStaging)
            {
                vkDestroyBuffer(_gd.Device, _stagingBuffer, null);
            }
            else
            {
                vkDestroyImage(_gd.Device, _optimalImage, null);
            }

            if (_memoryBlock.DeviceMemory != VkDeviceMemory.NULL)
            {
                _gd.MemoryManager.Free(_memoryBlock);
            }
        }

        internal void SetImageLayout(uint mipLevel, uint arrayLayer, VkImageLayout layout)
        {
            _imageLayouts[CalculateSubresource(mipLevel, arrayLayer)] = layout;
        }
    }
}
