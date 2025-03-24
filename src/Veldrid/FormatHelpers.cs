using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Veldrid;

internal static class FormatHelpers
{
    [SuppressMessage(
        "Style",
        "IDE0066:Convert switch statement to expression",
        Justification = "<Pending>"
    )]
    public static int GetElementCount(VertexElementFormat format) =>
        format switch
        {
            VertexElementFormat.Float1
                or VertexElementFormat.UInt1
                or VertexElementFormat.Int1
                or VertexElementFormat.Half1 => 1,

            VertexElementFormat.Float2
                or VertexElementFormat.Byte2_Norm
                or VertexElementFormat.Byte2
                or VertexElementFormat.SByte2_Norm
                or VertexElementFormat.SByte2
                or VertexElementFormat.UShort2_Norm
                or VertexElementFormat.UShort2
                or VertexElementFormat.Short2_Norm
                or VertexElementFormat.Short2
                or VertexElementFormat.UInt2
                or VertexElementFormat.Int2
                or VertexElementFormat.Half2 => 2,

            VertexElementFormat.Float3
                or VertexElementFormat.UInt3
                or VertexElementFormat.Int3 => 3,

            VertexElementFormat.Float4
                or VertexElementFormat.Byte4_Norm
                or VertexElementFormat.Byte4
                or VertexElementFormat.SByte4_Norm
                or VertexElementFormat.SByte4
                or VertexElementFormat.UShort4_Norm
                or VertexElementFormat.UShort4
                or VertexElementFormat.Short4_Norm
                or VertexElementFormat.Short4
                or VertexElementFormat.UInt4
                or VertexElementFormat.Int4
                or VertexElementFormat.Half4 => 4,

            _ => Illegal.Value<VertexElementFormat, int>()
        };

    internal static uint GetSampleCountUInt32(TextureSampleCount sampleCount) =>
        sampleCount switch
        {
            TextureSampleCount.Count1 => 1,
            TextureSampleCount.Count2 => 2,
            TextureSampleCount.Count4 => 4,
            TextureSampleCount.Count8 => 8,
            TextureSampleCount.Count16 => 16,
            TextureSampleCount.Count32 => 32,
            TextureSampleCount.Count64 => 64,
            _ => Illegal.Value<TextureSampleCount, uint>(),
        };

    internal static bool IsExactStencilFormat(PixelFormat format) => format is PixelFormat.D16_UNorm_S8_UInt or PixelFormat.D24_UNorm_S8_UInt or PixelFormat.D32_Float_S8_UInt;
    internal static bool IsStencilFormat(PixelFormat format) => IsExactStencilFormat(format);
    internal static bool IsExactDepthFormat(PixelFormat format) => format is PixelFormat.D16_UNorm or PixelFormat.D32_Float;

    internal static bool IsDepthFormat(PixelFormat format) =>
        format == PixelFormat.R16_UNorm
        || format == PixelFormat.R32_Float
        || IsExactDepthFormat(format);

    internal static bool IsExactDepthStencilFormat(PixelFormat format) => IsExactDepthFormat(format) || IsExactStencilFormat(format);
    internal static bool IsDepthStencilFormat(PixelFormat format) => IsDepthFormat(format) || IsStencilFormat(format);

    internal static bool IsDepthFormatPreferred(PixelFormat format, TextureUsage usage)
    {
        if ((usage & TextureUsage.DepthStencil) == TextureUsage.DepthStencil)
        {
            return true;
        }

        // TODO: could this still be useful?
        //       maybe instead of forcing a depth format, it could be used for asserts?
        // if ((usage & TextureUsage.Staging) == TextureUsage.Staging && IsDepthStencilFormat(format))
        // {
        //     return true;
        // }

        return false;
    }

    internal static bool IsCompressedFormat(PixelFormat format) =>
        format is PixelFormat.BC1_Rgb_UNorm
            or PixelFormat.BC1_Rgb_UNorm_SRgb
            or PixelFormat.BC1_Rgba_UNorm
            or PixelFormat.BC1_Rgba_UNorm_SRgb
            or PixelFormat.BC2_UNorm
            or PixelFormat.BC2_UNorm_SRgb
            or PixelFormat.BC3_UNorm
            or PixelFormat.BC3_UNorm_SRgb
            or PixelFormat.BC4_UNorm
            or PixelFormat.BC4_SNorm
            or PixelFormat.BC5_UNorm
            or PixelFormat.BC5_SNorm
            or PixelFormat.BC7_UNorm
            or PixelFormat.BC7_UNorm_SRgb
            or PixelFormat.ETC2_R8_G8_B8_UNorm
            or PixelFormat.ETC2_R8_G8_B8_A1_UNorm
            or PixelFormat.ETC2_R8_G8_B8_A8_UNorm;

