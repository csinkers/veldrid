﻿using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using static Veldrid.VirtualReality.Oculus.LibOvrNative;

namespace Veldrid.VirtualReality.Oculus;

internal unsafe class OculusContext : VRContext
{
    readonly ovrSession _session;
    readonly ovrGraphicsLuid _luid;
    readonly OculusMirrorTexture _mirrorTexture;
    readonly VRContextOptions _options;
    GraphicsDevice? _gd;
    ovrHmdDesc _hmdDesc;
    string _deviceName = "";
    ovrRecti[] _eyeRenderViewport = [];
    OculusSwapchain[] _eyeSwapchains = [];
    int _frameIndex;
    ovrTimewarpProjectionDesc _posTimewarpProjectionDesc;
    double _sensorSampleTime;
    EyePair_ovrPosef _eyeRenderPoses;
    readonly Quaternion[] _rotations = new Quaternion[2];
    readonly Vector3[] _positions = new Vector3[2];
    readonly Matrix4x4[] _projections = new Matrix4x4[2];

    static readonly Lazy<bool> s_isSupported = new(CheckSupport);

    static bool CheckSupport()
    {
        try
        {
            ovrInitParams initParams = new();
            initParams.Flags =
                ovrInitFlags.RequestVersion | ovrInitFlags.FocusAware | ovrInitFlags.Debug;
            initParams.RequestedMinorVersion = 30;

            ovrResult result = ovr_Initialize(&initParams);
            if (result != ovrResult.Success)
            {
                return false;
            }

            ovrSession session;
            ovrGraphicsLuid luid;
            result = ovr_Create(&session, &luid);
            if (result != ovrResult.Success)
            {
                return false;
            }

            ovr_Destroy(session);
            ovr_Shutdown();
            return true;
        }
        catch
        {
            return false;
        }
    }

    internal static bool IsSupported() => s_isSupported.Value;

    public override string DeviceName => _deviceName;

    public override Framebuffer LeftEyeFramebuffer => _eyeSwapchains[0].GetFramebuffer();

    public override Framebuffer RightEyeFramebuffer => _eyeSwapchains[1].GetFramebuffer();

    internal GraphicsDevice GraphicsDevice => _gd!;
    internal ovrSession Session => _session;

    public OculusContext(VRContextOptions options)
    {
        _options = options;

        ovrInitParams initParams = new();
        initParams.Flags =
            ovrInitFlags.RequestVersion | ovrInitFlags.FocusAware | ovrInitFlags.Debug;
        initParams.RequestedMinorVersion = 30;

        ovrResult result = ovr_Initialize(&initParams);
        if (result != ovrResult.Success)
            throw new VeldridException($"Failed to initialize Oculus: {result}");

        ovrSession session;
        ovrGraphicsLuid luid;
        result = ovr_Create(&session, &luid);
        if (result != ovrResult.Success)
            throw new VeldridException("Failed to create an Oculus session.");

        _session = session;
        _luid = luid;
        _mirrorTexture = new(this);
    }

    public override void Initialize(GraphicsDevice gd)
    {
        _gd = gd;
        if (gd.GetVulkanInfo(out BackendInfoVulkan? vkInfo))
        {
            IntPtr physicalDevice;
            ovrResult result = ovr_GetSessionPhysicalDeviceVk(
                _session,
                _luid,
                vkInfo.Instance,
                &physicalDevice
            );

            if (result != ovrResult.Success)
                throw new VeldridException("Failed to get Vulkan physical device.");

            result = ovr_SetSynchonizationQueueVk(_session, vkInfo.GraphicsQueue);
            if (result != ovrResult.Success)
            {
                throw new VeldridException(
                    "Failed to set the Oculus session's Vulkan synchronization queue."
                );
            }
        }

        _hmdDesc = ovr_GetHmdDesc(_session);
        _deviceName = _hmdDesc.ProductName.ToString();

        _eyeRenderViewport = new ovrRecti[2];
        _eyeSwapchains = new OculusSwapchain[2];
        for (int eye = 0; eye < 2; ++eye)
        {
            ovrSizei idealSize = ovr_GetFovTextureSize(
                _session,
                (ovrEyeType)eye,
                _hmdDesc.DefaultEyeFov[eye],
                1.0f
            );
            _eyeSwapchains[eye] = new(
                _gd,
                _session,
                idealSize.w,
                idealSize.h,
                Util.GetSampleCount(_options.EyeFramebufferSampleCount),
                createDepth: true
            );
            _eyeRenderViewport[eye].Pos.X = 0;
            _eyeRenderViewport[eye].Pos.Y = 0;
            _eyeRenderViewport[eye].Size = idealSize;
        }
    }

