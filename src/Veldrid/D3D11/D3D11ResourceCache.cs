﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Vortice.Direct3D11;

namespace Veldrid.D3D11;

internal sealed class D3D11ResourceCache(ID3D11Device device) : IDisposable
{
    readonly object _lock = new();

    readonly Dictionary<BlendStateDescription, ID3D11BlendState> _blendStates = new();
    readonly Dictionary<DepthStencilStateDescription, ID3D11DepthStencilState> _depthStencilStates =
        new();
    readonly Dictionary<D3D11RasterizerStateCacheKey, ID3D11RasterizerState> _rasterizerStates =
        new();
    readonly Dictionary<InputLayoutCacheKey, ID3D11InputLayout> _inputLayouts = new();

    public void GetPipelineResources(
        in BlendStateDescription blendDesc,
        in DepthStencilStateDescription dssDesc,
        in RasterizerStateDescription rasterDesc,
        bool multisample,
        VertexLayoutDescription[]? vertexLayouts,
        byte[]? vsBytecode,
        out ID3D11BlendState blendState,
        out ID3D11DepthStencilState depthState,
        out ID3D11RasterizerState rasterState,
        out ID3D11InputLayout? inputLayout
    )
    {
        lock (_lock)
        {
            blendState = GetBlendState(blendDesc);
            depthState = GetDepthStencilState(dssDesc);
            rasterState = GetRasterizerState(rasterDesc, multisample);
            inputLayout = GetInputLayout(vertexLayouts, vsBytecode);
        }
    }

    ID3D11BlendState GetBlendState(in BlendStateDescription description)
    {
        Debug.Assert(Monitor.IsEntered(_lock));
        if (!_blendStates.TryGetValue(description, out ID3D11BlendState? blendState))
        {
            blendState = CreateNewBlendState(description);
            BlendStateDescription key = description;
            key.AttachmentStates = (BlendAttachmentDescription[])key.AttachmentStates.Clone();
            _blendStates.Add(key, blendState);
        }

        return blendState;
    }

    ID3D11BlendState CreateNewBlendState(in BlendStateDescription description)
    {
        BlendAttachmentDescription[] attachmentStates = description.AttachmentStates;
        BlendDescription d3dBlendStateDesc = new();

        for (int i = 0; i < attachmentStates.Length; i++)
        {
            BlendAttachmentDescription state = attachmentStates[i];
            ref RenderTargetBlendDescription renderTarget = ref d3dBlendStateDesc.RenderTarget[i];
            renderTarget.BlendEnable = state.BlendEnabled;
            renderTarget.RenderTargetWriteMask = D3D11Formats.VdToD3D11ColorWriteEnable(
                state.ColorWriteMask.GetOrDefault()
            );
            renderTarget.SourceBlend = D3D11Formats.VdToD3D11Blend(state.SourceColorFactor);
            renderTarget.DestinationBlend = D3D11Formats.VdToD3D11Blend(
                state.DestinationColorFactor
            );
            renderTarget.BlendOperation = D3D11Formats.VdToD3D11BlendOperation(state.ColorFunction);
            renderTarget.SourceBlendAlpha = D3D11Formats.VdToD3D11Blend(state.SourceAlphaFactor);
            renderTarget.DestinationBlendAlpha = D3D11Formats.VdToD3D11Blend(
                state.DestinationAlphaFactor
            );
            renderTarget.BlendOperationAlpha = D3D11Formats.VdToD3D11BlendOperation(
                state.AlphaFunction
            );
        }

        d3dBlendStateDesc.AlphaToCoverageEnable = description.AlphaToCoverageEnabled;
        d3dBlendStateDesc.IndependentBlendEnable = true;

        return device.CreateBlendState(d3dBlendStateDesc);
    }

    ID3D11DepthStencilState GetDepthStencilState(in DepthStencilStateDescription description)
    {
        Debug.Assert(Monitor.IsEntered(_lock));
        if (!_depthStencilStates.TryGetValue(description, out ID3D11DepthStencilState? dss))
        {
            dss = CreateNewDepthStencilState(description);
            DepthStencilStateDescription key = description;
            _depthStencilStates.Add(key, dss);
        }

        return dss;
    }

