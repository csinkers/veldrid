﻿using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Veldrid.ImageSharp;
using Veldrid.SPIRV;
using Veldrid.Utilities;

namespace Veldrid.NeoDemo.Objects;

public class TexturedMesh : CullRenderable
{
    // Useful for testing uniform bindings with an offset.
    static readonly bool s_useUniformOffset = false;
    uint _uniformOffset;

    readonly string _name;
    readonly ConstructedMesh _meshData;
    readonly ImageSharpTexture? _textureData;
    readonly ImageSharpTexture? _alphaTextureData;
    readonly Transform _transform = new();

    readonly BoundingBox _centeredBounds;
    DeviceBuffer? _vb;
    DeviceBuffer? _ib;
    int _indexCount;
    Texture? _texture;
    Texture? _alphamapTexture;
    TextureView? _alphaMapView;

    Pipeline? _pipeline;
    Pipeline? _pipelineFrontCull;
    ResourceSet? _mainProjViewRS;
    ResourceSet? _mainSharedRS;
    ResourceSet? _mainPerObjectRS;
    ResourceSet? _reflectionRS;
    ResourceSet? _noReflectionRS;
    Pipeline? _shadowMapPipeline;
    ResourceSet[] _shadowMapResourceSets = [];

    DeviceBuffer? _worldAndInverseBuffer;

    readonly DisposeCollector _disposeCollector = new();

    readonly MaterialPropsAndBuffer _materialProps;
    readonly Vector3 _objectCenter;
    readonly bool _materialPropsOwned = false;

    public MaterialProperties MaterialProperties
    {
        get => _materialProps.Properties;
        set => _materialProps.Properties = value;
    }

    public Transform Transform => _transform;

    public TexturedMesh(
        string name,
        ConstructedMesh meshData,
        ImageSharpTexture? textureData,
        ImageSharpTexture? alphaTexture,
        MaterialPropsAndBuffer materialProps
    )
    {
        _name = name;
        _meshData = meshData;
        _centeredBounds = meshData.GetBoundingBox();
        _objectCenter = _centeredBounds.GetCenter();
        _textureData = textureData;
        _alphaTextureData = alphaTexture;
        _materialProps = materialProps;
    }

    public override BoundingBox BoundingBox =>
        BoundingBox.Transform(_centeredBounds, _transform.GetTransformMatrix());

