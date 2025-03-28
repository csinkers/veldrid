﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TerraFX.Interop.Vulkan;
using static TerraFX.Interop.Vulkan.Vulkan;
using static Veldrid.Vk.VulkanUtil;
using VkImageLayout = TerraFX.Interop.Vulkan.VkImageLayout;
using VulkanBuffer = TerraFX.Interop.Vulkan.VkBuffer;

namespace Veldrid.Vk;

internal sealed unsafe class VkCommandList : CommandList, IResourceRefCountTarget
{
    readonly VkGraphicsDevice _gd;
    readonly VkCommandPool _pool;
    VkCommandBuffer _cb;

    bool _commandBufferBegun;
    bool _commandBufferEnded;

    uint _viewportCount;
    bool _viewportsChanged;
    VkViewport[] _viewports = [];
    bool _scissorRectsChanged;
    VkRect2D[] _scissorRects = [];

    VkClearValue[] _clearValues = [];
    bool[] _validColorClearValues = [];
    VkClearValue? _depthClearValue;
    readonly List<VkTexture> _dispatchStorageImages = [];

    // Graphics State
    VkFramebufferBase? _currentFramebuffer;
    bool _currentFramebufferEverActive;
    VkRenderPass _activeRenderPass;
    VkPipeline? _currentGraphicsPipeline;
    BoundResourceSetInfo[] _currentGraphicsResourceSets = [];
    bool[] _graphicsResourceSetsChanged = [];

    bool _newFramebuffer; // Render pass cycle state

    bool _vertexBindingsChanged;
    uint _numVertexBindings;
    VulkanBuffer[] _vertexBindings = new VulkanBuffer[1];
    ulong[] _vertexOffsets = new ulong[1];

    // Compute State
    VkPipeline? _currentComputePipeline;
    BoundResourceSetInfo[] _currentComputeResourceSets = [];
    bool[] _computeResourceSetsChanged = [];
    string? _name;
    string _stagingBufferName;

    readonly object _commandBufferListLock = new();
    readonly Stack<VkCommandBuffer> _availableCommandBuffers = new();
    readonly List<VkCommandBuffer> _submittedCommandBuffers = [];

    StagingResourceInfo _currentStagingInfo;
    readonly Dictionary<VkCommandBuffer, StagingResourceInfo> _submittedStagingInfos = new();
    readonly ConcurrentQueue<StagingResourceInfo> _availableStagingInfos = new();

    public ResourceRefCount RefCount { get; }

    public override bool IsDisposed => RefCount.IsDisposed;

    public VkCommandList(VkGraphicsDevice gd, in CommandListDescription description)
        : base(
            gd.Features,
            gd.UniformBufferMinOffsetAlignment,
            gd.StructuredBufferMinOffsetAlignment
        )
    {
        _gd = gd;
        VkCommandPoolCreateInfo poolCI = new()
        {
            sType = VkStructureType.VK_STRUCTURE_TYPE_COMMAND_POOL_CREATE_INFO,
            flags = VkCommandPoolCreateFlags.VK_COMMAND_POOL_CREATE_RESET_COMMAND_BUFFER_BIT,
            queueFamilyIndex = gd.GraphicsQueueIndex,
        };

        if (description.Transient)
        {
            poolCI.flags |= VkCommandPoolCreateFlags.VK_COMMAND_POOL_CREATE_TRANSIENT_BIT;
        }

        VkCommandPool pool;
        VkResult result = vkCreateCommandPool(_gd.Device, &poolCI, null, &pool);
        CheckResult(result);
        _pool = pool;

        _cb = GetNextCommandBuffer();
        _stagingBufferName = "Staging Buffer (CommandList)";
        RefCount = new(this);
    }

    VkCommandBuffer GetNextCommandBuffer()
    {
        lock (_commandBufferListLock)
        {
            if (_availableCommandBuffers.TryPop(out VkCommandBuffer cachedCB))
            {
                VkResult resetResult = vkResetCommandBuffer(cachedCB, 0);
                CheckResult(resetResult);
                return cachedCB;
            }
        }

        VkCommandBufferAllocateInfo cbAI = new()
        {
            sType = VkStructureType.VK_STRUCTURE_TYPE_COMMAND_BUFFER_ALLOCATE_INFO,
            commandPool = _pool,
            commandBufferCount = 1,
            level = VkCommandBufferLevel.VK_COMMAND_BUFFER_LEVEL_PRIMARY,
        };
        VkCommandBuffer cb;
        VkResult result = vkAllocateCommandBuffers(_gd.Device, &cbAI, &cb);
        CheckResult(result);

        if (_gd.DebugMarkerEnabled)
        {
            _gd.SetDebugMarkerName(
                VkDebugReportObjectTypeEXT.VK_DEBUG_REPORT_OBJECT_TYPE_COMMAND_BUFFER_EXT,
                (ulong)cb.Value,
                _name
            );
        }

        return cb;
    }

    public VkCommandBuffer CommandBufferSubmitted()
    {
        RefCount.Increment();

        VkCommandBuffer cb = _cb;
        _cb = default;

        StagingResourceInfo info = _currentStagingInfo;
        _currentStagingInfo = default;

        lock (_commandBufferListLock)
        {
            if (!_submittedStagingInfos.TryAdd(cb, info))
            {
                ThrowUnreachableStateException();
            }

            _submittedCommandBuffers.Add(cb);
            return cb;
        }
    }

    public StagingResourceInfo CommandBufferCompleted(VkCommandBuffer completedCB)
    {
        lock (_commandBufferListLock)
        {
            for (int i = 0; i < _submittedCommandBuffers.Count; i++)
            {
                VkCommandBuffer submittedCB = _submittedCommandBuffers[i];
                if (submittedCB == completedCB)
                {
                    _availableCommandBuffers.Push(completedCB);
                    _submittedCommandBuffers.RemoveAt(i);
                    break;
                }
            }

            if (!_submittedStagingInfos.Remove(completedCB, out StagingResourceInfo info))
                ThrowUnreachableStateException();

            RefCount.Decrement();
            return info;
        }
    }

    public override void Begin()
    {
        if (_commandBufferBegun)
        {
            throw new VeldridException(
                "CommandList must be in its initial state, or End() must have been called, for Begin() to be valid to call."
            );
        }
        if (_commandBufferEnded)
        {
            _commandBufferEnded = false;

            if (_cb == VkCommandBuffer.NULL)
            {
                _cb = GetNextCommandBuffer();
            }
            else
            {
                VkResult resetResult = vkResetCommandBuffer(_cb, 0);
                CheckResult(resetResult);
            }

            if (_currentStagingInfo.IsValid)
            {
                RecycleStagingInfo(_currentStagingInfo);
            }
        }

        _currentStagingInfo = GetStagingResourceInfo();

        VkCommandBufferBeginInfo beginInfo = new()
        {
            sType = VkStructureType.VK_STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO,
            flags = VkCommandBufferUsageFlags.VK_COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT,
        };
        VkResult result = vkBeginCommandBuffer(_cb, &beginInfo);
        CheckResult(result);
        _commandBufferBegun = true;

        ClearCachedState();
        _currentFramebuffer = null;
        _currentGraphicsPipeline = null;
        ClearSets(_currentGraphicsResourceSets);
        Util.ClearArray(_scissorRects);

        _numVertexBindings = 0;
        Util.ClearArray(_vertexBindings);
        Util.ClearArray(_vertexOffsets);

        _currentComputePipeline = null;
        ClearSets(_currentComputeResourceSets);
    }

