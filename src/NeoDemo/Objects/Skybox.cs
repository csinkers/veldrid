﻿using System.Numerics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Veldrid.ImageSharp;
using Veldrid.Utilities;

namespace Veldrid.NeoDemo.Objects;

public class Skybox(
    Image<Rgba32> front,
    Image<Rgba32> back,
    Image<Rgba32> left,
    Image<Rgba32> right,
    Image<Rgba32> top,
    Image<Rgba32> bottom
) : Renderable
{
    // Context objects
    DeviceBuffer _vb;
    DeviceBuffer _ib;
    Pipeline _pipeline;
    Pipeline _reflectionPipeline;
    ResourceSet _resourceSet;
    readonly DisposeCollector _disposeCollector = new();

    public override void CreateDeviceObjects(GraphicsDevice gd, CommandList cl, SceneContext sc)
    {
        ResourceFactory factory = gd.ResourceFactory;

        _vb = factory.CreateBuffer(new(s_vertices.SizeInBytes(), BufferUsage.VertexBuffer));
        cl.UpdateBuffer(_vb, 0, s_vertices);

        _ib = factory.CreateBuffer(new(s_indices.SizeInBytes(), BufferUsage.IndexBuffer));
        cl.UpdateBuffer(_ib, 0, s_indices);

        ImageSharpCubemapTexture imageSharpCubemapTexture = new(
            right,
            left,
            top,
            bottom,
            back,
            front,
            false
        );

        Texture textureCube = imageSharpCubemapTexture.CreateDeviceTexture(gd, factory);
        TextureView textureView = factory.CreateTextureView(
            new TextureViewDescription(textureCube)
        );

        VertexLayoutDescription[] vertexLayouts =
        [
            new(
                new VertexElementDescription(
                    "vsin_Position",
                    VertexElementSemantic.TextureCoordinate,
                    VertexElementFormat.Float3
                )
            ),
        ];

        (Shader vs, Shader fs) = StaticResourceCache.GetShaders(gd, gd.ResourceFactory, "Skybox");

        _layout = factory.CreateResourceLayout(
            new(
                new ResourceLayoutElementDescription(
                    "Projection",
                    ResourceKind.UniformBuffer,
                    ShaderStages.Vertex
                ),
                new ResourceLayoutElementDescription(
                    "View",
                    ResourceKind.UniformBuffer,
                    ShaderStages.Vertex
                ),
                new ResourceLayoutElementDescription(
                    "CubeTexture",
                    ResourceKind.TextureReadOnly,
                    ShaderStages.Fragment
                ),
                new ResourceLayoutElementDescription(
                    "CubeSampler",
                    ResourceKind.Sampler,
                    ShaderStages.Fragment
                )
            )
        );

        GraphicsPipelineDescription pd = new(
            BlendStateDescription.SingleAlphaBlend,
            gd.IsDepthRangeZeroToOne
                ? DepthStencilStateDescription.DepthOnlyGreaterEqual
                : DepthStencilStateDescription.DepthOnlyLessEqual,
            new(FaceCullMode.None, PolygonFillMode.Solid, FrontFace.Clockwise, true, true),
            PrimitiveTopology.TriangleList,
            new(vertexLayouts, [vs, fs], ShaderHelper.GetSpecializations(gd)),
            [_layout],
            sc.MainSceneFramebuffer.OutputDescription
        );

        _pipeline = factory.CreateGraphicsPipeline(pd);
        pd.Outputs = sc.ReflectionFramebuffer.OutputDescription;
        _reflectionPipeline = factory.CreateGraphicsPipeline(pd);

        _resourceSet = factory.CreateResourceSet(
            new(
                _layout,
                sc.ProjectionMatrixBuffer,
                sc.ViewMatrixBuffer,
                textureView,
                gd.PointSampler
            )
        );

        _disposeCollector.Add(
            _vb,
            _ib,
            textureCube,
            textureView,
            _layout,
            _pipeline,
            _reflectionPipeline,
            _resourceSet,
            vs,
            fs
        );
    }

    public override void UpdatePerFrameResources(
        GraphicsDevice gd,
        CommandList cl,
        SceneContext sc
    ) { }

    public static Skybox LoadDefaultSkybox()
    {
        return new(
            Image.Load<Rgba32>(AssetHelper.GetPath("Textures/cloudtop/cloudtop_ft.png")),
            Image.Load<Rgba32>(AssetHelper.GetPath("Textures/cloudtop/cloudtop_bk.png")),
            Image.Load<Rgba32>(AssetHelper.GetPath("Textures/cloudtop/cloudtop_lf.png")),
            Image.Load<Rgba32>(AssetHelper.GetPath("Textures/cloudtop/cloudtop_rt.png")),
            Image.Load<Rgba32>(AssetHelper.GetPath("Textures/cloudtop/cloudtop_up.png")),
            Image.Load<Rgba32>(AssetHelper.GetPath("Textures/cloudtop/cloudtop_dn.png"))
        );
    }

    public override void DestroyDeviceObjects()
    {
        _disposeCollector.DisposeAll();
    }

    public override void Render(
        GraphicsDevice gd,
        CommandList cl,
        SceneContext sc,
        RenderPasses renderPass
    )
    {
        cl.SetVertexBuffer(0, _vb);
        cl.SetIndexBuffer(_ib, IndexFormat.UInt16);
        cl.SetPipeline(renderPass == RenderPasses.ReflectionMap ? _reflectionPipeline : _pipeline);
        cl.SetGraphicsResourceSet(0, _resourceSet);
        Texture texture =
            renderPass == RenderPasses.ReflectionMap
                ? sc.ReflectionColorTexture
                : sc.MainSceneColorTexture;
        float depth = gd.IsDepthRangeZeroToOne ? 0 : 1;
        cl.SetViewport(0, new(0, 0, texture.Width, texture.Height, depth, depth));
        cl.DrawIndexed((uint)s_indices.Length, 1, 0, 0, 0);
        cl.SetViewport(0, new(0, 0, texture.Width, texture.Height, 0, 1));
    }

    public override RenderPasses RenderPasses => RenderPasses.Standard | RenderPasses.ReflectionMap;

    public override RenderOrderKey GetRenderOrderKey(Vector3 cameraPosition)
    {
        return new(ulong.MaxValue);
    }

    static readonly VertexPosition[] s_vertices =
    [
        // Top
        new(new(-20.0f, 20.0f, -20.0f)),
        new(new(20.0f, 20.0f, -20.0f)),
        new(new(20.0f, 20.0f, 20.0f)),
        new(new(-20.0f, 20.0f, 20.0f)),
        // Bottom
        new(new(-20.0f, -20.0f, 20.0f)),
        new(new(20.0f, -20.0f, 20.0f)),
        new(new(20.0f, -20.0f, -20.0f)),
        new(new(-20.0f, -20.0f, -20.0f)),
        // Left
        new(new(-20.0f, 20.0f, -20.0f)),
        new(new(-20.0f, 20.0f, 20.0f)),
        new(new(-20.0f, -20.0f, 20.0f)),
        new(new(-20.0f, -20.0f, -20.0f)),
        // Right
        new(new(20.0f, 20.0f, 20.0f)),
        new(new(20.0f, 20.0f, -20.0f)),
        new(new(20.0f, -20.0f, -20.0f)),
        new(new(20.0f, -20.0f, 20.0f)),
        // Back
        new(new(20.0f, 20.0f, -20.0f)),
        new(new(-20.0f, 20.0f, -20.0f)),
        new(new(-20.0f, -20.0f, -20.0f)),
        new(new(20.0f, -20.0f, -20.0f)),
        // Front
        new(new(-20.0f, 20.0f, 20.0f)),
        new(new(20.0f, 20.0f, 20.0f)),
        new(new(20.0f, -20.0f, 20.0f)),
        new(new(-20.0f, -20.0f, 20.0f)),
    ];

    static readonly ushort[] s_indices =
    [
        0,
        1,
        2,
        0,
        2,
        3,
        4,
        5,
        6,
        4,
        6,
        7,
        8,
        9,
        10,
        8,
        10,
        11,
        12,
        13,
        14,
        12,
        14,
        15,
        16,
        17,
        18,
        16,
        18,
        19,
        20,
        21,
        22,
        20,
        22,
        23,
    ];

    ResourceLayout _layout;
}
