﻿using System;
using System.Numerics;
using System.Text;
using Veldrid.SPIRV;

namespace Veldrid.VirtualReality;

internal class TextureBlitter : IDisposable
{
    readonly ResourceLayout _rl;
    readonly ResourceLayout _sampleRegionLayout;
    readonly Pipeline _pipeline;
    readonly DeviceBuffer _sampleRegionUB;
    readonly ResourceSet _sampleRegionSet;
    Vector4 _lastMinMaxUV;

    public ResourceLayout ResourceLayout => _rl;

    public TextureBlitter(
        GraphicsDevice gd,
        ResourceFactory factory,
        OutputDescription outputDesc,
        bool srgbOutput
    )
    {
        SpecializationConstant[] specConstants =
        [
            new(0, srgbOutput),
            new(
                1,
                gd.BackendType == GraphicsBackend.OpenGL
                    || gd.BackendType == GraphicsBackend.OpenGLES
            ),
        ];

        Shader[] shaders = factory.CreateFromSpirv(
            new(ShaderStages.Vertex, Encoding.ASCII.GetBytes(vertexGlsl), "main"),
            new(ShaderStages.Fragment, Encoding.ASCII.GetBytes(fragmentGlsl), "main"),
            new CrossCompileOptions { Specializations = specConstants }
        );

        _rl = factory.CreateResourceLayout(
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

        _sampleRegionLayout = factory.CreateResourceLayout(
            new(
                new ResourceLayoutElementDescription(
                    "SampleRegionInfo",
                    ResourceKind.UniformBuffer,
                    ShaderStages.Fragment
                )
            )
        );

        _pipeline = factory.CreateGraphicsPipeline(
            new(
                BlendStateDescription.SingleOverrideBlend,
                DepthStencilStateDescription.Disabled,
                RasterizerStateDescription.CullNone,
                PrimitiveTopology.TriangleStrip,
                new([], [shaders[0], shaders[1]], specConstants),
                [_rl, _sampleRegionLayout],
                outputDesc
            )
        );

        _sampleRegionUB = factory.CreateBuffer(new(16, BufferUsage.UniformBuffer));
        _sampleRegionSet = factory.CreateResourceSet(new(_sampleRegionLayout, _sampleRegionUB));

        _lastMinMaxUV = new(0, 0, 1, 1);
        gd.UpdateBuffer(_sampleRegionUB, 0, _lastMinMaxUV);
    }

    public void Render(CommandList cl, ResourceSet rs, Vector2 minUV, Vector2 maxUV)
    {
        Vector4 newVal = new(minUV.X, minUV.Y, maxUV.X, maxUV.Y);
        if (_lastMinMaxUV != newVal)
        {
            _lastMinMaxUV = newVal;
            cl.UpdateBuffer(_sampleRegionUB, 0, newVal);
        }

        cl.SetPipeline(_pipeline);
        cl.SetGraphicsResourceSet(0, rs);
        cl.SetGraphicsResourceSet(1, _sampleRegionSet);
        cl.Draw(4);
    }

    public void Dispose()
    {
        _rl.Dispose();
        _pipeline.Dispose();
        _sampleRegionUB.Dispose();
        _sampleRegionSet.Dispose();
        _sampleRegionLayout.Dispose();
    }

    const string vertexGlsl =
        @"
#version 450
#extension GL_KHR_vulkan_glsl : enable

layout (location = 0) out vec2 fsin_UV;

const vec4 QuadInfos[4] = 
{
    vec4(-1, 1, 0, 0),
    vec4(1, 1, 1, 0),
    vec4(-1, -1, 0, 1),
    vec4(1, -1, 1, 1),
};

void main()
{
    gl_Position = vec4(QuadInfos[gl_VertexIndex].xy, 0, 1);
    fsin_UV = QuadInfos[gl_VertexIndex].zw;
}
";

    const string fragmentGlsl =
        @"
#version 450

layout(set = 0, binding = 0) uniform texture2D Input;
layout(set = 0, binding = 1) uniform sampler InputSampler;

layout(set = 1, binding = 0) uniform SampleRegionInfo
{
    vec2 MinUV;
    vec2 MaxUV;
};

layout(location = 0) in vec2 fsin_UV;
layout(location = 0) out vec4 fsout_Color0;

layout (constant_id = 0) const bool OutputSrgb = false;
layout (constant_id = 1) const bool InvertTexY = false;

// http://chilliant.blogspot.com/2012/08/srgb-approximations-for-hlsl.html
vec3 LinearToSrgb(vec3 linear)
{
  vec3 S1 = sqrt(linear);
  vec3 S2 = sqrt(S1);
  vec3 S3 = sqrt(S2);
  return 0.585122381 * S1 + 0.783140355 * S2 - 0.368262736 * S3;
}

void main()
{
    vec2 uv = fsin_UV;
    if (InvertTexY) { uv.y = 1 - uv.y; }
    uv = mix(MinUV, MaxUV, uv);

    fsout_Color0 = texture(sampler2D(Input, InputSampler), uv);
    if (OutputSrgb)
    {
        fsout_Color0 = vec4(LinearToSrgb(fsout_Color0.rgb), fsout_Color0.a);
    }
}
";
}