    private protected override void ClearColorTargetCore(uint index, RgbaFloat clearColor)
    {
        VkClearValue clearValue = new()
        {
            color = Unsafe.BitCast<RgbaFloat, VkClearColorValue>(clearColor),
        };

        if (_activeRenderPass != VkRenderPass.NULL)
        {
            VkClearAttachment clearAttachment = new()
            {
                colorAttachment = index,
                aspectMask = VkImageAspectFlags.VK_IMAGE_ASPECT_COLOR_BIT,
                clearValue = clearValue,
            };

            Texture colorTex = _currentFramebuffer!.ColorTargets[(int)index].Target;

            VkClearRect clearRect = new()
            {
                baseArrayLayer = 0,
                layerCount = 1,
                rect = new()
                {
                    offset = new(),
                    extent = new() { width = colorTex.Width, height = colorTex.Height },
                },
            };
            vkCmdClearAttachments(_cb, 1, &clearAttachment, 1, &clearRect);
        }
        else
        {
            // Queue up the clear value for the next RenderPass.
            _clearValues[index] = clearValue;
            _validColorClearValues[index] = true;
        }
    }

    private protected override void ClearDepthStencilCore(float depth, byte stencil)
    {
        VkClearValue clearValue = new()
        {
            depthStencil = new() { depth = depth, stencil = stencil },
        };

        if (_activeRenderPass != VkRenderPass.NULL)
        {
            VkImageAspectFlags aspect = FormatHelpers.IsStencilFormat(
                _currentFramebuffer!.DepthTarget!.Value.Target.Format
            )
                ? VkImageAspectFlags.VK_IMAGE_ASPECT_DEPTH_BIT
                    | VkImageAspectFlags.VK_IMAGE_ASPECT_STENCIL_BIT
                : VkImageAspectFlags.VK_IMAGE_ASPECT_DEPTH_BIT;
            VkClearAttachment clearAttachment = new()
            {
                aspectMask = aspect,
                clearValue = clearValue,
            };

            VkExtent2D renderableExtent = _currentFramebuffer.RenderableExtent;
            if (renderableExtent is { width: > 0, height: > 0 })
            {
                VkClearRect clearRect = new()
                {
                    baseArrayLayer = 0,
                    layerCount = 1,
                    rect = new() { offset = new(), extent = renderableExtent },
                };
                vkCmdClearAttachments(_cb, 1, &clearAttachment, 1, &clearRect);
            }
        }
        else
        {
            // Queue up the clear value for the next RenderPass.
            _depthClearValue = clearValue;
        }
    }

    private protected override void DrawCore(
        uint vertexCount,
        uint instanceCount,
        uint vertexStart,
        uint instanceStart
    )
    {
        PreDrawCommand();
        vkCmdDraw(_cb, vertexCount, instanceCount, vertexStart, instanceStart);
    }

    private protected override void DrawIndexedCore(
        uint indexCount,
        uint instanceCount,
        uint indexStart,
        int vertexOffset,
        uint instanceStart
    )
    {
        PreDrawCommand();
        vkCmdDrawIndexed(_cb, indexCount, instanceCount, indexStart, vertexOffset, instanceStart);
    }

    protected override void DrawIndirectCore(
        DeviceBuffer indirectBuffer,
        uint offset,
        uint drawCount,
        uint stride
    )
    {
        VkBuffer vkBuffer = Util.AssertSubtype<DeviceBuffer, VkBuffer>(indirectBuffer);
        _currentStagingInfo.AddResource(vkBuffer.RefCount);

        PreDrawCommand();
        vkCmdDrawIndirect(_cb, vkBuffer.DeviceBuffer, offset, drawCount, stride);
    }

    protected override void DrawIndexedIndirectCore(
        DeviceBuffer indirectBuffer,
        uint offset,
        uint drawCount,
        uint stride
    )
    {
        VkBuffer vkBuffer = Util.AssertSubtype<DeviceBuffer, VkBuffer>(indirectBuffer);
        _currentStagingInfo.AddResource(vkBuffer.RefCount);

        PreDrawCommand();
        vkCmdDrawIndexedIndirect(_cb, vkBuffer.DeviceBuffer, offset, drawCount, stride);
    }

    void PreDrawCommand()
    {
        if (_viewportsChanged)
        {
            _viewportsChanged = false;
            FlushViewports();
        }

        if (_scissorRectsChanged)
        {
            _scissorRectsChanged = false;
            FlushScissorRects();
        }

        if (_vertexBindingsChanged)
        {
            _vertexBindingsChanged = false;
            FlushVertexBindings();
        }

        EnsureRenderPassActive();

        FlushNewResourceSets(
            _currentGraphicsResourceSets,
            _graphicsResourceSetsChanged,
            _currentGraphicsPipeline!
        );
    }

    void FlushVertexBindings()
    {
        fixed (VulkanBuffer* vertexBindings = _vertexBindings)
        fixed (ulong* vertexOffsets = _vertexOffsets)
        {
            vkCmdBindVertexBuffers(_cb, 0, _numVertexBindings, vertexBindings, vertexOffsets);
        }
    }

    void FlushViewports()
    {
        uint count = _viewportCount;
        if (count > 1 && !_gd.Features.MultipleViewports)
        {
            count = 1;
        }

        fixed (VkViewport* viewports = _viewports)
        {
            vkCmdSetViewport(_cb, 0, count, viewports);
        }
    }

    void FlushScissorRects()
    {
        uint count = _viewportCount;
        if (count > 1 && !_gd.Features.MultipleViewports)
        {
            count = 1;
        }

        fixed (VkRect2D* scissorRects = _scissorRects)
        {
            vkCmdSetScissor(_cb, 0, count, scissorRects);
        }
    }

    void FlushNewResourceSets(
        BoundResourceSetInfo[] resourceSets,
        bool[] resourceSetsChanged,
        VkPipeline pipeline
    )
    {
        int resourceSetCount = (int)pipeline.ResourceSetCount;

        VkPipelineBindPoint bindPoint = pipeline.IsComputePipeline
            ? VkPipelineBindPoint.VK_PIPELINE_BIND_POINT_COMPUTE
            : VkPipelineBindPoint.VK_PIPELINE_BIND_POINT_GRAPHICS;

        VkDescriptorSet* descriptorSets = stackalloc VkDescriptorSet[resourceSetCount];
        uint* dynamicOffsets = stackalloc uint[pipeline.DynamicOffsetsCount];
        uint currentBatchCount = 0;
        uint currentBatchFirstSet = 0;
        uint currentBatchDynamicOffsetCount = 0;

        Span<BoundResourceSetInfo> sets = resourceSets.AsSpan(0, resourceSetCount);
        Span<bool> setsChanged = resourceSetsChanged.AsSpan(0, resourceSetCount);

        for (int currentSlot = 0; currentSlot < resourceSetCount; currentSlot++)
        {
            bool batchEnded = !setsChanged[currentSlot] || currentSlot == resourceSetCount - 1;

            if (setsChanged[currentSlot])
            {
                setsChanged[currentSlot] = false;
                ref BoundResourceSetInfo resourceSet = ref sets[currentSlot];
                VkResourceSet vkSet = Util.AssertSubtype<ResourceSet, VkResourceSet>(
                    resourceSet.Set
                );
                descriptorSets[currentBatchCount] = vkSet.DescriptorSet;
                currentBatchCount += 1;

                ref SmallFixedOrDynamicArray curSetOffsets = ref resourceSet.Offsets;
                for (uint i = 0; i < curSetOffsets.Count; i++)
                {
                    dynamicOffsets[currentBatchDynamicOffsetCount] = curSetOffsets.Get(i);
                    currentBatchDynamicOffsetCount += 1;
                }

                // Increment ref count on first use of a set.
                _currentStagingInfo.AddResource(vkSet.RefCount);
                for (int i = 0; i < vkSet.RefCounts.Count; i++)
                {
                    _currentStagingInfo.AddResource(vkSet.RefCounts[i]);
                }
            }

            if (batchEnded)
            {
                if (currentBatchCount != 0)
                {
                    // Flush current batch.
                    vkCmdBindDescriptorSets(
                        _cb,
                        bindPoint,
                        pipeline.PipelineLayout,
                        currentBatchFirstSet,
                        currentBatchCount,
                        descriptorSets,
                        currentBatchDynamicOffsetCount,
                        dynamicOffsets
                    );
                }

                currentBatchCount = 0;
                currentBatchFirstSet = (uint)(currentSlot + 1);
            }
        }
    }