    public override void RenderMirrorTexture(
        CommandList cl,
        Framebuffer fb,
        MirrorTextureEyeSource source
    ) => _mirrorTexture.Render(cl, fb, source);

    public override void SubmitFrame()
    {
        if (_gd!.GetOpenGLInfo(out BackendInfoOpenGL? glInfo))
            glInfo.FlushAndFinish();

        for (int eye = 0; eye < 2; ++eye)
            _eyeSwapchains[eye].Commit();

        // Initialize our single full screen Fov layer.
        ovrLayerEyeFovDepth ld = new();
        ld.Header.Type = ovrLayerType.EyeFovDepth;
        ld.Header.Flags =
            _gd.BackendType == GraphicsBackend.OpenGL || _gd.BackendType == GraphicsBackend.OpenGLES
                ? ovrLayerFlags.TextureOriginAtBottomLeft
                : ovrLayerFlags.None;
        ld.ProjectionDesc = _posTimewarpProjectionDesc;
        ld.SensorSampleTime = _sensorSampleTime;

        for (int eye = 0; eye < 2; ++eye)
        {
            ld.ColorTexture[eye] = _eyeSwapchains[eye].ColorChain;
            ld.DepthTexture[eye] = _eyeSwapchains[eye].DepthChain;
            ld.Viewport[eye] = _eyeRenderViewport[eye];
            ld.Fov[eye] = _hmdDesc.DefaultEyeFov[eye];
            ld.RenderPose[eye] = _eyeRenderPoses[eye];
        }

        ovrLayerHeader* layers = &ld.Header;
        ovrResult result = ovr_SubmitFrame(_session, _frameIndex, null, &layers, 1);

        if (result != ovrResult.Success)
            throw new VeldridException($"Failed to submit Oculus frame: {result}");

        _frameIndex++;
    }

    public override HmdPoseState WaitForPoses()
    {
        ovrSessionStatus sessionStatus;
        ovrResult result = ovr_GetSessionStatus(_session, &sessionStatus);
        if (result != ovrResult.Success)
            throw new VeldridException($"Failed to retrieve Oculus session status: {result}");

        if (sessionStatus.ShouldRecenter)
            ovr_RecenterTrackingOrigin(_session);

        // Call ovr_GetRenderDesc each frame to get the ovrEyeRenderDesc, as the returned values (e.g. HmdToEyePose) may change at runtime.
        ovrEyeRenderDesc* eyeRenderDescs = stackalloc ovrEyeRenderDesc[2];
        eyeRenderDescs[0] = ovr_GetRenderDesc2(
            _session,
            ovrEyeType.Left,
            _hmdDesc.DefaultEyeFov[0]
        );
        eyeRenderDescs[1] = ovr_GetRenderDesc2(
            _session,
            ovrEyeType.Right,
            _hmdDesc.DefaultEyeFov[1]
        );

        // Get both eye poses simultaneously, with IPD offset already included.
        EyePair_ovrPosef hmdToEyePoses = new(
            eyeRenderDescs[0].HmdToEyePose,
            eyeRenderDescs[1].HmdToEyePose
        );

        double predictedTime = ovr_GetPredictedDisplayTime(_session, _frameIndex);

        ovrTrackingState trackingState = ovr_GetTrackingState(_session, predictedTime, true);

        double sensorSampleTime; // sensorSampleTime is fed into the layer later
        EyePair_Vector3 hmdToEyeOffset = new(
            hmdToEyePoses.Left.Position,
            hmdToEyePoses.Right.Position
        );
        ovr_GetEyePoses(
            _session,
            _frameIndex,
            true,
            &hmdToEyeOffset,
            out _eyeRenderPoses,
            &sensorSampleTime
        );
        _sensorSampleTime = sensorSampleTime;

        // Render Scene to Eye Buffers
        for (int eye = 0; eye < 2; ++eye)
        {
            _rotations[eye] = _eyeRenderPoses[eye].Orientation;
            _positions[eye] = _eyeRenderPoses[eye].Position;
            Matrix4x4 proj = ovrMatrix4f_Projection(
                eyeRenderDescs[eye].Fov,
                0.2f,
                1000f,
                ovrProjectionModifier.None
            );
            _posTimewarpProjectionDesc = ovrTimewarpProjectionDesc_FromProjection(
                proj,
                ovrProjectionModifier.None
            );
            _projections[eye] = Matrix4x4.Transpose(proj);
        }

        return new(
            _projections[0],
            _projections[1],
            _positions[0],
            _positions[1],
            _rotations[0],
            _rotations[1]
        );
    }