    public override void CreateDeviceObjects(GraphicsDevice gd, CommandList cl, SceneContext sc)
    {
        if (s_useUniformOffset)
            _uniformOffset = gd.UniformBufferMinOffsetAlignment;

        ResourceFactory disposeFactory = new DisposeCollectorResourceFactory(
            gd.ResourceFactory,
            _disposeCollector
        );

        _vb = _meshData.CreateVertexBuffer(disposeFactory, cl);
        _vb.Name = _name + "_VB";
        _ib = _meshData.CreateIndexBuffer(disposeFactory, cl);
        _indexCount = _meshData.IndexCount;
        _ib.Name = _name + "_IB";

        uint bufferSize = 128;
        if (s_useUniformOffset)
            bufferSize += _uniformOffset * 2;

        _worldAndInverseBuffer = disposeFactory.CreateBuffer(
            new(bufferSize, BufferUsage.UniformBuffer | BufferUsage.DynamicWrite)
        );

        if (_materialPropsOwned)
            _materialProps.CreateDeviceObjects(gd, cl, sc);

        _texture =
            _textureData != null
                ? StaticResourceCache.GetTexture2D(gd, gd.ResourceFactory, _textureData)
                : StaticResourceCache.GetPinkTexture(gd, gd.ResourceFactory);

        _alphamapTexture =
            _alphaTextureData != null
                ? _alphaTextureData.CreateDeviceTexture(gd, disposeFactory)
                : StaticResourceCache.GetPinkTexture(gd, gd.ResourceFactory);

        _alphaMapView = StaticResourceCache.GetTextureView(gd.ResourceFactory, _alphamapTexture);

        VertexLayoutDescription[] shadowDepthVertexLayouts =
        [
            new(
                [
                    new(
                        "Position",
                        VertexElementSemantic.TextureCoordinate,
                        VertexElementFormat.Float3
                    ),
                    new(
                        "Normal",
                        VertexElementSemantic.TextureCoordinate,
                        VertexElementFormat.Float3
                    ),
                    new(
                        "TexCoord",
                        VertexElementSemantic.TextureCoordinate,
                        VertexElementFormat.Float2
                    ),
                ]
            ),
        ];

        (Shader depthVS, Shader depthFS) = StaticResourceCache.GetShaders(
            gd,
            gd.ResourceFactory,
            "ShadowDepth"
        );

        ResourceLayout projViewCombinedLayout = StaticResourceCache.GetResourceLayout(
            gd.ResourceFactory,
            new(
                new ResourceLayoutElementDescription(
                    "ViewProjection",
                    ResourceKind.UniformBuffer,
                    ShaderStages.Vertex
                )
            )
        );

        ResourceLayout worldLayout = StaticResourceCache.GetResourceLayout(
            gd.ResourceFactory,
            new(
                new ResourceLayoutElementDescription(
                    "WorldAndInverse",
                    ResourceKind.UniformBuffer,
                    ShaderStages.Vertex,
                    ResourceLayoutElementOptions.DynamicBinding
                )
            )
        );

        GraphicsPipelineDescription depthPD = new(
            BlendStateDescription.Empty,
            gd.IsDepthRangeZeroToOne
                ? DepthStencilStateDescription.DepthOnlyGreaterEqual
                : DepthStencilStateDescription.DepthOnlyLessEqual,
            RasterizerStateDescription.Default,
            PrimitiveTopology.TriangleList,
            new(shadowDepthVertexLayouts, [depthVS, depthFS], [new(100, gd.IsClipSpaceYInverted)]),
            [projViewCombinedLayout, worldLayout],
            sc.NearShadowMapFramebuffer.OutputDescription
        );
        _shadowMapPipeline = StaticResourceCache.GetPipeline(gd.ResourceFactory, ref depthPD);

        _shadowMapResourceSets = CreateShadowMapResourceSets(
            gd.ResourceFactory,
            disposeFactory,
            sc,
            projViewCombinedLayout,
            worldLayout
        );

        VertexLayoutDescription[] mainVertexLayouts =
        [
            new(
                new VertexElementDescription(
                    "Position",
                    VertexElementSemantic.TextureCoordinate,
                    VertexElementFormat.Float3
                ),
                new VertexElementDescription(
                    "Normal",
                    VertexElementSemantic.TextureCoordinate,
                    VertexElementFormat.Float3
                ),
                new VertexElementDescription(
                    "TexCoord",
                    VertexElementSemantic.TextureCoordinate,
                    VertexElementFormat.Float2
                )
            ),
        ];

        (Shader mainVS, Shader mainFS) = StaticResourceCache.GetShaders(
            gd,
            gd.ResourceFactory,
            "ShadowMain"
        );

        ResourceLayout projViewLayout = StaticResourceCache.GetResourceLayout(
            gd.ResourceFactory,
            StaticResourceCache.ProjViewLayoutDescription
        );

        ResourceLayout mainSharedLayout = StaticResourceCache.GetResourceLayout(
            gd.ResourceFactory,
            new(
                new ResourceLayoutElementDescription(
                    "LightViewProjection1",
                    ResourceKind.UniformBuffer,
                    ShaderStages.Vertex | ShaderStages.Fragment
                ),
                new ResourceLayoutElementDescription(
                    "LightViewProjection2",
                    ResourceKind.UniformBuffer,
                    ShaderStages.Vertex | ShaderStages.Fragment
                ),
                new ResourceLayoutElementDescription(
                    "LightViewProjection3",
                    ResourceKind.UniformBuffer,
                    ShaderStages.Vertex | ShaderStages.Fragment
                ),
                new ResourceLayoutElementDescription(
                    "DepthLimits",
                    ResourceKind.UniformBuffer,
                    ShaderStages.Vertex | ShaderStages.Fragment
                ),
                new ResourceLayoutElementDescription(
                    "LightInfo",
                    ResourceKind.UniformBuffer,
                    ShaderStages.Vertex | ShaderStages.Fragment
                ),
                new ResourceLayoutElementDescription(
                    "CameraInfo",
                    ResourceKind.UniformBuffer,
                    ShaderStages.Vertex | ShaderStages.Fragment
                ),
                new ResourceLayoutElementDescription(
                    "PointLights",
                    ResourceKind.UniformBuffer,
                    ShaderStages.Vertex | ShaderStages.Fragment
                )
            )
        );

        ResourceLayout mainPerObjectLayout = StaticResourceCache.GetResourceLayout(
            gd.ResourceFactory,
            new(
                new ResourceLayoutElementDescription(
                    "WorldAndInverse",
                    ResourceKind.UniformBuffer,
                    ShaderStages.Vertex | ShaderStages.Fragment,
                    ResourceLayoutElementOptions.DynamicBinding
                ),
                new ResourceLayoutElementDescription(
                    "MaterialProperties",
                    ResourceKind.UniformBuffer,
                    ShaderStages.Vertex | ShaderStages.Fragment
                ),
                new ResourceLayoutElementDescription(
                    "SurfaceTexture",
                    ResourceKind.TextureReadOnly,
                    ShaderStages.Fragment
                ),
                new ResourceLayoutElementDescription(
                    "RegularSampler",
                    ResourceKind.Sampler,
                    ShaderStages.Fragment
                ),
                new ResourceLayoutElementDescription(
                    "AlphaMap",
                    ResourceKind.TextureReadOnly,
                    ShaderStages.Fragment
                ),
                new ResourceLayoutElementDescription(
                    "AlphaMapSampler",
                    ResourceKind.Sampler,
                    ShaderStages.Fragment
                ),
                new ResourceLayoutElementDescription(
                    "ShadowMapNear",
                    ResourceKind.TextureReadOnly,
                    ShaderStages.Fragment
                ),
                new ResourceLayoutElementDescription(
                    "ShadowMapMid",
                    ResourceKind.TextureReadOnly,
                    ShaderStages.Fragment
                ),
                new ResourceLayoutElementDescription(
                    "ShadowMapFar",
                    ResourceKind.TextureReadOnly,
                    ShaderStages.Fragment
                ),
                new ResourceLayoutElementDescription(
                    "ShadowMapSampler",
                    ResourceKind.Sampler,
                    ShaderStages.Fragment
                )
            )
        );

        ResourceLayout reflectionLayout = StaticResourceCache.GetResourceLayout(
            gd.ResourceFactory,
            new(
                new ResourceLayoutElementDescription(
                    "ReflectionMap",
                    ResourceKind.TextureReadOnly,
                    ShaderStages.Fragment
                ),
                new ResourceLayoutElementDescription(
                    "ReflectionSampler",
                    ResourceKind.Sampler,
                    ShaderStages.Fragment
                ),
                new ResourceLayoutElementDescription(
                    "ReflectionViewProj",
                    ResourceKind.UniformBuffer,
                    ShaderStages.Vertex
                ),
                new ResourceLayoutElementDescription(
                    "ClipPlaneInfo",
                    ResourceKind.UniformBuffer,
                    ShaderStages.Fragment
                )
            )
        );

        BlendStateDescription alphaBlendDesc = BlendStateDescription.SingleAlphaBlend;
        alphaBlendDesc.AlphaToCoverageEnabled = true;

        GraphicsPipelineDescription mainPD = new(
            _alphamapTexture != null ? alphaBlendDesc : BlendStateDescription.SingleOverrideBlend,
            gd.IsDepthRangeZeroToOne
                ? DepthStencilStateDescription.DepthOnlyGreaterEqual
                : DepthStencilStateDescription.DepthOnlyLessEqual,
            RasterizerStateDescription.Default,
            PrimitiveTopology.TriangleList,
            new(mainVertexLayouts, [mainVS, mainFS], [new(100, gd.IsClipSpaceYInverted)]),
            [projViewLayout, mainSharedLayout, mainPerObjectLayout, reflectionLayout],
            sc.MainSceneFramebuffer.OutputDescription
        );
        _pipeline = StaticResourceCache.GetPipeline(gd.ResourceFactory, ref mainPD);
        _pipeline.Name = "TexturedMesh Main Pipeline";
        mainPD.RasterizerState.CullMode = FaceCullMode.Front;
        mainPD.Outputs = sc.ReflectionFramebuffer.OutputDescription;
        _pipelineFrontCull = StaticResourceCache.GetPipeline(gd.ResourceFactory, ref mainPD);

        _mainProjViewRS = StaticResourceCache.GetResourceSet(
            gd.ResourceFactory,
            new(projViewLayout, sc.ProjectionMatrixBuffer, sc.ViewMatrixBuffer)
        );

        _mainSharedRS = StaticResourceCache.GetResourceSet(
            gd.ResourceFactory,
            new(
                mainSharedLayout,
                sc.LightViewProjectionBuffer0,
                sc.LightViewProjectionBuffer1,
                sc.LightViewProjectionBuffer2,
                sc.DepthLimitsBuffer,
                sc.LightInfoBuffer,
                sc.CameraInfoBuffer,
                sc.PointLightsBuffer
            )
        );

        _mainPerObjectRS = disposeFactory.CreateResourceSet(
            new(
                mainPerObjectLayout,
                new DeviceBufferRange(_worldAndInverseBuffer, _uniformOffset, 128),
                _materialProps.UniformBuffer,
                _texture,
                gd.Aniso4xSampler,
                _alphaMapView,
                gd.LinearSampler,
                sc.NearShadowMapView,
                sc.MidShadowMapView,
                sc.FarShadowMapView,
                gd.PointSampler
            )
        );

        _reflectionRS = StaticResourceCache.GetResourceSet(
            gd.ResourceFactory,
            new(
                reflectionLayout,
                _alphaMapView, // Doesn't really matter -- just don't bind the actual reflection map since it's being rendered to.
                gd.PointSampler,
                sc.ReflectionViewProjBuffer,
                sc.MirrorClipPlaneBuffer
            )
        );

        _noReflectionRS = StaticResourceCache.GetResourceSet(
            gd.ResourceFactory,
            new(
                reflectionLayout,
                sc.ReflectionColorView,
                gd.PointSampler,
                sc.ReflectionViewProjBuffer,
                sc.NoClipPlaneBuffer
            )
        );
    }