    void TransitionImages(List<VkTexture> sampledTextures, VkImageLayout layout)
    {
        for (int i = 0; i < sampledTextures.Count; i++)
        {
            VkTexture tex = sampledTextures[i];
            tex.TransitionImageLayout(_cb, 0, tex.MipLevels, 0, tex.ActualArrayLayers, layout);
        }
    }

    public override void Dispatch(uint groupCountX, uint groupCountY, uint groupCountZ)
    {
        PreDispatchCommand();

        vkCmdDispatch(_cb, groupCountX, groupCountY, groupCountZ);
    }

    void PreDispatchCommand()
    {
        EnsureNoRenderPass();

        for (
            uint currentSlot = 0;
            currentSlot < _currentComputePipeline!.ResourceSetCount;
            currentSlot++
        )
        {
            VkResourceSet vkSet = Util.AssertSubtype<ResourceSet, VkResourceSet>(
                _currentComputeResourceSets[currentSlot].Set
            );

            TransitionImages(
                vkSet.SampledTextures,
                VkImageLayout.VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL
            );
            TransitionImages(vkSet.StorageTextures, VkImageLayout.VK_IMAGE_LAYOUT_GENERAL);

            for (int texIdx = 0; texIdx < vkSet.StorageTextures.Count; texIdx++)
            {
                VkTexture storageTex = vkSet.StorageTextures[texIdx];
                _dispatchStorageImages.Add(storageTex);
            }
        }

        FlushNewResourceSets(
            _currentComputeResourceSets,
            _computeResourceSetsChanged,
            _currentComputePipeline
        );
    }

    private protected override void DispatchIndirectCore(DeviceBuffer indirectBuffer, uint offset)
    {
        VkBuffer vkBuffer = Util.AssertSubtype<DeviceBuffer, VkBuffer>(indirectBuffer);
        _currentStagingInfo.AddResource(vkBuffer.RefCount);

        PreDispatchCommand();
        vkCmdDispatchIndirect(_cb, vkBuffer.DeviceBuffer, offset);
    }

    protected override void ResolveTextureCore(Texture source, Texture destination)
    {
        VkTexture vkSource = Util.AssertSubtype<Texture, VkTexture>(source);
        _currentStagingInfo.AddResource(vkSource.RefCount);
        VkTexture vkDestination = Util.AssertSubtype<Texture, VkTexture>(destination);
        _currentStagingInfo.AddResource(vkDestination.RefCount);

        EnsureNoRenderPass();

        VkImageAspectFlags aspectFlags =
            ((source.Usage & TextureUsage.DepthStencil) == TextureUsage.DepthStencil)
                ? VkImageAspectFlags.VK_IMAGE_ASPECT_DEPTH_BIT
                    | VkImageAspectFlags.VK_IMAGE_ASPECT_STENCIL_BIT
                : VkImageAspectFlags.VK_IMAGE_ASPECT_COLOR_BIT;
        VkImageResolve region = new()
        {
            extent = new()
            {
                width = source.Width,
                height = source.Height,
                depth = source.Depth,
            },
            srcSubresource = new() { layerCount = 1, aspectMask = aspectFlags },
            dstSubresource = new() { layerCount = 1, aspectMask = aspectFlags },
        };

        vkSource.TransitionImageLayout(
            _cb,
            0,
            1,
            0,
            1,
            VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL
        );
        vkDestination.TransitionImageLayout(
            _cb,
            0,
            1,
            0,
            1,
            VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL
        );

        vkCmdResolveImage(
            _cb,
            vkSource.OptimalDeviceImage,
            VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL,
            vkDestination.OptimalDeviceImage,
            VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
            1,
            &region
        );

        TransitionBackFromTransfer(_cb, vkSource, 0, 1, 0, 1);
        TransitionBackFromTransfer(_cb, vkDestination, 0, 1, 0, 1);
    }

    public override void End()
    {
        if (!_commandBufferBegun)
        {
            throw new VeldridException(
                "CommandBuffer must have been started before End() may be called."
            );
        }

        _commandBufferBegun = false;
        _commandBufferEnded = true;

        if (EnsureNoRenderPass())
        {
            _currentFramebuffer.TransitionToFinalLayout(_cb, false);
        }

        VkResult result = vkEndCommandBuffer(_cb);
        CheckResult(result);
    }

    protected override void SetFramebufferCore(Framebuffer fb)
    {
        VkFramebufferBase vkFB = Util.AssertSubtype<Framebuffer, VkFramebufferBase>(fb);
        _currentStagingInfo.AddResource(vkFB.RefCount);

        EnsureNoRenderPass();

        _currentFramebuffer?.TransitionToFinalLayout(_cb, false);

        _currentFramebuffer = vkFB;
        _currentFramebufferEverActive = false;
        _newFramebuffer = true;

        _viewportCount = Math.Max(1u, (uint)vkFB.ColorTargets.Length);
        Util.EnsureArrayMinimumSize(ref _viewports, _viewportCount);
        Util.ClearArray(_viewports);
        Util.EnsureArrayMinimumSize(ref _scissorRects, _viewportCount);
        Util.ClearArray(_scissorRects);

        uint clearValueCount = (uint)vkFB.ColorTargets.Length;
        Util.EnsureArrayMinimumSize(ref _clearValues, clearValueCount + 1); // Leave an extra space for the depth value (tracked separately).
        Util.ClearArray(_validColorClearValues);
        Util.EnsureArrayMinimumSize(ref _validColorClearValues, clearValueCount);
    }

    void EnsureRenderPassActive()
    {
        for (int i = 0; i < _dispatchStorageImages.Count; i++)
        {
            VkTexture tex = _dispatchStorageImages[i];
            VkImageLayout layout = GetTransitionBackLayout(tex.Usage);
            tex.TransitionImageLayout(_cb, 0, tex.MipLevels, 0, tex.ActualArrayLayers, layout);
        }
        _dispatchStorageImages.Clear();

        if (_activeRenderPass == VkRenderPass.NULL)
        {
            BeginCurrentRenderPass();
        }
    }

    [MemberNotNullWhen(true, nameof(_currentFramebuffer))]
    bool EnsureNoRenderPass()
    {
        if (_activeRenderPass != VkRenderPass.NULL)
        {
            Debug.Assert(_currentFramebufferEverActive);

            EndCurrentRenderPass();
            return true;
        }

        if (!_currentFramebufferEverActive && _currentFramebuffer != null)
        {
            // This forces any queued up texture clears to be emitted.
            EnsureRenderPassActive();
            EndCurrentRenderPass();
            return true;
        }

        return false;
    }

