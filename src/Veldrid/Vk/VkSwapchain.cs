﻿using System;
using System.Linq;
using TerraFX.Interop.Vulkan;
using static TerraFX.Interop.Vulkan.VkFormat;
using static TerraFX.Interop.Vulkan.Vulkan;
using static Veldrid.Vk.VulkanUtil;
using VulkanFence = TerraFX.Interop.Vulkan.VkFence;

namespace Veldrid.Vk;

internal sealed unsafe class VkSwapchain : Swapchain, IResourceRefCountTarget
{
    readonly VkGraphicsDevice _gd;
    readonly VkSurfaceKHR _surface;
    VkSwapchainKHR _deviceSwapchain;
    readonly VkSwapchainFramebuffer _framebuffer;
    readonly VulkanFence _imageAvailableFence;
    readonly uint _presentQueueIndex;
    readonly VkQueue _presentQueue;
    bool _syncToVBlank;
    readonly bool _colorSrgb;
    bool? _newSyncToVBlank;
    uint _currentImageIndex;
    string? _name;

    public override string? Name
    {
        get => _name;
        set
        {
            _name = value;
            _gd.SetResourceName(this, value);
        }
    }

    public override Framebuffer Framebuffer => _framebuffer;

    public override bool SyncToVerticalBlank
    {
        get => _newSyncToVBlank ?? _syncToVBlank;
        set
        {
            if (_syncToVBlank != value)
            {
                _newSyncToVBlank = value;
            }
        }
    }

    public override bool IsDisposed => RefCount.IsDisposed;

    public VkSwapchainKHR DeviceSwapchain => _deviceSwapchain;
    public uint ImageIndex => _currentImageIndex;
    public VulkanFence ImageAvailableFence => _imageAvailableFence;
    public VkSurfaceKHR Surface => _surface;
    public VkQueue PresentQueue => _presentQueue;
    public uint PresentQueueIndex => _presentQueueIndex;
    public ResourceRefCount RefCount { get; }
    public object PresentLock { get; }

    public VkSwapchain(VkGraphicsDevice gd, in SwapchainDescription description)
        : this(gd, description, default) { }

    public VkSwapchain(
        VkGraphicsDevice gd,
        in SwapchainDescription description,
        VkSurfaceKHR existingSurface
    )
    {
        _gd = gd;
        _syncToVBlank = description.SyncToVerticalBlank;
        SwapchainSource swapchainSource = description.Source;
        _colorSrgb = description.ColorSrgb;

        _surface =
            existingSurface == VkSurfaceKHR.NULL
                ? VkSurfaceUtil.CreateSurface(gd.Instance, swapchainSource)
                : existingSurface;

        if (!GetPresentQueueIndex(out _presentQueueIndex))
        {
            throw new VeldridException(
                "The system does not support presenting the given Vulkan surface."
            );
        }
        VkQueue presentQueue;
        vkGetDeviceQueue(_gd.Device, _presentQueueIndex, 0, &presentQueue);
        _presentQueue = presentQueue;

        RefCount = new(this);
        PresentLock = new();

        _framebuffer = new(gd, this, description);

        CreateSwapchain(description.Width, description.Height);

        VkFenceCreateInfo fenceCI = new()
        {
            sType = VkStructureType.VK_STRUCTURE_TYPE_FENCE_CREATE_INFO,
        };

        VulkanFence imageAvailableFence;
        vkCreateFence(_gd.Device, &fenceCI, null, &imageAvailableFence);

        AcquireNextImage(_gd.Device, default, imageAvailableFence);
        vkWaitForFences(_gd.Device, 1, &imageAvailableFence, (VkBool32)true, ulong.MaxValue);
        vkResetFences(_gd.Device, 1, &imageAvailableFence);

        _imageAvailableFence = imageAvailableFence;
    }

    public override void Resize(uint width, uint height)
    {
        RecreateAndReacquire(width, height);
    }

    public bool AcquireNextImage(VkDevice device, VkSemaphore semaphore, VulkanFence fence)
    {
        if (_newSyncToVBlank != null)
        {
            _syncToVBlank = _newSyncToVBlank.Value;
            _newSyncToVBlank = null;
            RecreateAndReacquire(_framebuffer.Width, _framebuffer.Height);
            return false;
        }

        uint imageIndex = _currentImageIndex;
        VkResult result = vkAcquireNextImageKHR(
            device,
            _deviceSwapchain,
            ulong.MaxValue,
            semaphore,
            fence,
            &imageIndex
        );

        _framebuffer.SetImageIndex(imageIndex);
        _currentImageIndex = imageIndex;

        if (result == VkResult.VK_ERROR_OUT_OF_DATE_KHR || result == VkResult.VK_SUBOPTIMAL_KHR)
        {
            CreateSwapchain(_framebuffer.Width, _framebuffer.Height);
            return false;
        }

        if (result != VkResult.VK_SUCCESS)
            throw new VeldridException("Could not acquire next image from the Vulkan swapchain.");

        return true;
    }

