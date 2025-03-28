using System;
using System.Diagnostics;
using Veldrid.MetalBindings;
using Veldrid.SPIRV;

namespace Veldrid.MTL;

internal sealed unsafe class MTLCommandList(MTLGraphicsDevice gd)
    : CommandList(
        gd.Features,
        gd.UniformBufferMinOffsetAlignment,
        gd.StructuredBufferMinOffsetAlignment
    )
{
    MTLCommandBuffer _cb;
    MTLFramebufferBase? _mtlFramebuffer;
    uint _viewportCount;
    bool _currentFramebufferEverActive;
    MTLRenderCommandEncoder _rce;
    MTLBlitCommandEncoder _bce;
    MTLComputeCommandEncoder _cce;
    RgbaFloat?[] _clearColors = [];
    (float depth, byte stencil)? _clearDepth;
    MTLBuffer? _indexBuffer;
    uint _ibOffset;
    MTLIndexType _indexType;
    MTLPipeline? _graphicsPipeline;
    bool _graphicsPipelineChanged;
    MTLPipeline? _computePipeline;
    bool _computePipelineChanged;
    MTLViewport[] _viewports = [];
    bool _viewportsChanged;
    MTLScissorRect[] _scissorRects = [];
    bool _scissorRectsChanged;
    uint _graphicsResourceSetCount;
    BoundResourceSetInfo[] _graphicsResourceSets = [];
    bool[] _graphicsResourceSetsActive = [];
    uint _computeResourceSetCount;
    BoundResourceSetInfo[] _computeResourceSets = [];
    bool[] _computeResourceSetsActive = [];
    uint _vertexBufferCount;
    uint _nonVertexBufferCount;
    MTLBuffer[] _vertexBuffers = [];
    uint[] _vbOffsets = [];
    bool[] _vertexBuffersActive = [];
    bool _disposed;

    public MTLCommandBuffer CommandBuffer => _cb;

    public override string? Name { get; set; }

    public override bool IsDisposed => _disposed;

    public MTLCommandBuffer Commit()
    {
        _cb.commit();
        MTLCommandBuffer ret = _cb;
        _cb = default;
        return ret;
    }

    public override void Begin()
    {
        if (_cb.NativePtr != IntPtr.Zero)
        {
            ObjectiveCRuntime.release(_cb.NativePtr);
        }

        using (NSAutoreleasePool.Begin())
        {
            _cb = gd.CommandQueue.commandBuffer();
            ObjectiveCRuntime.retain(_cb.NativePtr);
        }

        ClearCachedState();
    }

    private protected override void ClearColorTargetCore(uint index, RgbaFloat clearColor)
    {
        EnsureNoRenderPass();
        _clearColors[index] = clearColor;
    }

    private protected override void ClearDepthStencilCore(float depth, byte stencil)
    {
        EnsureNoRenderPass();
        _clearDepth = (depth, stencil);
    }

    public override void Dispatch(uint groupCountX, uint groupCountY, uint groupCountZ)
    {
        PreComputeCommand();
        _cce.dispatchThreadGroups(
            new(groupCountX, groupCountY, groupCountZ),
            _computePipeline!.ThreadsPerThreadgroup
        );
    }

    private protected override void DrawCore(
        uint vertexCount,
        uint instanceCount,
        uint vertexStart,
        uint instanceStart
    )
    {
        if (!PreDrawCommand())
            return;

        if (instanceStart == 0)
        {
            _rce.drawPrimitives(
                _graphicsPipeline!.PrimitiveType,
                vertexStart,
                vertexCount,
                instanceCount
            );
        }
        else
        {
            _rce.drawPrimitives(
                _graphicsPipeline!.PrimitiveType,
                vertexStart,
                vertexCount,
                instanceCount,
                instanceStart
            );
        }
    }

    private protected override void DrawIndexedCore(
        uint indexCount,
        uint instanceCount,
        uint indexStart,
        int vertexOffset,
        uint instanceStart
    )
    {
        if (!PreDrawCommand())
            return;

        uint indexSize = _indexType == MTLIndexType.UInt16 ? 2u : 4u;
        uint indexBufferOffset = (indexSize * indexStart) + _ibOffset;

        if (vertexOffset == 0 && instanceStart == 0)
        {
            _rce.drawIndexedPrimitives(
                _graphicsPipeline!.PrimitiveType,
                indexCount,
                _indexType,
                _indexBuffer!.DeviceBuffer,
                indexBufferOffset,
                instanceCount
            );
        }
        else
        {
            _rce.drawIndexedPrimitives(
                _graphicsPipeline!.PrimitiveType,
                indexCount,
                _indexType,
                _indexBuffer!.DeviceBuffer,
                indexBufferOffset,
                instanceCount,
                vertexOffset,
                instanceStart
            );
        }
    }

    bool PreDrawCommand()
    {
        if (!EnsureRenderPass())
            return false;

        if (_viewportsChanged)
        {
            FlushViewports();
            _viewportsChanged = false;
        }

        if (_scissorRectsChanged && _graphicsPipeline!.ScissorTestEnabled)
        {
            FlushScissorRects();
            _scissorRectsChanged = false;
        }

        if (_graphicsPipelineChanged)
        {
            Debug.Assert(_graphicsPipeline != null);
            _rce.setRenderPipelineState(_graphicsPipeline.RenderPipelineState);
            _rce.setCullMode(_graphicsPipeline.CullMode);
            _rce.setFrontFacing(_graphicsPipeline.FrontFace);
            _rce.setTriangleFillMode(_graphicsPipeline.FillMode);
            RgbaFloat blendColor = _graphicsPipeline.BlendColor;
            _rce.setBlendColor(blendColor.R, blendColor.G, blendColor.B, blendColor.A);
            if (Framebuffer!.DepthTarget != null)
            {
                _rce.setDepthStencilState(_graphicsPipeline.DepthStencilState);
                _rce.setDepthClipMode(_graphicsPipeline.DepthClipMode);
                _rce.setStencilReferenceValue(_graphicsPipeline.StencilReference);
            }
        }

        int graphicsSetCount = (int)_graphicsResourceSetCount;
        Span<BoundResourceSetInfo> graphicsSets = _graphicsResourceSets.AsSpan(0, graphicsSetCount);
        Span<bool> graphicsSetsActive = _graphicsResourceSetsActive.AsSpan(0, graphicsSetCount);
        for (int i = 0; i < graphicsSetCount; i++)
        {
            if (!graphicsSetsActive[i])
            {
                ActivateGraphicsResourceSet((uint)i, ref graphicsSets[i]);
                graphicsSetsActive[i] = true;
            }
        }

        for (uint i = 0; i < _vertexBufferCount; i++)
        {
            if (!_vertexBuffersActive[i])
            {
                UIntPtr index =
                    _graphicsPipeline!.ResourceBindingModel == ResourceBindingModel.Improved
                        ? _nonVertexBufferCount + i
                        : i;
                _rce.setVertexBuffer(_vertexBuffers[i].DeviceBuffer, _vbOffsets[i], index);
            }
        }

        return true;
    }

    void FlushViewports()
    {
        if (gd.MetalFeatures.IsSupported(MTLFeatureSet.macOS_GPUFamily1_v3))
        {
            fixed (MTLViewport* viewportsPtr = &_viewports[0])
            {
                _rce.setViewports(viewportsPtr, _viewportCount);
            }
        }
        else
        {
            _rce.setViewport(_viewports[0]);
        }
    }

    void FlushScissorRects()
    {
        if (gd.MetalFeatures.IsSupported(MTLFeatureSet.macOS_GPUFamily1_v3))
        {
            fixed (MTLScissorRect* scissorRectsPtr = &_scissorRects[0])
            {
                _rce.setScissorRects(scissorRectsPtr, _viewportCount);
            }
        }
        else
        {
            _rce.setScissorRect(_scissorRects[0]);
        }
    }

    void PreComputeCommand()
    {
        EnsureComputeEncoder();
        if (_computePipelineChanged)
        {
            _cce.setComputePipelineState(_computePipeline!.ComputePipelineState);
        }

        int computeSetCount = (int)_computeResourceSetCount;
        Span<BoundResourceSetInfo> computeSets = _computeResourceSets.AsSpan(0, computeSetCount);
        Span<bool> computeSetsActive = _computeResourceSetsActive.AsSpan(0, computeSetCount);
        for (int i = 0; i < computeSetCount; i++)
        {
            if (!computeSetsActive[i])
            {
                ActivateComputeResourceSet((uint)i, ref computeSets[i]);
                computeSetsActive[i] = true;
            }
        }
    }

    public override void End()
    {
        EnsureNoBlitEncoder();
        EnsureNoComputeEncoder();

        if (!_currentFramebufferEverActive && _mtlFramebuffer != null)
        {
            BeginCurrentRenderPass();
        }
        EnsureNoRenderPass();
    }

    private protected override void SetPipelineCore(Pipeline pipeline)
    {
        if (pipeline.IsComputePipeline)
        {
            _computePipeline = Util.AssertSubtype<Pipeline, MTLPipeline>(pipeline);
            _computeResourceSetCount = (uint)_computePipeline.ResourceLayouts.Length;
            Util.EnsureArrayMinimumSize(ref _computeResourceSets, _computeResourceSetCount);
            Util.EnsureArrayMinimumSize(ref _computeResourceSetsActive, _computeResourceSetCount);
            Util.ClearArray(_computeResourceSetsActive);
            _computePipelineChanged = true;
        }
        else
        {
            _graphicsPipeline = Util.AssertSubtype<Pipeline, MTLPipeline>(pipeline);
            _graphicsResourceSetCount = (uint)_graphicsPipeline.ResourceLayouts.Length;
            Util.EnsureArrayMinimumSize(ref _graphicsResourceSets, _graphicsResourceSetCount);
            Util.EnsureArrayMinimumSize(ref _graphicsResourceSetsActive, _graphicsResourceSetCount);
            Util.ClearArray(_graphicsResourceSetsActive);

            _nonVertexBufferCount = _graphicsPipeline.NonVertexBufferCount;

            _vertexBufferCount = _graphicsPipeline.VertexBufferCount;
            Util.EnsureArrayMinimumSize(ref _vertexBuffers, _vertexBufferCount);
            Util.EnsureArrayMinimumSize(ref _vbOffsets, _vertexBufferCount);
            Util.EnsureArrayMinimumSize(ref _vertexBuffersActive, _vertexBufferCount);
            Util.ClearArray(_vertexBuffersActive);

            _graphicsPipelineChanged = true;
        }
    }

    public override void SetScissorRect(uint index, uint x, uint y, uint width, uint height)
    {
        _scissorRectsChanged = true;
        _scissorRects[index] = new(x, y, width, height);
    }

    public override void SetViewport(uint index, in Viewport viewport)
    {
        _viewportsChanged = true;
        _viewports[index] = new(
            viewport.X,
            viewport.Y,
            viewport.Width,
            viewport.Height,
            viewport.MinDepth,
            viewport.MaxDepth
        );
    }

    private protected override void UpdateBufferCore(
        DeviceBuffer buffer,
        uint bufferOffsetInBytes,
        IntPtr source,
        uint sizeInBytes
    )
    {
        bool useComputeCopy =
            (bufferOffsetInBytes % 4 != 0)
            || (
                sizeInBytes % 4 != 0
                && bufferOffsetInBytes != 0
                && sizeInBytes != buffer.SizeInBytes
            );

        MTLBuffer dstMtlBuffer = Util.AssertSubtype<DeviceBuffer, MTLBuffer>(buffer);

        // TODO: Cache these, and rely on the command buffer's completion callback to add them back to a shared pool.
        using MTLBuffer copySrc = Util.AssertSubtype<DeviceBuffer, MTLBuffer>(
            gd.ResourceFactory.CreateBuffer(new(sizeInBytes, BufferUsage.StagingWrite))
        );

        gd.UpdateBuffer(copySrc, 0, source, sizeInBytes);

        if (useComputeCopy)
        {
            BufferCopyCommand command = new(0, bufferOffsetInBytes, sizeInBytes);
            CopyBufferUnaligned(copySrc, dstMtlBuffer, [command]);
        }
        else
        {
            Debug.Assert(bufferOffsetInBytes % 4 == 0);
            uint sizeRoundFactor = (4 - (sizeInBytes % 4)) % 4;
            EnsureBlitEncoder();
            _bce.copy(
                copySrc.DeviceBuffer,
                UIntPtr.Zero,
                dstMtlBuffer.DeviceBuffer,
                bufferOffsetInBytes,
                sizeInBytes + sizeRoundFactor
            );
        }
    }

    private protected override void CopyBufferCore(
        DeviceBuffer source,
        DeviceBuffer destination,
        ReadOnlySpan<BufferCopyCommand> commands
    )
    {
        MTLBuffer mtlSrc = Util.AssertSubtype<DeviceBuffer, MTLBuffer>(source);
        MTLBuffer mtlDst = Util.AssertSubtype<DeviceBuffer, MTLBuffer>(destination);

        bool useComputeCopy = false;

        foreach (ref readonly BufferCopyCommand command in commands)
        {
            if (
                command.ReadOffset % 4 != 0
                || command.WriteOffset % 4 != 0
                || command.Length % 4 != 0
            )
            {
                useComputeCopy = true;
                break;
            }
        }

        if (useComputeCopy)
        {
            CopyBufferUnaligned(mtlSrc, mtlDst, commands);
        }
        else
        {
            EnsureBlitEncoder();

            foreach (ref readonly BufferCopyCommand command in commands)
            {
                if (command.Length == 0)
                {
                    continue;
                }

                _bce.copy(
                    mtlSrc.DeviceBuffer,
                    (UIntPtr)command.ReadOffset,
                    mtlDst.DeviceBuffer,
                    (UIntPtr)command.WriteOffset,
                    (UIntPtr)command.Length
                );
            }
        }
    }

    void CopyBufferUnaligned(
        MTLBuffer mtlSrc,
        MTLBuffer mtlDst,
        ReadOnlySpan<BufferCopyCommand> commands
    )
    {
        // Unaligned copy -- use special compute shader.
        EnsureComputeEncoder();
        _cce.setComputePipelineState(gd.GetUnalignedBufferCopyPipeline());
        _cce.setBuffer(mtlSrc.DeviceBuffer, UIntPtr.Zero, 0);
        _cce.setBuffer(mtlDst.DeviceBuffer, UIntPtr.Zero, 1);

        foreach (ref readonly BufferCopyCommand command in commands)
        {
            if (command.Length == 0)
            {
                continue;
            }

            MTLUnalignedBufferCopyInfo copyInfo;
            copyInfo.SourceOffset = (uint)command.ReadOffset;
            copyInfo.DestinationOffset = (uint)command.WriteOffset;
            copyInfo.CopySize = (uint)command.Length;

            _cce.setBytes(&copyInfo, (UIntPtr)sizeof(MTLUnalignedBufferCopyInfo), 2);
            _cce.dispatchThreadGroups(new(1, 1, 1), new(1, 1, 1));
        }
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
        EnsureBlitEncoder();
        MTLTexture srcMtlTexture = Util.AssertSubtype<Texture, MTLTexture>(source);
        MTLTexture dstMtlTexture = Util.AssertSubtype<Texture, MTLTexture>(destination);

        bool srcIsStaging = (source.Usage & TextureUsage.Staging) != 0;
        bool dstIsStaging = (destination.Usage & TextureUsage.Staging) != 0;
        if (srcIsStaging && !dstIsStaging)
        {
            // Staging -> Normal
            MetalBindings.MTLBuffer srcBuffer = srcMtlTexture.StagingBuffer;
            MetalBindings.MTLTexture dstTexture = dstMtlTexture.DeviceTexture;

            Util.GetMipDimensions(
                srcMtlTexture,
                srcMipLevel,
                out uint mipWidth,
                out uint mipHeight,
                out _
            );
            for (uint layer = 0; layer < layerCount; layer++)
            {
                uint blockSize = FormatHelpers.IsCompressedFormat(srcMtlTexture.Format) ? 4u : 1u;
                uint compressedSrcX = srcX / blockSize;
                uint compressedSrcY = srcY / blockSize;
                uint blockSizeInBytes =
                    blockSize == 1
                        ? FormatSizeHelpers.GetSizeInBytes(srcMtlTexture.Format)
                        : FormatHelpers.GetBlockSizeInBytes(srcMtlTexture.Format);

                ulong srcSubresourceBase = Util.ComputeSubresourceOffset(
                    srcMtlTexture,
                    srcMipLevel,
                    layer + srcBaseArrayLayer
                );
                srcMtlTexture.GetSubresourceLayout(
                    srcMipLevel,
                    srcBaseArrayLayer + layer,
                    out uint srcRowPitch,
                    out uint srcDepthPitch
                );
                ulong sourceOffset =
                    srcSubresourceBase
                    + srcDepthPitch * srcZ
                    + srcRowPitch * compressedSrcY
                    + blockSizeInBytes * compressedSrcX;

                uint copyWidth = width > mipWidth && width <= blockSize ? mipWidth : width;

                uint copyHeight = height > mipHeight && height <= blockSize ? mipHeight : height;

                MTLSize sourceSize = new(copyWidth, copyHeight, depth);
                if (dstMtlTexture.Type != TextureType.Texture3D)
                {
                    srcDepthPitch = 0;
                }
                _bce.copyFromBuffer(
                    srcBuffer,
                    (UIntPtr)sourceOffset,
                    srcRowPitch,
                    srcDepthPitch,
                    sourceSize,
                    dstTexture,
                    dstBaseArrayLayer + layer,
                    dstMipLevel,
                    new(dstX, dstY, dstZ)
                );
            }
        }
        else if (srcIsStaging && dstIsStaging)
        {
            for (uint layer = 0; layer < layerCount; layer++)
            {
                // Staging -> Staging
                ulong srcSubresourceBase = Util.ComputeSubresourceOffset(
                    srcMtlTexture,
                    srcMipLevel,
                    layer + srcBaseArrayLayer
                );
                srcMtlTexture.GetSubresourceLayout(
                    srcMipLevel,
                    srcBaseArrayLayer + layer,
                    out uint srcRowPitch,
                    out uint srcDepthPitch
                );

                ulong dstSubresourceBase = Util.ComputeSubresourceOffset(
                    dstMtlTexture,
                    dstMipLevel,
                    layer + dstBaseArrayLayer
                );
                dstMtlTexture.GetSubresourceLayout(
                    dstMipLevel,
                    dstBaseArrayLayer + layer,
                    out uint dstRowPitch,
                    out uint dstDepthPitch
                );

                uint blockSize = FormatHelpers.IsCompressedFormat(dstMtlTexture.Format) ? 4u : 1u;
                if (blockSize == 1)
                {
                    uint pixelSize = FormatSizeHelpers.GetSizeInBytes(dstMtlTexture.Format);
                    uint copySize = width * pixelSize;
                    for (uint zz = 0; zz < depth; zz++)
                    for (uint yy = 0; yy < height; yy++)
                    {
                        ulong srcRowOffset =
                            srcSubresourceBase
                            + srcDepthPitch * (zz + srcZ)
                            + srcRowPitch * (yy + srcY)
                            + pixelSize * srcX;
                        ulong dstRowOffset =
                            dstSubresourceBase
                            + dstDepthPitch * (zz + dstZ)
                            + dstRowPitch * (yy + dstY)
                            + pixelSize * dstX;
                        _bce.copy(
                            srcMtlTexture.StagingBuffer,
                            (UIntPtr)srcRowOffset,
                            dstMtlTexture.StagingBuffer,
                            (UIntPtr)dstRowOffset,
                            copySize
                        );
                    }
                }
                else // blockSize != 1
                {
                    uint paddedWidth = Math.Max(blockSize, width);
                    uint paddedHeight = Math.Max(blockSize, height);
                    uint numRows = FormatHelpers.GetNumRows(paddedHeight, srcMtlTexture.Format);
                    uint rowPitch = FormatHelpers.GetRowPitch(paddedWidth, srcMtlTexture.Format);

                    uint compressedSrcX = srcX / 4;
                    uint compressedSrcY = srcY / 4;
                    uint compressedDstX = dstX / 4;
                    uint compressedDstY = dstY / 4;
                    uint blockSizeInBytes = FormatHelpers.GetBlockSizeInBytes(srcMtlTexture.Format);

                    for (uint zz = 0; zz < depth; zz++)
                    for (uint row = 0; row < numRows; row++)
                    {
                        ulong srcRowOffset =
                            srcSubresourceBase
                            + srcDepthPitch * (zz + srcZ)
                            + srcRowPitch * (row + compressedSrcY)
                            + blockSizeInBytes * compressedSrcX;
                        ulong dstRowOffset =
                            dstSubresourceBase
                            + dstDepthPitch * (zz + dstZ)
                            + dstRowPitch * (row + compressedDstY)
                            + blockSizeInBytes * compressedDstX;
                        _bce.copy(
                            srcMtlTexture.StagingBuffer,
                            (UIntPtr)srcRowOffset,
                            dstMtlTexture.StagingBuffer,
                            (UIntPtr)dstRowOffset,
                            rowPitch
                        );
                    }
                }
            }
        }
        else if (!srcIsStaging && dstIsStaging)
        {
            // Normal -> Staging
            MTLOrigin srcOrigin = new(srcX, srcY, srcZ);
            MTLSize srcSize = new(width, height, depth);
            for (uint layer = 0; layer < layerCount; layer++)
            {
                dstMtlTexture.GetSubresourceLayout(
                    dstMipLevel,
                    dstBaseArrayLayer + layer,
                    out uint dstBytesPerRow,
                    out uint dstBytesPerImage
                );

                Util.GetMipDimensions(
                    srcMtlTexture,
                    dstMipLevel,
                    out uint mipWidth,
                    out uint mipHeight,
                    out _
                );
                uint blockSize = FormatHelpers.IsCompressedFormat(srcMtlTexture.Format) ? 4u : 1u;
                uint bufferRowLength = Math.Max(mipWidth, blockSize);
                uint bufferImageHeight = Math.Max(mipHeight, blockSize);
                uint compressedDstX = dstX / blockSize;
                uint compressedDstY = dstY / blockSize;
                uint blockSizeInBytes =
                    blockSize == 1
                        ? FormatSizeHelpers.GetSizeInBytes(srcMtlTexture.Format)
                        : FormatHelpers.GetBlockSizeInBytes(srcMtlTexture.Format);
                uint rowPitch = FormatHelpers.GetRowPitch(bufferRowLength, srcMtlTexture.Format);
                uint depthPitch = FormatHelpers.GetDepthPitch(
                    rowPitch,
                    bufferImageHeight,
                    srcMtlTexture.Format
                );

                ulong dstOffset =
                    Util.ComputeSubresourceOffset(
                        dstMtlTexture,
                        dstMipLevel,
                        dstBaseArrayLayer + layer
                    )
                    + (dstZ * depthPitch)
                    + (compressedDstY * rowPitch)
                    + (compressedDstX * blockSizeInBytes);

                _bce.copyTextureToBuffer(
                    srcMtlTexture.DeviceTexture,
                    srcBaseArrayLayer + layer,
                    srcMipLevel,
                    srcOrigin,
                    srcSize,
                    dstMtlTexture.StagingBuffer,
                    (UIntPtr)dstOffset,
                    dstBytesPerRow,
                    dstBytesPerImage
                );
            }
        }
        else
        {
            // Normal -> Normal
            for (uint layer = 0; layer < layerCount; layer++)
            {
                _bce.copyFromTexture(
                    srcMtlTexture.DeviceTexture,
                    srcBaseArrayLayer + layer,
                    srcMipLevel,
                    new(srcX, srcY, srcZ),
                    new(width, height, depth),
                    dstMtlTexture.DeviceTexture,
                    dstBaseArrayLayer + layer,
                    dstMipLevel,
                    new(dstX, dstY, dstZ)
                );
            }
        }
    }

    private protected override void GenerateMipmapsCore(Texture texture)
    {
        Debug.Assert(texture.MipLevels > 1);
        EnsureBlitEncoder();
        MTLTexture mtlTex = Util.AssertSubtype<Texture, MTLTexture>(texture);
        _bce.generateMipmapsForTexture(mtlTex.DeviceTexture);
    }

    private protected override void DispatchIndirectCore(DeviceBuffer indirectBuffer, uint offset)
    {
        MTLBuffer mtlBuffer = Util.AssertSubtype<DeviceBuffer, MTLBuffer>(indirectBuffer);
        PreComputeCommand();
        _cce.dispatchThreadgroupsWithIndirectBuffer(
            mtlBuffer.DeviceBuffer,
            offset,
            _computePipeline!.ThreadsPerThreadgroup
        );
    }

    protected override void DrawIndexedIndirectCore(
        DeviceBuffer indirectBuffer,
        uint offset,
        uint drawCount,
        uint stride
    )
    {
        if (!PreDrawCommand())
            return;

        MTLBuffer mtlBuffer = Util.AssertSubtype<DeviceBuffer, MTLBuffer>(indirectBuffer);
        for (uint i = 0; i < drawCount; i++)
        {
            uint currentOffset = i * stride + offset;
            _rce.drawIndexedPrimitives(
                _graphicsPipeline!.PrimitiveType,
                _indexType,
                _indexBuffer!.DeviceBuffer,
                _ibOffset,
                mtlBuffer.DeviceBuffer,
                currentOffset
            );
        }
    }

    protected override void DrawIndirectCore(
        DeviceBuffer indirectBuffer,
        uint offset,
        uint drawCount,
        uint stride
    )
    {
        if (!PreDrawCommand())
            return;

        MTLBuffer mtlBuffer = Util.AssertSubtype<DeviceBuffer, MTLBuffer>(indirectBuffer);
        for (uint i = 0; i < drawCount; i++)
        {
            uint currentOffset = i * stride + offset;
            _rce.drawPrimitives(
                _graphicsPipeline!.PrimitiveType,
                mtlBuffer.DeviceBuffer,
                currentOffset
            );
        }
    }

    protected override void ResolveTextureCore(Texture source, Texture destination)
    {
        // TODO: This approach destroys the contents of the source Texture (according to the docs).
        EnsureNoBlitEncoder();
        EnsureNoRenderPass();

        MTLTexture mtlSrc = Util.AssertSubtype<Texture, MTLTexture>(source);
        MTLTexture mtlDst = Util.AssertSubtype<Texture, MTLTexture>(destination);

        MTLRenderPassDescriptor rpDesc = MTLRenderPassDescriptor.New();
        MTLRenderPassColorAttachmentDescriptor colorAttachment = rpDesc.colorAttachments[0];
        colorAttachment.texture = mtlSrc.DeviceTexture;
        colorAttachment.loadAction = MTLLoadAction.Load;
        colorAttachment.storeAction = MTLStoreAction.MultisampleResolve;
        colorAttachment.resolveTexture = mtlDst.DeviceTexture;

        using (NSAutoreleasePool.Begin())
        {
            MTLRenderCommandEncoder encoder = _cb.renderCommandEncoderWithDescriptor(rpDesc);
            encoder.endEncoding();
        }

        ObjectiveCRuntime.release(rpDesc.NativePtr);
    }

    protected override void SetComputeResourceSetCore(
        uint slot,
        ResourceSet rs,
        ReadOnlySpan<uint> dynamicOffsets
    )
    {
        ref BoundResourceSetInfo set = ref _computeResourceSets[slot];
        if (!set.Equals(rs, dynamicOffsets))
        {
            set.Offsets.Dispose();
            set = new(rs, dynamicOffsets);
            _computeResourceSetsActive[slot] = false;
        }
    }

    protected override void SetFramebufferCore(Framebuffer fb)
    {
        if (!_currentFramebufferEverActive && _mtlFramebuffer != null)
        {
            // This ensures that any submitted clear values will be used even if nothing has been drawn.
            if (EnsureRenderPass())
            {
                EndCurrentRenderPass();
            }
        }

        EnsureNoRenderPass();
        _mtlFramebuffer = Util.AssertSubtype<Framebuffer, MTLFramebufferBase>(fb);
        _viewportCount = Math.Max(1u, (uint)fb.ColorTargets.Length);
        Util.EnsureArrayMinimumSize(ref _viewports, _viewportCount);
        Util.ClearArray(_viewports);
        Util.EnsureArrayMinimumSize(ref _scissorRects, _viewportCount);
        Util.ClearArray(_scissorRects);
        Util.EnsureArrayMinimumSize(ref _clearColors, (uint)fb.ColorTargets.Length);
        Util.ClearArray(_clearColors);
        _currentFramebufferEverActive = false;
    }

    private protected override void SetGraphicsResourceSetCore(
        uint slot,
        ResourceSet rs,
        ReadOnlySpan<uint> dynamicOffsets
    )
    {
        ref BoundResourceSetInfo set = ref _graphicsResourceSets[slot];
        if (!set.Equals(rs, dynamicOffsets))
        {
            set.Offsets.Dispose();
            set = new(rs, dynamicOffsets);
            _graphicsResourceSetsActive[slot] = false;
        }
    }

    void ActivateGraphicsResourceSet(uint slot, ref BoundResourceSetInfo brsi)
    {
        Debug.Assert(RenderEncoderActive);
        MTLResourceSet mtlRS = Util.AssertSubtype<ResourceSet, MTLResourceSet>(brsi.Set);
        MTLResourceLayout layout = mtlRS.Layout;
        uint dynamicOffsetIndex = 0;

        for (int i = 0; i < mtlRS.Resources.Length; i++)
        {
            MTLResourceLayout.ResourceBindingInfo bindingInfo = layout.GetBindingInfo(i);
            BindableResource resource = mtlRS.Resources[i];
            uint bufferOffset = 0;
            if (bindingInfo.DynamicBuffer)
            {
                bufferOffset = brsi.Offsets.Get(dynamicOffsetIndex);
                dynamicOffsetIndex += 1;
            }
            switch (bindingInfo.Kind)
            {
                case ResourceKind.UniformBuffer:
                {
                    DeviceBufferRange range = Util.GetBufferRange(resource, bufferOffset);
                    BindBuffer(range, slot, bindingInfo.Slot, bindingInfo.Stages);
                    break;
                }
                case ResourceKind.TextureReadOnly:
                    TextureView texView = Util.GetTextureView(gd, resource);
                    MTLTextureView mtlTexView = Util.AssertSubtype<TextureView, MTLTextureView>(
                        texView
                    );
                    BindTexture(mtlTexView, slot, bindingInfo.Slot, bindingInfo.Stages);
                    break;
                case ResourceKind.TextureReadWrite:
                    TextureView texViewRW = Util.GetTextureView(gd, resource);
                    MTLTextureView mtlTexViewRW = Util.AssertSubtype<TextureView, MTLTextureView>(
                        texViewRW
                    );
                    BindTexture(mtlTexViewRW, slot, bindingInfo.Slot, bindingInfo.Stages);
                    break;
                case ResourceKind.Sampler:
                    MTLSampler mtlSampler = Util.AssertSubtype<Sampler, MTLSampler>(
                        resource.GetSampler()
                    );
                    BindSampler(mtlSampler, slot, bindingInfo.Slot, bindingInfo.Stages);
                    break;
                case ResourceKind.StructuredBufferReadOnly:
                {
                    DeviceBufferRange range = Util.GetBufferRange(resource, bufferOffset);
                    BindBuffer(range, slot, bindingInfo.Slot, bindingInfo.Stages);
                    break;
                }
                case ResourceKind.StructuredBufferReadWrite:
                {
                    DeviceBufferRange range = Util.GetBufferRange(resource, bufferOffset);
                    BindBuffer(range, slot, bindingInfo.Slot, bindingInfo.Stages);
                    break;
                }
                default:
                    Illegal.Value<ResourceKind>();
                    break;
            }
        }
    }

    void ActivateComputeResourceSet(uint slot, ref BoundResourceSetInfo brsi)
    {
        Debug.Assert(ComputeEncoderActive);
        MTLResourceSet mtlRS = Util.AssertSubtype<ResourceSet, MTLResourceSet>(brsi.Set);
        MTLResourceLayout layout = mtlRS.Layout;
        uint dynamicOffsetIndex = 0;

        for (int i = 0; i < mtlRS.Resources.Length; i++)
        {
            MTLResourceLayout.ResourceBindingInfo bindingInfo = layout.GetBindingInfo(i);
            BindableResource resource = mtlRS.Resources[i];
            uint bufferOffset = 0;
            if (bindingInfo.DynamicBuffer)
            {
                bufferOffset = brsi.Offsets.Get(dynamicOffsetIndex);
                dynamicOffsetIndex += 1;
            }

            switch (bindingInfo.Kind)
            {
                case ResourceKind.UniformBuffer:
                {
                    DeviceBufferRange range = Util.GetBufferRange(resource, bufferOffset);
                    BindBuffer(range, slot, bindingInfo.Slot, bindingInfo.Stages);
                    break;
                }
                case ResourceKind.TextureReadOnly:
                    TextureView texView = Util.GetTextureView(gd, resource);
                    MTLTextureView mtlTexView = Util.AssertSubtype<TextureView, MTLTextureView>(
                        texView
                    );
                    BindTexture(mtlTexView, slot, bindingInfo.Slot, bindingInfo.Stages);
                    break;
                case ResourceKind.TextureReadWrite:
                    TextureView texViewRW = Util.GetTextureView(gd, resource);
                    MTLTextureView mtlTexViewRW = Util.AssertSubtype<TextureView, MTLTextureView>(
                        texViewRW
                    );
                    BindTexture(mtlTexViewRW, slot, bindingInfo.Slot, bindingInfo.Stages);
                    break;
                case ResourceKind.Sampler:
                    MTLSampler mtlSampler = Util.AssertSubtype<Sampler, MTLSampler>(
                        resource.GetSampler()
                    );
                    BindSampler(mtlSampler, slot, bindingInfo.Slot, bindingInfo.Stages);
                    break;
                case ResourceKind.StructuredBufferReadOnly:
                {
                    DeviceBufferRange range = Util.GetBufferRange(resource, bufferOffset);
                    BindBuffer(range, slot, bindingInfo.Slot, bindingInfo.Stages);
                    break;
                }
                case ResourceKind.StructuredBufferReadWrite:
                {
                    DeviceBufferRange range = Util.GetBufferRange(resource, bufferOffset);
                    BindBuffer(range, slot, bindingInfo.Slot, bindingInfo.Stages);
                    break;
                }
                default:
                    Illegal.Value<ResourceKind>();
                    break;
            }
        }
    }

    void BindBuffer(DeviceBufferRange range, uint set, uint slot, ShaderStages stages)
    {
        MTLBuffer mtlBuffer = Util.AssertSubtype<DeviceBuffer, MTLBuffer>(range.Buffer);
        uint baseBuffer = GetBufferBase(set, stages != ShaderStages.Compute);
        if (stages == ShaderStages.Compute)
        {
            _cce.setBuffer(mtlBuffer.DeviceBuffer, range.Offset, slot + baseBuffer);
        }
        else
        {
            if ((stages & ShaderStages.Vertex) == ShaderStages.Vertex)
            {
                UIntPtr index =
                    _graphicsPipeline!.ResourceBindingModel == ResourceBindingModel.Improved
                        ? slot + baseBuffer
                        : slot + _vertexBufferCount + baseBuffer;
                _rce.setVertexBuffer(mtlBuffer.DeviceBuffer, range.Offset, index);
            }
            if ((stages & ShaderStages.Fragment) == ShaderStages.Fragment)
            {
                _rce.setFragmentBuffer(mtlBuffer.DeviceBuffer, range.Offset, slot + baseBuffer);
            }
        }
    }

    void BindTexture(MTLTextureView mtlTexView, uint set, uint slot, ShaderStages stages)
    {
        uint baseTexture = GetTextureBase(set, stages != ShaderStages.Compute);
        if (stages == ShaderStages.Compute)
        {
            _cce.setTexture(mtlTexView.TargetDeviceTexture, slot + baseTexture);
        }
        if ((stages & ShaderStages.Vertex) == ShaderStages.Vertex)
        {
            _rce.setVertexTexture(mtlTexView.TargetDeviceTexture, slot + baseTexture);
        }
        if ((stages & ShaderStages.Fragment) == ShaderStages.Fragment)
        {
            _rce.setFragmentTexture(mtlTexView.TargetDeviceTexture, slot + baseTexture);
        }
    }

    void BindSampler(MTLSampler mtlSampler, uint set, uint slot, ShaderStages stages)
    {
        uint baseSampler = GetSamplerBase(set, stages != ShaderStages.Compute);
        if (stages == ShaderStages.Compute)
        {
            _cce.setSamplerState(mtlSampler.DeviceSampler, slot + baseSampler);
        }
        if ((stages & ShaderStages.Vertex) == ShaderStages.Vertex)
        {
            _rce.setVertexSamplerState(mtlSampler.DeviceSampler, slot + baseSampler);
        }
        if ((stages & ShaderStages.Fragment) == ShaderStages.Fragment)
        {
            _rce.setFragmentSamplerState(mtlSampler.DeviceSampler, slot + baseSampler);
        }
    }

    uint GetBufferBase(uint set, bool graphics)
    {
        MTLResourceLayout[] layouts = graphics
            ? _graphicsPipeline!.ResourceLayouts
            : _computePipeline!.ResourceLayouts;
        uint ret = 0;
        for (int i = 0; i < set; i++)
        {
            Debug.Assert(layouts[i] != null);
            ret += layouts[i].BufferCount;
        }

        return ret;
    }

    uint GetTextureBase(uint set, bool graphics)
    {
        MTLResourceLayout[] layouts = graphics
            ? _graphicsPipeline!.ResourceLayouts
            : _computePipeline!.ResourceLayouts;
        uint ret = 0;
        for (int i = 0; i < set; i++)
        {
            Debug.Assert(layouts[i] != null);
            ret += layouts[i].TextureCount;
        }

        return ret;
    }

    uint GetSamplerBase(uint set, bool graphics)
    {
        MTLResourceLayout[] layouts = graphics
            ? _graphicsPipeline!.ResourceLayouts
            : _computePipeline!.ResourceLayouts;
        uint ret = 0;
        for (int i = 0; i < set; i++)
        {
            Debug.Assert(layouts[i] != null);
            ret += layouts[i].SamplerCount;
        }

        return ret;
    }

    bool EnsureRenderPass()
    {
        Debug.Assert(_mtlFramebuffer != null);
        EnsureNoBlitEncoder();
        EnsureNoComputeEncoder();
        return RenderEncoderActive || BeginCurrentRenderPass();
    }

    bool RenderEncoderActive => !_rce.IsNull;
    bool BlitEncoderActive => !_bce.IsNull;
    bool ComputeEncoderActive => !_cce.IsNull;

    bool BeginCurrentRenderPass()
    {
        Debug.Assert(_mtlFramebuffer != null);
        if (!_mtlFramebuffer.IsRenderable)
        {
            return false;
        }

        MTLRenderPassDescriptor rpDesc = _mtlFramebuffer.CreateRenderPassDescriptor();
        for (uint i = 0; i < _clearColors.Length; i++)
        {
            RgbaFloat? clearColor = _clearColors[i];
            if (clearColor.HasValue)
            {
                MTLRenderPassColorAttachmentDescriptor attachment = rpDesc.colorAttachments[0];
                attachment.loadAction = MTLLoadAction.Clear;
                RgbaFloat c = clearColor.GetValueOrDefault();
                attachment.clearColor = new(c.R, c.G, c.B, c.A);
                _clearColors[i] = null;
            }
        }

        if (_clearDepth != null)
        {
            MTLRenderPassDepthAttachmentDescriptor depthAttachment = rpDesc.depthAttachment;
            depthAttachment.loadAction = MTLLoadAction.Clear;
            depthAttachment.clearDepth = _clearDepth.GetValueOrDefault().depth;

            if (FormatHelpers.IsStencilFormat(_mtlFramebuffer.DepthTarget!.Value.Target.Format))
            {
                MTLRenderPassStencilAttachmentDescriptor stencilAttachment =
                    rpDesc.stencilAttachment;
                stencilAttachment.loadAction = MTLLoadAction.Clear;
                stencilAttachment.clearStencil = _clearDepth.GetValueOrDefault().stencil;
            }

            _clearDepth = null;
        }

        using (NSAutoreleasePool.Begin())
        {
            _rce = _cb.renderCommandEncoderWithDescriptor(rpDesc);
            ObjectiveCRuntime.retain(_rce.NativePtr);
        }

        ObjectiveCRuntime.release(rpDesc.NativePtr);
        _currentFramebufferEverActive = true;

        return true;
    }

    void EnsureNoRenderPass()
    {
        if (RenderEncoderActive)
        {
            EndCurrentRenderPass();
        }

        Debug.Assert(!RenderEncoderActive);
    }

    void EndCurrentRenderPass()
    {
        _rce.endEncoding();
        ObjectiveCRuntime.release(_rce.NativePtr);
        _rce = default;
        _graphicsPipelineChanged = true;
        Util.ClearArray(_graphicsResourceSetsActive);
        _viewportsChanged = true;
        _scissorRectsChanged = true;
    }

    void EnsureBlitEncoder()
    {
        if (!BlitEncoderActive)
        {
            EnsureNoRenderPass();
            EnsureNoComputeEncoder();
            using (NSAutoreleasePool.Begin())
            {
                _bce = _cb.blitCommandEncoder();
                ObjectiveCRuntime.retain(_bce.NativePtr);
            }
        }

        Debug.Assert(BlitEncoderActive);
        Debug.Assert(!RenderEncoderActive);
        Debug.Assert(!ComputeEncoderActive);
    }

    void EnsureNoBlitEncoder()
    {
        if (BlitEncoderActive)
        {
            _bce.endEncoding();
            ObjectiveCRuntime.release(_bce.NativePtr);
            _bce = default;
        }

        Debug.Assert(!BlitEncoderActive);
    }

    void EnsureComputeEncoder()
    {
        if (!ComputeEncoderActive)
        {
            EnsureNoBlitEncoder();
            EnsureNoRenderPass();

            using (NSAutoreleasePool.Begin())
            {
                _cce = _cb.computeCommandEncoder();
                ObjectiveCRuntime.retain(_cce.NativePtr);
            }
        }

        Debug.Assert(ComputeEncoderActive);
        Debug.Assert(!RenderEncoderActive);
        Debug.Assert(!BlitEncoderActive);
    }

    void EnsureNoComputeEncoder()
    {
        if (ComputeEncoderActive)
        {
            _cce.endEncoding();
            ObjectiveCRuntime.release(_cce.NativePtr);
            _cce = default;
            _computePipelineChanged = true;
            Util.ClearArray(_computeResourceSetsActive);
        }

        Debug.Assert(!ComputeEncoderActive);
    }

    private protected override void SetIndexBufferCore(
        DeviceBuffer buffer,
        IndexFormat format,
        uint offset
    )
    {
        _indexBuffer = Util.AssertSubtype<DeviceBuffer, MTLBuffer>(buffer);
        _ibOffset = offset;
        _indexType = MTLFormats.VdToMTLIndexFormat(format);
    }

    private protected override void SetVertexBufferCore(
        uint index,
        DeviceBuffer buffer,
        uint offset
    )
    {
        Util.EnsureArrayMinimumSize(ref _vertexBuffers, index + 1);
        Util.EnsureArrayMinimumSize(ref _vbOffsets, index + 1);
        Util.EnsureArrayMinimumSize(ref _vertexBuffersActive, index + 1);

        if (_vertexBuffers[index] != buffer || _vbOffsets[index] != offset)
        {
            MTLBuffer mtlBuffer = Util.AssertSubtype<DeviceBuffer, MTLBuffer>(buffer);
            _vertexBuffers[index] = mtlBuffer;
            _vbOffsets[index] = offset;
            _vertexBuffersActive[index] = false;
        }
    }

    private protected override void PushDebugGroupCore(ReadOnlySpan<char> name)
    {
        NSString nsName = NSString.New(name);
        if (!_bce.IsNull)
        {
            _bce.pushDebugGroup(nsName);
        }
        else if (!_cce.IsNull)
        {
            _cce.pushDebugGroup(nsName);
        }
        else if (!_rce.IsNull)
        {
            _rce.pushDebugGroup(nsName);
        }

        ObjectiveCRuntime.release(nsName);
    }

    private protected override void PopDebugGroupCore()
    {
        if (!_bce.IsNull)
        {
            _bce.popDebugGroup();
        }
        else if (!_cce.IsNull)
        {
            _cce.popDebugGroup();
        }
        else if (!_rce.IsNull)
        {
            _rce.popDebugGroup();
        }
    }

    private protected override void InsertDebugMarkerCore(ReadOnlySpan<char> name)
    {
        NSString nsName = NSString.New(name);
        if (!_bce.IsNull)
        {
            _bce.insertDebugSignpost(nsName);
        }
        else if (!_cce.IsNull)
        {
            _cce.insertDebugSignpost(nsName);
        }
        else if (!_rce.IsNull)
        {
            _rce.insertDebugSignpost(nsName);
        }

        ObjectiveCRuntime.release(nsName);
    }

    public override void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            EnsureNoRenderPass();
            if (_cb.NativePtr != IntPtr.Zero)
            {
                ObjectiveCRuntime.release(_cb.NativePtr);
            }
        }
    }
}
