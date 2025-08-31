using System;
using System.Diagnostics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Veldrid.ImageSharp;

/// <summary>
/// Contains helper methods for dealing with mipmaps.
/// </summary>
internal static class MipmapHelper
{
    internal static Image<Rgba32>[] GenerateMipmaps(Image<Rgba32> baseImage)
    {
        int mipLevelCount = ComputeMipLevels(baseImage.Width, baseImage.Height);
        Image<Rgba32>[] mipLevels = new Image<Rgba32>[mipLevelCount];
        mipLevels[0] = baseImage;
        int i = 1;

        int currentWidth = baseImage.Width;
        int currentHeight = baseImage.Height;
        while (currentWidth != 1 || currentHeight != 1)
        {
            int newWidth = Math.Max(1, currentWidth / 2);
            int newHeight = Math.Max(1, currentHeight / 2);
            Image<Rgba32> newImage = baseImage.Clone(context =>
                context.Resize(newWidth, newHeight, KnownResamplers.Lanczos3)
            );
            Debug.Assert(i < mipLevelCount);
            mipLevels[i] = newImage;

            i++;
            currentWidth = newWidth;
            currentHeight = newHeight;
        }

        Debug.Assert(i == mipLevelCount);

        return mipLevels;
    }

    // Gets the number of mipmap levels needed for a texture of the given dimensions.
    static int ComputeMipLevels(int width, int height)
        => 1 + (int)Math.Floor(Math.Log(Math.Max(width, height), 2));
}