    void RecreateAndReacquire(uint width, uint height)
    {
        if (!CreateSwapchain(width, height))
            return;

        VulkanFence imageAvailableFence = _imageAvailableFence;
        if (!AcquireNextImage(_gd.Device, default, imageAvailableFence))
            return;

        vkWaitForFences(_gd.Device, 1, &imageAvailableFence, (VkBool32)true, ulong.MaxValue);

        vkResetFences(_gd.Device, 1, &imageAvailableFence);
    }

    bool CreateSwapchain(uint width, uint height)
    {
        // Obtain the surface capabilities first -- this will indicate whether the surface has been lost.
        VkSurfaceCapabilitiesKHR surfaceCapabilities;
        VkResult result = vkGetPhysicalDeviceSurfaceCapabilitiesKHR(
            _gd.PhysicalDevice,
            _surface,
            &surfaceCapabilities
        );

        if (result == VkResult.VK_ERROR_SURFACE_LOST_KHR)
            throw new VeldridException("The Swapchain's underlying surface has been lost.");

        if (
            surfaceCapabilities.minImageExtent is { width: 0, height: 0 }
            && surfaceCapabilities.maxImageExtent is { width: 0, height: 0 }
        )
        {
            return false;
        }

        if (_deviceSwapchain != VkSwapchainKHR.NULL)
        {
            _gd.WaitForIdle();
        }

        _currentImageIndex = 0;
        uint surfaceFormatCount = 0;
        result = vkGetPhysicalDeviceSurfaceFormatsKHR(
            _gd.PhysicalDevice,
            _surface,
            &surfaceFormatCount,
            null
        );

        CheckResult(result);
        VkSurfaceFormatKHR[] formats = new VkSurfaceFormatKHR[surfaceFormatCount];
        fixed (VkSurfaceFormatKHR* formatsPtr = formats)
        {
            result = vkGetPhysicalDeviceSurfaceFormatsKHR(
                _gd.PhysicalDevice,
                _surface,
                &surfaceFormatCount,
                formatsPtr
            );
            CheckResult(result);
        }

        VkFormat desiredFormat = _colorSrgb ? VK_FORMAT_B8G8R8A8_SRGB : VK_FORMAT_B8G8R8A8_UNORM;

        VkSurfaceFormatKHR surfaceFormat = new();
        if (formats is [{ format: VK_FORMAT_UNDEFINED } _])
        {
            surfaceFormat.format = desiredFormat;
            surfaceFormat.colorSpace = VkColorSpaceKHR.VK_COLORSPACE_SRGB_NONLINEAR_KHR;
        }
        else
        {
            foreach (VkSurfaceFormatKHR format in formats)
            {
                if (
                    format.colorSpace == VkColorSpaceKHR.VK_COLORSPACE_SRGB_NONLINEAR_KHR
                    && format.format == desiredFormat
                )
                {
                    surfaceFormat = format;
                    break;
                }
            }
            if (surfaceFormat.format == VK_FORMAT_UNDEFINED)
            {
                if (_colorSrgb && surfaceFormat.format != VK_FORMAT_R8G8B8A8_SRGB)
                {
                    throw new VeldridException(
                        "Unable to create an sRGB Swapchain for this surface."
                    );
                }

                surfaceFormat = formats[0];
            }
        }

        uint presentModeCount = 0;
        result = vkGetPhysicalDeviceSurfacePresentModesKHR(
            _gd.PhysicalDevice,
            _surface,
            &presentModeCount,
            null
        );
        CheckResult(result);
        VkPresentModeKHR[] presentModes = new VkPresentModeKHR[presentModeCount];
        fixed (VkPresentModeKHR* presentModesPtr = presentModes)
        {
            result = vkGetPhysicalDeviceSurfacePresentModesKHR(
                _gd.PhysicalDevice,
                _surface,
                &presentModeCount,
                presentModesPtr
            );
            CheckResult(result);
        }

        VkPresentModeKHR presentMode = VkPresentModeKHR.VK_PRESENT_MODE_FIFO_KHR;

        if (_syncToVBlank)
        {
            if (presentModes.Contains(VkPresentModeKHR.VK_PRESENT_MODE_FIFO_RELAXED_KHR))
                presentMode = VkPresentModeKHR.VK_PRESENT_MODE_FIFO_RELAXED_KHR;
        }
        else
        {
            if (presentModes.Contains(VkPresentModeKHR.VK_PRESENT_MODE_MAILBOX_KHR))
                presentMode = VkPresentModeKHR.VK_PRESENT_MODE_MAILBOX_KHR;
            else if (presentModes.Contains(VkPresentModeKHR.VK_PRESENT_MODE_IMMEDIATE_KHR))
                presentMode = VkPresentModeKHR.VK_PRESENT_MODE_IMMEDIATE_KHR;
        }

        uint maxImageCount =
            surfaceCapabilities.maxImageCount == 0
                ? uint.MaxValue
                : surfaceCapabilities.maxImageCount;

        uint imageCount = Math.Min(maxImageCount, surfaceCapabilities.minImageCount + 1);

        uint clampedWidth = Util.Clamp(
            width,
            surfaceCapabilities.minImageExtent.width,
            surfaceCapabilities.maxImageExtent.width
        );

        uint clampedHeight = Util.Clamp(
            height,
            surfaceCapabilities.minImageExtent.height,
            surfaceCapabilities.maxImageExtent.height
        );

        VkSwapchainCreateInfoKHR swapchainCI = new()
        {
            sType = VkStructureType.VK_STRUCTURE_TYPE_SWAPCHAIN_CREATE_INFO_KHR,
            surface = _surface,
            presentMode = presentMode,
            imageFormat = surfaceFormat.format,
            imageColorSpace = surfaceFormat.colorSpace,
            imageExtent = new() { width = clampedWidth, height = clampedHeight },
            minImageCount = imageCount,
            imageArrayLayers = 1,
            imageUsage =
                VkImageUsageFlags.VK_IMAGE_USAGE_COLOR_ATTACHMENT_BIT
                | VkImageUsageFlags.VK_IMAGE_USAGE_TRANSFER_DST_BIT,
        };

        uint* queueFamilyIndices =
            stackalloc uint[] { _gd.GraphicsQueueIndex, _gd.PresentQueueIndex };

        if (_gd.GraphicsQueueIndex != _gd.PresentQueueIndex)
        {
            swapchainCI.imageSharingMode = VkSharingMode.VK_SHARING_MODE_CONCURRENT;
            swapchainCI.queueFamilyIndexCount = 2;
            swapchainCI.pQueueFamilyIndices = queueFamilyIndices;
        }
        else
        {
            swapchainCI.imageSharingMode = VkSharingMode.VK_SHARING_MODE_EXCLUSIVE;
            swapchainCI.queueFamilyIndexCount = 0;
        }

        swapchainCI.preTransform = VkSurfaceTransformFlagsKHR.VK_SURFACE_TRANSFORM_IDENTITY_BIT_KHR;
        swapchainCI.compositeAlpha = VkCompositeAlphaFlagsKHR.VK_COMPOSITE_ALPHA_OPAQUE_BIT_KHR;
        swapchainCI.clipped = (VkBool32)true;

        VkSwapchainKHR oldSwapchain = _deviceSwapchain;
        swapchainCI.oldSwapchain = oldSwapchain;

        VkSwapchainKHR deviceSwapchain;
        result = vkCreateSwapchainKHR(_gd.Device, &swapchainCI, null, &deviceSwapchain);
        CheckResult(result);
        _deviceSwapchain = deviceSwapchain;

        if (oldSwapchain != VkSwapchainKHR.NULL)
        {
            vkDestroySwapchainKHR(_gd.Device, oldSwapchain, null);
        }

        _framebuffer.SetNewSwapchain(
            _deviceSwapchain,
            width,
            height,
            surfaceFormat,
            swapchainCI.imageExtent
        );
        return true;
    }

