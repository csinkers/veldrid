﻿using System;
using Veldrid.SPIRV;
using Veldrid.Tests.Utilities;
using Xunit;

namespace Veldrid.Tests;

public abstract class PipelineTests<T> : GraphicsDeviceTestBase<T>
    where T : IGraphicsDeviceCreator
{
    [Fact]
    public void CreatePipelines_DifferentInstanceStepRate_Succeeds()
    {
        Texture colorTex = RF.CreateTexture(
            TextureDescription.Texture2D(
                1,
                1,
                1,
                1,
                PixelFormat.R8_G8_B8_A8_UNorm,
                TextureUsage.RenderTarget
            )
        );
        Framebuffer framebuffer = RF.CreateFramebuffer(new(null, colorTex));

        ShaderSetDescription shaderSet = new(
            [
                new(
                    24,
                    0,
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

        GraphicsPipelineDescription gpd = new(
            BlendStateDescription.SingleOverrideBlend,
            DepthStencilStateDescription.Disabled,
            RasterizerStateDescription.Default,
            PrimitiveTopology.PointList,
            shaderSet,
            layout,
            framebuffer.OutputDescription
        );

        _ = RF.CreateGraphicsPipeline(gpd);
        _ = RF.CreateGraphicsPipeline(gpd);

        if (gpd.ShaderSet.VertexLayouts == null || gpd.ShaderSet.VertexLayouts.Length == 0)
            throw new InvalidOperationException("VertexLayouts not set");

        gpd.ShaderSet.VertexLayouts[0].InstanceStepRate = 4;
        _ = RF.CreateGraphicsPipeline(gpd);

        gpd.ShaderSet.VertexLayouts[0].InstanceStepRate = 5;
        _ = RF.CreateGraphicsPipeline(gpd);
    }
}

#if TEST_OPENGL
[Trait("Backend", "OpenGL")]
public class OpenGLPipelineTests : PipelineTests<OpenGLDeviceCreator> { }
#endif
#if TEST_OPENGLES
[Trait("Backend", "OpenGLES")]
public class OpenGLESPipelineTests : PipelineTests<OpenGLESDeviceCreator> { }
#endif
#if TEST_VULKAN
[Trait("Backend", "Vulkan")]
public class VulkanPipelineTests : PipelineTests<VulkanDeviceCreator> { }
#endif
#if TEST_D3D11
[Trait("Backend", "D3D11")]
public class D3D11PipelineTests : PipelineTests<D3D11DeviceCreator> { }
#endif
#if TEST_METAL
[Trait("Backend", "Metal")]
public class MetalPipelineTests : PipelineTests<MetalDeviceCreator> { }
#endif