    ResourceSet[] CreateShadowMapResourceSets(
        ResourceFactory sharedFactory,
        ResourceFactory disposeFactory,
        SceneContext sc,
        ResourceLayout projViewLayout,
        ResourceLayout worldLayout
    )
    {
        ResourceSet[] ret = new ResourceSet[6];

        for (int i = 0; i < 3; i++)
        {
            DeviceBuffer viewProjBuffer =
                i == 0 ? sc.LightViewProjectionBuffer0
                : i == 1 ? sc.LightViewProjectionBuffer1
                : sc.LightViewProjectionBuffer2;

            ret[i * 2] = StaticResourceCache.GetResourceSet(
                sharedFactory,
                new(projViewLayout, viewProjBuffer)
            );

            ResourceSet worldRS = disposeFactory.CreateResourceSet(
                new(
                    worldLayout,
                    new DeviceBufferRange(_worldAndInverseBuffer!, _uniformOffset, 128)
                )
            );

            ret[i * 2 + 1] = worldRS;
        }

        return ret;
    }

    public override void DestroyDeviceObjects()
    {
        if (_materialPropsOwned)
        {
            _materialProps.DestroyDeviceObjects();
        }

        _disposeCollector.DisposeAll();
    }

    public override RenderOrderKey GetRenderOrderKey(Vector3 cameraPosition)
    {
        return RenderOrderKey.Create(
            _pipeline!.GetHashCode(),
            Vector3.Distance(
                (_objectCenter * _transform.Scale) + _transform.Position,
                cameraPosition
            )
        );
    }

