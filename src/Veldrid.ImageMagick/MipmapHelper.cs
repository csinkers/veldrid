using System;
using System.Diagnostics;
using ImageMagick;

namespace Veldrid.ImageMagick
{
    /// <summary>
    /// Contains helper methods for dealing with mipmaps.
    /// </summary>
    internal static class MipmapHelper
    {
        internal static MagickImage[] GenerateMipmaps(MagickImage baseImage)
        {
            uint mipLevelCount = ComputeMipLevels(baseImage.Width, baseImage.Height);
            MagickImage[] mipLevels = new MagickImage[mipLevelCount];
            mipLevels[0] = baseImage;
            int i = 1;

            uint currentWidth = baseImage.Width;
            uint currentHeight = baseImage.Height;

            while (currentWidth != 1 || currentHeight != 1)
            {
                uint newWidth = Math.Max(1, currentWidth / 2);
                uint newHeight = Math.Max(1, currentHeight / 2);

                MagickImage newImage = (MagickImage)baseImage.CloneAndMutate(x => 
                    x.Resize(newWidth, newHeight, FilterType.Cubic)
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
        static uint ComputeMipLevels(uint width, uint height)
            => 1 + (uint)Math.Floor(Math.Log(Math.Max(width, height), 2));
    }
}