    internal static uint GetRowPitch(uint width, PixelFormat format)
    {
        switch (format)
        {
            case PixelFormat.BC1_Rgb_UNorm:
            case PixelFormat.BC1_Rgb_UNorm_SRgb:
            case PixelFormat.BC1_Rgba_UNorm:
            case PixelFormat.BC1_Rgba_UNorm_SRgb:
            case PixelFormat.BC2_UNorm:
            case PixelFormat.BC2_UNorm_SRgb:
            case PixelFormat.BC3_UNorm:
            case PixelFormat.BC3_UNorm_SRgb:
            case PixelFormat.BC4_UNorm:
            case PixelFormat.BC4_SNorm:
            case PixelFormat.BC5_UNorm:
            case PixelFormat.BC5_SNorm:
            case PixelFormat.BC7_UNorm:
            case PixelFormat.BC7_UNorm_SRgb:
            case PixelFormat.ETC2_R8_G8_B8_UNorm:
            case PixelFormat.ETC2_R8_G8_B8_A1_UNorm:
            case PixelFormat.ETC2_R8_G8_B8_A8_UNorm:
                uint blocksPerRow = (width + 3) / 4;
                uint blockSizeInBytes = GetBlockSizeInBytes(format);
                return blocksPerRow * blockSizeInBytes;

            default:
                return width * FormatSizeHelpers.GetSizeInBytes(format);
        }
    }

    [SuppressMessage(
        "Style",
        "IDE0066:Convert switch statement to expression",
        Justification = "<Pending>"
    )]
    public static uint GetBlockSizeInBytes(PixelFormat format) =>
        format switch
        {
            PixelFormat.BC1_Rgb_UNorm
                or PixelFormat.BC1_Rgb_UNorm_SRgb
                or PixelFormat.BC1_Rgba_UNorm
                or PixelFormat.BC1_Rgba_UNorm_SRgb
                or PixelFormat.BC4_UNorm
                or PixelFormat.BC4_SNorm
                or PixelFormat.ETC2_R8_G8_B8_UNorm
                or PixelFormat.ETC2_R8_G8_B8_A1_UNorm => 8,

            PixelFormat.BC2_UNorm
                or PixelFormat.BC2_UNorm_SRgb
                or PixelFormat.BC3_UNorm
                or PixelFormat.BC3_UNorm_SRgb
                or PixelFormat.BC5_UNorm
                or PixelFormat.BC5_SNorm
                or PixelFormat.BC7_UNorm
                or PixelFormat.BC7_UNorm_SRgb
                or PixelFormat.ETC2_R8_G8_B8_A8_UNorm => 16,
            _ => Illegal.Value<PixelFormat, uint>()
        };

    internal static bool IsFormatViewCompatible(PixelFormat viewFormat, PixelFormat realFormat)
    {
        if (IsCompressedFormat(realFormat))
        {
            // return IsSrgbCounterpart(viewFormat, realFormat); // TODO
            throw new NotImplementedException();
        }

        return GetViewFamilyFormat(viewFormat) == GetViewFamilyFormat(realFormat);
    }

    [SuppressMessage(
        "Style",
        "IDE0066:Convert switch statement to expression",
        Justification = "<Pending>"
    )]
    internal static uint GetNumRows(uint height, PixelFormat format) =>
        format switch
        {
            PixelFormat.BC1_Rgb_UNorm
                or PixelFormat.BC1_Rgb_UNorm_SRgb
                or PixelFormat.BC1_Rgba_UNorm
                or PixelFormat.BC1_Rgba_UNorm_SRgb
                or PixelFormat.BC2_UNorm
                or PixelFormat.BC2_UNorm_SRgb
                or PixelFormat.BC3_UNorm
                or PixelFormat.BC3_UNorm_SRgb
                or PixelFormat.BC4_UNorm
                or PixelFormat.BC4_SNorm
                or PixelFormat.BC5_UNorm
                or PixelFormat.BC5_SNorm
                or PixelFormat.BC7_UNorm
                or PixelFormat.BC7_UNorm_SRgb
                or PixelFormat.ETC2_R8_G8_B8_UNorm
                or PixelFormat.ETC2_R8_G8_B8_A1_UNorm
                or PixelFormat.ETC2_R8_G8_B8_A8_UNorm => (height + 3) / 4,
            _ => height
        };

    internal static uint GetDepthPitch(uint rowPitch, uint height, PixelFormat format) => rowPitch * GetNumRows(height, format);
    internal static uint GetRegionSize(uint width, uint height, uint depth, PixelFormat format)
    {
        uint blockSizeInBytes;
        if (IsCompressedFormat(format))
        {
            Debug.Assert((width % 4 == 0 || width < 4) && (height % 4 == 0 || height < 4));
            blockSizeInBytes = GetBlockSizeInBytes(format);
            width /= 4;
            height /= 4;
        }
        else
        {
            blockSizeInBytes = FormatSizeHelpers.GetSizeInBytes(format);
        }

        return width * height * depth * blockSizeInBytes;
    }