    public override RenderPasses RenderPasses
    {
        get
        {
            if (_alphaTextureData != null)
            {
                return RenderPasses.AllShadowMap
                    | RenderPasses.AlphaBlend
                    | RenderPasses.ReflectionMap;
            }
            else
            {
                return RenderPasses.AllShadowMap
                    | RenderPasses.Standard
                    | RenderPasses.ReflectionMap;
            }
        }
    }

    public override void Render(
        GraphicsDevice gd,
        CommandList cl,
        SceneContext sc,
        RenderPasses renderPass
    )
    {
        if (_materialPropsOwned)
        {
            _materialProps.FlushChanges(cl);
        }

        if ((renderPass & RenderPasses.AllShadowMap) != 0)
        {
            int shadowMapIndex =
                renderPass == RenderPasses.ShadowMapNear ? 0
                : renderPass == RenderPasses.ShadowMapMid ? 1
                : 2;
            RenderShadowMap(cl, shadowMapIndex);
        }
        else if (renderPass == RenderPasses.Standard || renderPass == RenderPasses.AlphaBlend)
        {
            RenderStandard(cl, false);
        }
        else if (renderPass == RenderPasses.ReflectionMap)
        {
            RenderStandard(cl, true);
        }
    }

    public override void UpdatePerFrameResources(GraphicsDevice gd, CommandList cl, SceneContext sc)
    {
        WorldAndInverse wai;
        wai.World = _transform.GetTransformMatrix();

        Matrix4x4.Invert(wai.World, out Matrix4x4 invertedWorld);
        wai.InverseWorld = Matrix4x4.Transpose(invertedWorld);
        gd.UpdateBuffer(_worldAndInverseBuffer!, _uniformOffset * 2, ref wai);
    }

