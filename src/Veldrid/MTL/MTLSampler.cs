using System;
using Veldrid.MetalBindings;

namespace Veldrid.MTL;

internal sealed class MTLSampler : Sampler
{
    bool _disposed;

    public MTLSamplerState DeviceSampler { get; }

    public MTLSampler(in SamplerDescription description, MTLGraphicsDevice gd)
    {
        MTLFormats.GetMinMagMipFilter(
            description.Filter,
            out MTLSamplerMinMagFilter min,
            out MTLSamplerMinMagFilter mag,
            out MTLSamplerMipFilter mip
        );

        MTLSamplerDescriptor mtlDesc = MTLSamplerDescriptor.New();
        mtlDesc.sAddressMode = MTLFormats.VdToMTLAddressMode(description.AddressModeU);
        mtlDesc.tAddressMode = MTLFormats.VdToMTLAddressMode(description.AddressModeV);
        mtlDesc.rAddressMode = MTLFormats.VdToMTLAddressMode(description.AddressModeW);
        mtlDesc.minFilter = min;
        mtlDesc.magFilter = mag;
        mtlDesc.mipFilter = mip;
        if (gd.MetalFeatures.IsMacOS)
        {
            mtlDesc.borderColor = MTLFormats.VdToMTLBorderColor(description.BorderColor);
        }
        if (description.ComparisonKind != null)
        {
            mtlDesc.compareFunction = MTLFormats.VdToMTLCompareFunction(
                description.ComparisonKind.Value
            );
        }
        mtlDesc.lodMinClamp = description.MinimumLod;
        mtlDesc.lodMaxClamp = description.MaximumLod;
        mtlDesc.maxAnisotropy = Math.Max(1, description.MaximumAnisotropy);
        DeviceSampler = gd.Device.newSamplerStateWithDescriptor(mtlDesc);
        ObjectiveCRuntime.release(mtlDesc.NativePtr);
    }

    public override string? Name { get; set; }

    public override bool IsDisposed => _disposed;

    public override void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            ObjectiveCRuntime.release(DeviceSampler.NativePtr);
        }
    }
}
