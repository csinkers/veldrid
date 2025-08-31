using ImageMagick;

namespace Veldrid.ImageMagick;

/// <summary>
/// Represents a texture loaded from an image file, using the Magick.NET library (ImageMagick).
/// </summary>
public class ImageMagickTexture
{
    /// <summary>
    /// An array of images, each a single element in the mipmap chain.
    /// The first element is the largest, most detailed level, and each subsequent element
    /// is half its size, down to 1x1 pixel.
    /// </summary>
    public MagickImage[] Images { get; }

    /// <summary>
    /// The width of the largest image in the chain.
    /// </summary>
    public uint Width => Images[0].Width;

    /// <summary>
    /// The height of the largest image in the chain.
    /// </summary>
    public uint Height => Images[0].Height;

    /// <summary>
    /// The pixel format of all images.
    /// </summary>
    public PixelFormat Format { get; }

    /// <summary>
    /// The number of levels in the mipmap chain. This is equal to the length of the Images array.
    /// </summary>
    public uint MipLevels => (uint)Images.Length;

    /// <summary>Constructs a new MagickTexture</summary>
    public ImageMagickTexture(string path, bool mipmap = true, bool srgb = false)
        : this(new MagickImage(path), mipmap, srgb)
    {
    }

    /// <summary>Constructs a new MagickTexture</summary>
    public ImageMagickTexture(MagickImage image, bool mipmap = true, bool srgb = false)
    {
        Format = srgb
            ? PixelFormat.R8_G8_B8_A8_UNorm_SRgb
            : PixelFormat.R8_G8_B8_A8_UNorm;

        image.ColorSpace = srgb
            ? ColorSpace.sRGB
            : ColorSpace.RGB;

        Images = mipmap
            ? MipmapHelper.GenerateMipmaps(image)
            : [image];
    }

    /// <summary>
    /// Creates a Veldrid Texture object from this MagickTexture.
    /// </summary>
    public Texture CreateDeviceTexture(GraphicsDevice gd, ResourceFactory factory)
        => CreateTextureViaUpdate(gd, factory);

    Texture CreateTextureViaUpdate(GraphicsDevice gd, ResourceFactory factory)
    {
        Texture tex = factory.CreateTexture(
            TextureDescription.Texture2D(Width, Height, MipLevels, 1, Format, TextureUsage.Sampled)
        );

        for (int level = 0; level < MipLevels; level++)
        {
            MagickImage image = Images[level];
            using IPixelCollection<byte> pixelDataArray = image.GetPixels();
            byte[] pixelArray = pixelDataArray.ToByteArray(0, 0, image.Width, image.Height, PixelMapping.RGBA)!;

            gd.UpdateTexture(
                tex,
                pixelArray,
                0, 0, 0,
                image.Width,
                image.Height,
                1,
                (uint)level,
                0
            );
        }

        return tex;
    }
}
