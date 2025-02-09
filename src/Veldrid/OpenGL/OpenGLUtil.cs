using System;
using System.Diagnostics;
using Veldrid.OpenGLBinding;
using static Veldrid.OpenGLBinding.OpenGLNative;

namespace Veldrid.OpenGL;

internal static class OpenGLUtil
{
    static int? _maxLabelLength;

    [Conditional("DEBUG")]
    internal static void CheckLastError() => VerifyLastError();

    internal static void VerifyLastError()
    {
        uint error = glGetError();
        if (error != 0)
            ThrowLastError(error);
    }

    static void ThrowLastError(uint error) =>
        throw new VeldridException("glGetError: " + (ErrorCode)error);

    internal static unsafe void SetObjectLabel(
        ObjectLabelIdentifier identifier,
        uint target,
        ReadOnlySpan<char> name
    )
    {
        if (!HasGlObjectLabel)
        {
            return;
        }

        if (name.IsEmpty)
        {
            glObjectLabel(identifier, target, 0, null);
            CheckLastError();
            return;
        }

        int maxLabelLength = 0;
        if (!_maxLabelLength.HasValue)
        {
            glGetIntegerv(GetPName.MaxLabelLength, &maxLabelLength);
            CheckLastError();
            _maxLabelLength = maxLabelLength;
        }
        maxLabelLength = _maxLabelLength.GetValueOrDefault();

        int byteCount = Util.UTF8.GetByteCount(name);
        if (byteCount >= maxLabelLength)
        {
            name = name[..(maxLabelLength - 4)].ToString() + "...";
            byteCount = Util.UTF8.GetByteCount(name);
        }

        Span<byte> utf8Bytes = stackalloc byte[1024];
        byteCount = Util.GetNullTerminatedUtf8(name, ref utf8Bytes);

        fixed (byte* utf8BytePtr = utf8Bytes)
        {
            glObjectLabel(identifier, target, (uint)byteCount, utf8BytePtr);
            CheckLastError();
        }
    }

    internal static TextureTarget GetTextureTarget(OpenGLTexture glTex, uint arrayLayer)
    {
        if ((glTex.Usage & TextureUsage.Cubemap) == 0)
            return glTex.TextureTarget;

        return (arrayLayer % 6) switch
        {
            0 => TextureTarget.TextureCubeMapPositiveX,
            1 => TextureTarget.TextureCubeMapNegativeX,
            2 => TextureTarget.TextureCubeMapPositiveY,
            3 => TextureTarget.TextureCubeMapNegativeY,
            4 => TextureTarget.TextureCubeMapPositiveZ,
            5 => TextureTarget.TextureCubeMapNegativeZ,
            _ => glTex.TextureTarget,
        };
    }
}
