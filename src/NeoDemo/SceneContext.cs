﻿using System.Runtime.CompilerServices;
using Veldrid.NeoDemo.Objects;
using Veldrid.SPIRV;

namespace Veldrid.NeoDemo;

public class SceneContext
{
    public DeviceBuffer ProjectionMatrixBuffer { get; private set; } = null!;
    public DeviceBuffer ViewMatrixBuffer { get; private set; } = null!;
    public DeviceBuffer LightInfoBuffer { get; private set; } = null!;
    public DeviceBuffer LightViewProjectionBuffer0 { get; internal set; } = null!;
    public DeviceBuffer LightViewProjectionBuffer1 { get; internal set; } = null!;
    public DeviceBuffer LightViewProjectionBuffer2 { get; internal set; } = null!;
    public DeviceBuffer DepthLimitsBuffer { get; internal set; } = null!;
    public DeviceBuffer CameraInfoBuffer { get; private set; } = null!;
    public DeviceBuffer PointLightsBuffer { get; private set; } = null!;

    public CascadedShadowMaps ShadowMaps { get; private set; } = new();
    public TextureView NearShadowMapView => ShadowMaps.NearShadowMapView!;
    public TextureView MidShadowMapView => ShadowMaps.MidShadowMapView!;
    public TextureView FarShadowMapView => ShadowMaps.FarShadowMapView!;
    public Framebuffer NearShadowMapFramebuffer => ShadowMaps.NearShadowMapFramebuffer!;
    public Framebuffer MidShadowMapFramebuffer => ShadowMaps.MidShadowMapFramebuffer!;
    public Framebuffer FarShadowMapFramebuffer => ShadowMaps.FarShadowMapFramebuffer!;
    public Texture ShadowMapTexture => ShadowMaps.NearShadowMap!; // Only used for size.

    public Texture ReflectionColorTexture { get; private set; } = null!;
    public Texture ReflectionDepthTexture { get; private set; } = null!;
    public TextureView ReflectionColorView { get; private set; } = null!;
    public Framebuffer ReflectionFramebuffer { get; private set; } = null!;
    public DeviceBuffer ReflectionViewProjBuffer { get; private set; } = null!;

    // MainSceneView and Duplicator resource sets both use this.
    public ResourceLayout TextureSamplerResourceLayout { get; private set; } = null!;

    public Texture MainSceneColorTexture { get; private set; } = null!;
    public Texture MainSceneDepthTexture { get; private set; } = null!;
    public Framebuffer MainSceneFramebuffer { get; private set; } = null!;
    public Texture MainSceneResolvedColorTexture { get; private set; } = null!;
    public TextureView MainSceneResolvedColorView { get; private set; } = null!;
    public ResourceSet MainSceneViewResourceSet { get; private set; } = null!;

    public Texture DuplicatorTarget0 { get; private set; } = null!;
    public TextureView DuplicatorTargetView0 { get; private set; } = null!;
    public ResourceSet DuplicatorTargetSet0 { get; internal set; } = null!;
    public Texture DuplicatorTarget1 { get; private set; } = null!;
    public TextureView DuplicatorTargetView1 { get; private set; } = null!;
    public ResourceSet DuplicatorTargetSet1 { get; internal set; } = null!;
    public Framebuffer DuplicatorFramebuffer { get; private set; } = null!;

    public Camera? Camera { get; set; }
    public DirectionalLight DirectionalLight { get; } = new();
    public TextureSampleCount MainSceneSampleCount { get; internal set; }
    public ColorWriteMask MainSceneMask { get; internal set; } = ColorWriteMask.All;
    public DeviceBuffer MirrorClipPlaneBuffer { get; private set; } = null!;
    public DeviceBuffer NoClipPlaneBuffer { get; private set; } = null!;

