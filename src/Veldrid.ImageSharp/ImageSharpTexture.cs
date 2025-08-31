using System;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Veldrid.ImageSharp;

/// <summary>
/// Represents a texture loaded from an image file, using the ImageSharp library.
/// </summary>
public class ImageSharpTexture
{
    /// <summary>
    /// An array of images, each a single element in the mipmap chain.
    /// The first element is the largest, most detailed level, and each subsequent element
    /// is half its size, down to 1x1 pixel.
    /// </summary>
    public Image<Rgba32>[] Images { get; }

    /// <summary>
    /// The width of the largest image in the chain.
    /// </summary>
    public uint Width => (uint)Images[0].Width;

    /// <summary>
    /// The height of the largest image in the chain.
    /// </summary>
    public uint Height => (uint)Images[0].Height;

    /// <summary>
    /// The pixel format of all images.
    /// </summary>
    public PixelFormat Format { get; }

    /// <summary>
    /// The number of levels in the mipmap chain. This is equal to the length of the Images array.
    /// </summary>
    public uint MipLevels => (uint)Images.Length;

    /// <summary>Constructs a new ImageSharpTexture</summary>
    public ImageSharpTexture(string path)
        : this(Image.Load<Rgba32>(path)) { }

    /// <summary>Constructs a new ImageSharpTexture</summary>
    public ImageSharpTexture(string path, bool mipmap)
        : this(Image.Load<Rgba32>(path), mipmap) { }

    /// <summary>Constructs a new ImageSharpTexture</summary>
    public ImageSharpTexture(string path, bool mipmap, bool srgb)
        : this(Image.Load<Rgba32>(path), mipmap, srgb) { }

    /// <summary>Constructs a new ImageSharpTexture</summary>
    public ImageSharpTexture(Stream stream)
        : this(Image.Load<Rgba32>(stream)) { }

    /// <summary>Constructs a new ImageSharpTexture</summary>
    public ImageSharpTexture(Stream stream, bool mipmap)
        : this(Image.Load<Rgba32>(stream), mipmap) { }

    /// <summary>Constructs a new ImageSharpTexture</summary>
    public ImageSharpTexture(Stream stream, bool mipmap, bool srgb)
        : this(Image.Load<Rgba32>(stream), mipmap, srgb) { }

    /// <summary>Constructs a new ImageSharpTexture</summary>
    public ImageSharpTexture(Image<Rgba32> image, bool mipmap = true)
        : this(image, mipmap, false) { }

    /// <summary>Constructs a new ImageSharpTexture</summary>
    public ImageSharpTexture(Image<Rgba32> image, bool mipmap, bool srgb)
    {
        Format = srgb ? PixelFormat.R8_G8_B8_A8_UNorm_SRgb : PixelFormat.R8_G8_B8_A8_UNorm;
        if (mipmap)
        {
            Images = MipmapHelper.GenerateMipmaps(image);
        }
        else
        {
            Images = [image];
        }
    }

    /// <summary>
    /// Creates a Veldrid Texture object from this ImageSharpTexture.
    /// </summary>
    public Texture CreateDeviceTexture(GraphicsDevice gd, ResourceFactory factory) => CreateTextureViaUpdate(gd, factory);

    unsafe Texture CreateTextureViaUpdate(GraphicsDevice gd, ResourceFactory factory)
    {
        Texture tex = factory.CreateTexture(
            TextureDescription.Texture2D(Width, Height, MipLevels, 1, Format, TextureUsage.Sampled)
        );

        for (int level = 0; level < MipLevels; level++)
        {
            Image<Rgba32> image = Images[level];

            if (image.DangerousTryGetSinglePixelMemory(out Memory<Rgba32> pixels))
            {
                fixed (void* pixelPtr = pixels.Span)
                {
                    gd.UpdateTexture(
                        tex,
                        (IntPtr)pixelPtr,
                        (uint)(sizeof(Rgba32) * image.Width * image.Height),
                        0, 0, 0,
                        (uint)image.Width,
                        (uint)image.Height,
                        1,
                        (uint)level,
                        0
                    );
                }
            }
            else
            {
                int level2 = level;
                image.ProcessPixelRows(pixels2 =>
                {
                    for (int y = 0; y < pixels2.Height; y++)
                    {
                        Span<Rgba32> span = pixels2.GetRowSpan(y);

                        fixed (void* pixelPtr = span)
                        {
                            gd.UpdateTexture(
                                tex,
                                (IntPtr)pixelPtr,
                                (uint)(sizeof(Rgba32) * image.Width),
                                0,
                                (uint)y,
                                0,
                                (uint)pixels2.Width,
                                height: 1,
                                depth: 1,
                                (uint)level2,
                                0
                            );
                        }
                    }
                });
            }
        }

        return tex;
    }
}
