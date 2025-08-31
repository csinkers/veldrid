using System;
using ImageMagick;

namespace Veldrid.ImageMagick
{
    /// <summary>
    /// A cubemap texture created from 6 images.
    /// </summary>
    public class ImageMagickCubemapTexture
    {
        /// <summary>
        /// An array of images, each face of a cubemap.
        /// Access of CubemapTextures[2][3] means face 2 with mipmap level 3
        /// </summary>
        public MagickImage[][] CubemapTextures { get; }

        /// <summary>
        /// The width of a cubemap texture.
        /// </summary>
        public uint Width => CubemapTextures[0][0].Width;

        /// <summary>
        /// The height of a cubemap texture.
        /// </summary>
        public uint Height => CubemapTextures[0][0].Height;

        /// <summary>
        /// The pixel format cubemap textures.
        /// </summary>
        public PixelFormat Format => PixelFormat.R8_G8_B8_A8_UNorm;

        /// <summary>
        /// The size of each pixel, in bytes.
        /// </summary>
        public uint PixelSizeInBytes => sizeof(byte) * 4;

        /// <summary>
        /// The number of levels in the mipmap chain. This is equal to the length of the Images array.
        /// </summary>
        public uint MipLevels => (uint)CubemapTextures[0].Length;

        /// <summary>
        /// Provides standardized access to the cubemap texture array
        /// </summary>
        const int PositiveXArrayLayer = 0;

        const int NegativeXArrayLayer = 1;
        const int PositiveYArrayLayer = 2;
        const int NegativeYArrayLayer = 3;
        const int PositiveZArrayLayer = 4;
        const int NegativeZArrayLayer = 5;

        /// <summary>
        /// Creates a new instance of ImageMagickCubemapTexture.
        /// </summary>
        public ImageMagickCubemapTexture(
            MagickImage positiveX,
            MagickImage negativeX,
            MagickImage positiveY,
            MagickImage negativeY,
            MagickImage positiveZ,
            MagickImage negativeZ,
            bool mipmap = true
        )
        {
            CubemapTextures = new MagickImage[6][];
            if (mipmap)
            {
                CubemapTextures[0] = MipmapHelper.GenerateMipmaps(positiveX);
                CubemapTextures[1] = MipmapHelper.GenerateMipmaps(negativeX);
                CubemapTextures[2] = MipmapHelper.GenerateMipmaps(positiveY);
                CubemapTextures[3] = MipmapHelper.GenerateMipmaps(negativeY);
                CubemapTextures[4] = MipmapHelper.GenerateMipmaps(positiveZ);
                CubemapTextures[5] = MipmapHelper.GenerateMipmaps(negativeZ);
            }
            else
            {
                CubemapTextures[0] = [positiveX];
                CubemapTextures[1] = [negativeX];
                CubemapTextures[2] = [positiveY];
                CubemapTextures[3] = [negativeY];
                CubemapTextures[4] = [positiveZ];
                CubemapTextures[5] = [negativeZ];
            }
        }

        /// <summary>
        /// Creates a new instance of ImageMagickCubemapTexture.
        /// </summary>
        public ImageMagickCubemapTexture(
            MagickImage[] positiveX,
            MagickImage[] negativeX,
            MagickImage[] positiveY,
            MagickImage[] negativeY,
            MagickImage[] positiveZ,
            MagickImage[] negativeZ
        )
        {
            CubemapTextures = new MagickImage[6][];
            if (positiveX.Length == 0)
                throw new ArgumentException("Texture should have at least one mip level.");

            if (
                positiveX.Length != negativeX.Length
                || positiveX.Length != positiveY.Length
                || positiveX.Length != negativeY.Length
                || positiveX.Length != positiveZ.Length
                || positiveX.Length != negativeZ.Length
            )
            {
                throw new ArgumentException("Mip count doesn't match.");
            }

            CubemapTextures[0] = positiveX;
            CubemapTextures[1] = negativeX;
            CubemapTextures[2] = positiveY;
            CubemapTextures[3] = negativeY;
            CubemapTextures[4] = positiveZ;
            CubemapTextures[5] = negativeZ;
        }

        byte[] GetPixels(int layer, int level)
        {
            MagickImage image = CubemapTextures[layer][level];
            return image.GetPixels().ToByteArray(0, 0, image.Width, image.Height, PixelMapping.RGBA)!;
        }

        /// <summary>
        /// Creates the device texture for the cubemap texture.
        /// </summary>
        public Texture CreateDeviceTexture(GraphicsDevice gd, ResourceFactory factory)
        {
            Texture cubemapTexture = factory.CreateTexture(
                TextureDescription.Texture2D(
                    Width,
                    Height,
                    MipLevels,
                    1,
                    Format,
                    TextureUsage.Sampled | TextureUsage.Cubemap
                )
            );

            for (int level = 0; level < MipLevels; level++)
            {
                byte[] pixelsPosX = GetPixels(PositiveXArrayLayer, level);
                byte[] pixelsNegX = GetPixels(NegativeXArrayLayer, level);
                byte[] pixelsPosY = GetPixels(PositiveYArrayLayer, level);
                byte[] pixelsNegY = GetPixels(NegativeYArrayLayer, level);
                byte[] pixelsPosZ = GetPixels(PositiveZArrayLayer, level);
                byte[] pixelsNegZ = GetPixels(NegativeZArrayLayer, level);

                MagickImage image = CubemapTextures[0][level];
                uint width = image.Width;
                uint height = image.Height;

                gd.UpdateTexture(cubemapTexture, pixelsPosX, 0, 0, 0, width, height, 1, (uint)level, PositiveXArrayLayer);
                gd.UpdateTexture(cubemapTexture, pixelsNegX, 0, 0, 0, width, height, 1, (uint)level, NegativeXArrayLayer);
                gd.UpdateTexture(cubemapTexture, pixelsPosY, 0, 0, 0, width, height, 1, (uint)level, PositiveYArrayLayer);
                gd.UpdateTexture(cubemapTexture, pixelsNegY, 0, 0, 0, width, height, 1, (uint)level, NegativeYArrayLayer);
                gd.UpdateTexture(cubemapTexture, pixelsPosZ, 0, 0, 0, width, height, 1, (uint)level, PositiveZArrayLayer);
                gd.UpdateTexture(cubemapTexture, pixelsNegZ, 0, 0, 0, width, height, 1, (uint)level, NegativeZArrayLayer);
            }

            return cubemapTexture;
        }
    }
}