    public virtual void CreateDeviceObjects(GraphicsDevice gd, CommandList cl, SceneContext sc)
    {
        ResourceFactory factory = gd.ResourceFactory;
        ProjectionMatrixBuffer = factory.CreateBuffer(
            new(64, BufferUsage.UniformBuffer | BufferUsage.DynamicWrite)
        );
        ViewMatrixBuffer = factory.CreateBuffer(
            new(64, BufferUsage.UniformBuffer | BufferUsage.DynamicWrite)
        );
        LightViewProjectionBuffer0 = factory.CreateBuffer(
            new(64, BufferUsage.UniformBuffer | BufferUsage.DynamicWrite)
        );
        LightViewProjectionBuffer0.Name = "LightViewProjectionBuffer0";
        LightViewProjectionBuffer1 = factory.CreateBuffer(
            new(64, BufferUsage.UniformBuffer | BufferUsage.DynamicWrite)
        );
        LightViewProjectionBuffer1.Name = "LightViewProjectionBuffer1";
        LightViewProjectionBuffer2 = factory.CreateBuffer(
            new(64, BufferUsage.UniformBuffer | BufferUsage.DynamicWrite)
        );
        LightViewProjectionBuffer2.Name = "LightViewProjectionBuffer2";
        DepthLimitsBuffer = factory.CreateBuffer(
            new(
                (uint)Unsafe.SizeOf<DepthCascadeLimits>(),
                BufferUsage.UniformBuffer | BufferUsage.DynamicWrite
            )
        );
        LightInfoBuffer = factory.CreateBuffer(
            new(
                (uint)Unsafe.SizeOf<DirectionalLightInfo>(),
                BufferUsage.UniformBuffer | BufferUsage.DynamicWrite
            )
        );
        CameraInfoBuffer = factory.CreateBuffer(
            new(
                (uint)Unsafe.SizeOf<CameraInfo>(),
                BufferUsage.UniformBuffer | BufferUsage.DynamicWrite
            )
        );

        if (Camera != null)
            UpdateCameraBuffers(cl);

        PointLightsBuffer = factory.CreateBuffer(
            new((uint)Unsafe.SizeOf<PointLightsInfo.Blittable>(), BufferUsage.UniformBuffer)
        );

        PointLightsInfo pli = new();
        pli.NumActiveLights = 4;
        pli.PointLights =
        [
            new()
            {
                Color = new(.6f, .6f, .6f),
                Position = new(-50, 5, 0),
                Range = 75f,
            },
            new()
            {
                Color = new(.6f, .35f, .4f),
                Position = new(0, 5, 0),
                Range = 100f,
            },
            new()
            {
                Color = new(.6f, .6f, 0.35f),
                Position = new(50, 5, 0),
                Range = 40f,
            },
            new()
            {
                Color = new(0.4f, 0.4f, .6f),
                Position = new(25, 5, 45),
                Range = 150f,
            },
        ];

        cl.UpdateBuffer(PointLightsBuffer, 0, pli.GetBlittable());

        TextureSamplerResourceLayout = factory.CreateResourceLayout(
            new(
                new ResourceLayoutElementDescription(
                    "SourceTexture",
                    ResourceKind.TextureReadOnly,
                    ShaderStages.Fragment
                ),
                new ResourceLayoutElementDescription(
                    "SourceSampler",
                    ResourceKind.Sampler,
                    ShaderStages.Fragment
                )
            )
        );

        const uint reflectionMapSize = 2048;
        ReflectionColorTexture = factory.CreateTexture(
            TextureDescription.Texture2D(
                reflectionMapSize,
                reflectionMapSize,
                12,
                1,
                PixelFormat.R16_G16_B16_A16_Float,
                TextureUsage.RenderTarget | TextureUsage.Sampled | TextureUsage.GenerateMipmaps
            )
        );
        ReflectionDepthTexture = factory.CreateTexture(
            TextureDescription.Texture2D(
                reflectionMapSize,
                reflectionMapSize,
                1,
                1,
                PixelFormat.R32_Float,
                TextureUsage.DepthStencil
            )
        );
        ReflectionColorView = factory.CreateTextureView(ReflectionColorTexture);
        ReflectionFramebuffer = factory.CreateFramebuffer(
            new(ReflectionDepthTexture, ReflectionColorTexture)
        );
        ReflectionViewProjBuffer = factory.CreateBuffer(
            new(64, BufferUsage.UniformBuffer | BufferUsage.DynamicWrite)
        );

        MirrorClipPlaneBuffer = factory.CreateBuffer(new(32, BufferUsage.UniformBuffer));
        gd.UpdateBuffer(MirrorClipPlaneBuffer, 0, new ClipPlaneInfo(MirrorMesh.Plane, true));
        NoClipPlaneBuffer = factory.CreateBuffer(new(32, BufferUsage.UniformBuffer));
        gd.UpdateBuffer(NoClipPlaneBuffer, 0, new ClipPlaneInfo());

        RecreateWindowSizedResources(gd, cl);

        ShadowMaps.CreateDeviceResources(gd);
    }

