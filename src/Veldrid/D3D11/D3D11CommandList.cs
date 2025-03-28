﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Veldrid.SPIRV;
using Vortice;
using Vortice.Direct3D11;
using Vortice.Mathematics;

namespace Veldrid.D3D11;

internal sealed class D3D11CommandList : CommandList
{
    readonly D3D11GraphicsDevice _gd;
    readonly ID3D11DeviceContext _context;
    readonly ID3D11DeviceContext1? _context1;
    readonly ID3DUserDefinedAnnotation? _uda;
    bool _begun;
    bool _disposed;
    ID3D11CommandList? _commandList;

    Viewport[] _viewports = [];
    RawRect[] _scissors = [];
    bool _viewportsChanged;
    bool _scissorRectsChanged;

    uint _numVertexBindings;
    ID3D11Buffer[] _vertexBindings = new ID3D11Buffer[1];
    int[]? _vertexStrides;
    int[] _vertexOffsets = new int[1];

    // Cached pipeline State
    DeviceBuffer? _ib;
    uint _ibOffset;
    ID3D11BlendState? _blendState;
    RgbaFloat _blendFactor;
    ID3D11DepthStencilState? _depthStencilState;
    uint _stencilReference;
    ID3D11RasterizerState? _rasterizerState;
    Vortice.Direct3D.PrimitiveTopology _primitiveTopology;
    ID3D11InputLayout? _inputLayout;
    ID3D11VertexShader? _vertexShader;
    ID3D11GeometryShader? _geometryShader;
    ID3D11HullShader? _hullShader;
    ID3D11DomainShader? _domainShader;
    ID3D11PixelShader? _pixelShader;

    D3D11Pipeline? _graphicsPipeline;

    BoundResourceSetInfo[] _graphicsResourceSets = new BoundResourceSetInfo[1];

    // Resource sets are invalidated when a new resource set is bound with an incompatible SRV or UAV.
    bool[] _invalidatedGraphicsResourceSets = new bool[1];

    D3D11Pipeline? _computePipeline;

    BoundResourceSetInfo[] _computeResourceSets = new BoundResourceSetInfo[1];

    // Resource sets are invalidated when a new resource set is bound with an incompatible SRV or UAV.
    bool[] _invalidatedComputeResourceSets = new bool[1];
    string? _name;
    bool _vertexBindingsChanged;
    readonly ID3D11Buffer[] _cbOut = new ID3D11Buffer[1];
    readonly int[] _firstConstRef = new int[1];
    readonly int[] _numConstsRef = new int[1];

    // Cached resources
    const int MaxCachedUniformBuffers = 15;
    readonly D3D11BufferRange[] _vertexBoundUniformBuffers = new D3D11BufferRange[
        MaxCachedUniformBuffers
    ];
    readonly D3D11BufferRange[] _fragmentBoundUniformBuffers = new D3D11BufferRange[
        MaxCachedUniformBuffers
    ];
    const int MaxCachedTextureViews = 16;
    readonly D3D11TextureView?[] _vertexBoundTextureViews = new D3D11TextureView[
        MaxCachedTextureViews
    ];
    readonly D3D11TextureView?[] _fragmentBoundTextureViews = new D3D11TextureView[
        MaxCachedTextureViews
    ];
    const int MaxCachedSamplers = 4;
    readonly D3D11Sampler[] _vertexBoundSamplers = new D3D11Sampler[MaxCachedSamplers];
    readonly D3D11Sampler[] _fragmentBoundSamplers = new D3D11Sampler[MaxCachedSamplers];

    readonly Dictionary<Texture, List<BoundTextureInfo>> _boundSRVs = new();
    readonly Dictionary<Texture, List<BoundTextureInfo>> _boundUAVs = new();
    readonly List<List<BoundTextureInfo>> _boundTextureInfoPool = new(20);

    const int MaxUAVs = 8;
    readonly List<(DeviceBuffer, int)> _boundComputeUAVBuffers = new(MaxUAVs);
    readonly List<(DeviceBuffer, int)> _boundOMUAVBuffers = new(MaxUAVs);

    readonly List<D3D11Buffer> _availableStagingBuffers = [];
    readonly List<D3D11Buffer> _submittedStagingBuffers = [];

    uint _viewportCount;
    readonly List<D3D11Swapchain> _referencedSwapchains = [];

    public D3D11CommandList(D3D11GraphicsDevice gd)
        : base(gd.Features,
            gd.UniformBufferMinOffsetAlignment,
            gd.StructuredBufferMinOffsetAlignment
        )
    {
        _gd = gd;
        _context = gd.Device.CreateDeferredContext();
        _context1 = _context.QueryInterfaceOrNull<ID3D11DeviceContext1>();
        _uda = _context.QueryInterfaceOrNull<ID3DUserDefinedAnnotation>();
    }

    public ID3D11CommandList? DeviceCommandList => _commandList;

    internal ID3D11DeviceContext DeviceContext => _context;

    D3D11Framebuffer D3D11Framebuffer =>
        Util.AssertSubtype<Framebuffer, D3D11Framebuffer>(Framebuffer!);

    public override bool IsDisposed => _disposed;

    public override void Begin()
    {
        _commandList?.Dispose();
        _commandList = null;
        ClearState();
        _begun = true;
    }

    void ClearState()
    {
        ClearCachedState();
        _context.ClearState();
        ResetManagedState();
    }

