﻿using TerraFX.Interop.Vulkan;
using static TerraFX.Interop.Vulkan.Vulkan;
using VulkanFence = TerraFX.Interop.Vulkan.VkFence;

namespace Veldrid.Vk;

internal sealed unsafe class VkFence : Fence, IResourceRefCountTarget
{
    readonly VkGraphicsDevice _gd;
    readonly VulkanFence _fence;
    string? _name;

    public ResourceRefCount RefCount { get; }

    public VulkanFence DeviceFence => _fence;

    public VkFence(VkGraphicsDevice gd, bool signaled)
    {
        _gd = gd;
        VkFenceCreateInfo fenceCI = new()
        {
            sType = VkStructureType.VK_STRUCTURE_TYPE_FENCE_CREATE_INFO,
            flags = signaled ? VkFenceCreateFlags.VK_FENCE_CREATE_SIGNALED_BIT : 0,
        };

        VulkanFence fence;
        VkResult result = vkCreateFence(_gd.Device, &fenceCI, null, &fence);
        VulkanUtil.CheckResult(result);
        _fence = fence;

        RefCount = new(this);
    }

    public override void Reset()
    {
        _gd.ResetFence(this);
    }

    public override bool Signaled => vkGetFenceStatus(_gd.Device, _fence) == VkResult.VK_SUCCESS;
    public override bool IsDisposed => RefCount.IsDisposed;

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
        vkDestroyFence(_gd.Device, _fence, null);
    }
}