    public override void Dispose()
    {
        foreach (OculusSwapchain sc in _eyeSwapchains)
            sc.Dispose();

        _mirrorTexture.Dispose();
        ovr_Destroy(_session);
        ovr_Shutdown();
    }

    public override (string[] instance, string[] device) GetRequiredVulkanExtensions()
    {
        uint instanceExtCount;
        ovrResult result = ovr_GetInstanceExtensionsVk(_luid, null, &instanceExtCount);
        if (result != ovrResult.Success)
        {
            throw new VeldridException(
                $"Failed to retrieve the number of required Vulkan instance extensions: {result}"
            );
        }

        byte[] instanceExtensions = new byte[instanceExtCount];
        fixed (byte* instanceExtensionsPtr = &instanceExtensions[0])
        {
            result = ovr_GetInstanceExtensionsVk(_luid, instanceExtensionsPtr, &instanceExtCount);
            if (result != ovrResult.Success)
            {
                throw new VeldridException(
                    $"Failed to retrieve the required Vulkan instance extensions: {result}"
                );
            }
        }

        string[] instance = GetStringArray(instanceExtensions);

        uint deviceExtCount;
        result = ovr_GetDeviceExtensionsVk(_luid, null, &deviceExtCount);
        if (result != ovrResult.Success)
        {
            throw new VeldridException(
                $"Failed to retrieve the number of required Vulkan device extensions: {result}"
            );
        }

        byte[] deviceExtensions = new byte[deviceExtCount];
        fixed (byte* deviceExtensionsPtr = &deviceExtensions[0])
        {
            result = ovr_GetDeviceExtensionsVk(_luid, deviceExtensionsPtr, &deviceExtCount);
            if (result != ovrResult.Success)
            {
                throw new VeldridException(
                    $"Failed to retrieve the required Vulkan device extensions: {result}"
                );
            }
        }

        string[] device = GetStringArray(deviceExtensions);

        return (instance, device);
    }

    static string[] GetStringArray(byte[] utf8Data)
    {
        List<string> ret = new();
        int start = 0;
        for (int i = 0; i < utf8Data.Length; i++)
        {
            if ((char)utf8Data[i] == ' ' || utf8Data[i] == 0)
            {
                string s = Encoding.UTF8.GetString(utf8Data, start, i - start);
                ret.Add(s);
                i += 1;
                start = i;
            }
        }

        return ret.ToArray();
    }
}

internal unsafe class OculusSwapchain
{
    static readonly Guid s_d3d11Tex2DGuid = new("6f15aaf2-d208-4e89-9ab4-489535d34f9c");

    readonly ovrSession _session;
    public readonly ovrTextureSwapChain ColorChain;
    public readonly ovrTextureSwapChain DepthChain;
    public readonly Framebuffer[] Framebuffers;

