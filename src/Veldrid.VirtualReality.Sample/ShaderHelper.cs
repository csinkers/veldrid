using System;
using System.Collections.Generic;
using Veldrid.SPIRV;

namespace Veldrid.VirtualReality.Sample;

internal static class ShaderHelper
{
    public static CrossCompileOptions GetOptions(GraphicsDevice gd)
    {
        bool fixClipZ = false;
        bool invertY = false;
        List<SpecializationConstant> specializations = [new(102, gd.IsDepthRangeZeroToOne)];
        switch (gd.BackendType)
        {
            case GraphicsBackend.Direct3D11:
            case GraphicsBackend.Metal:
                specializations.Add(new(100, false));
                break;
            case GraphicsBackend.Vulkan:
                specializations.Add(new(100, true));
                break;
            case GraphicsBackend.OpenGL:
            case GraphicsBackend.OpenGLES:
                specializations.Add(new(100, false));
                specializations.Add(new(101, true));
                fixClipZ = !gd.IsDepthRangeZeroToOne;
                break;
            default:
                throw new InvalidOperationException();
        }

        return new(fixClipZ, invertY, specializations.ToArray());
    }
}