    void ResetManagedState()
    {
        _numVertexBindings = 0;
        Util.ClearArray(_vertexBindings);
        _vertexStrides = null;
        Util.ClearArray(_vertexOffsets);

        Framebuffer = null;

        Util.ClearArray(_viewports);
        Util.ClearArray(_scissors);
        _viewportsChanged = false;
        _scissorRectsChanged = false;

        _ib = null;
        _graphicsPipeline = null;
        _blendState = null;
        _blendFactor = default;
        _depthStencilState = null;
        _rasterizerState = null;
        _primitiveTopology = Vortice.Direct3D.PrimitiveTopology.Undefined;
        _inputLayout = null;
        _vertexShader = null;
        _geometryShader = null;
        _hullShader = null;
        _domainShader = null;
        _pixelShader = null;

        ClearSets(_graphicsResourceSets);

        Util.ClearArray(_vertexBoundUniformBuffers);
        Util.ClearArray(_vertexBoundTextureViews);
        Util.ClearArray(_vertexBoundSamplers);

        Util.ClearArray(_fragmentBoundUniformBuffers);
        Util.ClearArray(_fragmentBoundTextureViews);
        Util.ClearArray(_fragmentBoundSamplers);

        _computePipeline = null;
        ClearSets(_computeResourceSets);

        foreach (KeyValuePair<Texture, List<BoundTextureInfo>> kvp in _boundSRVs)
        {
            List<BoundTextureInfo> list = kvp.Value;
            list.Clear();
            PoolBoundTextureList(list);
        }
        _boundSRVs.Clear();

        foreach (KeyValuePair<Texture, List<BoundTextureInfo>> kvp in _boundUAVs)
        {
            List<BoundTextureInfo> list = kvp.Value;
            list.Clear();
            PoolBoundTextureList(list);
        }
        _boundUAVs.Clear();
    }

    static void ClearSets(Span<BoundResourceSetInfo> boundSets)
    {
        foreach (ref BoundResourceSetInfo boundSetInfo in boundSets)
        {
            boundSetInfo.Offsets.Dispose();
            boundSetInfo = default;
        }
    }

    public override void End()
    {
        if (_commandList != null)
            throw new VeldridException("Invalid use of End().");

        _context.FinishCommandList(false, out _commandList).CheckError();
        _commandList.DebugName = _name!;
        ResetManagedState();
        _begun = false;
    }

    public void Reset()
    {
        if (_commandList != null)
        {
            _commandList.Dispose();
            _commandList = null;
        }
        else if (_begun)
        {
            _context.ClearState();
            _context.FinishCommandList(false, out _commandList);
            _commandList.Dispose();
            _commandList = null;
        }

        ResetManagedState();
        _begun = false;
    }

    private protected override void SetIndexBufferCore(
        DeviceBuffer buffer,
        IndexFormat format,
        uint offset
    )
    {
        if (_ib != buffer || _ibOffset != offset)
        {
            _ib = buffer;
            _ibOffset = offset;
            D3D11Buffer d3d11Buffer = Util.AssertSubtype<DeviceBuffer, D3D11Buffer>(buffer);
            UnbindUAVBuffer(buffer);
            _context.IASetIndexBuffer(
                d3d11Buffer.Buffer,
                D3D11Formats.ToDxgiFormat(format),
                (int)offset
            );
        }
    }

