using System;

namespace Veldrid.MetalBindings;

[Flags]
internal enum MTLTextureUsage
{
    Unknown = 0,
    ShaderRead = 1 << 0,
    ShaderWrite = 1 << 1,
    RenderTarget = 1 << 2,
    PixelFormatView = 0x10,
}
