﻿using System;
using System.Diagnostics;
using Vortice.Direct3D11;

namespace Veldrid.D3D11;

internal sealed class D3D11Texture : Texture
{
    string? _name;

    public override bool IsDisposed => DeviceTexture.NativePointer == IntPtr.Zero;

    public ID3D11Resource DeviceTexture { get; }
    public Vortice.DXGI.Format DxgiFormat { get; }
    public Vortice.DXGI.Format TypelessDxgiFormat => D3D11Formats.GetTypelessFormat(DxgiFormat);

    public D3D11Texture(ID3D11Device device, in TextureDescription description)
    {
        Width = description.Width;
        Height = description.Height;
        Depth = description.Depth;
        MipLevels = description.MipLevels;
        ArrayLayers = description.ArrayLayers;
        Format = description.Format;
        Usage = description.Usage;
        Type = description.Type;
        SampleCount = description.SampleCount;

        DxgiFormat = D3D11Formats.ToDxgiFormat(description.Format, description.Usage);

        CpuAccessFlags cpuFlags = CpuAccessFlags.None;
        ResourceUsage resourceUsage = ResourceUsage.Default;
        BindFlags bindFlags = BindFlags.None;
        ResourceOptionFlags optionFlags = ResourceOptionFlags.None;

        if ((description.Usage & TextureUsage.RenderTarget) == TextureUsage.RenderTarget)
            bindFlags |= BindFlags.RenderTarget;

        if ((description.Usage & TextureUsage.DepthStencil) == TextureUsage.DepthStencil)
            bindFlags |= BindFlags.DepthStencil;

        if ((description.Usage & TextureUsage.Sampled) == TextureUsage.Sampled)
            bindFlags |= BindFlags.ShaderResource;

        if ((description.Usage & TextureUsage.Storage) == TextureUsage.Storage)
            bindFlags |= BindFlags.UnorderedAccess | BindFlags.ShaderResource;

        if ((description.Usage & TextureUsage.Staging) == TextureUsage.Staging)
        {
            cpuFlags = CpuAccessFlags.Read | CpuAccessFlags.Write;
            resourceUsage = ResourceUsage.Staging;
        }

        if ((description.Usage & TextureUsage.GenerateMipmaps) != 0)
        {
            bindFlags |= BindFlags.RenderTarget | BindFlags.ShaderResource;
            optionFlags |= ResourceOptionFlags.GenerateMips;
        }

        int arraySize = (int)description.ArrayLayers;
        if ((description.Usage & TextureUsage.Cubemap) == TextureUsage.Cubemap)
        {
            optionFlags |= ResourceOptionFlags.TextureCube;
            arraySize *= 6;
        }

        int roundedWidth = (int)description.Width;
        int roundedHeight = (int)description.Height;
        if (FormatHelpers.IsCompressedFormat(description.Format))
        {
            roundedWidth = ((roundedWidth + 3) / 4) * 4;
            roundedHeight = ((roundedHeight + 3) / 4) * 4;
        }

        if (Type == TextureType.Texture1D)
        {
            Texture1DDescription desc1D = new()
            {
                Width = roundedWidth,
                MipLevels = (int)description.MipLevels,
                ArraySize = arraySize,
                Format = TypelessDxgiFormat,
                BindFlags = bindFlags,
                CPUAccessFlags = cpuFlags,
                Usage = resourceUsage,
                MiscFlags = optionFlags,
            };

            DeviceTexture = device.CreateTexture1D(desc1D);
        }
        else if (Type == TextureType.Texture2D)
        {
            Texture2DDescription deviceDescription = new()
            {
                Width = roundedWidth,
                Height = roundedHeight,
                MipLevels = (int)description.MipLevels,
                ArraySize = arraySize,
                Format = TypelessDxgiFormat,
                BindFlags = bindFlags,
                CPUAccessFlags = cpuFlags,
                Usage = resourceUsage,
                SampleDescription = new((int)FormatHelpers.GetSampleCountUInt32(SampleCount), 0),
                MiscFlags = optionFlags,
            };

            DeviceTexture = device.CreateTexture2D(deviceDescription);
        }
        else
        {
            Debug.Assert(Type == TextureType.Texture3D);
            Texture3DDescription desc3D = new()
            {
                Width = roundedWidth,
                Height = roundedHeight,
                Depth = (int)description.Depth,
                MipLevels = (int)description.MipLevels,
                Format = TypelessDxgiFormat,
                BindFlags = bindFlags,
                CPUAccessFlags = cpuFlags,
                Usage = resourceUsage,
                MiscFlags = optionFlags,
            };

            DeviceTexture = device.CreateTexture3D(desc3D);
        }
    }

    public D3D11Texture(ID3D11Texture2D existingTexture, TextureType type, PixelFormat format)
    {
        DeviceTexture = existingTexture;
        Width = (uint)existingTexture.Description.Width;
        Height = (uint)existingTexture.Description.Height;
        Depth = 1;
        MipLevels = (uint)existingTexture.Description.MipLevels;
        ArrayLayers = (uint)existingTexture.Description.ArraySize;
        Format = format;
        SampleCount = FormatHelpers.GetSampleCount(
            (uint)existingTexture.Description.SampleDescription.Count
        );
        Type = type;
        Usage = D3D11Formats.GetVdUsage(
            existingTexture.Description.BindFlags,
            existingTexture.Description.CPUAccessFlags,
            existingTexture.Description.MiscFlags
        );

        DxgiFormat = D3D11Formats.ToDxgiFormat(format, Usage);
    }

    private protected override TextureView CreateFullTextureView(GraphicsDevice gd)
    {
        TextureViewDescription desc = new(this);
        D3D11GraphicsDevice d3d11GD = Util.AssertSubtype<GraphicsDevice, D3D11GraphicsDevice>(gd);
        return new D3D11TextureView(d3d11GD, desc);
    }

    public override string? Name
    {
        get => _name;
        set
        {
            _name = value;
            DeviceTexture.DebugName = value!;
        }
    }

    private protected override void DisposeCore()
    {
        DeviceTexture.Dispose();
    }
}