    void BeginCurrentRenderPass()
    {
        Debug.Assert(_activeRenderPass == VkRenderPass.NULL);
        Debug.Assert(_currentFramebuffer != null);
        _currentFramebufferEverActive = true;

        uint attachmentCount = _currentFramebuffer.AttachmentCount;
        int colorTargetCount = _currentFramebuffer.ColorTargets.Length;
        bool haveAnyAttachments = colorTargetCount > 0 || _currentFramebuffer.DepthTarget != null;
        bool haveAllClearValues =
            _depthClearValue.HasValue || _currentFramebuffer.DepthTarget == null;
        bool haveAnyClearValues = _depthClearValue.HasValue;
        for (int i = 0; i < colorTargetCount; i++)
        {
            if (!_validColorClearValues[i])
            {
                haveAllClearValues = false;
            }
            else
            {
                haveAnyClearValues = true;
            }
        }

        VkRenderPassBeginInfo renderPassBI = new()
        {
            sType = VkStructureType.VK_STRUCTURE_TYPE_RENDER_PASS_BEGIN_INFO,
            renderArea = new() { offset = new(), extent = _currentFramebuffer.RenderableExtent },
            framebuffer = _currentFramebuffer.CurrentFramebuffer,
        };

        if (!haveAnyAttachments || !haveAllClearValues)
        {
            _currentFramebuffer.TransitionToFinalLayout(_cb, !_newFramebuffer);

            renderPassBI.renderPass = _newFramebuffer
                ? _currentFramebuffer.RenderPassNoClear_Init
                : _currentFramebuffer.RenderPassNoClear_Load;
            vkCmdBeginRenderPass(_cb, &renderPassBI, VkSubpassContents.VK_SUBPASS_CONTENTS_INLINE);
            _activeRenderPass = renderPassBI.renderPass;

            if (haveAnyClearValues)
            {
                if (_depthClearValue.HasValue)
                {
                    VkClearDepthStencilValue depthStencil = _depthClearValue
                        .GetValueOrDefault()
                        .depthStencil;
                    ClearDepthStencilCore(depthStencil.depth, (byte)depthStencil.stencil);
                    _depthClearValue = null;
                }

                for (uint i = 0; i < colorTargetCount; i++)
                {
                    if (_validColorClearValues[i])
                    {
                        _validColorClearValues[i] = false;
                        VkClearValue vkClearValue = _clearValues[i];
                        RgbaFloat clearColor = Unsafe.BitCast<VkClearValue, RgbaFloat>(
                            vkClearValue
                        );
                        ClearColorTargetCore(i, clearColor);
                    }
                }
            }
        }
        else
        {
            _currentFramebuffer.TransitionToFinalLayout(_cb, true);

            // We have clear values for every attachment.
            renderPassBI.renderPass = _currentFramebuffer.RenderPassClear;
            fixed (VkClearValue* clearValuesPtr = _clearValues)
            {
                renderPassBI.clearValueCount = attachmentCount;
                renderPassBI.pClearValues = clearValuesPtr;
                if (_depthClearValue.HasValue)
                {
                    _clearValues[colorTargetCount] = _depthClearValue.GetValueOrDefault();
                    _depthClearValue = null;
                }
                vkCmdBeginRenderPass(
                    _cb,
                    &renderPassBI,
                    VkSubpassContents.VK_SUBPASS_CONTENTS_INLINE
                );
                _activeRenderPass = renderPassBI.renderPass;
                Util.ClearArray(_validColorClearValues);
            }
        }

        _newFramebuffer = false;
    }

    [MemberNotNull(nameof(_currentFramebuffer))]
    void EndCurrentRenderPass()
    {
        Debug.Assert(_activeRenderPass != VkRenderPass.NULL);
        vkCmdEndRenderPass(_cb);
        _currentFramebuffer!.TransitionToIntermediateLayout(_cb);
        _activeRenderPass = default;

        // Place a barrier between RenderPasses, so that color / depth outputs
        // can be read in subsequent passes.
        vkCmdPipelineBarrier(
            _cb,
            VkPipelineStageFlags.VK_PIPELINE_STAGE_BOTTOM_OF_PIPE_BIT,
            VkPipelineStageFlags.VK_PIPELINE_STAGE_TOP_OF_PIPE_BIT,
            0,
            0,
            null,
            0,
            null,
            0,
            null
        );
    }

    private protected override void SetVertexBufferCore(
        uint index,
        DeviceBuffer buffer,
        uint offset
    )
    {
        VkBuffer vkBuffer = Util.AssertSubtype<DeviceBuffer, VkBuffer>(buffer);

        bool differentBuffer = _vertexBindings[index] != vkBuffer.DeviceBuffer;
        if (differentBuffer || _vertexOffsets[index] != offset)
        {
            _vertexBindingsChanged = true;
            if (differentBuffer)
            {
                _currentStagingInfo.AddResource(vkBuffer.RefCount);
                _vertexBindings[index] = vkBuffer.DeviceBuffer;
            }

            _vertexOffsets[index] = offset;
            _numVertexBindings = Math.Max(index + 1, _numVertexBindings);
        }
    }

    private protected override void SetIndexBufferCore(
        DeviceBuffer buffer,
        IndexFormat format,
        uint offset
    )
    {
        VkBuffer vkBuffer = Util.AssertSubtype<DeviceBuffer, VkBuffer>(buffer);
        _currentStagingInfo.AddResource(vkBuffer.RefCount);

        vkCmdBindIndexBuffer(
            _cb,
            vkBuffer.DeviceBuffer,
            offset,
            VkFormats.VdToVkIndexFormat(format)
        );
    }

    private protected override void SetPipelineCore(Pipeline pipeline)
    {
        VkPipeline vkPipeline = Util.AssertSubtype<Pipeline, VkPipeline>(pipeline);
        _currentStagingInfo.AddResource(vkPipeline.RefCount);

        if (!pipeline.IsComputePipeline && _currentGraphicsPipeline != pipeline)
        {
            Util.EnsureArrayMinimumSize(
                ref _currentGraphicsResourceSets,
                vkPipeline.ResourceSetCount
            );
            ClearSets(_currentGraphicsResourceSets);
            Util.EnsureArrayMinimumSize(
                ref _graphicsResourceSetsChanged,
                vkPipeline.ResourceSetCount
            );
            vkCmdBindPipeline(
                _cb,
                VkPipelineBindPoint.VK_PIPELINE_BIND_POINT_GRAPHICS,
                vkPipeline.DevicePipeline
            );
            _currentGraphicsPipeline = vkPipeline;

            uint vertexBufferCount = vkPipeline.VertexLayoutCount;
            Util.EnsureArrayMinimumSize(ref _vertexBindings, vertexBufferCount);
            Util.EnsureArrayMinimumSize(ref _vertexOffsets, vertexBufferCount);
        }
        else if (pipeline.IsComputePipeline && _currentComputePipeline != pipeline)
        {
            Util.EnsureArrayMinimumSize(
                ref _currentComputeResourceSets,
                vkPipeline.ResourceSetCount
            );
            ClearSets(_currentComputeResourceSets);
            Util.EnsureArrayMinimumSize(
                ref _computeResourceSetsChanged,
                vkPipeline.ResourceSetCount
            );
            vkCmdBindPipeline(
                _cb,
                VkPipelineBindPoint.VK_PIPELINE_BIND_POINT_COMPUTE,
                vkPipeline.DevicePipeline
            );
            _currentComputePipeline = vkPipeline;
        }
    }

    static void ClearSets(Span<BoundResourceSetInfo> boundSets)
    {
        foreach (ref BoundResourceSetInfo boundSetInfo in boundSets)
        {
            boundSetInfo.Offsets.Dispose();
            boundSetInfo = default;
        }
    }

    private protected override void SetGraphicsResourceSetCore(
        uint slot,
        ResourceSet rs,
        ReadOnlySpan<uint> dynamicOffsets
    )
    {
        ref BoundResourceSetInfo set = ref _currentGraphicsResourceSets[slot];
        if (!set.Equals(rs, dynamicOffsets))
        {
            set.Offsets.Dispose();
            set = new(rs, dynamicOffsets);
            _graphicsResourceSetsChanged[slot] = true;
            Util.AssertSubtype<ResourceSet, VkResourceSet>(rs);
        }
    }

    protected override void SetComputeResourceSetCore(
        uint slot,
        ResourceSet rs,
        ReadOnlySpan<uint> dynamicOffsets
    )
    {
        ref BoundResourceSetInfo set = ref _currentComputeResourceSets[slot];
        if (!set.Equals(rs, dynamicOffsets))
        {
            set.Offsets.Dispose();
            set = new(rs, dynamicOffsets);
            _computeResourceSetsChanged[slot] = true;
            Util.AssertSubtype<ResourceSet, VkResourceSet>(rs);
        }
    }

    public override void SetScissorRect(uint index, uint x, uint y, uint width, uint height)
    {
        VkRect2D scissor = new()
        {
            offset = new() { x = (int)x, y = (int)y },
            extent = new() { width = width, height = height },
        };

        VkRect2D[] scissorRects = _scissorRects;
        if (
            scissorRects[index].offset.x != scissor.offset.x
            || scissorRects[index].offset.y != scissor.offset.y
            || scissorRects[index].extent.width != scissor.extent.width
            || scissorRects[index].extent.height != scissor.extent.height
        )
        {
            _scissorRectsChanged = true;
            scissorRects[index] = scissor;
        }
    }