    public virtual void DestroyDeviceObjects()
    {
        ProjectionMatrixBuffer.Dispose();
        ViewMatrixBuffer.Dispose();
        LightInfoBuffer.Dispose();
        LightViewProjectionBuffer0.Dispose();
        LightViewProjectionBuffer1.Dispose();
        LightViewProjectionBuffer2.Dispose();
        DepthLimitsBuffer.Dispose();
        CameraInfoBuffer.Dispose();
        PointLightsBuffer.Dispose();
        MainSceneColorTexture.Dispose();
        MainSceneResolvedColorTexture.Dispose();
        MainSceneResolvedColorView.Dispose();
        MainSceneDepthTexture.Dispose();
        MainSceneFramebuffer.Dispose();
        MainSceneViewResourceSet.Dispose();
        DuplicatorTarget0.Dispose();
        DuplicatorTarget1.Dispose();
        DuplicatorTargetView0.Dispose();
        DuplicatorTargetView1.Dispose();
        DuplicatorTargetSet0.Dispose();
        DuplicatorTargetSet1.Dispose();
        DuplicatorFramebuffer.Dispose();
        TextureSamplerResourceLayout.Dispose();
        ReflectionColorTexture.Dispose();
        ReflectionDepthTexture.Dispose();
        ReflectionColorView.Dispose();
        ReflectionFramebuffer.Dispose();
        ReflectionViewProjBuffer.Dispose();
        MirrorClipPlaneBuffer.Dispose();
        NoClipPlaneBuffer.Dispose();
        ShadowMaps.DestroyDeviceObjects();
    }

    public void SetCurrentScene(Scene scene)
    {
        Camera = scene.Camera;
    }

    public void UpdateCameraBuffers(CommandList cl)
    {
        if (Camera == null)
            return;

        cl.UpdateBuffer(ProjectionMatrixBuffer, 0, Camera.ProjectionMatrix);
        cl.UpdateBuffer(ViewMatrixBuffer, 0, Camera.ViewMatrix);
        cl.UpdateBuffer(CameraInfoBuffer, 0, Camera.GetCameraInfo());
    }

    internal void RecreateWindowSizedResources(GraphicsDevice gd, CommandList cl)
    {
        MainSceneColorTexture?.Dispose();
        MainSceneDepthTexture?.Dispose();
        MainSceneResolvedColorTexture?.Dispose();
        MainSceneResolvedColorView?.Dispose();
        MainSceneViewResourceSet?.Dispose();
        MainSceneFramebuffer?.Dispose();
        DuplicatorTarget0?.Dispose();
        DuplicatorTarget1?.Dispose();
        DuplicatorTargetView0?.Dispose();
        DuplicatorTargetView1?.Dispose();
        DuplicatorTargetSet0?.Dispose();
        DuplicatorTargetSet1?.Dispose();
        DuplicatorFramebuffer?.Dispose();

        ResourceFactory factory = gd.ResourceFactory;

        gd.GetPixelFormatSupport(
            PixelFormat.R16_G16_B16_A16_Float,
            TextureType.Texture2D,
            TextureUsage.RenderTarget,
            out PixelFormatProperties properties
        );

        TextureSampleCount sampleCount = MainSceneSampleCount;
        while (!properties.IsSampleCountSupported(sampleCount))
        {
            sampleCount--;
        }

        TextureDescription mainColorDesc = TextureDescription.Texture2D(
            gd.SwapchainFramebuffer!.Width,
            gd.SwapchainFramebuffer.Height,
            1,
            1,
            PixelFormat.R16_G16_B16_A16_Float,
            TextureUsage.RenderTarget | TextureUsage.Sampled,
            sampleCount
        );

        MainSceneColorTexture = factory.CreateTexture(mainColorDesc);
        if (sampleCount != TextureSampleCount.Count1)
        {
            mainColorDesc.SampleCount = TextureSampleCount.Count1;
            MainSceneResolvedColorTexture = factory.CreateTexture(mainColorDesc);
        }
        else
        {
            MainSceneResolvedColorTexture = MainSceneColorTexture;
        }
        MainSceneResolvedColorView = factory.CreateTextureView(MainSceneResolvedColorTexture);
        MainSceneDepthTexture = factory.CreateTexture(
            TextureDescription.Texture2D(
                gd.SwapchainFramebuffer.Width,
                gd.SwapchainFramebuffer.Height,
                1,
                1,
                PixelFormat.R32_Float,
                TextureUsage.DepthStencil,
                sampleCount
            )
        );
        MainSceneFramebuffer = factory.CreateFramebuffer(
            new(MainSceneDepthTexture, MainSceneColorTexture)
        );
        MainSceneViewResourceSet = factory.CreateResourceSet(
            new(TextureSamplerResourceLayout, MainSceneResolvedColorView, gd.PointSampler)
        );

        TextureDescription colorTargetDesc = TextureDescription.Texture2D(
            gd.SwapchainFramebuffer.Width,
            gd.SwapchainFramebuffer.Height,
            1,
            1,
            PixelFormat.R16_G16_B16_A16_Float,
            TextureUsage.RenderTarget | TextureUsage.Sampled
        );
        DuplicatorTarget0 = factory.CreateTexture(colorTargetDesc);
        DuplicatorTargetView0 = factory.CreateTextureView(DuplicatorTarget0);
        DuplicatorTarget1 = factory.CreateTexture(colorTargetDesc);
        DuplicatorTargetView1 = factory.CreateTextureView(DuplicatorTarget1);
        DuplicatorTargetSet0 = factory.CreateResourceSet(
            new(TextureSamplerResourceLayout, DuplicatorTargetView0, gd.PointSampler)
        );
        DuplicatorTargetSet1 = factory.CreateResourceSet(
            new(TextureSamplerResourceLayout, DuplicatorTargetView1, gd.PointSampler)
        );

        FramebufferDescription fbDesc = new(null, DuplicatorTarget0, DuplicatorTarget1);
        DuplicatorFramebuffer = factory.CreateFramebuffer(fbDesc);
    }
}

