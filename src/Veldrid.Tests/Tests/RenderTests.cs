﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Veldrid.SPIRV;
using Veldrid.Tests.Utilities;
using Xunit;

namespace Veldrid.Tests;

internal struct UIntVertexAttribsVertex
{
    public Vector2 Position;
    public UInt4 Color_Int;
}

[StructLayout(LayoutKind.Sequential)]
internal struct UIntVertexAttribsInfo
{
    public uint ColorNormalizationFactor;
    float padding0;
    float padding1;
    float padding2;
}

[StructLayout(LayoutKind.Sequential)]
public struct ColoredVertex
{
    public Vector4 Color;
    public Vector2 Position;
    Vector2 _padding0;
}

[StructLayout(LayoutKind.Sequential)]
public struct TestVertex
{
    public Vector3 A_V3;
    public Vector4 B_V4;
    public Vector2 C_V2;
    public Vector4 D_V4;
}

public abstract class RenderTests<T> : GraphicsDeviceTestBase<T>
    where T : IGraphicsDeviceCreator
{
    [Fact]
    public void Points_WithUIntColor()
    {
        Texture target = RF.CreateTexture(
            TextureDescription.Texture2D(
                50,
                50,
                1,
                1,
                PixelFormat.R32_G32_B32_A32_Float,
                TextureUsage.RenderTarget
            )
        );
        Texture staging = RF.CreateTexture(
            TextureDescription.Texture2D(
                50,
                50,
                1,
                1,
                PixelFormat.R32_G32_B32_A32_Float,
                TextureUsage.Staging
            )
        );

        Framebuffer framebuffer = RF.CreateFramebuffer(new(null, target));

        DeviceBuffer infoBuffer = RF.CreateBuffer(new(16, BufferUsage.UniformBuffer));
        DeviceBuffer orthoBuffer = RF.CreateBuffer(new(64, BufferUsage.UniformBuffer));
        Matrix4x4 orthoMatrix = Matrix4x4.CreateOrthographicOffCenter(
            0,
            framebuffer.Width,
            framebuffer.Height,
            0,
            -1,
            1
        );
        GD.UpdateBuffer(orthoBuffer, 0, ref orthoMatrix);

        ShaderSetDescription shaderSet = new(
            [
                new(
                    new VertexElementDescription(
                        "Position",
                        VertexElementSemantic.TextureCoordinate,
                        VertexElementFormat.Float2
                    ),
                    new VertexElementDescription(
                        "Color_UInt",
                        VertexElementSemantic.TextureCoordinate,
                        VertexElementFormat.UInt4
                    )
                ),
            ],
            TestShaders.LoadVertexFragment(RF, "UIntVertexAttribs")
        );

        ResourceLayout layout = RF.CreateResourceLayout(
            new(
                new ResourceLayoutElementDescription(
                    "InfoBuffer",
                    ResourceKind.UniformBuffer,
                    ShaderStages.Vertex
                ),
                new ResourceLayoutElementDescription(
                    "Ortho",
                    ResourceKind.UniformBuffer,
                    ShaderStages.Vertex
                )
            )
        );

        ResourceSet set = RF.CreateResourceSet(new(layout, infoBuffer, orthoBuffer));

        GraphicsPipelineDescription gpd = new(
            BlendStateDescription.SingleOverrideBlend,
            DepthStencilStateDescription.Disabled,
            RasterizerStateDescription.Default,
            PrimitiveTopology.PointList,
            shaderSet,
            layout,
            framebuffer.OutputDescription
        );

        Pipeline pipeline = RF.CreateGraphicsPipeline(gpd);

        uint colorNormalizationFactor = 2500;

        UIntVertexAttribsVertex[] vertices =
        [
            new()
            {
                Position = new(0.5f, 0.5f),
                Color_Int = new()
                {
                    X = (uint)(0.25f * colorNormalizationFactor),
                    Y = (uint)(0.5f * colorNormalizationFactor),
                    Z = (uint)(0.75f * colorNormalizationFactor),
                },
            },
            new()
            {
                Position = new(10.5f, 12.5f),
                Color_Int = new()
                {
                    X = (uint)(0.25f * colorNormalizationFactor),
                    Y = (uint)(0.5f * colorNormalizationFactor),
                    Z = (uint)(0.75f * colorNormalizationFactor),
                },
            },
            new()
            {
                Position = new(25.5f, 35.5f),
                Color_Int = new()
                {
                    X = (uint)(0.75f * colorNormalizationFactor),
                    Y = (uint)(0.5f * colorNormalizationFactor),
                    Z = (uint)(0.25f * colorNormalizationFactor),
                },
            },
            new()
            {
                Position = new(49.5f, 49.5f),
                Color_Int = new()
                {
                    X = (uint)(0.15f * colorNormalizationFactor),
                    Y = (uint)(0.25f * colorNormalizationFactor),
                    Z = (uint)(0.35f * colorNormalizationFactor),
                },
            },
        ];

        DeviceBuffer vb = RF.CreateBuffer(
            new(
                (uint)(Unsafe.SizeOf<UIntVertexAttribsVertex>() * vertices.Length),
                BufferUsage.VertexBuffer
            )
        );
        GD.UpdateBuffer(vb, 0, vertices);
        GD.UpdateBuffer(
            infoBuffer,
            0,
            new UIntVertexAttribsInfo { ColorNormalizationFactor = colorNormalizationFactor }
        );

        CommandList cl = RF.CreateCommandList();

        cl.Begin();
        cl.SetFramebuffer(framebuffer);
        cl.SetFullViewports();
        cl.SetFullScissorRects();
        cl.ClearColorTarget(0, RgbaFloat.Black);
        cl.SetPipeline(pipeline);
        cl.SetVertexBuffer(0, vb);
        cl.SetGraphicsResourceSet(0, set);
        cl.Draw((uint)vertices.Length);
        cl.CopyTexture(target, staging);
        cl.End();
        GD.SubmitCommands(cl);
        GD.WaitForIdle();

        MappedResourceView<RgbaFloat> readView = GD.Map<RgbaFloat>(staging, MapMode.Read);

        bool flip = !GD.IsUvOriginTopLeft || GD.IsClipSpaceYInverted;
        foreach (UIntVertexAttribsVertex vertex in vertices)
        {
            uint x = (uint)vertex.Position.X;
            uint y = (uint)vertex.Position.Y;
            if (flip)
            {
                y = framebuffer.Height - y - 1;
            }

            RgbaFloat expectedColor = new(
                vertex.Color_Int.X / (float)colorNormalizationFactor,
                vertex.Color_Int.Y / (float)colorNormalizationFactor,
                vertex.Color_Int.Z / (float)colorNormalizationFactor,
                1
            );
            Assert.Equal(expectedColor, readView[x, y], RgbaFloatFuzzyComparer.Instance);
        }
        GD.Unmap(staging);
    }

    [Fact]
    public void Points_WithUShortNormColor()
    {
        Texture target = RF.CreateTexture(
            TextureDescription.Texture2D(
                50,
                50,
                1,
                1,
                PixelFormat.R32_G32_B32_A32_Float,
                TextureUsage.RenderTarget
            )
        );
        Texture staging = RF.CreateTexture(
            TextureDescription.Texture2D(
                50,
                50,
                1,
                1,
                PixelFormat.R32_G32_B32_A32_Float,
                TextureUsage.Staging
            )
        );

        Framebuffer framebuffer = RF.CreateFramebuffer(new(null, target));

        DeviceBuffer orthoBuffer = RF.CreateBuffer(new(64, BufferUsage.UniformBuffer));
        Matrix4x4 orthoMatrix = Matrix4x4.CreateOrthographicOffCenter(
            0,
            framebuffer.Width,
            framebuffer.Height,
            0,
            -1,
            1
        );
        GD.UpdateBuffer(orthoBuffer, 0, ref orthoMatrix);

        ShaderSetDescription shaderSet = new(
            [
                new(
                    new VertexElementDescription(
                        "Position",
                        VertexElementSemantic.TextureCoordinate,
                        VertexElementFormat.Float2
                    ),
                    new VertexElementDescription(
                        "Color",
                        VertexElementSemantic.TextureCoordinate,
                        VertexElementFormat.UShort4_Norm
                    )
                ),
            ],
            TestShaders.LoadVertexFragment(RF, "U16NormVertexAttribs")
        );

        ResourceLayout layout = RF.CreateResourceLayout(
            new(
                new ResourceLayoutElementDescription(
                    "Ortho",
                    ResourceKind.UniformBuffer,
                    ShaderStages.Vertex
                )
            )
        );

        ResourceSet set = RF.CreateResourceSet(new(layout, orthoBuffer));

        GraphicsPipelineDescription gpd = new(
            BlendStateDescription.SingleOverrideBlend,
            DepthStencilStateDescription.Disabled,
            RasterizerStateDescription.Default,
            PrimitiveTopology.PointList,
            shaderSet,
            layout,
            framebuffer.OutputDescription
        );

        Pipeline pipeline = RF.CreateGraphicsPipeline(gpd);

        VertexCPU_UShortNorm[] vertices =
        [
            new()
            {
                Position = new(0.5f, 0.5f),
                R = UShortNorm(0.25f),
                G = UShortNorm(0.5f),
                B = UShortNorm(0.75f),
            },
            new()
            {
                Position = new(10.5f, 12.5f),
                R = UShortNorm(0.25f),
                G = UShortNorm(0.5f),
                B = UShortNorm(0.75f),
            },
            new()
            {
                Position = new(25.5f, 35.5f),
                R = UShortNorm(0.75f),
                G = UShortNorm(0.5f),
                B = UShortNorm(0.25f),
            },
            new()
            {
                Position = new(49.5f, 49.5f),
                R = UShortNorm(0.15f),
                G = UShortNorm(0.25f),
                B = UShortNorm(0.35f),
            },
        ];

        DeviceBuffer vb = RF.CreateBuffer(
            new(
                (uint)(Unsafe.SizeOf<VertexCPU_UShortNorm>() * vertices.Length),
                BufferUsage.VertexBuffer
            )
        );
        GD.UpdateBuffer(vb, 0, vertices);

        CommandList cl = RF.CreateCommandList();

        cl.Begin();
        cl.SetFramebuffer(framebuffer);
        cl.SetFullViewports();
        cl.SetFullScissorRects();
        cl.ClearColorTarget(0, RgbaFloat.Black);
        cl.SetPipeline(pipeline);
        cl.SetVertexBuffer(0, vb);
        cl.SetGraphicsResourceSet(0, set);
        cl.Draw((uint)vertices.Length);
        cl.CopyTexture(target, staging);
        cl.End();
        GD.SubmitCommands(cl);
        GD.WaitForIdle();

        MappedResourceView<RgbaFloat> readView = GD.Map<RgbaFloat>(staging, MapMode.Read);

        bool flip = !GD.IsUvOriginTopLeft || GD.IsClipSpaceYInverted;
        foreach (VertexCPU_UShortNorm vertex in vertices)
        {
            uint x = (uint)vertex.Position.X;
            uint y = (uint)vertex.Position.Y;
            if (flip)
            {
                y = framebuffer.Height - y - 1;
            }

            RgbaFloat expectedColor = new(
                vertex.R / (float)ushort.MaxValue,
                vertex.G / (float)ushort.MaxValue,
                vertex.B / (float)ushort.MaxValue,
                1
            );
            Assert.Equal(expectedColor, readView[x, y], RgbaFloatFuzzyComparer.Instance);
        }
        GD.Unmap(staging);
    }

    public struct VertexCPU_UShortNorm
    {
        public Vector2 Position;
        public ushort R;
        public ushort G;
        public ushort B;
        public ushort A;
    }

    public struct VertexCPU_UShort
    {
        public Vector2 Position;
        public ushort R;
        public ushort G;
        public ushort B;
        public ushort A;
    }

    static ushort UShortNorm(float normalizedValue)
    {
        Debug.Assert(normalizedValue >= 0 && normalizedValue <= 1);
        return (ushort)(normalizedValue * ushort.MaxValue);
    }

    [Fact]
    public void Points_WithUShortColor()
    {
        Texture target = RF.CreateTexture(
            TextureDescription.Texture2D(
                50,
                50,
                1,
                1,
                PixelFormat.R32_G32_B32_A32_Float,
                TextureUsage.RenderTarget
            )
        );
        Texture staging = RF.CreateTexture(
            TextureDescription.Texture2D(
                50,
                50,
                1,
                1,
                PixelFormat.R32_G32_B32_A32_Float,
                TextureUsage.Staging
            )
        );

        Framebuffer framebuffer = RF.CreateFramebuffer(new(null, target));

        DeviceBuffer infoBuffer = RF.CreateBuffer(new(16, BufferUsage.UniformBuffer));
        DeviceBuffer orthoBuffer = RF.CreateBuffer(new(64, BufferUsage.UniformBuffer));
        Matrix4x4 orthoMatrix = Matrix4x4.CreateOrthographicOffCenter(
            0,
            framebuffer.Width,
            framebuffer.Height,
            0,
            -1,
            1
        );
        GD.UpdateBuffer(orthoBuffer, 0, ref orthoMatrix);

        ShaderSetDescription shaderSet = new(
            [
                new(
                    new VertexElementDescription(
                        "Position",
                        VertexElementSemantic.TextureCoordinate,
                        VertexElementFormat.Float2
                    ),
                    new VertexElementDescription(
                        "Color_UInt",
                        VertexElementSemantic.TextureCoordinate,
                        VertexElementFormat.UShort4
                    )
                ),
            ],
            TestShaders.LoadVertexFragment(RF, "U16VertexAttribs")
        );

        ResourceLayout layout = RF.CreateResourceLayout(
            new(
                new ResourceLayoutElementDescription(
                    "InfoBuffer",
                    ResourceKind.UniformBuffer,
                    ShaderStages.Vertex
                ),
                new ResourceLayoutElementDescription(
                    "Ortho",
                    ResourceKind.UniformBuffer,
                    ShaderStages.Vertex
                )
            )
        );

        ResourceSet set = RF.CreateResourceSet(new(layout, infoBuffer, orthoBuffer));

        GraphicsPipelineDescription gpd = new(
            BlendStateDescription.SingleOverrideBlend,
            DepthStencilStateDescription.Disabled,
            RasterizerStateDescription.Default,
            PrimitiveTopology.PointList,
            shaderSet,
            layout,
            framebuffer.OutputDescription
        );

        Pipeline pipeline = RF.CreateGraphicsPipeline(gpd);

        uint colorNormalizationFactor = 2500;

        VertexCPU_UShort[] vertices =
        [
            new()
            {
                Position = new(0.5f, 0.5f),
                R = (ushort)(0.25f * colorNormalizationFactor),
                G = (ushort)(0.5f * colorNormalizationFactor),
                B = (ushort)(0.75f * colorNormalizationFactor),
            },
            new()
            {
                Position = new(10.5f, 12.5f),
                R = (ushort)(0.25f * colorNormalizationFactor),
                G = (ushort)(0.5f * colorNormalizationFactor),
                B = (ushort)(0.75f * colorNormalizationFactor),
            },
            new()
            {
                Position = new(25.5f, 35.5f),
                R = (ushort)(0.75f * colorNormalizationFactor),
                G = (ushort)(0.5f * colorNormalizationFactor),
                B = (ushort)(0.25f * colorNormalizationFactor),
            },
            new()
            {
                Position = new(49.5f, 49.5f),
                R = (ushort)(0.15f * colorNormalizationFactor),
                G = (ushort)(0.2f * colorNormalizationFactor),
                B = (ushort)(0.35f * colorNormalizationFactor),
            },
        ];

        DeviceBuffer vb = RF.CreateBuffer(
            new(
                (uint)(Unsafe.SizeOf<UIntVertexAttribsVertex>() * vertices.Length),
                BufferUsage.VertexBuffer
            )
        );
        GD.UpdateBuffer(vb, 0, vertices);
        GD.UpdateBuffer(
            infoBuffer,
            0,
            new UIntVertexAttribsInfo { ColorNormalizationFactor = colorNormalizationFactor }
        );

        CommandList cl = RF.CreateCommandList();

        cl.Begin();
        cl.SetFramebuffer(framebuffer);
        cl.SetFullViewports();
        cl.SetFullScissorRects();
        cl.ClearColorTarget(0, RgbaFloat.Black);
        cl.SetPipeline(pipeline);
        cl.SetVertexBuffer(0, vb);
        cl.SetGraphicsResourceSet(0, set);
        cl.Draw((uint)vertices.Length);
        cl.CopyTexture(target, staging);
        cl.End();
        GD.SubmitCommands(cl);
        GD.WaitForIdle();

        MappedResourceView<RgbaFloat> readView = GD.Map<RgbaFloat>(staging, MapMode.Read);

        bool flip = !GD.IsUvOriginTopLeft || GD.IsClipSpaceYInverted;
        foreach (VertexCPU_UShort vertex in vertices)
        {
            uint x = (uint)vertex.Position.X;
            uint y = (uint)vertex.Position.Y;
            if (flip)
            {
                y = framebuffer.Height - y - 1;
            }

            RgbaFloat expectedColor = new(
                vertex.R / (float)colorNormalizationFactor,
                vertex.G / (float)colorNormalizationFactor,
                vertex.B / (float)colorNormalizationFactor,
                1
            );
            Assert.Equal(expectedColor, readView[x, y], RgbaFloatFuzzyComparer.Instance);
        }
        GD.Unmap(staging);
    }

    [Fact]
    public void Points_WithFloat16Color()
    {
        Texture target = RF.CreateTexture(
            TextureDescription.Texture2D(
                50,
                50,
                1,
                1,
                PixelFormat.R32_G32_B32_A32_Float,
                TextureUsage.RenderTarget
            )
        );
        Texture staging = RF.CreateTexture(
            TextureDescription.Texture2D(
                50,
                50,
                1,
                1,
                PixelFormat.R32_G32_B32_A32_Float,
                TextureUsage.Staging
            )
        );

        Framebuffer framebuffer = RF.CreateFramebuffer(new(null, target));

        DeviceBuffer infoBuffer = RF.CreateBuffer(new(16, BufferUsage.UniformBuffer));
        DeviceBuffer orthoBuffer = RF.CreateBuffer(new(64, BufferUsage.UniformBuffer));
        Matrix4x4 orthoMatrix = Matrix4x4.CreateOrthographicOffCenter(
            0,
            framebuffer.Width,
            framebuffer.Height,
            0,
            -1,
            1
        );
        GD.UpdateBuffer(orthoBuffer, 0, ref orthoMatrix);

        ShaderSetDescription shaderSet = new(
            [
                new(
                    new VertexElementDescription(
                        "Position",
                        VertexElementSemantic.TextureCoordinate,
                        VertexElementFormat.Float2
                    ),
                    new VertexElementDescription(
                        "Color",
                        VertexElementSemantic.TextureCoordinate,
                        VertexElementFormat.Half4
                    )
                ),
            ],
            TestShaders.LoadVertexFragment(RF, "F16VertexAttribs")
        );

        ResourceLayout layout = RF.CreateResourceLayout(
            new(
                new ResourceLayoutElementDescription(
                    "InfoBuffer",
                    ResourceKind.UniformBuffer,
                    ShaderStages.Vertex
                ),
                new ResourceLayoutElementDescription(
                    "OrthoBuffer",
                    ResourceKind.UniformBuffer,
                    ShaderStages.Vertex
                )
            )
        );

        ResourceSet set = RF.CreateResourceSet(new(layout, infoBuffer, orthoBuffer));

        GraphicsPipelineDescription gpd = new(
            BlendStateDescription.SingleOverrideBlend,
            DepthStencilStateDescription.Disabled,
            RasterizerStateDescription.Default,
            PrimitiveTopology.PointList,
            shaderSet,
            layout,
            framebuffer.OutputDescription
        );

        Pipeline pipeline = RF.CreateGraphicsPipeline(gpd);

        uint colorNormalizationFactor = 2500;

        const ushort f16_375 = 0x5DDC; // 375.0
        const ushort f16_500 = 0x5FD0; // 500.0
        const ushort f16_625 = 0x60E2; // 625.0
        const ushort f16_875 = 0x62D6; // 875.0
        const ushort f16_1250 = 0x64E2; // 1250.0
        const ushort f16_1875 = 0x6753; // 1875.0

        VertexCPU_UShort[] vertices =
        [
            new()
            {
                Position = new(0.5f, 0.5f),
                R = f16_625,
                G = f16_1250,
                B = f16_1875,
            },
            new()
            {
                Position = new(10.5f, 12.5f),
                R = f16_625,
                G = f16_1250,
                B = f16_1875,
            },
            new()
            {
                Position = new(25.5f, 35.5f),
                R = f16_1875,
                G = f16_1250,
                B = f16_625,
            },
            new()
            {
                Position = new(49.5f, 49.5f),
                R = f16_375,
                G = f16_500,
                B = f16_875,
            },
        ];

        RgbaFloat[] expectedColors =
        [
            new(
                625.0f / colorNormalizationFactor,
                1250.0f / colorNormalizationFactor,
                1875.0f / colorNormalizationFactor,
                1
            ),
            new(
                625.0f / colorNormalizationFactor,
                1250.0f / colorNormalizationFactor,
                1875.0f / colorNormalizationFactor,
                1
            ),
            new(
                1875.0f / colorNormalizationFactor,
                1250.0f / colorNormalizationFactor,
                625.0f / colorNormalizationFactor,
                1
            ),
            new(
                375.0f / colorNormalizationFactor,
                500.0f / colorNormalizationFactor,
                875.0f / colorNormalizationFactor,
                1
            ),
        ];

        DeviceBuffer vb = RF.CreateBuffer(
            new(
                (uint)(Unsafe.SizeOf<UIntVertexAttribsVertex>() * vertices.Length),
                BufferUsage.VertexBuffer
            )
        );
        GD.UpdateBuffer(vb, 0, vertices);
        GD.UpdateBuffer(
            infoBuffer,
            0,
            new UIntVertexAttribsInfo { ColorNormalizationFactor = colorNormalizationFactor }
        );

        CommandList cl = RF.CreateCommandList();

        cl.Begin();
        cl.SetFramebuffer(framebuffer);
        cl.SetFullViewports();
        cl.SetFullScissorRects();
        cl.ClearColorTarget(0, RgbaFloat.Black);
        cl.SetPipeline(pipeline);
        cl.SetVertexBuffer(0, vb);
        cl.SetGraphicsResourceSet(0, set);
        cl.Draw((uint)vertices.Length);
        cl.CopyTexture(target, staging);
        cl.End();
        GD.SubmitCommands(cl);
        GD.WaitForIdle();

        MappedResourceView<RgbaFloat> readView = GD.Map<RgbaFloat>(staging, MapMode.Read);

        bool flip = !GD.IsUvOriginTopLeft || GD.IsClipSpaceYInverted;
        for (int i = 0; i < vertices.Length; i++)
        {
            VertexCPU_UShort vertex = vertices[i];
            uint x = (uint)vertex.Position.X;
            uint y = (uint)vertex.Position.Y;
            if (flip)
            {
                y = framebuffer.Height - y - 1;
            }

            RgbaFloat expectedColor = expectedColors[i];
            Assert.Equal(expectedColor, readView[x, y], RgbaFloatFuzzyComparer.Instance);
        }
        GD.Unmap(staging);
    }

    [InlineData(false)]
    [InlineData(true)]
    [Theory]
    public unsafe void Points_WithTexture_UpdateUnrelated(bool useTextureView)
    {
        // This is a regression test for the case where a user modifies an unrelated texture
        // at a time after a ResourceSet containing a texture has been bound. The OpenGL
        // backend was caching texture state improperly, resulting in wrong textures being sampled.

        Texture target = RF.CreateTexture(
            TextureDescription.Texture2D(
                50,
                50,
                1,
                1,
                PixelFormat.R32_G32_B32_A32_Float,
                TextureUsage.RenderTarget
            )
        );
        Texture staging = RF.CreateTexture(
            TextureDescription.Texture2D(
                50,
                50,
                1,
                1,
                PixelFormat.R32_G32_B32_A32_Float,
                TextureUsage.Staging
            )
        );

        Framebuffer framebuffer = RF.CreateFramebuffer(new(null, target));

        DeviceBuffer orthoBuffer = RF.CreateBuffer(new(64, BufferUsage.UniformBuffer));
        Matrix4x4 orthoMatrix = Matrix4x4.CreateOrthographicOffCenter(
            0,
            framebuffer.Width,
            framebuffer.Height,
            0,
            -1,
            1
        );
        GD.UpdateBuffer(orthoBuffer, 0, ref orthoMatrix);

        Texture sampledTexture = RF.CreateTexture(
            TextureDescription.Texture2D(
                1,
                1,
                1,
                1,
                PixelFormat.R32_G32_B32_A32_Float,
                TextureUsage.Sampled
            )
        );

        RgbaFloat white = RgbaFloat.White;
        GD.UpdateTexture(
            sampledTexture,
            (IntPtr)(&white),
            (uint)Unsafe.SizeOf<RgbaFloat>(),
            0,
            0,
            0,
            1,
            1,
            1,
            0,
            0
        );

        Texture shouldntBeSampledTexture = RF.CreateTexture(
            TextureDescription.Texture2D(
                1,
                1,
                1,
                1,
                PixelFormat.R32_G32_B32_A32_Float,
                TextureUsage.Sampled
            )
        );

        ShaderSetDescription shaderSet = new(
            [
                new(
                    new VertexElementDescription(
                        "Position",
                        VertexElementSemantic.TextureCoordinate,
                        VertexElementFormat.Float2
                    )
                ),
            ],
            TestShaders.LoadVertexFragment(RF, "TexturedPoints")
        );

        ResourceLayout layout = RF.CreateResourceLayout(
            new(
                new ResourceLayoutElementDescription(
                    "Ortho",
                    ResourceKind.UniformBuffer,
                    ShaderStages.Vertex
                ),
                new ResourceLayoutElementDescription(
                    "Tex",
                    ResourceKind.TextureReadOnly,
                    ShaderStages.Fragment
                ),
                new ResourceLayoutElementDescription(
                    "Smp",
                    ResourceKind.Sampler,
                    ShaderStages.Fragment
                )
            )
        );

        ResourceSet set;
        if (useTextureView)
        {
            TextureView view = RF.CreateTextureView(sampledTexture);
            set = RF.CreateResourceSet(new(layout, orthoBuffer, view, GD.PointSampler));
        }
        else
        {
            set = RF.CreateResourceSet(new(layout, orthoBuffer, sampledTexture, GD.PointSampler));
        }

        GraphicsPipelineDescription gpd = new(
            BlendStateDescription.SingleOverrideBlend,
            DepthStencilStateDescription.Disabled,
            RasterizerStateDescription.Default,
            PrimitiveTopology.PointList,
            shaderSet,
            layout,
            framebuffer.OutputDescription
        );

        Pipeline pipeline = RF.CreateGraphicsPipeline(gpd);

        Vector2[] vertices =
        [
            new(0.5f, 0.5f),
            new(15.5f, 15.5f),
            new(25.5f, 26.5f),
            new(3.5f, 25.5f),
        ];

        DeviceBuffer vb = RF.CreateBuffer(
            new((uint)(Unsafe.SizeOf<Vector2>() * vertices.Length), BufferUsage.VertexBuffer)
        );
        GD.UpdateBuffer(vb, 0, vertices);

        CommandList cl = RF.CreateCommandList();

        for (int i = 0; i < 2; i++)
        {
            cl.Begin();
            cl.SetFramebuffer(framebuffer);
            cl.ClearColorTarget(0, RgbaFloat.Black);
            cl.SetPipeline(pipeline);
            cl.SetVertexBuffer(0, vb);
            cl.SetGraphicsResourceSet(0, set);

            // Modify an unrelated texture.
            // This must have no observable effect on the next draw call.
            RgbaFloat pink = RgbaFloat.Pink;
            GD.UpdateTexture(
                shouldntBeSampledTexture,
                (IntPtr)(&pink),
                (uint)Unsafe.SizeOf<RgbaFloat>(),
                0,
                0,
                0,
                1,
                1,
                1,
                0,
                0
            );

            cl.Draw((uint)vertices.Length);
            cl.End();
            GD.SubmitCommands(cl);
            GD.WaitForIdle();
        }

        cl.Begin();
        cl.CopyTexture(target, staging);
        cl.End();
        GD.SubmitCommands(cl);
        GD.WaitForIdle();

        MappedResourceView<RgbaFloat> readView = GD.Map<RgbaFloat>(staging, MapMode.Read);

        bool flip = !GD.IsUvOriginTopLeft || GD.IsClipSpaceYInverted;
        foreach (Vector2 vertex in vertices)
        {
            uint x = (uint)vertex.X;
            uint y = (uint)vertex.Y;
            if (flip)
            {
                y = framebuffer.Height - y - 1;
            }

            Assert.Equal(white, readView[x, y], RgbaFloatFuzzyComparer.Instance);
        }
        GD.Unmap(staging);
    }

    [SkippableFact]
    public void ComputeGeneratedVertices()
    {
        SkipIfNotComputeShader();

        uint width = 512;
        uint height = 512;
        Texture output = RF.CreateTexture(
            TextureDescription.Texture2D(
                width,
                height,
                1,
                1,
                PixelFormat.R32_G32_B32_A32_Float,
                TextureUsage.RenderTarget
            )
        );
        Framebuffer framebuffer = RF.CreateFramebuffer(new(null, output));

        uint vertexSize = (uint)Unsafe.SizeOf<ColoredVertex>();
        DeviceBuffer buffer = RF.CreateBuffer(
            new(vertexSize * 4, BufferUsage.StructuredBufferReadWrite, vertexSize, true)
        );

        ResourceLayout computeLayout = RF.CreateResourceLayout(
            new(
                new ResourceLayoutElementDescription(
                    "OutputVertices",
                    ResourceKind.StructuredBufferReadWrite,
                    ShaderStages.Compute
                )
            )
        );
        ResourceSet computeSet = RF.CreateResourceSet(new(computeLayout, buffer));

        Pipeline computePipeline = RF.CreateComputePipeline(
            new(TestShaders.LoadCompute(RF, "ComputeColoredQuadGenerator"), computeLayout, 1, 1, 1)
        );

        ResourceLayout graphicsLayout = RF.CreateResourceLayout(
            new(
                new ResourceLayoutElementDescription(
                    "InputVertices",
                    ResourceKind.StructuredBufferReadOnly,
                    ShaderStages.Vertex
                )
            )
        );
        ResourceSet graphicsSet = RF.CreateResourceSet(new(graphicsLayout, buffer));

        Pipeline graphicsPipeline = RF.CreateGraphicsPipeline(
            new(
                BlendStateDescription.SingleOverrideBlend,
                DepthStencilStateDescription.Disabled,
                RasterizerStateDescription.Default,
                PrimitiveTopology.TriangleStrip,
                new([], TestShaders.LoadVertexFragment(RF, "ColoredQuadRenderer")),
                graphicsLayout,
                framebuffer.OutputDescription
            )
        );

        CommandList cl = RF.CreateCommandList();
        cl.Begin();
        cl.SetPipeline(computePipeline);
        cl.SetComputeResourceSet(0, computeSet);
        cl.Dispatch(1, 1, 1);
        cl.SetFramebuffer(framebuffer);
        cl.ClearColorTarget(0, new());
        cl.SetPipeline(graphicsPipeline);
        cl.SetGraphicsResourceSet(0, graphicsSet);
        cl.Draw(4);
        cl.End();
        GD.SubmitCommands(cl);
        GD.WaitForIdle();

        Texture readback = GetReadback(output);
        MappedResourceView<RgbaFloat> readView = GD.Map<RgbaFloat>(readback, MapMode.Read);
        for (uint y = 0; y < height; y++)
        {
            for (uint x = 0; x < width; x++)
            {
                Assert.Equal(RgbaFloat.Red, readView[x, y]);
            }
        }
        GD.Unmap(readback);
    }

    [SkippableFact]
    public void ComputeGeneratedTexture()
    {
        SkipIfNotComputeShader();

        uint width = 4;
        uint height = 1;
        TextureDescription texDesc = TextureDescription.Texture2D(
            width,
            height,
            1,
            1,
            PixelFormat.R32_G32_B32_A32_Float,
            TextureUsage.Sampled | TextureUsage.Storage
        );
        Texture computeOutput = RF.CreateTexture(texDesc);
        texDesc.Usage = TextureUsage.RenderTarget;
        Texture finalOutput = RF.CreateTexture(texDesc);
        Framebuffer framebuffer = RF.CreateFramebuffer(new(null, finalOutput));

        ResourceLayout computeLayout = RF.CreateResourceLayout(
            new(
                new ResourceLayoutElementDescription(
                    "ComputeOutput",
                    ResourceKind.TextureReadWrite,
                    ShaderStages.Compute
                )
            )
        );
        ResourceSet computeSet = RF.CreateResourceSet(new(computeLayout, computeOutput));

        Pipeline computePipeline = RF.CreateComputePipeline(
            new(TestShaders.LoadCompute(RF, "ComputeTextureGenerator"), computeLayout, 4, 1, 1)
        );

        ResourceLayout graphicsLayout = RF.CreateResourceLayout(
            new(
                new ResourceLayoutElementDescription(
                    "Input",
                    ResourceKind.TextureReadOnly,
                    ShaderStages.Fragment
                ),
                new ResourceLayoutElementDescription(
                    "InputSampler",
                    ResourceKind.Sampler,
                    ShaderStages.Fragment
                )
            )
        );
        ResourceSet graphicsSet = RF.CreateResourceSet(
            new(graphicsLayout, computeOutput, GD.PointSampler)
        );

        Pipeline graphicsPipeline = RF.CreateGraphicsPipeline(
            new(
                BlendStateDescription.SingleOverrideBlend,
                DepthStencilStateDescription.Disabled,
                RasterizerStateDescription.CullNone,
                PrimitiveTopology.TriangleStrip,
                new([], TestShaders.LoadVertexFragment(RF, "FullScreenBlit")),
                graphicsLayout,
                framebuffer.OutputDescription
            )
        );

        CommandList cl = RF.CreateCommandList();
        cl.Begin();
        cl.SetPipeline(computePipeline);
        cl.SetComputeResourceSet(0, computeSet);
        cl.Dispatch(1, 1, 1);
        cl.SetFramebuffer(framebuffer);
        cl.ClearColorTarget(0, new());
        cl.SetPipeline(graphicsPipeline);
        cl.SetGraphicsResourceSet(0, graphicsSet);
        cl.Draw(4);
        cl.End();
        GD.SubmitCommands(cl);
        GD.WaitForIdle();

        Texture readback = GetReadback(finalOutput);
        MappedResourceView<RgbaFloat> readView = GD.Map<RgbaFloat>(readback, MapMode.Read);
        Assert.Equal(RgbaFloat.Red, readView[0, 0]);
        Assert.Equal(RgbaFloat.Green, readView[1, 0]);
        Assert.Equal(RgbaFloat.Blue, readView[2, 0]);
        Assert.Equal(RgbaFloat.White, readView[3, 0]);
        GD.Unmap(readback);
    }

    [SkippableTheory]
    [InlineData(2)]
    [InlineData(6)]
    public void ComputeBindTextureWithArrayLayersAsWriteable(uint ArrayLayers)
    {
        SkipIfNotComputeShader();

        uint TexSize = 32;
        uint MipLevels = 1;
        TextureDescription texDesc = TextureDescription.Texture2D(
            TexSize,
            TexSize,
            MipLevels,
            ArrayLayers,
            PixelFormat.R8_G8_B8_A8_UNorm,
            TextureUsage.Sampled | TextureUsage.Storage
        );
        Texture computeOutput = RF.CreateTexture(texDesc);

        ResourceLayout computeLayout = RF.CreateResourceLayout(
            new(
                new ResourceLayoutElementDescription(
                    "ComputeOutput",
                    ResourceKind.TextureReadWrite,
                    ShaderStages.Compute
                )
            )
        );
        ResourceSet computeSet = RF.CreateResourceSet(new(computeLayout, computeOutput));

        Pipeline computePipeline = RF.CreateComputePipeline(
            new(
                TestShaders.LoadCompute(RF, "ComputeImage2DArrayGenerator"),
                computeLayout,
                32,
                32,
                1
            )
        );

        CommandList cl = RF.CreateCommandList();
        cl.Begin();
        cl.SetPipeline(computePipeline);
        cl.SetComputeResourceSet(0, computeSet);
        cl.Dispatch(TexSize / 32, TexSize / 32, ArrayLayers);
        cl.End();
        GD.SubmitCommands(cl);
        GD.WaitForIdle();

        float sideColorStep = (float)Math.Floor(1.0f / ArrayLayers);
        Texture readback = GetReadback(computeOutput);

        foreach (int mip in Enumerable.Range(0, (int)MipLevels))
        {
            foreach (int layer in Enumerable.Range(0, (int)ArrayLayers))
            {
                uint subresource = readback.CalculateSubresource((uint)mip, (uint)layer);
                uint mipSize = TexSize >> mip;
                float expectedColor = (byte)255.0f * ((layer + 1) * sideColorStep);
                MappedResourceView<byte> map = GD.Map<byte>(readback, MapMode.Read, subresource);
                for (int y = 0; y < mipSize; y++)
                {
                    for (int x = 0; x < mipSize; x++)
                    {
                        Assert.Equal(map[x, y], expectedColor);
                    }
                }
                GD.Unmap(readback, subresource);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SampleTexture1D(bool arrayTexture)
    {
        if (!GD.Features.Texture1D)
        {
            return;
        }

        Texture target = RF.CreateTexture(
            TextureDescription.Texture2D(
                50,
                50,
                1,
                1,
                PixelFormat.R32_G32_B32_A32_Float,
                TextureUsage.RenderTarget
            )
        );
        Texture staging = RF.CreateTexture(
            TextureDescription.Texture2D(
                50,
                50,
                1,
                1,
                PixelFormat.R32_G32_B32_A32_Float,
                TextureUsage.Staging
            )
        );

        Framebuffer framebuffer = RF.CreateFramebuffer(new(null, target));

        string SetName = arrayTexture
            ? "FullScreenTriSampleTextureArray"
            : "FullScreenTriSampleTexture";
        ShaderSetDescription shaderSet = new([], TestShaders.LoadVertexFragment(RF, SetName));

        uint layers = arrayTexture ? 10u : 1u;
        Texture tex1D = RF.CreateTexture(
            TextureDescription.Texture1D(
                128,
                1,
                layers,
                PixelFormat.R32_G32_B32_A32_Float,
                TextureUsage.Sampled
            )
        );
        RgbaFloat[] colors = new RgbaFloat[tex1D.Width];
        colors.AsSpan().Fill(RgbaFloat.Pink);
        GD.UpdateTexture(tex1D, colors, 0, 0, 0, tex1D.Width, 1, 1, 0, 0);

        ResourceLayout layout = RF.CreateResourceLayout(
            new(
                new ResourceLayoutElementDescription(
                    "Tex",
                    ResourceKind.TextureReadOnly,
                    ShaderStages.Fragment
                ),
                new ResourceLayoutElementDescription(
                    "Smp",
                    ResourceKind.Sampler,
                    ShaderStages.Fragment
                )
            )
        );

        ResourceSet set = RF.CreateResourceSet(new(layout, tex1D, GD.PointSampler));

        GraphicsPipelineDescription gpd = new(
            BlendStateDescription.SingleOverrideBlend,
            DepthStencilStateDescription.Disabled,
            RasterizerStateDescription.CullNone,
            PrimitiveTopology.TriangleList,
            shaderSet,
            layout,
            framebuffer.OutputDescription
        );

        Pipeline pipeline = RF.CreateGraphicsPipeline(gpd);

        CommandList cl = RF.CreateCommandList();

        cl.Begin();
        cl.SetFramebuffer(framebuffer);
        cl.SetFullViewports();
        cl.SetFullScissorRects();
        cl.ClearColorTarget(0, RgbaFloat.Black);
        cl.SetPipeline(pipeline);
        cl.SetGraphicsResourceSet(0, set);
        cl.Draw(3);
        cl.CopyTexture(target, staging);
        cl.End();
        GD.SubmitCommands(cl);
        GD.WaitForIdle();

        MappedResourceView<RgbaFloat> readView = GD.Map<RgbaFloat>(staging, MapMode.Read);
        for (int x = 0; x < staging.Width; x++)
        {
            Assert.Equal(RgbaFloat.Pink, readView[x, 0]);
        }
        GD.Unmap(staging);
    }

    [Fact]
    public void BindTextureAcrossMultipleDrawCalls()
    {
        Texture target1 = RF.CreateTexture(
            TextureDescription.Texture2D(
                50,
                50,
                1,
                1,
                PixelFormat.R32_G32_B32_A32_Float,
                TextureUsage.RenderTarget
            )
        );
        Texture target2 = RF.CreateTexture(
            TextureDescription.Texture2D(
                50,
                50,
                1,
                1,
                PixelFormat.R32_G32_B32_A32_Float,
                TextureUsage.RenderTarget | TextureUsage.Sampled
            )
        );
        TextureView textureView = RF.CreateTextureView(target2);

        Texture staging1 = RF.CreateTexture(
            TextureDescription.Texture2D(
                50,
                50,
                1,
                1,
                PixelFormat.R32_G32_B32_A32_Float,
                TextureUsage.Staging
            )
        );
        Texture staging2 = RF.CreateTexture(
            TextureDescription.Texture2D(
                50,
                50,
                1,
                1,
                PixelFormat.R32_G32_B32_A32_Float,
                TextureUsage.Staging
            )
        );
        Texture staging3 = RF.CreateTexture(
            TextureDescription.Texture2D(
                50,
                50,
                1,
                1,
                PixelFormat.R32_G32_B32_A32_Float,
                TextureUsage.Staging
            )
        );

        Framebuffer framebuffer1 = RF.CreateFramebuffer(new(null, target1));
        Framebuffer framebuffer2 = RF.CreateFramebuffer(new(null, target2));

        // This shader doesn't really matter, just as long as it is different to the first
        // and third render pass and also doesn't use any texture bindings
        ShaderSetDescription textureShaderSet = new(
            [],
            TestShaders.LoadVertexFragment(RF, "FullScreenTriSampleTexture2D")
        );
        ShaderSetDescription quadShaderSet = new(
            [
                new(
                    new VertexElementDescription(
                        "A_V3",
                        VertexElementSemantic.TextureCoordinate,
                        VertexElementFormat.Float3
                    ),
                    new VertexElementDescription(
                        "B_V4",
                        VertexElementSemantic.TextureCoordinate,
                        VertexElementFormat.Float4
                    ),
                    new VertexElementDescription(
                        "C_V2",
                        VertexElementSemantic.TextureCoordinate,
                        VertexElementFormat.Float2
                    ),
                    new VertexElementDescription(
                        "D_V4",
                        VertexElementSemantic.TextureCoordinate,
                        VertexElementFormat.Float4
                    )
                ),
            ],
            TestShaders.LoadVertexFragment(RF, "VertexLayoutTestShader")
        );

        DeviceBuffer vertexBuffer = RF.CreateBuffer(
            new((uint)Unsafe.SizeOf<TestVertex>() * 3, BufferUsage.VertexBuffer)
        );
        GD.UpdateBuffer(
            vertexBuffer,
            0,
            new[] { new TestVertex(), new TestVertex(), new TestVertex() }
        );

        // Fill the second target with a known color
        RgbaFloat[] colors = new RgbaFloat[target2.Width * target2.Height];
        colors.AsSpan().Fill(RgbaFloat.Pink);
        GD.UpdateTexture(target2, colors, 0, 0, 0, target2.Width, target2.Height, 1, 0, 0);

        ResourceLayout textureLayout = RF.CreateResourceLayout(
            new(
                new ResourceLayoutElementDescription(
                    "Tex",
                    ResourceKind.TextureReadOnly,
                    ShaderStages.Fragment
                ),
                new ResourceLayoutElementDescription(
                    "Smp",
                    ResourceKind.Sampler,
                    ShaderStages.Fragment
                )
            )
        );

        ResourceSet textureSet = RF.CreateResourceSet(
            new(textureLayout, textureView, GD.PointSampler)
        );

        Pipeline texturePipeline = RF.CreateGraphicsPipeline(
            new(
                BlendStateDescription.SingleOverrideBlend,
                DepthStencilStateDescription.Disabled,
                RasterizerStateDescription.CullNone,
                PrimitiveTopology.TriangleList,
                textureShaderSet,
                textureLayout,
                framebuffer1.OutputDescription
            )
        );
        Pipeline quadPipeline = RF.CreateGraphicsPipeline(
            new(
                BlendStateDescription.SingleOverrideBlend,
                DepthStencilStateDescription.Disabled,
                RasterizerStateDescription.CullNone,
                PrimitiveTopology.TriangleList,
                quadShaderSet,
                [],
                framebuffer2.OutputDescription
            )
        );

        CommandList cl = RF.CreateCommandList();

        cl.Begin();
        cl.SetFramebuffer(framebuffer1);
        cl.SetFullViewports();
        cl.SetFullScissorRects();

        // First pass using texture shader
        cl.SetPipeline(texturePipeline);
        cl.ClearColorTarget(0, RgbaFloat.Black);
        cl.SetGraphicsResourceSet(0, textureSet);
        cl.Draw(3);
        cl.CopyTexture(target1, staging1);

        //  Second pass using dummy shader
        cl.SetPipeline(quadPipeline);
        cl.SetFramebuffer(framebuffer2);
        cl.ClearColorTarget(0, RgbaFloat.Blue);
        cl.SetVertexBuffer(0, vertexBuffer);
        cl.Draw(3);
        cl.CopyTexture(target2, staging2);

        // Third pass using texture shader again
        cl.SetPipeline(texturePipeline);
        cl.SetFramebuffer(framebuffer1);
        cl.ClearColorTarget(0, RgbaFloat.Black);
        cl.SetGraphicsResourceSet(0, textureSet);
        cl.Draw(3);
        cl.CopyTexture(target1, staging3);

        cl.End();
        GD.SubmitCommands(cl);
        GD.WaitForIdle();

        MappedResourceView<RgbaFloat> readView1 = GD.Map<RgbaFloat>(staging1, MapMode.Read);
        MappedResourceView<RgbaFloat> readView2 = GD.Map<RgbaFloat>(staging2, MapMode.Read);
        MappedResourceView<RgbaFloat> readView3 = GD.Map<RgbaFloat>(staging3, MapMode.Read);
        for (int x = 0; x < staging1.Width; x++)
        {
            Assert.Equal(RgbaFloat.Pink, readView1[x, 0]);
            Assert.Equal(RgbaFloat.Blue, readView2[x, 0]);
            Assert.Equal(RgbaFloat.Blue, readView3[x, 0]);
        }
        GD.Unmap(staging1);
        GD.Unmap(staging2);
        GD.Unmap(staging3);
    }

    [Theory]
    [InlineData(2, 0)]
    [InlineData(5, 3)]
    [InlineData(32, 31)]
    public void FramebufferArrayLayer(uint layerCount, uint targetLayer)
    {
        Texture target = RF.CreateTexture(
            TextureDescription.Texture2D(
                16,
                16,
                1,
                layerCount,
                PixelFormat.R32_G32_B32_A32_Float,
                TextureUsage.RenderTarget
            )
        );
        Framebuffer framebuffer = RF.CreateFramebuffer(
            new(null, [new FramebufferAttachmentDescription(target, targetLayer)])
        );

        string setName = "FullScreenTriSampleTexture2D";
        ShaderSetDescription shaderSet = new([], TestShaders.LoadVertexFragment(RF, setName));

        Texture tex2D = RF.CreateTexture(
            TextureDescription.Texture2D(
                128,
                128,
                1,
                1,
                PixelFormat.R32_G32_B32_A32_Float,
                TextureUsage.Sampled
            )
        );
        RgbaFloat[] colors = new RgbaFloat[tex2D.Width * tex2D.Height];
        colors.AsSpan().Fill(RgbaFloat.Pink);
        GD.UpdateTexture(tex2D, colors, 0, 0, 0, tex2D.Width, 1, 1, 0, 0);

        ResourceLayout layout = RF.CreateResourceLayout(
            new(
                new ResourceLayoutElementDescription(
                    "Tex",
                    ResourceKind.TextureReadOnly,
                    ShaderStages.Fragment
                ),
                new ResourceLayoutElementDescription(
                    "Smp",
                    ResourceKind.Sampler,
                    ShaderStages.Fragment
                )
            )
        );

        ResourceSet set = RF.CreateResourceSet(new(layout, tex2D, GD.PointSampler));

        GraphicsPipelineDescription gpd = new(
            BlendStateDescription.SingleOverrideBlend,
            DepthStencilStateDescription.Disabled,
            RasterizerStateDescription.CullNone,
            PrimitiveTopology.TriangleList,
            shaderSet,
            layout,
            framebuffer.OutputDescription
        );

        Pipeline pipeline = RF.CreateGraphicsPipeline(gpd);

        CommandList cl = RF.CreateCommandList();

        cl.Begin();
        cl.SetFramebuffer(framebuffer);
        cl.SetFullViewports();
        cl.SetFullScissorRects();
        cl.ClearColorTarget(0, RgbaFloat.Black);
        cl.SetPipeline(pipeline);
        cl.SetGraphicsResourceSet(0, set);
        cl.Draw(3);
        cl.End();
        GD.SubmitCommands(cl);
        GD.WaitForIdle();

        Texture staging = GetReadback(target);
        MappedResourceView<RgbaFloat> readView = GD.Map<RgbaFloat>(
            staging,
            MapMode.Read,
            targetLayer
        );
        for (int x = 0; x < staging.Width; x++)
        {
            Assert.Equal(RgbaFloat.Pink, readView[x, 0]);
        }
        GD.Unmap(staging, targetLayer);
    }

    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(1, 0, 3)]
    [InlineData(1, 0, 5)]
    [InlineData(4, 2, 0)]
    [InlineData(4, 2, 3)]
    [InlineData(4, 2, 5)]
    public void RenderToCubemapFace(uint layerCount, uint targetLayer, uint targetFace)
    {
        Texture target = RF.CreateTexture(
            TextureDescription.Texture2D(
                16,
                16,
                1,
                layerCount,
                PixelFormat.R32_G32_B32_A32_Float,
                TextureUsage.RenderTarget | TextureUsage.Cubemap
            )
        );
        Framebuffer framebuffer = RF.CreateFramebuffer(
            new(
                null,
                [new FramebufferAttachmentDescription(target, (targetLayer * 6) + targetFace)]
            )
        );

        string setName = "FullScreenTriSampleTexture2D";
        ShaderSetDescription shaderSet = new([], TestShaders.LoadVertexFragment(RF, setName));

        Texture tex2D = RF.CreateTexture(
            TextureDescription.Texture2D(
                128,
                128,
                1,
                1,
                PixelFormat.R32_G32_B32_A32_Float,
                TextureUsage.Sampled
            )
        );
        RgbaFloat[] colors = new RgbaFloat[tex2D.Width * tex2D.Height];
        colors.AsSpan().Fill(RgbaFloat.Pink);
        GD.UpdateTexture(tex2D, colors, 0, 0, 0, tex2D.Width, 1, 1, 0, 0);

        ResourceLayout layout = RF.CreateResourceLayout(
            new(
                new ResourceLayoutElementDescription(
                    "Tex",
                    ResourceKind.TextureReadOnly,
                    ShaderStages.Fragment
                ),
                new ResourceLayoutElementDescription(
                    "Smp",
                    ResourceKind.Sampler,
                    ShaderStages.Fragment
                )
            )
        );

        ResourceSet set = RF.CreateResourceSet(new(layout, tex2D, GD.PointSampler));

        GraphicsPipelineDescription gpd = new(
            BlendStateDescription.SingleOverrideBlend,
            DepthStencilStateDescription.Disabled,
            RasterizerStateDescription.CullNone,
            PrimitiveTopology.TriangleList,
            shaderSet,
            layout,
            framebuffer.OutputDescription
        );

        Pipeline pipeline = RF.CreateGraphicsPipeline(gpd);

        CommandList cl = RF.CreateCommandList();

        cl.Begin();
        cl.SetFramebuffer(framebuffer);
        cl.SetFullViewports();
        cl.SetFullScissorRects();
        cl.ClearColorTarget(0, RgbaFloat.Black);
        cl.SetPipeline(pipeline);
        cl.SetGraphicsResourceSet(0, set);
        cl.Draw(3);
        cl.End();
        GD.SubmitCommands(cl);
        GD.WaitForIdle();

        Texture staging = GetReadback(target);
        MappedResourceView<RgbaFloat> readView = GD.Map<RgbaFloat>(
            staging,
            MapMode.Read,
            (targetLayer * 6) + targetFace
        );
        for (int x = 0; x < staging.Width; x++)
        {
            Assert.Equal(RgbaFloat.Pink, readView[x, 0]);
        }
        GD.Unmap(staging, (targetLayer * 6) + targetFace);
    }

    [Fact]
    public void WriteFragmentDepth()
    {
        Texture depthTarget = RF.CreateTexture(
            TextureDescription.Texture2D(
                64,
                64,
                1,
                1,
                PixelFormat.D32_Float,
                TextureUsage.DepthStencil | TextureUsage.Sampled
            )
        );
        Framebuffer framebuffer = RF.CreateFramebuffer(new(depthTarget));

        string setName = "FullScreenWriteDepth";
        ShaderSetDescription shaderSet = new([], TestShaders.LoadVertexFragment(RF, setName));

        ResourceLayout layout = RF.CreateResourceLayout(
            new(
                new ResourceLayoutElementDescription(
                    "FramebufferInfo",
                    ResourceKind.UniformBuffer,
                    ShaderStages.Fragment
                )
            )
        );

        DeviceBuffer ub = RF.CreateBuffer(new(16, BufferUsage.UniformBuffer));
        GD.UpdateBuffer(ub, 0, new Vector4(depthTarget.Width, depthTarget.Height, 0, 0));
        ResourceSet rs = RF.CreateResourceSet(new(layout, ub));

        GraphicsPipelineDescription gpd = new(
            BlendStateDescription.SingleOverrideBlend,
            new(true, true, ComparisonKind.Always),
            RasterizerStateDescription.CullNone,
            PrimitiveTopology.TriangleList,
            shaderSet,
            layout,
            framebuffer.OutputDescription
        );

        Pipeline pipeline = RF.CreateGraphicsPipeline(gpd);

        CommandList cl = RF.CreateCommandList();

        cl.Begin();
        cl.SetFramebuffer(framebuffer);
        cl.SetFullViewports();
        cl.SetFullScissorRects();
        cl.ClearDepthStencil(0f);
        cl.SetPipeline(pipeline);
        cl.SetGraphicsResourceSet(0, rs);
        cl.Draw(3);
        cl.End();
        GD.SubmitCommands(cl);
        GD.WaitForIdle();

        Texture readback = GetReadback(depthTarget);

        MappedResourceView<float> readView = GD.Map<float>(readback, MapMode.Read);
        for (uint y = 0; y < readback.Height; y++)
        {
            for (uint x = 0; x < readback.Width; x++)
            {
                float xComp = x;
                float yComp = y * readback.Width;
                float val = (yComp + xComp) / (readback.Width * readback.Height);

                Assert.Equal(val, readView[x, y], 2.0f);
            }
        }
        GD.Unmap(readback);
    }

    [Fact]
    public void UseBlendFactor()
    {
        const uint width = 512;
        const uint height = 512;
        using Texture output = RF.CreateTexture(
            TextureDescription.Texture2D(
                width,
                height,
                1,
                1,
                PixelFormat.R32_G32_B32_A32_Float,
                TextureUsage.RenderTarget
            )
        );
        using Framebuffer framebuffer = RF.CreateFramebuffer(new(null, output));

        float yMod = GD.IsClipSpaceYInverted ? -1.0f : 1.0f;
        ColoredVertex[] vertices =
        [
            new() { Position = new(-1, 1 * yMod), Color = Vector4.One },
            new() { Position = new(1, 1 * yMod), Color = Vector4.One },
            new() { Position = new(-1, -1 * yMod), Color = Vector4.One },
            new() { Position = new(1, -1 * yMod), Color = Vector4.One },
        ];
        uint vertexSize = (uint)Unsafe.SizeOf<ColoredVertex>();
        using DeviceBuffer buffer = RF.CreateBuffer(
            new(
                vertexSize * (uint)vertices.Length,
                BufferUsage.StructuredBufferReadOnly,
                vertexSize,
                true
            )
        );
        GD.UpdateBuffer(buffer, 0, vertices);

        using ResourceLayout graphicsLayout = RF.CreateResourceLayout(
            new(
                new ResourceLayoutElementDescription(
                    "InputVertices",
                    ResourceKind.StructuredBufferReadOnly,
                    ShaderStages.Vertex
                )
            )
        );
        using ResourceSet graphicsSet = RF.CreateResourceSet(new(graphicsLayout, buffer));

        BlendStateDescription blendDesc = new()
        {
            BlendFactor = new(0.25f, 0.5f, 0.75f, 1),
            AttachmentStates =
            [
                new()
                {
                    BlendEnabled = true,
                    SourceColorFactor = BlendFactor.BlendFactor,
                    DestinationColorFactor = BlendFactor.Zero,
                    ColorFunction = BlendFunction.Add,
                    SourceAlphaFactor = BlendFactor.BlendFactor,
                    DestinationAlphaFactor = BlendFactor.Zero,
                    AlphaFunction = BlendFunction.Add,
                },
            ],
        };
        GraphicsPipelineDescription pipelineDesc = new(
            blendDesc,
            DepthStencilStateDescription.Disabled,
            RasterizerStateDescription.Default,
            PrimitiveTopology.TriangleStrip,
            new([], TestShaders.LoadVertexFragment(RF, "ColoredQuadRenderer")),
            graphicsLayout,
            framebuffer.OutputDescription
        );

        using (Pipeline pipeline1 = RF.CreateGraphicsPipeline(pipelineDesc))
        using (CommandList cl = RF.CreateCommandList())
        {
            cl.Begin();
            cl.SetFramebuffer(framebuffer);
            cl.ClearColorTarget(0, RgbaFloat.Clear);
            cl.SetPipeline(pipeline1);
            cl.SetGraphicsResourceSet(0, graphicsSet);
            cl.Draw((uint)vertices.Length);
            cl.End();
            GD.SubmitCommands(cl);
            GD.WaitForIdle();
        }

        using (Texture readback = GetReadback(output))
        {
            MappedResourceView<RgbaFloat> readView = GD.Map<RgbaFloat>(readback, MapMode.Read);
            for (uint y = 0; y < height; y++)
            {
                for (uint x = 0; x < width; x++)
                {
                    Assert.Equal(new(0.25f, 0.5f, 0.75f, 1), readView[x, y]);
                }
            }
            GD.Unmap(readback);
        }

        blendDesc.BlendFactor = new(0, 1, 0.5f, 0);
        blendDesc.AttachmentStates[0].DestinationColorFactor = BlendFactor.InverseBlendFactor;
        blendDesc.AttachmentStates[0].DestinationAlphaFactor = BlendFactor.InverseBlendFactor;
        pipelineDesc.BlendState = blendDesc;

        using (Pipeline pipeline2 = RF.CreateGraphicsPipeline(pipelineDesc))
        using (CommandList cl = RF.CreateCommandList())
        {
            cl.Begin();
            cl.SetFramebuffer(framebuffer);
            cl.SetPipeline(pipeline2);
            cl.SetGraphicsResourceSet(0, graphicsSet);
            cl.Draw((uint)vertices.Length);
            cl.End();
            GD.SubmitCommands(cl);
            GD.WaitForIdle();
        }

        using (Texture readback = GetReadback(output))
        {
            MappedResourceView<RgbaFloat> readView = GD.Map<RgbaFloat>(readback, MapMode.Read);
            for (uint y = 0; y < height; y++)
            {
                for (uint x = 0; x < width; x++)
                {
                    Assert.Equal(new(0.25f, 1, 0.875f, 1), readView[x, y]);
                }
            }
            GD.Unmap(readback);
        }
    }

    [Fact]
    public void UseColorWriteMask()
    {
        Texture output = RF.CreateTexture(
            TextureDescription.Texture2D(
                64,
                64,
                1,
                1,
                PixelFormat.R32_G32_B32_A32_Float,
                TextureUsage.RenderTarget
            )
        );
        using Framebuffer framebuffer = RF.CreateFramebuffer(new(null, output));

        float yMod = GD.IsClipSpaceYInverted ? -1.0f : 1.0f;
        ColoredVertex[] vertices =
        [
            new() { Position = new(-1, 1 * yMod), Color = Vector4.One },
            new() { Position = new(1, 1 * yMod), Color = Vector4.One },
            new() { Position = new(-1, -1 * yMod), Color = Vector4.One },
            new() { Position = new(1, -1 * yMod), Color = Vector4.One },
        ];
        uint vertexSize = (uint)Unsafe.SizeOf<ColoredVertex>();
        using DeviceBuffer buffer = RF.CreateBuffer(
            new(
                vertexSize * (uint)vertices.Length,
                BufferUsage.StructuredBufferReadOnly,
                vertexSize,
                true
            )
        );
        GD.UpdateBuffer(buffer, 0, vertices);

        using ResourceLayout graphicsLayout = RF.CreateResourceLayout(
            new(
                new ResourceLayoutElementDescription(
                    "InputVertices",
                    ResourceKind.StructuredBufferReadOnly,
                    ShaderStages.Vertex
                )
            )
        );
        using ResourceSet graphicsSet = RF.CreateResourceSet(new(graphicsLayout, buffer));

        BlendStateDescription blendDesc = new()
        {
            AttachmentStates =
            [
                new()
                {
                    BlendEnabled = true,
                    SourceColorFactor = BlendFactor.One,
                    DestinationColorFactor = BlendFactor.Zero,
                    ColorFunction = BlendFunction.Add,
                    SourceAlphaFactor = BlendFactor.One,
                    DestinationAlphaFactor = BlendFactor.Zero,
                    AlphaFunction = BlendFunction.Add,
                },
            ],
        };

        GraphicsPipelineDescription pipelineDesc = new(
            blendDesc,
            DepthStencilStateDescription.Disabled,
            RasterizerStateDescription.Default,
            PrimitiveTopology.TriangleStrip,
            new([], TestShaders.LoadVertexFragment(RF, "ColoredQuadRenderer")),
            graphicsLayout,
            framebuffer.OutputDescription
        );

        using (Pipeline pipeline1 = RF.CreateGraphicsPipeline(pipelineDesc))
        using (CommandList cl = RF.CreateCommandList())
        {
            cl.Begin();
            cl.SetFramebuffer(framebuffer);
            cl.ClearColorTarget(0, RgbaFloat.Clear);
            cl.SetPipeline(pipeline1);
            cl.SetGraphicsResourceSet(0, graphicsSet);
            cl.Draw((uint)vertices.Length);
            cl.End();
            GD.SubmitCommands(cl);
            GD.WaitForIdle();
        }

        using (Texture readback = GetReadback(output))
        {
            MappedResourceView<RgbaFloat> readView = GD.Map<RgbaFloat>(readback, MapMode.Read);
            for (uint y = 0; y < output.Height; y++)
            {
                for (uint x = 0; x < output.Width; x++)
                {
                    Assert.Equal(RgbaFloat.White, readView[x, y]);
                }
            }
            GD.Unmap(readback);
        }

        foreach (ColorWriteMask mask in Enum.GetValues<ColorWriteMask>())
        {
            blendDesc.AttachmentStates[0].ColorWriteMask = mask;
            pipelineDesc.BlendState = blendDesc;

            using (Pipeline maskedPipeline = RF.CreateGraphicsPipeline(pipelineDesc))
            using (CommandList cl = RF.CreateCommandList())
            {
                cl.Begin();
                cl.SetFramebuffer(framebuffer);
                cl.ClearColorTarget(0, new(0.25f, 0.25f, 0.25f, 0.25f));
                cl.SetPipeline(maskedPipeline);
                cl.SetGraphicsResourceSet(0, graphicsSet);
                cl.Draw((uint)vertices.Length);
                cl.End();
                GD.SubmitCommands(cl);
                GD.WaitForIdle();
            }

            using (Texture readback = GetReadback(output))
            {
                MappedResourceView<RgbaFloat> readView = GD.Map<RgbaFloat>(readback, MapMode.Read);
                for (uint y = 0; y < output.Height; y++)
                {
                    for (uint x = 0; x < output.Width; x++)
                    {
                        Assert.Equal(
                            mask.HasFlag(ColorWriteMask.Red) ? 1 : 0.25f,
                            readView[x, y].R
                        );
                        Assert.Equal(
                            mask.HasFlag(ColorWriteMask.Green) ? 1 : 0.25f,
                            readView[x, y].G
                        );
                        Assert.Equal(
                            mask.HasFlag(ColorWriteMask.Blue) ? 1 : 0.25f,
                            readView[x, y].B
                        );
                        Assert.Equal(
                            mask.HasFlag(ColorWriteMask.Alpha) ? 1 : 0.25f,
                            readView[x, y].A
                        );
                    }
                }
                GD.Unmap(readback);
            }
        }
    }
}

#if TEST_OPENGL
[Trait("Backend", "OpenGL")]
public class OpenGLRenderTests : RenderTests<OpenGLDeviceCreator> { }
#endif
#if TEST_OPENGLES
[Trait("Backend", "OpenGLES")]
public class OpenGLESRenderTests : RenderTests<OpenGLESDeviceCreator> { }
#endif
#if TEST_VULKAN
[Trait("Backend", "Vulkan")]
public class VulkanRenderTests : RenderTests<VulkanDeviceCreator> { }
#endif
#if TEST_D3D11
[Trait("Backend", "D3D11")]
public class D3D11RenderTests : RenderTests<D3D11DeviceCreator> { }
#endif
#if TEST_METAL
[Trait("Backend", "Metal")]
public class MetalRenderTests : RenderTests<MetalDeviceCreator> { }
#endif