    public override void SetViewport(uint index, in Viewport viewport)
    {
        bool yInverted = _gd.IsClipSpaceYInverted;
        float vpY = yInverted ? viewport.Y : viewport.Height + viewport.Y;
        float vpHeight = yInverted ? viewport.Height : -viewport.Height;

        _viewportsChanged = true;
        _viewports[index] = new()
        {
            x = viewport.X,
            y = vpY,
            width = viewport.Width,
            height = vpHeight,
            minDepth = viewport.MinDepth,
            maxDepth = viewport.MaxDepth,
        };
    }

    private protected override void UpdateBufferCore(
        DeviceBuffer buffer,
        uint bufferOffsetInBytes,
        IntPtr source,
        uint sizeInBytes
    )
    {
        VkBuffer stagingBuffer = _gd.GetPooledStagingBuffer(sizeInBytes);
        stagingBuffer.Name = _stagingBufferName;
        AddStagingResource(stagingBuffer);

        _gd.UpdateBuffer(stagingBuffer, 0, source, sizeInBytes);
        CopyBuffer(stagingBuffer, 0, buffer, bufferOffsetInBytes, sizeInBytes);
    }

    private protected override void CopyBufferCore(
        DeviceBuffer source,
        DeviceBuffer destination,
        ReadOnlySpan<BufferCopyCommand> commands
    )
    {
        VkBuffer srcVkBuffer = Util.AssertSubtype<DeviceBuffer, VkBuffer>(source);
        _currentStagingInfo.AddResource(srcVkBuffer.RefCount);
        VkBuffer dstVkBuffer = Util.AssertSubtype<DeviceBuffer, VkBuffer>(destination);
        _currentStagingInfo.AddResource(dstVkBuffer.RefCount);

        EnsureNoRenderPass();

        fixed (BufferCopyCommand* commandPtr = commands)
        {
            int offset = 0;
            int prevOffset = 0;

            while (offset < commands.Length)
            {
                if (commands[offset].Length != 0)
                {
                    offset++;
                    continue;
                }

                int count = offset - prevOffset;
                if (count > 0)
                {
                    vkCmdCopyBuffer(
                        _cb,
                        srcVkBuffer.DeviceBuffer,
                        dstVkBuffer.DeviceBuffer,
                        (uint)count,
                        (VkBufferCopy*)(commandPtr + prevOffset)
                    );
                }

                while (offset < commands.Length)
                {
                    if (commands[offset].Length != 0)
                    {
                        break;
                    }
                    offset++;
                }
                prevOffset = offset;
            }

            {
                int count = offset - prevOffset;
                if (count > 0)
                {
                    vkCmdCopyBuffer(
                        _cb,
                        srcVkBuffer.DeviceBuffer,
                        dstVkBuffer.DeviceBuffer,
                        (uint)count,
                        (VkBufferCopy*)(commandPtr + prevOffset)
                    );
                }
            }
        }

        VkMemoryBarrier barrier = new()
        {
            sType = VkStructureType.VK_STRUCTURE_TYPE_MEMORY_BARRIER,
            srcAccessMask = VkAccessFlags.VK_ACCESS_TRANSFER_WRITE_BIT,
            dstAccessMask = VkAccessFlags.VK_ACCESS_VERTEX_ATTRIBUTE_READ_BIT,
        };

        vkCmdPipelineBarrier(
            _cb,
            VkPipelineStageFlags.VK_PIPELINE_STAGE_TRANSFER_BIT,
            VkPipelineStageFlags.VK_PIPELINE_STAGE_VERTEX_INPUT_BIT,
            0,
            1,
            &barrier,
            0,
            null,
            0,
            null
        );
    }

    private protected override void CopyTextureCore(
        Texture source,
        uint srcX,
        uint srcY,
        uint srcZ,
        uint srcMipLevel,
        uint srcBaseArrayLayer,
        Texture destination,
        uint dstX,
        uint dstY,
        uint dstZ,
        uint dstMipLevel,
        uint dstBaseArrayLayer,
        uint width,
        uint height,
        uint depth,
        uint layerCount
    )
    {
        VkTexture srcVkTexture = Util.AssertSubtype<Texture, VkTexture>(source);
        _currentStagingInfo.AddResource(srcVkTexture.RefCount);
        VkTexture dstVkTexture = Util.AssertSubtype<Texture, VkTexture>(destination);
        _currentStagingInfo.AddResource(dstVkTexture.RefCount);

        EnsureNoRenderPass();

        CopyTextureCore_VkCommandBuffer(
            _cb,
            srcVkTexture,
            srcX,
            srcY,
            srcZ,
            srcMipLevel,
            srcBaseArrayLayer,
            dstVkTexture,
            dstX,
            dstY,
            dstZ,
            dstMipLevel,
            dstBaseArrayLayer,
            width,
            height,
            depth,
            layerCount
        );
    }

    internal static VkImageLayout GetTransitionBackLayout(TextureUsage usage)
    {
        if ((usage & TextureUsage.Sampled) != 0)
        {
            return VkImageLayout.VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL;
        }
        else if ((usage & TextureUsage.RenderTarget) != 0)
        {
            return (usage & TextureUsage.DepthStencil) != 0
                ? VkImageLayout.VK_IMAGE_LAYOUT_DEPTH_STENCIL_ATTACHMENT_OPTIMAL
                : VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL;
        }
        else
        {
            return VkImageLayout.VK_IMAGE_LAYOUT_GENERAL;
        }
    }

    internal static void TransitionBackFromTransfer(
        VkCommandBuffer cb,
        VkTexture texture,
        uint baseMipLevel,
        uint levelCount,
        uint baseArrayLayer,
        uint layerCount
    )
    {
        VkImageLayout layout = GetTransitionBackLayout(texture.Usage);

        texture.TransitionImageLayout(
            cb,
            baseMipLevel,
            levelCount,
            baseArrayLayer,
            layerCount,
            layout
        );
    }

