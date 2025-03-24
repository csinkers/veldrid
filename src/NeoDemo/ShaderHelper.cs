using System;
using System.Collections.Generic;
using System.IO;
using Veldrid.SPIRV;

namespace Veldrid.NeoDemo;

public static class ShaderHelper
{
    public static (Shader vs, Shader fs) LoadSPIRV(
        GraphicsDevice gd,
        ResourceFactory factory,
        string setName
    )
    {
        byte[] vsBytes = LoadBytecode(GraphicsBackend.Vulkan, setName, ShaderStages.Vertex);
        byte[] fsBytes = LoadBytecode(GraphicsBackend.Vulkan, setName, ShaderStages.Fragment);
        bool debug = false;
#if DEBUG
        debug = true;
#endif

        Shader[] shaders = factory.CreateFromSpirv(
            new(ShaderStages.Vertex, vsBytes, "main", debug),
            new(ShaderStages.Fragment, fsBytes, "main", debug),
            GetOptions(gd)
        );

        Shader vs = shaders[0];
        Shader fs = shaders[1];

        vs.Name = setName + "-Vertex";
        fs.Name = setName + "-Fragment";

        return (vs, fs);
    }

    static CrossCompileOptions GetOptions(GraphicsDevice gd)
    {
        SpecializationConstant[] specializations = GetSpecializations(gd);

        bool fixClipZ =
            gd.BackendType is GraphicsBackend.OpenGL or GraphicsBackend.OpenGLES
            && !gd.IsDepthRangeZeroToOne;

        bool invertY = false;
        return new CrossCompileOptions
        {
            FixClipSpaceZ =
            fixClipZ,
            InvertVertexOutputY = invertY,
            Specializations = specializations
        };
    }

    public static SpecializationConstant[] GetSpecializations(GraphicsDevice gd)
    {
        bool glOrGles = gd.BackendType is GraphicsBackend.OpenGL or GraphicsBackend.OpenGLES;

        List<SpecializationConstant> specializations =
        [
            new(100, gd.IsClipSpaceYInverted),
            new(101, glOrGles), // TextureCoordinatesInvertedY
            new(102, gd.IsDepthRangeZeroToOne),
        ];

        if (gd.MainSwapchain?.Framebuffer.OutputDescription.ColorAttachments != null)
        {
            PixelFormat swapchainFormat = gd.MainSwapchain
                .Framebuffer
                .OutputDescription
                .ColorAttachments[0]
                .Format;

            bool swapchainIsSrgb =
                swapchainFormat
                    is PixelFormat.B8_G8_R8_A8_UNorm_SRgb
                        or PixelFormat.R8_G8_B8_A8_UNorm_SRgb;
            specializations.Add(new(103, swapchainIsSrgb));
        }

        return specializations.ToArray();
    }

    public static byte[] LoadBytecode(GraphicsBackend backend, string setName, ShaderStages stage)
    {
        string stageExt = stage == ShaderStages.Vertex ? "vert" : "frag";
        string name = setName + "." + stageExt;

        if (backend == GraphicsBackend.Vulkan || backend == GraphicsBackend.Direct3D11)
        {
            string bytecodeExtension = GetBytecodeExtension(backend);
            string bytecodePath = AssetHelper.GetPath(
                Path.Combine("Shaders", name + bytecodeExtension)
            );
            if (File.Exists(bytecodePath))
            {
                return File.ReadAllBytes(bytecodePath);
            }
        }

        string extension = GetSourceExtension(backend);
        string path = AssetHelper.GetPath(Path.Combine("Shaders.Generated", name + extension));
        return File.ReadAllBytes(path);
    }

    static string GetBytecodeExtension(GraphicsBackend backend)
    {
        return backend switch
        {
            GraphicsBackend.Direct3D11 => ".hlsl.bytes",
            GraphicsBackend.Vulkan => ".spv",
            GraphicsBackend.OpenGL => throw new InvalidOperationException(
                "OpenGL and OpenGLES do not support shader bytecode."
            ),
            _ => throw new InvalidOperationException("Invalid Graphics backend: " + backend),
        };
    }

    static string GetSourceExtension(GraphicsBackend backend)
    {
        return backend switch
        {
            GraphicsBackend.Direct3D11 => ".hlsl",
            GraphicsBackend.Vulkan => ".450.glsl",
            GraphicsBackend.OpenGL => ".330.glsl",
            GraphicsBackend.OpenGLES => ".300.glsles",
            GraphicsBackend.Metal => ".metallib",
            _ => throw new InvalidOperationException("Invalid Graphics backend: " + backend),
        };
    }
}
