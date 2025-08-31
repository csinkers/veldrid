using System;
using Vortice.Direct3D11;
using Vortice.Mathematics;

namespace Veldrid.D3D11;

internal sealed class D3D11Sampler : Sampler
{
    string? _name;

    public ID3D11SamplerState DeviceSampler { get; }

    public D3D11Sampler(ID3D11Device device, in SamplerDescription description)
    {
        ComparisonFunction comparision =
            description.ComparisonKind == null
                ? ComparisonFunction.Never
                : D3D11Formats.VdToD3D11ComparisonFunc(description.ComparisonKind.Value);

        Vortice.Direct3D11.SamplerDescription samplerStateDesc = new()
        {
            AddressU = D3D11Formats.VdToD3D11AddressMode(description.AddressModeU),
            AddressV = D3D11Formats.VdToD3D11AddressMode(description.AddressModeV),
            AddressW = D3D11Formats.VdToD3D11AddressMode(description.AddressModeW),
            Filter = D3D11Formats.ToD3D11Filter(
                description.Filter,
                description.ComparisonKind.HasValue
            ),
            MinLOD = description.MinimumLod,
            MaxLOD = description.MaximumLod,
            MaxAnisotropy = description.MaximumAnisotropy,
            ComparisonFunc = comparision,
            MipLODBias = description.LodBias,
            BorderColor = ToRawColor4(description.BorderColor),
        };

        DeviceSampler = device.CreateSamplerState(samplerStateDesc);
    }

    static Color4 ToRawColor4(SamplerBorderColor borderColor)
    {
        return borderColor switch
        {
            SamplerBorderColor.TransparentBlack => new(0, 0, 0, 0),
            SamplerBorderColor.OpaqueBlack => new(0, 0, 0),
            SamplerBorderColor.OpaqueWhite => new(1, 1, 1),
            _ => Illegal.Value<SamplerBorderColor, Color4>(),
        };
    }

    public override string? Name
    {
        get => _name;
        set
        {
            _name = value;
            DeviceSampler.DebugName = value!;
        }
    }

    public override bool IsDisposed => DeviceSampler.NativePointer == IntPtr.Zero;

    public override void Dispose()
    {
        DeviceSampler.Dispose();
    }
}