    internal static TextureSampleCount GetSampleCount(uint samples) =>
        samples switch
        {
            1 => TextureSampleCount.Count1,
            2 => TextureSampleCount.Count2,
            4 => TextureSampleCount.Count4,
            8 => TextureSampleCount.Count8,
            16 => TextureSampleCount.Count16,
            32 => TextureSampleCount.Count32,
            64 => TextureSampleCount.Count64,
            _ => throw new VeldridException("Unsupported multisample count: " + samples),
        };

    [SuppressMessage(
        "Style",
        "IDE0066:Convert switch statement to expression",
        Justification = "<Pending>"
    )]
    internal static PixelFormat GetViewFamilyFormat(PixelFormat format) =>
        format switch
        {
            PixelFormat.R32_G32_B32_A32_Float
                or PixelFormat.R32_G32_B32_A32_UInt
                or PixelFormat.R32_G32_B32_A32_SInt => PixelFormat.R32_G32_B32_A32_Float,

            PixelFormat.R16_G16_B16_A16_Float
                or PixelFormat.R16_G16_B16_A16_UNorm
                or PixelFormat.R16_G16_B16_A16_UInt
                or PixelFormat.R16_G16_B16_A16_SNorm
                or PixelFormat.R16_G16_B16_A16_SInt => PixelFormat.R16_G16_B16_A16_Float,

            PixelFormat.R32_G32_Float
                or PixelFormat.R32_G32_UInt
                or PixelFormat.R32_G32_SInt => PixelFormat.R32_G32_Float,

            PixelFormat.R10_G10_B10_A2_UNorm
                or PixelFormat.R10_G10_B10_A2_UInt => PixelFormat.R10_G10_B10_A2_UNorm,

            PixelFormat.R8_G8_B8_A8_UNorm
                or PixelFormat.R8_G8_B8_A8_UNorm_SRgb
                or PixelFormat.R8_G8_B8_A8_UInt
                or PixelFormat.R8_G8_B8_A8_SNorm
                or PixelFormat.R8_G8_B8_A8_SInt => PixelFormat.R8_G8_B8_A8_UNorm,

            PixelFormat.R16_G16_Float
                or PixelFormat.R16_G16_UNorm
                or PixelFormat.R16_G16_UInt
                or PixelFormat.R16_G16_SNorm
                or PixelFormat.R16_G16_SInt => PixelFormat.R16_G16_Float,

            PixelFormat.R32_Float
                or PixelFormat.R32_UInt
                or PixelFormat.R32_SInt => PixelFormat.R32_Float,

            PixelFormat.R8_G8_UNorm
                or PixelFormat.R8_G8_UInt
                or PixelFormat.R8_G8_SNorm
                or PixelFormat.R8_G8_SInt => PixelFormat.R8_G8_UNorm,

            PixelFormat.R16_Float
                or PixelFormat.R16_UNorm
                or PixelFormat.R16_UInt
                or PixelFormat.R16_SNorm
                or PixelFormat.R16_SInt => PixelFormat.R16_Float,

            PixelFormat.R8_UNorm
                or PixelFormat.R8_UInt
                or PixelFormat.R8_SNorm
                or PixelFormat.R8_SInt => PixelFormat.R8_UNorm,

            PixelFormat.BC1_Rgba_UNorm
                or PixelFormat.BC1_Rgba_UNorm_SRgb
                or PixelFormat.BC1_Rgb_UNorm
                or PixelFormat.BC1_Rgb_UNorm_SRgb => PixelFormat.BC1_Rgba_UNorm,

            PixelFormat.BC2_UNorm or PixelFormat.BC2_UNorm_SRgb => PixelFormat.BC2_UNorm,
            PixelFormat.BC3_UNorm or PixelFormat.BC3_UNorm_SRgb => PixelFormat.BC3_UNorm,
            PixelFormat.BC4_UNorm or PixelFormat.BC4_SNorm => PixelFormat.BC4_UNorm,
            PixelFormat.BC5_UNorm or PixelFormat.BC5_SNorm => PixelFormat.BC5_UNorm,
            PixelFormat.B8_G8_R8_A8_UNorm or PixelFormat.B8_G8_R8_A8_UNorm_SRgb => PixelFormat.B8_G8_R8_A8_UNorm,
            PixelFormat.BC7_UNorm or PixelFormat.BC7_UNorm_SRgb => PixelFormat.BC7_UNorm,
            _ => format
        };
}