    bool GetPresentQueueIndex(out uint queueFamilyIndex)
    {
        uint graphicsQueueIndex = _gd.GraphicsQueueIndex;
        uint presentQueueIndex = _gd.PresentQueueIndex;

        if (QueueSupportsPresent(graphicsQueueIndex, _surface))
        {
            queueFamilyIndex = graphicsQueueIndex;
            return true;
        }

        if (
            graphicsQueueIndex != presentQueueIndex
            && QueueSupportsPresent(presentQueueIndex, _surface)
        )
        {
            queueFamilyIndex = presentQueueIndex;
            return true;
        }

        queueFamilyIndex = 0;
        return false;
    }

    bool QueueSupportsPresent(uint queueFamilyIndex, VkSurfaceKHR surface)
    {
        uint supported;
        VkResult result = vkGetPhysicalDeviceSurfaceSupportKHR(
            _gd.PhysicalDevice,
            queueFamilyIndex,
            surface,
            &supported
        );
        CheckResult(result);
        return (VkBool32)supported;
    }

    public override void Dispose()
    {
        _framebuffer.Dispose();
        RefCount.DecrementDispose();
    }

    void IResourceRefCountTarget.RefZeroed()
    {
        vkDestroyFence(_gd.Device, _imageAvailableFence, null);
        vkDestroySwapchainKHR(_gd.Device, _deviceSwapchain, null);
        vkDestroySurfaceKHR(_gd.Instance, _surface, null);
    }
}