    internal static void CopyTextureCore_VkCommandBuffer(
        VkCommandBuffer cb,
        VkTexture source,
        uint srcX,
        uint srcY,
        uint srcZ,
        uint srcMipLevel,
        uint srcBaseArrayLayer,
        VkTexture destination,
        uint dstX,
        uint dstY,
        uint dstZ,
        uint dstMipLevel,
        uint dstBaseArrayLayer,
        uint width,
        uint height,
        uint depth,
        uint layerCount
    )
    {
        VkTexture srcVkTexture = source;
        VkTexture dstVkTexture = destination;

        bool sourceIsStaging = (source.Usage & TextureUsage.Staging) == TextureUsage.Staging;
        bool destIsStaging = (destination.Usage & TextureUsage.Staging) == TextureUsage.Staging;

        if (!sourceIsStaging && !destIsStaging)
        {
            VkImageSubresourceLayers srcSubresource = new()
            {
                aspectMask = VkImageAspectFlags.VK_IMAGE_ASPECT_COLOR_BIT,
                layerCount = layerCount,
                mipLevel = srcMipLevel,
                baseArrayLayer = srcBaseArrayLayer,
            };

            VkImageSubresourceLayers dstSubresource = new()
            {
                aspectMask = VkImageAspectFlags.VK_IMAGE_ASPECT_COLOR_BIT,
                layerCount = layerCount,
                mipLevel = dstMipLevel,
                baseArrayLayer = dstBaseArrayLayer,
            };

            VkImageCopy region = new()
            {
                srcOffset = new()
                {
                    x = (int)srcX,
                    y = (int)srcY,
                    z = (int)srcZ,
                },
                dstOffset = new()
                {
                    x = (int)dstX,
                    y = (int)dstY,
                    z = (int)dstZ,
                },
                srcSubresource = srcSubresource,
                dstSubresource = dstSubresource,
                extent = new()
                {
                    width = width,
                    height = height,
                    depth = depth,
                },
            };

            srcVkTexture.TransitionImageLayout(
                cb,
                srcMipLevel,
                1,
                srcBaseArrayLayer,
                layerCount,
                VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL
            );

            dstVkTexture.TransitionImageLayout(
                cb,
                dstMipLevel,
                1,
                dstBaseArrayLayer,
                layerCount,
                VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL
            );

            vkCmdCopyImage(
                cb,
                srcVkTexture.OptimalDeviceImage,
                VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL,
                dstVkTexture.OptimalDeviceImage,
                VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
                1,
                &region
            );

            TransitionBackFromTransfer(
                cb,
                srcVkTexture,
                srcMipLevel,
                1,
                srcBaseArrayLayer,
                layerCount
            );

            TransitionBackFromTransfer(
                cb,
                dstVkTexture,
                dstMipLevel,
                1,
                dstBaseArrayLayer,
                layerCount
            );
        }
        else if (sourceIsStaging && !destIsStaging)
        {
            VulkanBuffer srcBuffer = srcVkTexture.StagingBuffer;
            VkSubresourceLayout srcLayout = srcVkTexture.GetSubresourceLayout(
                srcMipLevel,
                srcBaseArrayLayer
            );
            VkImage dstImage = dstVkTexture.OptimalDeviceImage;
            dstVkTexture.TransitionImageLayout(
                cb,
                dstMipLevel,
                1,
                dstBaseArrayLayer,
                layerCount,
                VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL
            );

            VkImageSubresourceLayers dstSubresource = new()
            {
                aspectMask = VkImageAspectFlags.VK_IMAGE_ASPECT_COLOR_BIT,
                layerCount = layerCount,
                mipLevel = dstMipLevel,
                baseArrayLayer = dstBaseArrayLayer,
            };

            Util.GetMipDimensions(
                srcVkTexture,
                srcMipLevel,
                out uint mipWidth,
                out uint mipHeight,
                out _
            );
            uint blockSize = FormatHelpers.IsCompressedFormat(srcVkTexture.Format) ? 4u : 1u;
            uint bufferRowLength = Math.Max(mipWidth, blockSize);
            uint bufferImageHeight = Math.Max(mipHeight, blockSize);
            uint compressedX = srcX / blockSize;
            uint compressedY = srcY / blockSize;
            uint blockSizeInBytes =
                blockSize == 1
                    ? FormatSizeHelpers.GetSizeInBytes(srcVkTexture.Format)
                    : FormatHelpers.GetBlockSizeInBytes(srcVkTexture.Format);
            uint rowPitch = FormatHelpers.GetRowPitch(bufferRowLength, srcVkTexture.Format);
            uint depthPitch = FormatHelpers.GetDepthPitch(
                rowPitch,
                bufferImageHeight,
                srcVkTexture.Format
            );

            uint copyWidth = Math.Min(width, mipWidth);
            uint copyheight = Math.Min(height, mipHeight);

            VkBufferImageCopy regions = new()
            {
                bufferOffset =
                    srcLayout.offset
                    + (srcZ * depthPitch)
                    + (compressedY * rowPitch)
                    + (compressedX * blockSizeInBytes),
                bufferRowLength = bufferRowLength,
                bufferImageHeight = bufferImageHeight,
                imageExtent = new()
                {
                    width = copyWidth,
                    height = copyheight,
                    depth = depth,
                },
                imageOffset = new()
                {
                    x = (int)dstX,
                    y = (int)dstY,
                    z = (int)dstZ,
                },
                imageSubresource = dstSubresource,
            };

            vkCmdCopyBufferToImage(
                cb,
                srcBuffer,
                dstImage,
                VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
                1,
                &regions
            );

            TransitionBackFromTransfer(
                cb,
                dstVkTexture,
                dstMipLevel,
                1,
                dstBaseArrayLayer,
                layerCount
            );
        }
        else if (!sourceIsStaging && destIsStaging)
        {
            VkImage srcImage = srcVkTexture.OptimalDeviceImage;
            srcVkTexture.TransitionImageLayout(
                cb,
                srcMipLevel,
                1,
                srcBaseArrayLayer,
                layerCount,
                VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL
            );

            VulkanBuffer dstBuffer = dstVkTexture.StagingBuffer;

            VkImageAspectFlags srcAspect =
                (srcVkTexture.Usage & TextureUsage.DepthStencil) != 0
                    ? VkImageAspectFlags.VK_IMAGE_ASPECT_DEPTH_BIT
                    : VkImageAspectFlags.VK_IMAGE_ASPECT_COLOR_BIT;

            Util.GetMipDimensions(dstVkTexture, dstMipLevel, out uint mipWidth, out uint mipHeight);
            uint blockSize = FormatHelpers.IsCompressedFormat(dstVkTexture.Format) ? 4u : 1u;
            uint bufferRowLength = Math.Max(mipWidth, blockSize);
            uint bufferImageHeight = Math.Max(mipHeight, blockSize);
            uint compressedDstX = dstX / blockSize;
            uint compressedDstY = dstY / blockSize;
            uint blockSizeInBytes =
                blockSize == 1
                    ? FormatSizeHelpers.GetSizeInBytes(dstVkTexture.Format)
                    : FormatHelpers.GetBlockSizeInBytes(dstVkTexture.Format);
            uint rowPitch = FormatHelpers.GetRowPitch(bufferRowLength, dstVkTexture.Format);
            uint depthPitch = FormatHelpers.GetDepthPitch(
                rowPitch,
                bufferImageHeight,
                dstVkTexture.Format
            );

            VkBufferImageCopy* layers = stackalloc VkBufferImageCopy[(int)layerCount];
            for (uint layer = 0; layer < layerCount; layer++)
            {
                VkSubresourceLayout dstLayout = dstVkTexture.GetSubresourceLayout(
                    dstMipLevel,
                    dstBaseArrayLayer + layer
                );

                VkImageSubresourceLayers srcSubresource = new()
                {
                    aspectMask = srcAspect,
                    layerCount = 1,
                    mipLevel = srcMipLevel,
                    baseArrayLayer = srcBaseArrayLayer + layer,
                };

                VkBufferImageCopy region = new()
                {
                    bufferRowLength = bufferRowLength,
                    bufferImageHeight = bufferImageHeight,
                    bufferOffset =
                        dstLayout.offset
                        + (dstZ * depthPitch)
                        + (compressedDstY * rowPitch)
                        + (compressedDstX * blockSizeInBytes),
                    imageExtent = new()
                    {
                        width = width,
                        height = height,
                        depth = depth,
                    },
                    imageOffset = new()
                    {
                        x = (int)srcX,
                        y = (int)srcY,
                        z = (int)srcZ,
                    },
                    imageSubresource = srcSubresource,
                };

                layers[layer] = region;
            }

            vkCmdCopyImageToBuffer(
                cb,
                srcImage,
                VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL,
                dstBuffer,
                layerCount,
                layers
            );

            TransitionBackFromTransfer(
                cb,
                srcVkTexture,
                srcMipLevel,
                1,
                srcBaseArrayLayer,
                layerCount
            );
        }
        else
        {
            Debug.Assert(sourceIsStaging && destIsStaging);
            VulkanBuffer srcBuffer = srcVkTexture.StagingBuffer;
            VkSubresourceLayout srcLayout = srcVkTexture.GetSubresourceLayout(
                srcMipLevel,
                srcBaseArrayLayer
            );
            VulkanBuffer dstBuffer = dstVkTexture.StagingBuffer;
            VkSubresourceLayout dstLayout = dstVkTexture.GetSubresourceLayout(
                dstMipLevel,
                dstBaseArrayLayer
            );

            uint zLimit = Math.Max(depth, layerCount);
            if (!FormatHelpers.IsCompressedFormat(source.Format))
            {
                // TODO: batch BufferCopy

                uint pixelSize = FormatSizeHelpers.GetSizeInBytes(srcVkTexture.Format);
                for (uint zz = 0; zz < zLimit; zz++)
                {
                    for (uint yy = 0; yy < height; yy++)
                    {
                        VkBufferCopy region = new()
                        {
                            srcOffset =
                                srcLayout.offset
                                + srcLayout.depthPitch * (zz + srcZ)
                                + srcLayout.rowPitch * (yy + srcY)
                                + pixelSize * srcX,
                            dstOffset =
                                dstLayout.offset
                                + dstLayout.depthPitch * (zz + dstZ)
                                + dstLayout.rowPitch * (yy + dstY)
                                + pixelSize * dstX,
                            size = width * pixelSize,
                        };
                        vkCmdCopyBuffer(cb, srcBuffer, dstBuffer, 1, &region);
                    }
                }
            }
            else // IsCompressedFormat
            {
                uint denseRowSize = FormatHelpers.GetRowPitch(width, source.Format);
                uint numRows = FormatHelpers.GetNumRows(height, source.Format);
                uint compressedSrcX = srcX / 4;
                uint compressedSrcY = srcY / 4;
                uint compressedDstX = dstX / 4;
                uint compressedDstY = dstY / 4;
                uint blockSizeInBytes = FormatHelpers.GetBlockSizeInBytes(source.Format);

                // TODO: batch BufferCopy

                for (uint zz = 0; zz < zLimit; zz++)
                {
                    for (uint row = 0; row < numRows; row++)
                    {
                        VkBufferCopy region = new()
                        {
                            srcOffset =
                                srcLayout.offset
                                + srcLayout.depthPitch * (zz + srcZ)
                                + srcLayout.rowPitch * (row + compressedSrcY)
                                + blockSizeInBytes * compressedSrcX,
                            dstOffset =
                                dstLayout.offset
                                + dstLayout.depthPitch * (zz + dstZ)
                                + dstLayout.rowPitch * (row + compressedDstY)
                                + blockSizeInBytes * compressedDstX,
                            size = denseRowSize,
                        };
                        vkCmdCopyBuffer(cb, srcBuffer, dstBuffer, 1, &region);
                    }
                }
            }
        }
    }