    ID3D11DepthStencilState CreateNewDepthStencilState(in DepthStencilStateDescription description)
    {
        DepthStencilDescription dssDesc = new()
        {
            DepthFunc = D3D11Formats.VdToD3D11ComparisonFunc(description.DepthComparison),
            DepthEnable = description.DepthTestEnabled,
            DepthWriteMask = description.DepthWriteEnabled
                ? DepthWriteMask.All
                : DepthWriteMask.Zero,
            StencilEnable = description.StencilTestEnabled,
            FrontFace = ToD3D11StencilOpDesc(description.StencilFront),
            BackFace = ToD3D11StencilOpDesc(description.StencilBack),
            StencilReadMask = description.StencilReadMask,
            StencilWriteMask = description.StencilWriteMask,
        };

        return device.CreateDepthStencilState(dssDesc);
    }

    static DepthStencilOperationDescription ToD3D11StencilOpDesc(StencilBehaviorDescription sbd)
    {
        return new()
        {
            StencilFunc = D3D11Formats.VdToD3D11ComparisonFunc(sbd.Comparison),
            StencilPassOp = D3D11Formats.VdToD3D11StencilOperation(sbd.Pass),
            StencilFailOp = D3D11Formats.VdToD3D11StencilOperation(sbd.Fail),
            StencilDepthFailOp = D3D11Formats.VdToD3D11StencilOperation(sbd.DepthFail),
        };
    }

    ID3D11RasterizerState GetRasterizerState(
        in RasterizerStateDescription description,
        bool multisample
    )
    {
        Debug.Assert(Monitor.IsEntered(_lock));
        D3D11RasterizerStateCacheKey key = new(description, multisample);
        if (!_rasterizerStates.TryGetValue(key, out ID3D11RasterizerState? rasterizerState))
        {
            rasterizerState = CreateNewRasterizerState(key);
            _rasterizerStates.Add(key, rasterizerState);
        }

        return rasterizerState;
    }

    ID3D11RasterizerState CreateNewRasterizerState(in D3D11RasterizerStateCacheKey key)
    {
        RasterizerDescription rssDesc = new()
        {
            CullMode = D3D11Formats.VdToD3D11CullMode(key.VeldridDescription.CullMode),
            FillMode = D3D11Formats.VdToD3D11FillMode(key.VeldridDescription.FillMode),
            DepthClipEnable = key.VeldridDescription.DepthClipEnabled,
            ScissorEnable = key.VeldridDescription.ScissorTestEnabled,
            FrontCounterClockwise = key.VeldridDescription.FrontFace == FrontFace.CounterClockwise,
            MultisampleEnable = key.Multisampled,
        };

        return device.CreateRasterizerState(rssDesc);
    }

    ID3D11InputLayout? GetInputLayout(VertexLayoutDescription[]? vertexLayouts, byte[]? vsBytecode)
    {
        Debug.Assert(Monitor.IsEntered(_lock));

        if (vsBytecode == null || vertexLayouts == null || vertexLayouts.Length == 0)
        {
            return null;
        }

        InputLayoutCacheKey tempKey = InputLayoutCacheKey.CreateTempKey(vertexLayouts);
        if (!_inputLayouts.TryGetValue(tempKey, out ID3D11InputLayout? inputLayout))
        {
            inputLayout = CreateNewInputLayout(vertexLayouts, vsBytecode);
            InputLayoutCacheKey permanentKey = InputLayoutCacheKey.CreatePermanentKey(
                vertexLayouts
            );
            _inputLayouts.Add(permanentKey, inputLayout);
        }

        return inputLayout;
    }

    ID3D11InputLayout CreateNewInputLayout(
        VertexLayoutDescription[] vertexLayouts,
        byte[] vsBytecode
    )
    {
        int totalCount = 0;
        for (int i = 0; i < vertexLayouts.Length; i++)
        {
            totalCount += vertexLayouts[i].Elements.Length;
        }

        int element = 0; // Total element index across slots.
        InputElementDescription[] elements = new InputElementDescription[totalCount];
        SemanticIndices si = new();
        for (int slot = 0; slot < vertexLayouts.Length; slot++)
        {
            VertexElementDescription[] elementDescs = vertexLayouts[slot].Elements;
            uint stepRate = vertexLayouts[slot].InstanceStepRate;
            int currentOffset = 0;
            for (int i = 0; i < elementDescs.Length; i++)
            {
                VertexElementDescription desc = elementDescs[i];
                elements[element] = new(
                    GetSemanticString(desc.Semantic),
                    SemanticIndices.GetAndIncrement(ref si, desc.Semantic),
                    D3D11Formats.ToDxgiFormat(desc.Format),
                    desc.Offset != 0 ? (int)desc.Offset : currentOffset,
                    slot,
                    stepRate == 0
                        ? InputClassification.PerVertexData
                        : InputClassification.PerInstanceData,
                    (int)stepRate
                );

                currentOffset += (int)FormatSizeHelpers.GetSizeInBytes(desc.Format);
                element += 1;
            }
        }

        return device.CreateInputLayout(elements, vsBytecode);
    }