    private protected override void SetPipelineCore(Pipeline pipeline)
    {
        if (!pipeline.IsComputePipeline && _graphicsPipeline != pipeline)
        {
            D3D11Pipeline d3dPipeline = Util.AssertSubtype<Pipeline, D3D11Pipeline>(pipeline);
            _graphicsPipeline = d3dPipeline;
            ClearSets(_graphicsResourceSets); // Invalidate resource set bindings -- they may be invalid.
            Util.ClearArray(_invalidatedGraphicsResourceSets);

            ID3D11BlendState? blendState = d3dPipeline.BlendState;
            RgbaFloat blendFactor = d3dPipeline.BlendFactor;
            if (_blendState != blendState || _blendFactor != blendFactor)
            {
                _blendState = blendState;
                _blendFactor = blendFactor;

                _context.OMSetBlendState(
                    blendState!,
                    Unsafe.BitCast<RgbaFloat, Color4>(blendFactor)
                );
            }

            ID3D11DepthStencilState? depthStencilState = d3dPipeline.DepthStencilState;
            uint stencilReference = d3dPipeline.StencilReference;
            if (_depthStencilState != depthStencilState || _stencilReference != stencilReference)
            {
                _depthStencilState = depthStencilState;
                _stencilReference = stencilReference;
                _context.OMSetDepthStencilState(depthStencilState!, (int)stencilReference);
            }

            ID3D11RasterizerState? rasterizerState = d3dPipeline.RasterizerState;
            if (_rasterizerState != rasterizerState)
            {
                _rasterizerState = rasterizerState;
                _context.RSSetState(rasterizerState!);
            }

            Vortice.Direct3D.PrimitiveTopology primitiveTopology = d3dPipeline.PrimitiveTopology;
            if (_primitiveTopology != primitiveTopology)
            {
                _primitiveTopology = primitiveTopology;
                _context.IASetPrimitiveTopology(primitiveTopology);
            }

            ID3D11InputLayout? inputLayout = d3dPipeline.InputLayout;
            if (_inputLayout != inputLayout)
            {
                _inputLayout = inputLayout;
                _context.IASetInputLayout(inputLayout!);
            }

            ID3D11VertexShader? vertexShader = d3dPipeline.VertexShader;
            if (_vertexShader != vertexShader)
            {
                _vertexShader = vertexShader;
                _context.VSSetShader(vertexShader!);
            }

            ID3D11GeometryShader? geometryShader = d3dPipeline.GeometryShader;
            if (_geometryShader != geometryShader)
            {
                _geometryShader = geometryShader;
                _context.GSSetShader(geometryShader!);
            }

            ID3D11HullShader? hullShader = d3dPipeline.HullShader;
            if (_hullShader != hullShader)
            {
                _hullShader = hullShader;
                _context.HSSetShader(hullShader!);
            }

            ID3D11DomainShader? domainShader = d3dPipeline.DomainShader;
            if (_domainShader != domainShader)
            {
                _domainShader = domainShader;
                _context.DSSetShader(domainShader!);
            }

            ID3D11PixelShader? pixelShader = d3dPipeline.PixelShader;
            if (_pixelShader != pixelShader)
            {
                _pixelShader = pixelShader;
                _context.PSSetShader(pixelShader!);
            }

            _vertexStrides = d3dPipeline.VertexStrides;
            if (_vertexStrides != null)
            {
                int vertexStridesCount = _vertexStrides.Length;
                Util.EnsureArrayMinimumSize(ref _vertexBindings, (uint)vertexStridesCount);
                Util.EnsureArrayMinimumSize(ref _vertexOffsets, (uint)vertexStridesCount);
            }

            Util.EnsureArrayMinimumSize(
                ref _graphicsResourceSets,
                (uint)d3dPipeline.ResourceLayouts.Length
            );
            Util.EnsureArrayMinimumSize(
                ref _invalidatedGraphicsResourceSets,
                (uint)d3dPipeline.ResourceLayouts.Length
            );
        }
        else if (pipeline.IsComputePipeline && _computePipeline != pipeline)
        {
            D3D11Pipeline d3dPipeline = Util.AssertSubtype<Pipeline, D3D11Pipeline>(pipeline);
            _computePipeline = d3dPipeline;
            ClearSets(_computeResourceSets); // Invalidate resource set bindings -- they may be invalid.
            Util.ClearArray(_invalidatedComputeResourceSets);

            ID3D11ComputeShader? computeShader = d3dPipeline.ComputeShader;
            _context.CSSetShader(computeShader!);
            Util.EnsureArrayMinimumSize(
                ref _computeResourceSets,
                (uint)d3dPipeline.ResourceLayouts.Length
            );
            Util.EnsureArrayMinimumSize(
                ref _invalidatedComputeResourceSets,
                (uint)d3dPipeline.ResourceLayouts.Length
            );
        }
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
            ActivateResourceSet(slot, ref set, true);
        }
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
            ActivateResourceSet(slot, ref set, false);
        }
    }

    void ActivateResourceSet(uint slot, ref BoundResourceSetInfo brsi, bool graphics)
    {
        D3D11ResourceSet d3d11RS = Util.AssertSubtype<ResourceSet, D3D11ResourceSet>(brsi.Set);

        D3D11Pipeline? pipeline = graphics ? _graphicsPipeline : _computePipeline;
        Debug.Assert(pipeline != null);

        ReadOnlySpan<D3D11ResourceLayout> pipelinelayouts = pipeline.ResourceLayouts.AsSpan(
            0,
            (int)slot
        );
        int cbBase = 0;
        int uaBase = 0;
        int textureBase = 0;
        int samplerBase = 0;

        foreach (D3D11ResourceLayout pLayout in pipelinelayouts)
        {
            Debug.Assert(pLayout != null);
            cbBase += pLayout.UniformBufferCount;
            uaBase += pLayout.StorageBufferCount;
            textureBase += pLayout.TextureCount;
            samplerBase += pLayout.SamplerCount;
        }

        D3D11ResourceLayout layout = d3d11RS.Layout;
        BindableResource[] resources = d3d11RS.Resources;
        uint dynamicOffsetIndex = 0;
        for (int i = 0; i < resources.Length; i++)
        {
            BindableResource resource = resources[i];
            D3D11ResourceLayout.ResourceBindingInfo rbi = layout.GetDeviceSlotIndex(i);

            uint bufferOffset = 0;
            if (rbi.DynamicBuffer)
            {
                bufferOffset = brsi.Offsets.Get(dynamicOffsetIndex);
                dynamicOffsetIndex += 1;
            }

            switch (rbi.Kind)
            {
                case ResourceKind.UniformBuffer:
                {
                    D3D11BufferRange range = GetBufferRange(resource, bufferOffset);
                    BindUniformBuffer(range, cbBase + rbi.Slot, rbi.Stages);
                    break;
                }
                case ResourceKind.StructuredBufferReadOnly:
                {
                    D3D11BufferRange range = GetBufferRange(resource, bufferOffset);
                    BindStorageBufferView(range, textureBase + rbi.Slot, rbi.Stages);
                    break;
                }
                case ResourceKind.StructuredBufferReadWrite:
                {
                    D3D11BufferRange range = GetBufferRange(resource, bufferOffset);
                    ID3D11UnorderedAccessView uav = range.Buffer.GetUnorderedAccessView(
                        range.Offset,
                        range.Size
                    );
                    BindUnorderedAccessView(
                        null,
                        range.Buffer,
                        uav,
                        uaBase + rbi.Slot,
                        rbi.Stages,
                        slot
                    );
                    break;
                }
                case ResourceKind.TextureReadOnly:
                {
                    TextureView texView = Util.GetTextureView(_gd, resource);
                    D3D11TextureView d3d11TexView = Util.AssertSubtype<
                        TextureView,
                        D3D11TextureView
                    >(texView);
                    UnbindUAVTexture(d3d11TexView.Target);
                    BindTextureView(d3d11TexView, textureBase + rbi.Slot, rbi.Stages, slot);
                    break;
                }
                case ResourceKind.TextureReadWrite:
                {
                    TextureView rwTexView = Util.GetTextureView(_gd, resource);
                    D3D11TextureView d3d11RWTexView = Util.AssertSubtype<
                        TextureView,
                        D3D11TextureView
                    >(rwTexView);
                    UnbindSRVTexture(d3d11RWTexView.Target);
                    BindUnorderedAccessView(
                        d3d11RWTexView.Target,
                        null,
                        d3d11RWTexView.UnorderedAccessView,
                        uaBase + rbi.Slot,
                        rbi.Stages,
                        slot
                    );
                    break;
                }
                case ResourceKind.Sampler:
                {
                    D3D11Sampler sampler = Util.AssertSubtype<Sampler, D3D11Sampler>(
                        resource.GetSampler()
                    );
                    BindSampler(sampler, samplerBase + rbi.Slot, rbi.Stages);
                    break;
                }
                default:
                    Illegal.Value<ResourceKind>();
                    break;
            }
        }
    }

    static D3D11BufferRange GetBufferRange(BindableResource resource, uint additionalOffset)
    {
        DeviceBufferRange range = Util.GetBufferRange(resource, additionalOffset);
        D3D11Buffer buffer = Util.AssertSubtype<DeviceBuffer, D3D11Buffer>(range.Buffer);
        return new(buffer, range.Offset, range.SizeInBytes);
    }

    void UnbindSRVTexture(Texture target)
    {
        if (!_boundSRVs.Remove(target, out List<BoundTextureInfo>? btis))
            return;

        foreach (BoundTextureInfo bti in btis)
        {
            BindTextureView(null, bti.Slot, bti.Stages, 0);

            if ((bti.Stages & ShaderStages.Compute) != 0)
                _invalidatedComputeResourceSets[bti.ResourceSet] = true;
            else
                _invalidatedGraphicsResourceSets[bti.ResourceSet] = true;
        }

        btis.Clear();
        PoolBoundTextureList(btis);
    }

    void PoolBoundTextureList(List<BoundTextureInfo> btis)
    {
        _boundTextureInfoPool.Add(btis);
    }

    void UnbindUAVTexture(Texture target)
    {
        if (!_boundUAVs.Remove(target, out List<BoundTextureInfo>? btis))
        {
            return;
        }

        foreach (BoundTextureInfo bti in btis)
        {
            BindUnorderedAccessView(null, null, null, bti.Slot, bti.Stages, bti.ResourceSet);
            if ((bti.Stages & ShaderStages.Compute) == ShaderStages.Compute)
            {
                _invalidatedComputeResourceSets[bti.ResourceSet] = true;
            }
            else
            {
                _invalidatedGraphicsResourceSets[bti.ResourceSet] = true;
            }
        }

        btis.Clear();
        PoolBoundTextureList(btis);
    }

    private protected override void SetVertexBufferCore(
        uint index,
        DeviceBuffer buffer,
        uint offset
    )
    {
        D3D11Buffer d3d11Buffer = Util.AssertSubtype<DeviceBuffer, D3D11Buffer>(buffer);
        if (_vertexBindings[index] == d3d11Buffer.Buffer && _vertexOffsets[index] == offset)
            return;

        _vertexBindingsChanged = true;
        UnbindUAVBuffer(buffer);
        _vertexBindings[index] = d3d11Buffer.Buffer;
        _vertexOffsets[index] = (int)offset;
        _numVertexBindings = Math.Max((index + 1), _numVertexBindings);
    }

    private protected override void DrawCore(
        uint vertexCount,
        uint instanceCount,
        uint vertexStart,
        uint instanceStart
    )
    {
        PreDrawCommand();

        if (instanceCount == 1 && instanceStart == 0)
        {
            _context.Draw((int)vertexCount, (int)vertexStart);
        }
        else
        {
            _context.DrawInstanced(
                (int)vertexCount,
                (int)instanceCount,
                (int)vertexStart,
                (int)instanceStart
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
        PreDrawCommand();

        Debug.Assert(_ib != null);
        if (instanceCount == 1 && instanceStart == 0)
        {
            _context.DrawIndexed((int)indexCount, (int)indexStart, vertexOffset);
        }
        else
        {
            _context.DrawIndexedInstanced(
                (int)indexCount,
                (int)instanceCount,
                (int)indexStart,
                vertexOffset,
                (int)instanceStart
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
        PreDrawCommand();

        D3D11Buffer d3d11Buffer = Util.AssertSubtype<DeviceBuffer, D3D11Buffer>(indirectBuffer);
        int currentOffset = (int)offset;
        for (uint i = 0; i < drawCount; i++)
        {
            _context.DrawInstancedIndirect(d3d11Buffer.Buffer, currentOffset);
            currentOffset += (int)stride;
        }
    }

    protected override void DrawIndexedIndirectCore(
        DeviceBuffer indirectBuffer,
        uint offset,
        uint drawCount,
        uint stride
    )
    {
        PreDrawCommand();

        D3D11Buffer d3d11Buffer = Util.AssertSubtype<DeviceBuffer, D3D11Buffer>(indirectBuffer);
        int currentOffset = (int)offset;
        for (uint i = 0; i < drawCount; i++)
        {
            _context.DrawIndexedInstancedIndirect(d3d11Buffer.Buffer, currentOffset);
            currentOffset += (int)stride;
        }
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

        int graphicsResourceCount = _graphicsPipeline!.ResourceLayouts.Length;
        Span<bool> invalidatedSets = _invalidatedGraphicsResourceSets.AsSpan(
            0,
            graphicsResourceCount
        );

        Span<BoundResourceSetInfo> sets = _graphicsResourceSets.AsSpan(0, graphicsResourceCount);
        for (int i = 0; i < graphicsResourceCount; i++)
        {
            if (invalidatedSets[i])
            {
                invalidatedSets[i] = false;
                ActivateResourceSet((uint)i, ref sets[i], true);
            }
        }
    }

    public override void Dispatch(uint groupCountX, uint groupCountY, uint groupCountZ)
    {
        PreDispatchCommand();
        _context.Dispatch((int)groupCountX, (int)groupCountY, (int)groupCountZ);
    }

    private protected override void DispatchIndirectCore(DeviceBuffer indirectBuffer, uint offset)
    {
        PreDispatchCommand();
        D3D11Buffer d3d11Buffer = Util.AssertSubtype<DeviceBuffer, D3D11Buffer>(indirectBuffer);
        _context.DispatchIndirect(d3d11Buffer.Buffer, (int)offset);
    }

    void PreDispatchCommand()
    {
        int computeResourceCount = _computePipeline!.ResourceLayouts.Length;
        Span<bool> invalidatedSets = _invalidatedComputeResourceSets.AsSpan(
            0,
            computeResourceCount
        );

        Span<BoundResourceSetInfo> sets = _computeResourceSets.AsSpan(0, computeResourceCount);
        for (int i = 0; i < computeResourceCount; i++)
        {
            if (invalidatedSets[i])
            {
                invalidatedSets[i] = false;
                ActivateResourceSet((uint)i, ref sets[i], false);
            }
        }
    }

    protected override void ResolveTextureCore(Texture source, Texture destination)
    {
        D3D11Texture d3d11Source = Util.AssertSubtype<Texture, D3D11Texture>(source);
        D3D11Texture d3d11Destination = Util.AssertSubtype<Texture, D3D11Texture>(destination);
        _context.ResolveSubresource(
            d3d11Destination.DeviceTexture,
            0,
            d3d11Source.DeviceTexture,
            0,
            d3d11Destination.DxgiFormat
        );
    }

    void FlushViewports()
    {
        _context.RSSetViewports(_viewports.AsSpan(0, (int)_viewportCount));
    }

    void FlushScissorRects()
    {
        // Because this array is resized using Util.EnsureMinimumArraySize, this might set more scissor rectangles
        // than are actually needed, but this is okay -- extras are essentially ignored and should be harmless.
        _context.RSSetScissorRects(_scissors.AsSpan(0, (int)_viewportCount));
    }

    void FlushVertexBindings()
    {
        _context.IASetVertexBuffers(
            0,
            (int)_numVertexBindings,
            _vertexBindings,
            _vertexStrides!,
            _vertexOffsets
        );
    }

    public override void SetScissorRect(uint index, uint x, uint y, uint width, uint height)
    {
        _scissorRectsChanged = true;
        _scissors[index] = new((int)x, (int)y, (int)(x + width), (int)(y + height));
    }

    public override void SetViewport(uint index, in Viewport viewport)
    {
        _viewportsChanged = true;
        _viewports[index] = viewport;
    }

    void BindTextureView(D3D11TextureView? texView, int slot, ShaderStages stages, uint resourceSet)
    {
        ID3D11ShaderResourceView? srv = texView?.ShaderResourceView ?? null;
        if (srv != null)
        {
            if (!_boundSRVs.TryGetValue(texView!.Target, out List<BoundTextureInfo>? list))
            {
                list = GetNewOrCachedBoundTextureInfoList();
                _boundSRVs.Add(texView.Target, list);
            }
            list.Add(
                new()
                {
                    Slot = slot,
                    Stages = stages,
                    ResourceSet = resourceSet,
                }
            );
        }

        if ((stages & ShaderStages.Vertex) == ShaderStages.Vertex)
        {
            bool bind = false;
            if (slot < MaxCachedUniformBuffers)
            {
                if (_vertexBoundTextureViews[slot] != texView)
                {
                    _vertexBoundTextureViews[slot] = texView;
                    bind = true;
                }
            }
            else
            {
                bind = true;
            }
            if (bind)
            {
                _context.VSSetShaderResource(slot, srv!);
            }
        }

        if ((stages & ShaderStages.Geometry) == ShaderStages.Geometry)
            _context.GSSetShaderResource(slot, srv!);

        if ((stages & ShaderStages.TessellationControl) == ShaderStages.TessellationControl)
            _context.HSSetShaderResource(slot, srv!);

        if ((stages & ShaderStages.TessellationEvaluation) == ShaderStages.TessellationEvaluation)
            _context.DSSetShaderResource(slot, srv!);

        if ((stages & ShaderStages.Fragment) == ShaderStages.Fragment)
        {
            bool bind = false;
            if (slot < MaxCachedUniformBuffers)
            {
                if (_fragmentBoundTextureViews[slot] != texView)
                {
                    _fragmentBoundTextureViews[slot] = texView;
                    bind = true;
                }
            }
            else
            {
                bind = true;
            }

            if (bind)
                _context.PSSetShaderResource(slot, srv!);
        }

        if ((stages & ShaderStages.Compute) == ShaderStages.Compute)
            _context.CSSetShaderResource(slot, srv!);
    }

    List<BoundTextureInfo> GetNewOrCachedBoundTextureInfoList()
    {
        if (_boundTextureInfoPool.Count <= 0)
            return [];

        int index = _boundTextureInfoPool.Count - 1;
        List<BoundTextureInfo> ret = _boundTextureInfoPool[index];
        _boundTextureInfoPool.RemoveAt(index);
        return ret;
    }

    void BindStorageBufferView(D3D11BufferRange range, int slot, ShaderStages stages)
    {
        bool compute = (stages & ShaderStages.Compute) != 0;
        UnbindUAVBuffer(range.Buffer);

        ID3D11ShaderResourceView srv = range.Buffer.GetShaderResourceView(range.Offset, range.Size);

        if ((stages & ShaderStages.Vertex) == ShaderStages.Vertex)
            _context.VSSetShaderResource(slot, srv);

        if ((stages & ShaderStages.Geometry) == ShaderStages.Geometry)
            _context.GSSetShaderResource(slot, srv);

        if ((stages & ShaderStages.TessellationControl) == ShaderStages.TessellationControl)
            _context.HSSetShaderResource(slot, srv);

        if ((stages & ShaderStages.TessellationEvaluation) == ShaderStages.TessellationEvaluation)
            _context.DSSetShaderResource(slot, srv);

        if ((stages & ShaderStages.Fragment) == ShaderStages.Fragment)
            _context.PSSetShaderResource(slot, srv);

        if (compute)
            _context.CSSetShaderResource(slot, srv);
    }

    void BindUniformBuffer(D3D11BufferRange range, int slot, ShaderStages stages)
    {
        bool fullRange = range.IsFullRange;
        ID3D11DeviceContext1? context1 = _context1;
        if (!fullRange && context1 == null)
        {
            throw new VeldridException(
                $"The range of the uniform buffer in slot {slot} ({range.Buffer}) does not "
                    + $"meet the requirements of this device."
            );
        }
        Debug.Assert(context1 != null);

        if ((stages & ShaderStages.Vertex) == ShaderStages.Vertex)
        {
            bool bind = false;
            if (slot < MaxCachedUniformBuffers)
            {
                if (!_vertexBoundUniformBuffers[slot].Equals(range))
                {
                    _vertexBoundUniformBuffers[slot] = range;
                    bind = true;
                }
            }
            else
            {
                bind = true;
            }
            if (bind)
            {
                if (fullRange)
                {
                    _context.VSSetConstantBuffer(slot, range.Buffer.Buffer);
                }
                else
                {
                    PackRangeParams(range);
                    if (!_gd.SupportsCommandLists)
                    {
                        _context.VSUnsetConstantBuffer(slot);
                    }
                    context1.VSSetConstantBuffers1(slot, 1, _cbOut, _firstConstRef, _numConstsRef);
                }
            }
        }
        if ((stages & ShaderStages.Geometry) == ShaderStages.Geometry)
        {
            if (fullRange)
            {
                _context.GSSetConstantBuffer(slot, range.Buffer.Buffer);
            }
            else
            {
                PackRangeParams(range);
                if (!_gd.SupportsCommandLists)
                {
                    _context.GSUnsetConstantBuffer(slot);
                }
                context1.GSSetConstantBuffers1(slot, 1, _cbOut, _firstConstRef, _numConstsRef);
            }
        }
        if ((stages & ShaderStages.TessellationControl) == ShaderStages.TessellationControl)
        {
            if (fullRange)
            {
                _context.HSSetConstantBuffer(slot, range.Buffer.Buffer);
            }
            else
            {
                PackRangeParams(range);
                if (!_gd.SupportsCommandLists)
                {
                    _context.HSUnsetConstantBuffer(slot);
                }
                context1.HSSetConstantBuffers1(slot, 1, _cbOut, _firstConstRef, _numConstsRef);
            }
        }
        if ((stages & ShaderStages.TessellationEvaluation) == ShaderStages.TessellationEvaluation)
        {
            if (fullRange)
            {
                _context.DSSetConstantBuffer(slot, range.Buffer.Buffer);
            }
            else
            {
                PackRangeParams(range);
                if (!_gd.SupportsCommandLists)
                {
                    _context.DSUnsetConstantBuffer(slot);
                }
                context1.DSSetConstantBuffers1(slot, 1, _cbOut, _firstConstRef, _numConstsRef);
            }
        }
        if ((stages & ShaderStages.Fragment) == ShaderStages.Fragment)
        {
            bool bind = false;
            if (slot < MaxCachedUniformBuffers)
            {
                if (!_fragmentBoundUniformBuffers[slot].Equals(range))
                {
                    _fragmentBoundUniformBuffers[slot] = range;
                    bind = true;
                }
            }
            else
            {
                bind = true;
            }
            if (bind)
            {
                if (fullRange)
                {
                    _context.PSSetConstantBuffer(slot, range.Buffer.Buffer);
                }
                else
                {
                    PackRangeParams(range);
                    if (!_gd.SupportsCommandLists)
                    {
                        _context.PSUnsetConstantBuffer(slot);
                    }
                    context1.PSSetConstantBuffers1(slot, 1, _cbOut, _firstConstRef, _numConstsRef);
                }
            }
        }
        if ((stages & ShaderStages.Compute) == ShaderStages.Compute)
        {
            if (fullRange)
            {
                _context.CSSetConstantBuffer(slot, range.Buffer.Buffer);
            }
            else
            {
                PackRangeParams(range);
                if (!_gd.SupportsCommandLists)
                {
                    _context.CSSetConstantBuffer(slot, null!);
                }
                context1.CSSetConstantBuffers1(slot, 1, _cbOut, _firstConstRef, _numConstsRef);
            }
        }
    }

    void PackRangeParams(D3D11BufferRange range)
    {
        _cbOut[0] = range.Buffer.Buffer;
        _firstConstRef[0] = (int)(range.Offset / 16);
        uint roundedSize = range.Size < 256 ? 256u : range.Size;
        _numConstsRef[0] = (int)(roundedSize / 16);
    }

    void BindUnorderedAccessView(
        Texture? texture,
        DeviceBuffer? buffer,
        ID3D11UnorderedAccessView? uav,
        int slot,
        ShaderStages stages,
        uint resourceSet
    )
    {
        bool compute = stages == ShaderStages.Compute;
        Debug.Assert(compute || ((stages & ShaderStages.Compute) == 0));
        Debug.Assert(texture == null || buffer == null);

        if (texture != null && uav != null)
        {
            if (!_boundUAVs.TryGetValue(texture, out List<BoundTextureInfo>? list))
            {
                list = GetNewOrCachedBoundTextureInfoList();
                _boundUAVs.Add(texture, list);
            }
            list.Add(
                new()
                {
                    Slot = slot,
                    Stages = stages,
                    ResourceSet = resourceSet,
                }
            );
        }

        int baseSlot = 0;
        if (!compute)
            baseSlot = Framebuffer!.ColorTargets.Length;

        int actualSlot = baseSlot + slot;

        if (buffer != null)
            TrackBoundUAVBuffer(buffer, actualSlot, compute);

        if (compute)
            _context.CSSetUnorderedAccessView(actualSlot, uav!);
        else
            _context.OMSetUnorderedAccessView(actualSlot, uav!);
    }

    void TrackBoundUAVBuffer(DeviceBuffer buffer, int slot, bool compute)
    {
        List<(DeviceBuffer, int)> list = compute ? _boundComputeUAVBuffers : _boundOMUAVBuffers;
        list.Add((buffer, slot));
    }

    void UnbindUAVBuffer(DeviceBuffer buffer)
    {
        UnbindUAVBufferIndividual(buffer, false);
        UnbindUAVBufferIndividual(buffer, true);
    }

    void UnbindUAVBufferIndividual(DeviceBuffer buffer, bool compute)
    {
        List<(DeviceBuffer, int)> list = compute ? _boundComputeUAVBuffers : _boundOMUAVBuffers;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].Item1 == buffer)
            {
                int slot = list[i].Item2;
                if (compute)
                    _context.CSUnsetUnorderedAccessView(slot);
                else
                    _context.OMUnsetUnorderedAccessView(slot);

                list.RemoveAt(i);
                i -= 1;
            }
        }
    }

    void BindSampler(D3D11Sampler sampler, int slot, ShaderStages stages)
    {
        if ((stages & ShaderStages.Vertex) == ShaderStages.Vertex)
        {
            bool bind = false;
            if (slot < MaxCachedSamplers)
            {
                if (_vertexBoundSamplers[slot] != sampler)
                {
                    _vertexBoundSamplers[slot] = sampler;
                    bind = true;
                }
            }
            else
            {
                bind = true;
            }

            if (bind)
                _context.VSSetSampler(slot, sampler.DeviceSampler);
        }

        if ((stages & ShaderStages.Geometry) == ShaderStages.Geometry)
            _context.GSSetSampler(slot, sampler.DeviceSampler);

        if ((stages & ShaderStages.TessellationControl) == ShaderStages.TessellationControl)
            _context.HSSetSampler(slot, sampler.DeviceSampler);

        if ((stages & ShaderStages.TessellationEvaluation) == ShaderStages.TessellationEvaluation)
            _context.DSSetSampler(slot, sampler.DeviceSampler);

        if ((stages & ShaderStages.Fragment) == ShaderStages.Fragment)
        {
            bool bind = false;
            if (slot < MaxCachedSamplers)
            {
                if (_fragmentBoundSamplers[slot] != sampler)
                {
                    _fragmentBoundSamplers[slot] = sampler;
                    bind = true;
                }
            }
            else
            {
                bind = true;
            }

            if (bind)
                _context.PSSetSampler(slot, sampler.DeviceSampler);
        }

        if ((stages & ShaderStages.Compute) == ShaderStages.Compute)
            _context.CSSetSampler(slot, sampler.DeviceSampler);
    }

    protected override void SetFramebufferCore(Framebuffer fb)
    {
        D3D11Framebuffer d3dFB = Util.AssertSubtype<Framebuffer, D3D11Framebuffer>(fb);
        if (d3dFB.Swapchain != null)
        {
            d3dFB.Swapchain.AddCommandListReference(this);
            _referencedSwapchains.Add(d3dFB.Swapchain);
        }

        foreach (ref readonly FramebufferAttachment colorTarget in fb.ColorTargets)
            UnbindSRVTexture(colorTarget.Target);

        _context.OMSetRenderTargets(d3dFB.RenderTargetViews, d3dFB.DepthStencilView);

        _viewportCount = Math.Max(1u, (uint)fb.ColorTargets.Length);
        Util.EnsureArrayMinimumSize(ref _viewports, _viewportCount);
        Util.ClearArray(_viewports);
        Util.EnsureArrayMinimumSize(ref _scissors, _viewportCount);
        Util.ClearArray(_scissors);
    }

    private protected override void ClearColorTargetCore(uint index, RgbaFloat clearColor)
    {
        _context.ClearRenderTargetView(
            D3D11Framebuffer.RenderTargetViews[index],
            Unsafe.BitCast<RgbaFloat, Color4>(clearColor)
        );
    }

    private protected override void ClearDepthStencilCore(float depth, byte stencil)
    {
        _context.ClearDepthStencilView(
            D3D11Framebuffer.DepthStencilView,
            DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil,
            depth,
            stencil
        );
    }

    private protected override unsafe void UpdateBufferCore(
        DeviceBuffer buffer,
        uint bufferOffsetInBytes,
        IntPtr source,
        uint sizeInBytes
    )
    {
        D3D11Buffer d3dBuffer = Util.AssertSubtype<DeviceBuffer, D3D11Buffer>(buffer);

        BufferUsage usage = buffer.Usage;
        bool isDynamic = (usage & BufferUsage.DynamicReadWrite) != 0;
        bool isStaging = (usage & BufferUsage.StagingReadWrite) != 0;
        bool isUniformBuffer = (usage & BufferUsage.UniformBuffer) != 0;
        bool isFullBuffer = bufferOffsetInBytes == 0 && sizeInBytes == buffer.SizeInBytes;

        bool useUpdateSubresource =
            (!isDynamic && !isStaging) && (!isUniformBuffer || isFullBuffer);

        bool useMap =
            ((usage & BufferUsage.DynamicWrite) != 0 && isFullBuffer)
            || (usage & BufferUsage.StagingWrite) != 0;

        if (useUpdateSubresource)
        {
            Box? subregion = new Box(
                (int)bufferOffsetInBytes,
                0,
                0,
                (int)(sizeInBytes + bufferOffsetInBytes),
                1,
                1
            );

            if (isUniformBuffer)
                subregion = null;

            if (bufferOffsetInBytes == 0)
                _context.UpdateSubresource(d3dBuffer.Buffer, 0, subregion, source, 0, 0);
            else
                UpdateSubresource_Workaround(d3dBuffer.Buffer, 0, subregion!.Value, source);
        }
        else if (useMap && isFullBuffer) // Can only update full buffer with WriteDiscard.
        {
            MappedSubresource msb = _context.Map(
                d3dBuffer.Buffer,
                0,
                D3D11Formats.VdToD3D11MapMode(isDynamic, MapMode.Write)
            );

            Unsafe.CopyBlock(msb.DataPointer.ToPointer(), source.ToPointer(), sizeInBytes);

            _context.Unmap(d3dBuffer.Buffer, 0);
        }
        else
        {
            D3D11Buffer staging = GetFreeStagingBuffer(sizeInBytes);
            _gd.UpdateBuffer(staging, 0, source, sizeInBytes);
            CopyBuffer(staging, 0, buffer, bufferOffsetInBytes, sizeInBytes);
            _submittedStagingBuffers.Add(staging);
        }
    }

    unsafe void UpdateSubresource_Workaround(
        ID3D11Resource resource,
        int subresource,
        Box region,
        IntPtr data
    )
    {
        bool needWorkaround = !_gd.SupportsCommandLists;
        void* pAdjustedSrcData = data.ToPointer();
        if (needWorkaround)
        {
            Debug.Assert(region is { Top: 0, Front: 0 });
            pAdjustedSrcData = (byte*)data - region.Left;
        }

        _context.UpdateSubresource(resource, subresource, region, (IntPtr)pAdjustedSrcData, 0, 0);
    }

    D3D11Buffer GetFreeStagingBuffer(uint sizeInBytes)
    {
        foreach (D3D11Buffer buffer in _availableStagingBuffers)
        {
            if (buffer.SizeInBytes >= sizeInBytes)
            {
                _availableStagingBuffers.Remove(buffer);
                return buffer;
            }
        }

        DeviceBuffer staging = _gd.ResourceFactory.CreateBuffer(
            new(sizeInBytes, BufferUsage.StagingWrite)
        );

        return Util.AssertSubtype<DeviceBuffer, D3D11Buffer>(staging);
    }

    private protected override void CopyBufferCore(
        DeviceBuffer source,
        DeviceBuffer destination,
        ReadOnlySpan<BufferCopyCommand> commands
    )
    {
        D3D11Buffer srcD3D11Buffer = Util.AssertSubtype<DeviceBuffer, D3D11Buffer>(source);
        D3D11Buffer dstD3D11Buffer = Util.AssertSubtype<DeviceBuffer, D3D11Buffer>(destination);

        foreach (ref readonly BufferCopyCommand command in commands)
        {
            if (command.Length == 0)
            {
                continue;
            }

            Box region = new(
                (int)command.ReadOffset,
                0,
                0,
                (int)(command.ReadOffset + command.Length),
                1,
                1
            );

            _context.CopySubresourceRegion(
                dstD3D11Buffer.Buffer,
                0,
                (int)command.WriteOffset,
                0,
                0,
                srcD3D11Buffer.Buffer,
                0,
                region
            );
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
        D3D11Texture srcD3D11Texture = Util.AssertSubtype<Texture, D3D11Texture>(source);
        D3D11Texture dstD3D11Texture = Util.AssertSubtype<Texture, D3D11Texture>(destination);

        uint blockSize = FormatHelpers.IsCompressedFormat(source.Format) ? 4u : 1u;
        uint clampedWidth = Math.Max(blockSize, width);
        uint clampedHeight = Math.Max(blockSize, height);

        Box? region = null;
        if (
            srcX != 0
            || srcY != 0
            || srcZ != 0
            || clampedWidth != source.Width
            || clampedHeight != source.Height
            || depth != source.Depth
        )
        {
            region = new Box(
                (int)srcX,
                (int)srcY,
                (int)srcZ,
                (int)(srcX + clampedWidth),
                (int)(srcY + clampedHeight),
                (int)(srcZ + depth)
            );
        }

        for (uint i = 0; i < layerCount; i++)
        {
            int srcSubresource = D3D11Util.ComputeSubresource(
                srcMipLevel,
                source.MipLevels,
                srcBaseArrayLayer + i
            );
            int dstSubresource = D3D11Util.ComputeSubresource(
                dstMipLevel,
                destination.MipLevels,
                dstBaseArrayLayer + i
            );

            _context.CopySubresourceRegion(
                dstD3D11Texture.DeviceTexture,
                dstSubresource,
                (int)dstX,
                (int)dstY,
                (int)dstZ,
                srcD3D11Texture.DeviceTexture,
                srcSubresource,
                region
            );
        }
    }

    private protected override void GenerateMipmapsCore(Texture texture)
    {
        TextureView fullTexView = texture.GetFullTextureView(_gd);
        D3D11TextureView d3d11View = Util.AssertSubtype<TextureView, D3D11TextureView>(fullTexView);
        ID3D11ShaderResourceView srv = d3d11View.ShaderResourceView;
        _context.GenerateMips(srv);
    }

    public override string? Name
    {
        get => _name;
        set
        {
            _name = value;
            _context.DebugName = value!;
        }
    }

    internal void OnCompleted()
    {
        _commandList?.Dispose();
        _commandList = null;

        foreach (D3D11Swapchain sc in _referencedSwapchains)
            sc.RemoveCommandListReference(this);

        foreach (D3D11Buffer buffer in _submittedStagingBuffers)
            _availableStagingBuffers.Add(buffer);

        _referencedSwapchains.Clear();
        _submittedStagingBuffers.Clear();
    }

    private protected override void PushDebugGroupCore(ReadOnlySpan<char> name) =>
        _uda?.BeginEvent(name.ToString());

    private protected override void PopDebugGroupCore() => _uda?.EndEvent();

    private protected override void InsertDebugMarkerCore(ReadOnlySpan<char> name) =>
        _uda?.SetMarker(name.ToString());

    public override void Dispose()
    {
        if (_disposed)
            return;

        _uda?.Dispose();
        DeviceCommandList?.Dispose();
        _context1?.Dispose();
        _context.Dispose();

        foreach (ref BoundResourceSetInfo boundGraphicsSet in _graphicsResourceSets.AsSpan())
            boundGraphicsSet.Offsets.Dispose();

        foreach (ref BoundResourceSetInfo boundComputeSet in _computeResourceSets.AsSpan())
            boundComputeSet.Offsets.Dispose();

        foreach (D3D11Buffer buffer in _availableStagingBuffers)
            buffer.Dispose();

        _availableStagingBuffers.Clear();
        _disposed = true;
    }

    struct BoundTextureInfo
    {
        public int Slot;
        public ShaderStages Stages;
        public uint ResourceSet;
    }

    readonly struct D3D11BufferRange(D3D11Buffer buffer, uint offset, uint size)
        : IEquatable<D3D11BufferRange>
    {
        public readonly D3D11Buffer Buffer = buffer;
        public readonly uint Offset = offset;
        public readonly uint Size = size;

        public bool IsFullRange => Offset == 0 && Size == Buffer.SizeInBytes;

        public bool Equals(D3D11BufferRange other) =>
            Buffer == other.Buffer && Offset.Equals(other.Offset) && Size.Equals(other.Size);
    }
}