    private protected override void GenerateMipmapsCore(Texture texture)
    {
        VkTexture vkTex = Util.AssertSubtype<Texture, VkTexture>(texture);
        _currentStagingInfo.AddResource(vkTex.RefCount);

        EnsureNoRenderPass();

        uint layerCount = vkTex.ActualArrayLayers;

        VkImageBlit region;

        uint width = vkTex.Width;
        uint height = vkTex.Height;
        uint depth = vkTex.Depth;
        for (uint level = 1; level < vkTex.MipLevels; level++)
        {
            vkTex.TransitionImageLayoutNonmatching(
                _cb,
                level - 1,
                1,
                0,
                layerCount,
                VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL
            );
            vkTex.TransitionImageLayoutNonmatching(
                _cb,
                level,
                1,
                0,
                layerCount,
                VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL
            );

            VkImage deviceImage = vkTex.OptimalDeviceImage;
            uint mipWidth = Math.Max(width >> 1, 1);
            uint mipHeight = Math.Max(height >> 1, 1);
            uint mipDepth = Math.Max(depth >> 1, 1);

            region.srcSubresource = new()
            {
                aspectMask = VkImageAspectFlags.VK_IMAGE_ASPECT_COLOR_BIT,
                baseArrayLayer = 0,
                layerCount = layerCount,
                mipLevel = level - 1,
            };
            region.srcOffsets[0] = new();
            region.srcOffsets[1] = new()
            {
                x = (int)width,
                y = (int)height,
                z = (int)depth,
            };
            region.dstOffsets[0] = new();

            region.dstSubresource = new()
            {
                aspectMask = VkImageAspectFlags.VK_IMAGE_ASPECT_COLOR_BIT,
                baseArrayLayer = 0,
                layerCount = layerCount,
                mipLevel = level,
            };

            region.dstOffsets[1] = new()
            {
                x = (int)mipWidth,
                y = (int)mipHeight,
                z = (int)mipDepth,
            };
            vkCmdBlitImage(
                _cb,
                deviceImage,
                VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL,
                deviceImage,
                VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
                1,
                &region,
                _gd.GetFormatFilter(vkTex.VkFormat)
            );

            width = mipWidth;
            height = mipHeight;
            depth = mipDepth;
        }

        VkImageLayout layout = GetTransitionBackLayout(vkTex.Usage);
        vkTex.TransitionImageLayoutNonmatching(_cb, 0, vkTex.MipLevels, 0, layerCount, layout);
    }

    /// <summary>
    /// Adds a staging buffer to the current recording.
    /// </summary>
    /// <param name="buffer">The buffer resource to add.</param>
    internal void AddStagingResource(VkBuffer buffer)
    {
        _currentStagingInfo.BuffersUsed.Add(buffer);
    }

    /// <summary>
    /// Adds a staging texture to the current recording.
    /// </summary>
    /// <param name="texture">The texture resource to add.</param>
    internal void AddStagingResource(VkTexture texture)
    {
        _currentStagingInfo.TexturesUsed.Add(texture);
    }

    internal void ClearColorTexture(VkTexture texture, VkClearColorValue color)
    {
        _currentStagingInfo.AddResource(texture.RefCount);

        uint effectiveLayers = texture.ActualArrayLayers;

        VkImageSubresourceRange range = new()
        {
            aspectMask = VkImageAspectFlags.VK_IMAGE_ASPECT_COLOR_BIT,
            baseMipLevel = 0,
            levelCount = texture.MipLevels,
            baseArrayLayer = 0,
            layerCount = effectiveLayers,
        };

        texture.TransitionImageLayout(
            _cb,
            0,
            texture.MipLevels,
            0,
            effectiveLayers,
            VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL
        );
        vkCmdClearColorImage(
            _cb,
            texture.OptimalDeviceImage,
            VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
            &color,
            1,
            &range
        );
        VkImageLayout colorLayout = texture.IsSwapchainTexture
            ? VkImageLayout.VK_IMAGE_LAYOUT_PRESENT_SRC_KHR
            : VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL;
        texture.TransitionImageLayout(_cb, 0, texture.MipLevels, 0, effectiveLayers, colorLayout);
    }

    internal void ClearDepthTexture(VkTexture texture, VkClearDepthStencilValue clearValue)
    {
        _currentStagingInfo.AddResource(texture.RefCount);

        uint effectiveLayers = texture.ActualArrayLayers;

        VkImageAspectFlags aspect = FormatHelpers.IsStencilFormat(texture.Format)
            ? VkImageAspectFlags.VK_IMAGE_ASPECT_DEPTH_BIT
                | VkImageAspectFlags.VK_IMAGE_ASPECT_STENCIL_BIT
            : VkImageAspectFlags.VK_IMAGE_ASPECT_DEPTH_BIT;

        VkImageSubresourceRange range = new()
        {
            aspectMask = aspect,
            baseMipLevel = 0,
            levelCount = texture.MipLevels,
            baseArrayLayer = 0,
            layerCount = effectiveLayers,
        };

        texture.TransitionImageLayout(
            _cb,
            0,
            texture.MipLevels,
            0,
            effectiveLayers,
            VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL
        );
        vkCmdClearDepthStencilImage(
            _cb,
            texture.OptimalDeviceImage,
            VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
            &clearValue,
            1,
            &range
        );
        texture.TransitionImageLayout(
            _cb,
            0,
            texture.MipLevels,
            0,
            effectiveLayers,
            VkImageLayout.VK_IMAGE_LAYOUT_DEPTH_STENCIL_ATTACHMENT_OPTIMAL
        );
    }

    internal void TransitionImageLayout(VkTexture texture, VkImageLayout layout)
    {
        _currentStagingInfo.AddResource(texture.RefCount);

        texture.TransitionImageLayout(
            _cb,
            0,
            texture.MipLevels,
            0,
            texture.ActualArrayLayers,
            layout
        );
    }