    public OculusSwapchain(
        GraphicsDevice gd,
        ovrSession session,
        int sizeW,
        int sizeH,
        int sampleCount,
        bool createDepth
    )
    {
        _session = session;

        Texture[] colorTextures;
        Texture[]? depthTextures = null;

        ovrTextureSwapChainDesc colorDesc = new();
        colorDesc.Type = ovrTextureType.Texture2D;
        colorDesc.ArraySize = 1;
        colorDesc.Width = sizeW;
        colorDesc.Height = sizeH;
        colorDesc.MipLevels = 1;
        colorDesc.SampleCount = sampleCount;
        colorDesc.Format = ovrTextureFormat.R8G8B8A8_UNORM_SRGB;
        colorDesc.MiscFlags =
            ovrTextureMiscFlags.DX_Typeless | ovrTextureMiscFlags.AllowGenerateMips;
        colorDesc.BindFlags = ovrTextureBindFlags.DX_RenderTarget;
        colorDesc.StaticImage = false;

        (ColorChain, colorTextures) = CreateSwapchain(session, gd, colorDesc);

        // if requested, then create depth swap chain
        if (createDepth)
        {
            ovrTextureSwapChainDesc depthDesc = new();
            depthDesc.Type = ovrTextureType.Texture2D;
            depthDesc.ArraySize = 1;
            depthDesc.Width = sizeW;
            depthDesc.Height = sizeH;
            depthDesc.MipLevels = 1;
            depthDesc.SampleCount = sampleCount;
            depthDesc.Format = ovrTextureFormat.D32_FLOAT;
            depthDesc.MiscFlags = ovrTextureMiscFlags.None;
            depthDesc.BindFlags = ovrTextureBindFlags.DX_DepthStencil;
            depthDesc.StaticImage = false;

            (DepthChain, depthTextures) = CreateSwapchain(session, gd, depthDesc);
        }

        Framebuffers = new Framebuffer[colorTextures.Length];
        for (int i = 0; i < Framebuffers.Length; i++)
        {
            Framebuffers[i] = gd.ResourceFactory.CreateFramebuffer(
                new(depthTextures?[i], colorTextures[i])
            );
        }

        CommandList = gd.ResourceFactory.CreateCommandList();
    }

    (ovrTextureSwapChain, Texture[]) CreateSwapchain(
        ovrSession session,
        GraphicsDevice gd,
        ovrTextureSwapChainDesc desc
    )
    {
        return gd.BackendType switch
        {
            GraphicsBackend.Direct3D11 => CreateSwapchainD3D11(session, gd, desc),
            GraphicsBackend.OpenGL or GraphicsBackend.OpenGLES => CreateSwapchainGL(
                session,
                gd,
                desc
            ),
            GraphicsBackend.Vulkan => CreateSwapchainVk(session, gd, desc),
            GraphicsBackend.Metal => throw new PlatformNotSupportedException(
                "Using Oculus with the Metal backend is not supported."
            ),
            _ => throw new NotImplementedException(),
        };
    }

    (ovrTextureSwapChain, Texture[]) CreateSwapchainVk(
        ovrSession session,
        GraphicsDevice gd,
        ovrTextureSwapChainDesc desc
    )
    {
        ovrTextureSwapChain otsc;
        Texture[] textures;

        ovrResult result = ovr_CreateTextureSwapChainVk(
            session,
            gd.GetVulkanInfo().Device,
            &desc,
            &otsc
        );
        if (result != ovrResult.Success)
        {
            throw new VeldridException($"Failed to call ovr_CreateTextureSwapChainVk: {result}");
        }

        int textureCount = 0;
        ovr_GetTextureSwapChainLength(session, otsc, &textureCount);
        textures = new Texture[textureCount];
        for (int i = 0; i < textureCount; ++i)
        {
            ulong nativeTexture;
            ovr_GetTextureSwapChainBufferVk(session, otsc, i, &nativeTexture);
            textures[i] = gd.ResourceFactory.CreateTexture(
                nativeTexture,
                OculusUtil.GetVeldridTextureDescription(desc)
            );
        }

        return (otsc, textures);
    }