    void RenderShadowMap(CommandList cl, int shadowMapIndex)
    {
        cl.SetVertexBuffer(0, _vb!);
        cl.SetIndexBuffer(_ib!, _meshData.IndexFormat);
        cl.SetPipeline(_shadowMapPipeline!);
        cl.SetGraphicsResourceSet(0, _shadowMapResourceSets[shadowMapIndex * 2]);
        ReadOnlySpan<uint> offsets = MemoryMarshal.CreateReadOnlySpan(ref _uniformOffset, 1);
        cl.SetGraphicsResourceSet(1, _shadowMapResourceSets[shadowMapIndex * 2 + 1], offsets);
        cl.DrawIndexed((uint)_indexCount, 1, 0, 0, 0);
    }

    void RenderStandard(CommandList cl, bool reflectionPass)
    {
        cl.SetVertexBuffer(0, _vb!);
        cl.SetIndexBuffer(_ib!, _meshData.IndexFormat);
        cl.SetPipeline(reflectionPass ? _pipelineFrontCull! : _pipeline!);
        cl.SetGraphicsResourceSet(0, _mainProjViewRS!);
        cl.SetGraphicsResourceSet(1, _mainSharedRS!);
        ReadOnlySpan<uint> offsets = MemoryMarshal.CreateReadOnlySpan(ref _uniformOffset, 1);
        cl.SetGraphicsResourceSet(2, _mainPerObjectRS!, offsets);
        cl.SetGraphicsResourceSet(3, reflectionPass ? _reflectionRS! : _noReflectionRS!);
        cl.DrawIndexed((uint)_indexCount, 1, 0, 0, 0);
    }
}

public struct WorldAndInverse
{
    public Matrix4x4 World;
    public Matrix4x4 InverseWorld;
}