    [Conditional("DEBUG")]
    void DebugFullPipelineBarrier()
    {
        VkMemoryBarrier memoryBarrier = new()
        {
            sType = VkStructureType.VK_STRUCTURE_TYPE_MEMORY_BARRIER,
            srcAccessMask =
                VkAccessFlags.VK_ACCESS_INDIRECT_COMMAND_READ_BIT
                | VkAccessFlags.VK_ACCESS_INDEX_READ_BIT
                | VkAccessFlags.VK_ACCESS_VERTEX_ATTRIBUTE_READ_BIT
                | VkAccessFlags.VK_ACCESS_UNIFORM_READ_BIT
                | VkAccessFlags.VK_ACCESS_INPUT_ATTACHMENT_READ_BIT
                | VkAccessFlags.VK_ACCESS_SHADER_READ_BIT
                | VkAccessFlags.VK_ACCESS_SHADER_WRITE_BIT
                | VkAccessFlags.VK_ACCESS_COLOR_ATTACHMENT_READ_BIT
                | VkAccessFlags.VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT
                | VkAccessFlags.VK_ACCESS_DEPTH_STENCIL_ATTACHMENT_READ_BIT
                | VkAccessFlags.VK_ACCESS_DEPTH_STENCIL_ATTACHMENT_WRITE_BIT
                | VkAccessFlags.VK_ACCESS_TRANSFER_READ_BIT
                | VkAccessFlags.VK_ACCESS_TRANSFER_WRITE_BIT
                | VkAccessFlags.VK_ACCESS_HOST_READ_BIT
                | VkAccessFlags.VK_ACCESS_HOST_WRITE_BIT,
            dstAccessMask =
                VkAccessFlags.VK_ACCESS_INDIRECT_COMMAND_READ_BIT
                | VkAccessFlags.VK_ACCESS_INDEX_READ_BIT
                | VkAccessFlags.VK_ACCESS_VERTEX_ATTRIBUTE_READ_BIT
                | VkAccessFlags.VK_ACCESS_UNIFORM_READ_BIT
                | VkAccessFlags.VK_ACCESS_INPUT_ATTACHMENT_READ_BIT
                | VkAccessFlags.VK_ACCESS_SHADER_READ_BIT
                | VkAccessFlags.VK_ACCESS_SHADER_WRITE_BIT
                | VkAccessFlags.VK_ACCESS_COLOR_ATTACHMENT_READ_BIT
                | VkAccessFlags.VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT
                | VkAccessFlags.VK_ACCESS_DEPTH_STENCIL_ATTACHMENT_READ_BIT
                | VkAccessFlags.VK_ACCESS_DEPTH_STENCIL_ATTACHMENT_WRITE_BIT
                | VkAccessFlags.VK_ACCESS_TRANSFER_READ_BIT
                | VkAccessFlags.VK_ACCESS_TRANSFER_WRITE_BIT
                | VkAccessFlags.VK_ACCESS_HOST_READ_BIT
                | VkAccessFlags.VK_ACCESS_HOST_WRITE_BIT,
        };

        vkCmdPipelineBarrier(
            _cb,
            VkPipelineStageFlags.VK_PIPELINE_STAGE_ALL_COMMANDS_BIT, // srcStageMask
            VkPipelineStageFlags.VK_PIPELINE_STAGE_ALL_COMMANDS_BIT, // dstStageMask
            0,
            1, // memoryBarrierCount
            &memoryBarrier, // pMemoryBarriers
            0,
            null,
            0,
            null
        );
    }

    public override string? Name
    {
        get => _name;
        set
        {
            _name = value;
            _stagingBufferName = $"Staging Buffer (CommandList {_name})";

            if (_gd.DebugMarkerEnabled)
            {
                SetDebugMarkerName(_name);
            }
        }
    }

    [SkipLocalsInit]
    void SetDebugMarkerName(string? name)
    {
        void SetName(VkCommandBuffer cb, ReadOnlySpan<byte> nameUtf8)
        {
            _gd.SetDebugMarkerName(
                VkDebugReportObjectTypeEXT.VK_DEBUG_REPORT_OBJECT_TYPE_COMMAND_BUFFER_EXT,
                (ulong)cb.Value,
                nameUtf8
            );
        }

        Span<byte> utf8Buffer = stackalloc byte[1024];
        Util.GetNullTerminatedUtf8(name, ref utf8Buffer);

        lock (_commandBufferListLock)
        {
            foreach (VkCommandBuffer cb in _submittedCommandBuffers)
            {
                SetName(cb, utf8Buffer);
            }
            foreach (VkCommandBuffer cb in _availableCommandBuffers)
            {
                SetName(cb, utf8Buffer);
            }
        }

        VkCommandBuffer currentCb = _cb;
        if (currentCb != VkCommandBuffer.NULL)
        {
            SetName(currentCb, utf8Buffer);
        }

        _gd.SetDebugMarkerName(
            VkDebugReportObjectTypeEXT.VK_DEBUG_REPORT_OBJECT_TYPE_COMMAND_POOL_EXT,
            _pool.Value,
            utf8Buffer
        );
    }

    [SkipLocalsInit]
    private protected override void PushDebugGroupCore(ReadOnlySpan<char> name)
    {
        Span<byte> byteBuffer = stackalloc byte[1024];

        vkCmdDebugMarkerBeginEXT_t? func = _gd.MarkerBegin;
        if (func == null)
        {
            return;
        }

        Util.GetNullTerminatedUtf8(name, ref byteBuffer);
        fixed (byte* utf8Ptr = byteBuffer)
        {
            VkDebugMarkerMarkerInfoEXT markerInfo = new()
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_DEBUG_MARKER_MARKER_INFO_EXT,
                pMarkerName = (sbyte*)utf8Ptr,
            };
            func(_cb, &markerInfo);
        }
    }

    private protected override void PopDebugGroupCore()
    {
        vkCmdDebugMarkerEndEXT_t? func = _gd.MarkerEnd;

        func?.Invoke(_cb);
    }

    [SkipLocalsInit]
    private protected override void InsertDebugMarkerCore(ReadOnlySpan<char> name)
    {
        Span<byte> byteBuffer = stackalloc byte[1024];

        vkCmdDebugMarkerInsertEXT_t? func = _gd.MarkerInsert;
        if (func == null)
        {
            return;
        }

        Util.GetNullTerminatedUtf8(name, ref byteBuffer);
        fixed (byte* utf8Ptr = byteBuffer)
        {
            VkDebugMarkerMarkerInfoEXT markerInfo = new()
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_DEBUG_MARKER_MARKER_INFO_EXT,
                pMarkerName = (sbyte*)utf8Ptr,
            };
            func(_cb, &markerInfo);
        }
    }

    public override void Dispose()
    {
        RefCount.DecrementDispose();
    }

    void IResourceRefCountTarget.RefZeroed()
    {
        vkDestroyCommandPool(_gd.Device, _pool, null);

        Debug.Assert(_submittedStagingInfos.Count == 0);

        if (_currentStagingInfo.IsValid)
        {
            RecycleStagingInfo(_currentStagingInfo);
            _currentStagingInfo = default;
        }
    }

    internal readonly struct StagingResourceInfo()
    {
        public List<VkBuffer> BuffersUsed { get; } = [];
        public List<VkTexture> TexturesUsed { get; } = [];
        public HashSet<ResourceRefCount> Resources { get; } = [];

        public bool IsValid => Resources != null;

        public void AddResource(ResourceRefCount count)
        {
            if (Resources.Add(count))
            {
                count.Increment();
            }
        }

        public void Clear()
        {
            BuffersUsed.Clear();
            TexturesUsed.Clear();
            Resources.Clear();
        }
    }

    StagingResourceInfo GetStagingResourceInfo()
    {
        if (!_availableStagingInfos.TryDequeue(out StagingResourceInfo ret))
        {
            ret = new();
        }
        return ret;
    }

    internal void RecycleStagingInfo(StagingResourceInfo info)
    {
        if (info.BuffersUsed.Count > 0)
        {
            _gd.ReturnPooledStagingBuffers(CollectionsMarshal.AsSpan(info.BuffersUsed));
        }

        if (info.TexturesUsed.Count > 0)
        {
            _gd.ReturnPooledStagingTextures(CollectionsMarshal.AsSpan(info.TexturesUsed));
        }

        foreach (ResourceRefCount rrc in info.Resources)
        {
            rrc.Decrement();
        }

        info.Clear();

        _availableStagingInfos.Enqueue(info);
    }

    [DoesNotReturn]
    static void ThrowUnreachableStateException()
    {
        throw new("Implementation reached unexpected condition.");
    }
}