    static (ovrTextureSwapChain, Texture[]) CreateSwapchainD3D11(
        ovrSession session,
        GraphicsDevice gd,
        ovrTextureSwapChainDesc desc
    )
    {
        ovrTextureSwapChain otsc;
        Texture[] textures;

        ovrResult result = ovr_CreateTextureSwapChainDX(
            session,
            gd.GetD3D11Info().Device,
            &desc,
            &otsc
        );
        if (result != ovrResult.Success)
        {
            throw new VeldridException($"Failed to call ovr_CreateTextureSwapChainDX: {result}");
        }

        int textureCount = 0;
        ovr_GetTextureSwapChainLength(session, otsc, &textureCount);
        textures = new Texture[textureCount];
        for (int i = 0; i < textureCount; ++i)
        {
            IntPtr nativeTexture;
            ovr_GetTextureSwapChainBufferDX(session, otsc, i, s_d3d11Tex2DGuid, &nativeTexture);
            textures[i] = gd.ResourceFactory.CreateTexture(
                (ulong)nativeTexture,
                OculusUtil.GetVeldridTextureDescription(desc)
            );
        }

        return (otsc, textures);
    }

    static (ovrTextureSwapChain, Texture[]) CreateSwapchainGL(
        ovrSession session,
        GraphicsDevice gd,
        ovrTextureSwapChainDesc desc
    )
    {
        ovrTextureSwapChain otsc = default;

        ovrResult result = ovrResult.Success;
        gd.GetOpenGLInfo()
            .ExecuteOnGLThread(() =>
            {
                ovrTextureSwapChainDesc localDesc = desc;
                localDesc.MiscFlags &= ~(
                    ovrTextureMiscFlags.DX_Typeless | ovrTextureMiscFlags.AllowGenerateMips
                );
                localDesc.BindFlags = ovrTextureBindFlags.None;

                ovrTextureSwapChain sc;
                result = ovr_CreateTextureSwapChainGL(session, &localDesc, &sc);

                if (result != ovrResult.Success)
                {
                    return;
                }
                otsc = sc;
            });

        if (otsc.IsNull)
            throw new VeldridException($"Failed to call ovr_CreateTextureSwapChainGL: {result}");

        int textureCount = 0;
        ovr_GetTextureSwapChainLength(session, otsc, &textureCount);

        Texture[] textures = new Texture[textureCount];
        for (int i = 0; i < textureCount; ++i)
        {
            uint glID;
            ovr_GetTextureSwapChainBufferGL(session, otsc, i, &glID);
            textures[i] = gd.ResourceFactory.CreateTexture(
                glID,
                OculusUtil.GetVeldridTextureDescription(desc)
            );
        }

        return (otsc, textures);
    }

    public void Dispose()
    {
        foreach (Framebuffer fb in Framebuffers)
            fb.Dispose();

        if (ColorChain.NativePtr != IntPtr.Zero)
            ovr_DestroyTextureSwapChain(_session, ColorChain);

        if (DepthChain.NativePtr != IntPtr.Zero)
            ovr_DestroyTextureSwapChain(_session, DepthChain);
    }

    public Framebuffer GetFramebuffer()
    {
        int index = 0;
        ovr_GetTextureSwapChainCurrentIndex(_session, ColorChain, &index);
        return Framebuffers[index];
    }

    public OutputDescription GetOutputDescription() => Framebuffers[0].OutputDescription;

    public CommandList CommandList { get; private set; }

    public void Commit()
    {
        ovrResult result = ovr_CommitTextureSwapChain(_session, ColorChain);
        if (result != ovrResult.Success)
            throw new InvalidOperationException();

        result = ovr_CommitTextureSwapChain(_session, DepthChain);
        if (result != ovrResult.Success)
            throw new InvalidOperationException();
    }
}