    string GetSemanticString(VertexElementSemantic semantic)
    {
        return semantic switch
        {
            VertexElementSemantic.Position => "POSITION",
            VertexElementSemantic.Normal => "NORMAL",
            VertexElementSemantic.TextureCoordinate => "TEXCOORD",
            VertexElementSemantic.Color => "COLOR",
            _ => Illegal.Value<VertexElementSemantic, string>(),
        };
    }

    public void Dispose()
    {
        foreach (KeyValuePair<BlendStateDescription, ID3D11BlendState> kvp in _blendStates)
        {
            kvp.Value.Dispose();
        }
        foreach (
            KeyValuePair<
                DepthStencilStateDescription,
                ID3D11DepthStencilState
            > kvp in _depthStencilStates
        )
        {
            kvp.Value.Dispose();
        }
        foreach (
            KeyValuePair<
                D3D11RasterizerStateCacheKey,
                ID3D11RasterizerState
            > kvp in _rasterizerStates
        )
        {
            kvp.Value.Dispose();
        }
        foreach (KeyValuePair<InputLayoutCacheKey, ID3D11InputLayout> kvp in _inputLayouts)
        {
            kvp.Value.Dispose();
        }
    }

    struct SemanticIndices
    {
        int _position;
        int _texCoord;
        int _normal;
        int _color;

        public static int GetAndIncrement(ref SemanticIndices si, VertexElementSemantic type)
        {
            return type switch
            {
                VertexElementSemantic.Position => si._position++,
                VertexElementSemantic.TextureCoordinate => si._texCoord++,
                VertexElementSemantic.Normal => si._normal++,
                VertexElementSemantic.Color => si._color++,
                _ => Illegal.Value<VertexElementSemantic, int>(),
            };
        }
    }

    struct InputLayoutCacheKey : IEquatable<InputLayoutCacheKey>
    {
        public VertexLayoutDescription[] VertexLayouts;

        public static InputLayoutCacheKey CreateTempKey(VertexLayoutDescription[] original)
        {
            return new() { VertexLayouts = original };
        }

        public static InputLayoutCacheKey CreatePermanentKey(VertexLayoutDescription[] original)
        {
            VertexLayoutDescription[] vertexLayouts = new VertexLayoutDescription[original.Length];
            for (int i = 0; i < original.Length; i++)
            {
                vertexLayouts[i].Stride = original[i].Stride;
                vertexLayouts[i].InstanceStepRate = original[i].InstanceStepRate;
                vertexLayouts[i].Elements = (VertexElementDescription[])
                    original[i].Elements.Clone();
            }

            return new() { VertexLayouts = vertexLayouts };
        }

        public bool Equals(InputLayoutCacheKey other)
        {
            return Util.ArrayEqualsEquatable(VertexLayouts, other.VertexLayouts);
        }

        public override int GetHashCode()
        {
            return HashHelper.Array(VertexLayouts);
        }
    }

    struct D3D11RasterizerStateCacheKey(
        RasterizerStateDescription veldridDescription,
        bool multisampled
    ) : IEquatable<D3D11RasterizerStateCacheKey>
    {
        public RasterizerStateDescription VeldridDescription = veldridDescription;
        public readonly bool Multisampled = multisampled;

        public bool Equals(D3D11RasterizerStateCacheKey other) =>
            VeldridDescription.Equals(other.VeldridDescription)
            && Multisampled.Equals(other.Multisampled);

        public override int GetHashCode() =>
            HashHelper.Combine(VeldridDescription.GetHashCode(), Multisampled.GetHashCode());
    }
}