public class CascadedShadowMaps
{
    public Texture? NearShadowMap { get; private set; }
    public TextureView? NearShadowMapView { get; private set; }
    public Framebuffer? NearShadowMapFramebuffer { get; private set; }

    public Texture? MidShadowMap { get; private set; }
    public TextureView? MidShadowMapView { get; private set; }
    public Framebuffer? MidShadowMapFramebuffer { get; private set; }

    public Texture? FarShadowMap { get; private set; }
    public TextureView? FarShadowMapView { get; private set; }
    public Framebuffer? FarShadowMapFramebuffer { get; private set; }

    public void CreateDeviceResources(GraphicsDevice gd)
    {
        ResourceFactory factory = gd.ResourceFactory;
        TextureDescription desc = TextureDescription.Texture2D(
            2048,
            2048,
            1,
            1,
            PixelFormat.D32_Float_S8_UInt,
            TextureUsage.DepthStencil | TextureUsage.Sampled
        );
        NearShadowMap = factory.CreateTexture(desc);
        NearShadowMap.Name = "Near Shadow Map";
        NearShadowMapView = factory.CreateTextureView(NearShadowMap);
        NearShadowMapFramebuffer = factory.CreateFramebuffer(
            new(new FramebufferAttachmentDescription(NearShadowMap, 0), [])
        );

        MidShadowMap = factory.CreateTexture(desc);
        MidShadowMapView = factory.CreateTextureView(
            new TextureViewDescription(MidShadowMap, 0, 1, 0, 1)
        );
        MidShadowMapFramebuffer = factory.CreateFramebuffer(
            new(new FramebufferAttachmentDescription(MidShadowMap, 0), [])
        );

        FarShadowMap = factory.CreateTexture(desc);
        FarShadowMapView = factory.CreateTextureView(
            new TextureViewDescription(FarShadowMap, 0, 1, 0, 1)
        );
        FarShadowMapFramebuffer = factory.CreateFramebuffer(
            new(new FramebufferAttachmentDescription(FarShadowMap, 0), [])
        );
    }

    public void DestroyDeviceObjects()
    {
        NearShadowMap?.Dispose();
        NearShadowMapView?.Dispose();
        NearShadowMapFramebuffer?.Dispose();

        MidShadowMap?.Dispose();
        MidShadowMapView?.Dispose();
        MidShadowMapFramebuffer?.Dispose();

        FarShadowMap?.Dispose();
        FarShadowMapView?.Dispose();
        FarShadowMapFramebuffer?.Dispose();
    }
}
