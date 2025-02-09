using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Veldrid;

/// <summary>
/// Helper methods for determining the size of various pixel and vertex formats.
/// </summary>
public static class FormatSizeHelpers
{
    /// <summary>
    /// Given a pixel format, returns the number of bytes required to store a single pixel.
    /// </summary>
    /// <param name="format">An uncompressed pixel format.</param>
    /// <returns>The number of bytes required to store a single pixel in the given format.</returns>
    public static uint GetSizeInBytes(PixelFormat format)
    {
        switch (format)
        {
            case PixelFormat.R8_UNorm:
            case PixelFormat.R8_SNorm:
            case PixelFormat.R8_UInt:
            case PixelFormat.R8_SInt:
                return 1;

            case PixelFormat.R16_UNorm:
            case PixelFormat.R16_SNorm:
            case PixelFormat.R16_UInt:
            case PixelFormat.R16_SInt:
            case PixelFormat.R16_Float:
            case PixelFormat.R8_G8_UNorm:
            case PixelFormat.R8_G8_SNorm:
            case PixelFormat.R8_G8_UInt:
            case PixelFormat.R8_G8_SInt:
            case PixelFormat.D16_UNorm:
                return 2;

            case PixelFormat.D16_UNorm_S8_UInt:
                return 3;

            case PixelFormat.R32_UInt:
            case PixelFormat.R32_SInt:
            case PixelFormat.R32_Float:
            case PixelFormat.R16_G16_UNorm:
            case PixelFormat.R16_G16_SNorm:
            case PixelFormat.R16_G16_UInt:
            case PixelFormat.R16_G16_SInt:
            case PixelFormat.R16_G16_Float:
            case PixelFormat.R8_G8_B8_A8_UNorm:
            case PixelFormat.R8_G8_B8_A8_UNorm_SRgb:
            case PixelFormat.R8_G8_B8_A8_SNorm:
            case PixelFormat.R8_G8_B8_A8_UInt:
            case PixelFormat.R8_G8_B8_A8_SInt:
            case PixelFormat.B8_G8_R8_A8_UNorm:
            case PixelFormat.B8_G8_R8_A8_UNorm_SRgb:
            case PixelFormat.R10_G10_B10_A2_UNorm:
            case PixelFormat.R10_G10_B10_A2_UInt:
            case PixelFormat.R11_G11_B10_Float:
            case PixelFormat.D24_UNorm_S8_UInt:
            case PixelFormat.D32_Float:
                return 4;

            case PixelFormat.D32_Float_S8_UInt:
                return 5;

            case PixelFormat.R16_G16_B16_A16_UNorm:
            case PixelFormat.R16_G16_B16_A16_SNorm:
            case PixelFormat.R16_G16_B16_A16_UInt:
            case PixelFormat.R16_G16_B16_A16_SInt:
            case PixelFormat.R16_G16_B16_A16_Float:
            case PixelFormat.R32_G32_UInt:
            case PixelFormat.R32_G32_SInt:
            case PixelFormat.R32_G32_Float:
                return 8;

            case PixelFormat.R32_G32_B32_A32_Float:
            case PixelFormat.R32_G32_B32_A32_UInt:
            case PixelFormat.R32_G32_B32_A32_SInt:
                return 16;

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
                Debug.Fail("GetSizeInBytes should not be used on a compressed format.");
                return Illegal.Value<PixelFormat, uint>();

            default:
                return Illegal.Value<PixelFormat, uint>();
        }
    }

    /// <summary>
    /// Given a vertex element format, returns the number of bytes required
    /// to store an element in that format.
    /// </summary>
    /// <param name="format">A vertex element format.</param>
    /// <returns>The number of bytes required to store an element in the given format.</returns>
    public static uint GetSizeInBytes(VertexElementFormat format) =>
        format switch
        {
            VertexElementFormat.Byte2_Norm
            or VertexElementFormat.Byte2
            or VertexElementFormat.SByte2_Norm
            or VertexElementFormat.SByte2
            or VertexElementFormat.Half1 => 2,

            VertexElementFormat.Float1
            or VertexElementFormat.UInt1
            or VertexElementFormat.Int1
            or VertexElementFormat.Byte4_Norm
            or VertexElementFormat.Byte4
            or VertexElementFormat.SByte4_Norm
            or VertexElementFormat.SByte4
            or VertexElementFormat.UShort2_Norm
            or VertexElementFormat.UShort2
            or VertexElementFormat.Short2_Norm
            or VertexElementFormat.Short2
            or VertexElementFormat.Half2 => 4,

            VertexElementFormat.Float2
            or VertexElementFormat.UInt2
            or VertexElementFormat.Int2
            or VertexElementFormat.UShort4_Norm
            or VertexElementFormat.UShort4
            or VertexElementFormat.Short4_Norm
            or VertexElementFormat.Short4
            or VertexElementFormat.Half4 => 8,

            VertexElementFormat.Float3 or VertexElementFormat.UInt3 or VertexElementFormat.Int3 =>
                12,

            VertexElementFormat.Float4 or VertexElementFormat.UInt4 or VertexElementFormat.Int4 =>
                16,

            _ => Illegal.Value<VertexElementFormat, uint>(),
        };
}
