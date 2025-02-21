using System;
using Veldrid.MetalBindings;

namespace Veldrid.MTL;

internal sealed class MTLTexture : Texture
{
    bool _disposed;

    /// <summary>
    /// The native MTLTexture object. This property is only valid for non-staging Textures.
    /// </summary>
    public MetalBindings.MTLTexture DeviceTexture { get; }

    /// <summary>
    /// The staging MTLBuffer object. This property is only valid for staging Textures.
    /// </summary>
    public MetalBindings.MTLBuffer StagingBuffer { get; }

    public override string? Name { get; set; }

    public override bool IsDisposed => _disposed;

    public MTLPixelFormat MTLPixelFormat { get; }
    public MTLTextureType MTLTextureType { get; }

    public MTLTexture(in TextureDescription description, MTLGraphicsDevice _gd)
    {
        Width = description.Width;
        Height = description.Height;
        Depth = description.Depth;
        ArrayLayers = description.ArrayLayers;
        MipLevels = description.MipLevels;
        Format = description.Format;
        Usage = description.Usage;
        Type = description.Type;
        SampleCount = description.SampleCount;

        MTLPixelFormat = MTLFormats.VdToMTLPixelFormat(Format, Usage);
        MTLTextureType = MTLFormats.VdToMTLTextureType(
            Type,
            ArrayLayers,
            SampleCount != TextureSampleCount.Count1,
            (Usage & TextureUsage.Cubemap) != 0
        );
        if (Usage != TextureUsage.Staging)
        {
            MTLTextureDescriptor texDescriptor = MTLTextureDescriptor.New();
            texDescriptor.width = Width;
            texDescriptor.height = Height;
            texDescriptor.depth = Depth;
            texDescriptor.mipmapLevelCount = MipLevels;
            texDescriptor.arrayLength = ArrayLayers;
            texDescriptor.sampleCount = FormatHelpers.GetSampleCountUInt32(SampleCount);
            texDescriptor.textureType = MTLTextureType;
            texDescriptor.pixelFormat = MTLPixelFormat;
            texDescriptor.textureUsage = MTLFormats.VdToMTLTextureUsage(Usage);
            texDescriptor.storageMode = MTLStorageMode.Private;

            DeviceTexture = _gd.Device.newTextureWithDescriptor(texDescriptor);
            ObjectiveCRuntime.release(texDescriptor.NativePtr);
        }
        else
        {
            // uint blockSize = FormatHelpers.IsCompressedFormat(Format) ? 4u : 1u;
            uint totalStorageSize = 0;
            for (uint level = 0; level < MipLevels; level++)
            {
                Util.GetMipDimensions(
                    this,
                    level,
                    out uint levelWidth,
                    out uint levelHeight,
                    out uint levelDepth
                );

                // uint storageWidth = Math.Max(levelWidth, blockSize);
                // uint storageHeight = Math.Max(levelHeight, blockSize);

                totalStorageSize +=
                    levelDepth
                    * FormatHelpers.GetDepthPitch(
                        FormatHelpers.GetRowPitch(levelWidth, Format),
                        levelHeight,
                        Format
                    );
            }
            totalStorageSize *= ArrayLayers;

            StagingBuffer = _gd.Device.newBufferWithLengthOptions(
                totalStorageSize,
                MTLResourceOptions.StorageModeShared
            );
        }
    }

    public MTLTexture(ulong nativeTexture, in TextureDescription description)
    {
        DeviceTexture = new((IntPtr)nativeTexture);
        Width = description.Width;
        Height = description.Height;
        Depth = description.Depth;
        ArrayLayers = description.ArrayLayers;
        MipLevels = description.MipLevels;
        Format = description.Format;
        Usage = description.Usage;
        Type = description.Type;
        SampleCount = description.SampleCount;

        MTLPixelFormat = MTLFormats.VdToMTLPixelFormat(Format, Usage);
        MTLTextureType = MTLFormats.VdToMTLTextureType(
            Type,
            ArrayLayers,
            SampleCount != TextureSampleCount.Count1,
            (Usage & TextureUsage.Cubemap) != 0
        );
    }

    private protected override void DisposeCore()
    {
        if (!_disposed)
        {
            _disposed = true;
            if (!StagingBuffer.IsNull)
            {
                ObjectiveCRuntime.release(StagingBuffer.NativePtr);
            }
            else
            {
                ObjectiveCRuntime.release(DeviceTexture.NativePtr);
            }
        }
    }
}
