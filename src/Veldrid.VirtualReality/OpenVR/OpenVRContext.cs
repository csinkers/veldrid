﻿using System;
using System.Numerics;
using System.Text;
using TerraFX.Interop.Vulkan;
using Valve.VR;
using OVR = Valve.VR.OpenVR;

namespace Veldrid.VirtualReality.OpenVR;

internal class OpenVRContext : VRContext
{
    readonly CVRSystem _vrSystem;
    readonly CVRCompositor _compositor;
    readonly OpenVRMirrorTexture _mirrorTexture;
    readonly VRContextOptions _options;
    GraphicsDevice? _gd;
    string _deviceName = "";
    Framebuffer? _leftEyeFB;
    Framebuffer? _rightEyeFB;
    Matrix4x4 _projLeft;
    Matrix4x4 _projRight;
    Matrix4x4 _headToEyeLeft;
    Matrix4x4 _headToEyeRight;
    readonly TrackedDevicePose_t[] _devicePoses = new TrackedDevicePose_t[1];

    public override string DeviceName => _deviceName;
    public override Framebuffer LeftEyeFramebuffer => _leftEyeFB!;
    public override Framebuffer RightEyeFramebuffer => _rightEyeFB!;

    internal GraphicsDevice GraphicsDevice => _gd!;

    public OpenVRContext(VRContextOptions options)
    {
        _options = options;
        EVRInitError initError = EVRInitError.None;
        CVRSystem? vrSystem = OVR.Init(ref initError);
        if (initError != EVRInitError.None || vrSystem == null)
        {
            throw new VeldridException(
                $"Failed to initialize OpenVR: {OVR.GetStringForHmdError(initError)}"
            );
        }

        _vrSystem = vrSystem;
        _compositor =
            OVR.Compositor ?? throw new VeldridException("Failed to access the OpenVR Compositor.");

        _mirrorTexture = new(this);
    }

    internal static bool IsSupported()
    {
        try
        {
            return OVR.IsHmdPresent();
        }
        catch
        {
            return false;
        }
    }

    public override void Initialize(GraphicsDevice gd)
    {
        _gd = gd;

        StringBuilder sb = new(512);
        ETrackedPropertyError error = ETrackedPropertyError.TrackedProp_Success;
        uint ret = _vrSystem.GetStringTrackedDeviceProperty(
            OVR.k_unTrackedDeviceIndex_Hmd,
            ETrackedDeviceProperty.Prop_TrackingSystemName_String,
            sb,
            512u,
            ref error
        );
        if (error != ETrackedPropertyError.TrackedProp_Success)
        {
            _deviceName = "<Unknown OpenVR Device>";
        }
        else
        {
            _deviceName = sb.ToString();
        }

        uint eyeWidth = 0;
        uint eyeHeight = 0;
        _vrSystem.GetRecommendedRenderTargetSize(ref eyeWidth, ref eyeHeight);

        _leftEyeFB = CreateFramebuffer(eyeWidth, eyeHeight);
        _rightEyeFB = CreateFramebuffer(eyeWidth, eyeHeight);

        Matrix4x4 eyeToHeadLeft = ToSysMatrix(_vrSystem.GetEyeToHeadTransform(EVREye.Eye_Left));
        Matrix4x4.Invert(eyeToHeadLeft, out _headToEyeLeft);

        Matrix4x4 eyeToHeadRight = ToSysMatrix(_vrSystem.GetEyeToHeadTransform(EVREye.Eye_Right));
        Matrix4x4.Invert(eyeToHeadRight, out _headToEyeRight);

        _projLeft = ToSysMatrix(_vrSystem.GetProjectionMatrix(EVREye.Eye_Left, 0.1f, 1000f));
        _projRight = ToSysMatrix(_vrSystem.GetProjectionMatrix(EVREye.Eye_Right, 0.1f, 1000f));
    }

    public override (string[] instance, string[] device) GetRequiredVulkanExtensions()
    {
        StringBuilder sb = new(1024);
        uint ret = _compositor.GetVulkanInstanceExtensionsRequired(sb, 1024);
        string[] instance = sb.ToString().Split(' ');
        sb.Clear();
        ret = _compositor.GetVulkanDeviceExtensionsRequired(IntPtr.Zero, sb, 1024);
        string[] device = sb.ToString().Split(' ');
        return (instance, device);
    }

    public override HmdPoseState WaitForPoses()
    {
        EVRCompositorError compositorError = _compositor.WaitGetPoses(_devicePoses, []);

        TrackedDevicePose_t hmdPose = _devicePoses[OVR.k_unTrackedDeviceIndex_Hmd];
        Matrix4x4 deviceToAbsolute = ToSysMatrix(hmdPose.mDeviceToAbsoluteTracking);
        Matrix4x4.Invert(deviceToAbsolute, out Matrix4x4 absoluteToDevice);

        Matrix4x4 viewLeft = absoluteToDevice * _headToEyeLeft;
        Matrix4x4 viewRight = absoluteToDevice * _headToEyeRight;

        Matrix4x4.Invert(viewLeft, out Matrix4x4 invViewLeft);
        Matrix4x4.Decompose(
            invViewLeft,
            out _,
            out Quaternion leftRotation,
            out Vector3 leftPosition
        );

        Matrix4x4.Invert(viewRight, out Matrix4x4 invViewRight);
        Matrix4x4.Decompose(
            invViewRight,
            out _,
            out Quaternion rightRotation,
            out Vector3 rightPosition
        );

        return new(_projLeft, _projRight, leftPosition, rightPosition, leftRotation, rightRotation);
    }

    public override void SubmitFrame()
    {
        if (_gd!.GetOpenGLInfo(out BackendInfoOpenGL? glInfo))
            glInfo.FlushAndFinish();

        SubmitTexture(_compositor, LeftEyeFramebuffer.ColorTargets[0].Target, EVREye.Eye_Left);
        SubmitTexture(_compositor, RightEyeFramebuffer.ColorTargets[0].Target, EVREye.Eye_Right);
    }

    public override void RenderMirrorTexture(
        CommandList cl,
        Framebuffer fb,
        MirrorTextureEyeSource source
    )
    {
        _mirrorTexture.Render(cl, fb, source);
    }

    void SubmitTexture(CVRCompositor compositor, Texture colorTex, EVREye eye)
    {
        Texture_t texT;

        if (_gd!.GetD3D11Info(out BackendInfoD3D11? d3dInfo))
        {
            texT.eColorSpace = EColorSpace.Gamma;
            texT.eType = ETextureType.DirectX;
            texT.handle = d3dInfo.GetTexturePointer(colorTex);
        }
        else if (_gd.GetOpenGLInfo(out BackendInfoOpenGL? openglInfo))
        {
            texT.eColorSpace = EColorSpace.Gamma;
            texT.eType = ETextureType.OpenGL;
            texT.handle = (IntPtr)openglInfo.GetTextureName(colorTex);
        }
        else if (_gd.GetVulkanInfo(out BackendInfoVulkan? vkInfo))
        {
            vkInfo.TransitionImageLayout(
                colorTex,
                (uint)VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL
            );

            VRVulkanTextureData_t vkTexData;
            vkTexData.m_nImage = vkInfo.GetVkImage(colorTex);
            vkTexData.m_pDevice = vkInfo.Device;
            vkTexData.m_pPhysicalDevice = vkInfo.PhysicalDevice;
            vkTexData.m_pInstance = vkInfo.Instance;
            vkTexData.m_pQueue = vkInfo.GraphicsQueue;
            vkTexData.m_nQueueFamilyIndex = vkInfo.GraphicsQueueFamilyIndex;
            vkTexData.m_nWidth = colorTex.Width;
            vkTexData.m_nHeight = colorTex.Height;
            vkTexData.m_nFormat = (uint)vkInfo.GetVkFormat(colorTex);
            vkTexData.m_nSampleCount = GetSampleCount(colorTex.SampleCount);

            texT.eColorSpace = EColorSpace.Gamma;
            texT.eType = ETextureType.Vulkan;
            unsafe
            {
                texT.handle = (IntPtr)(&vkTexData);
            }
        }
        else
        {
            throw new NotSupportedException();
        }

        VRTextureBounds_t boundsT;
        boundsT.uMin = 0;
        boundsT.uMax = 1;
        boundsT.vMin = 0;
        boundsT.vMax = 1;

        EVRCompositorError compositorError = EVRCompositorError.None;
        if (_gd.GetOpenGLInfo(out BackendInfoOpenGL? glInfo))
        {
            glInfo.ExecuteOnGLThread(() =>
            {
                compositorError = compositor.Submit(
                    eye,
                    ref texT,
                    ref boundsT,
                    EVRSubmitFlags.Submit_Default
                );
            });
        }
        else
        {
            compositorError = compositor.Submit(
                eye,
                ref texT,
                ref boundsT,
                EVRSubmitFlags.Submit_Default
            );
        }

        if (compositorError != EVRCompositorError.None)
        {
            throw new VeldridException(
                $"Failed to submit to the OpenVR Compositor: {compositorError}"
            );
        }
    }

    public override void Dispose()
    {
        _mirrorTexture.Dispose();

        if (_leftEyeFB != null)
        {
            _leftEyeFB.ColorTargets[0].Target.Dispose();
            _leftEyeFB.DepthTarget?.Target.Dispose();
            _leftEyeFB.Dispose();
        }

        if (_rightEyeFB != null)
        {
            _rightEyeFB.ColorTargets[0].Target.Dispose();
            _rightEyeFB.DepthTarget?.Target.Dispose();
            _rightEyeFB.Dispose();
        }

        OVR.Shutdown();
    }

    Framebuffer CreateFramebuffer(uint width, uint height)
    {
        ResourceFactory factory = _gd!.ResourceFactory;
        Texture colorTarget = factory.CreateTexture(
            TextureDescription.Texture2D(
                width,
                height,
                1,
                1,
                PixelFormat.R8_G8_B8_A8_UNorm_SRgb,
                TextureUsage.RenderTarget | TextureUsage.Sampled,
                _options.EyeFramebufferSampleCount
            )
        );
        Texture depthTarget = factory.CreateTexture(
            TextureDescription.Texture2D(
                width,
                height,
                1,
                1,
                PixelFormat.R32_Float,
                TextureUsage.DepthStencil,
                _options.EyeFramebufferSampleCount
            )
        );
        return factory.CreateFramebuffer(new(depthTarget, colorTarget));
    }

    static Matrix4x4 ToSysMatrix(HmdMatrix34_t hmdMat)
    {
        return new(
            hmdMat.m0,
            hmdMat.m4,
            hmdMat.m8,
            0f,
            hmdMat.m1,
            hmdMat.m5,
            hmdMat.m9,
            0f,
            hmdMat.m2,
            hmdMat.m6,
            hmdMat.m10,
            0f,
            hmdMat.m3,
            hmdMat.m7,
            hmdMat.m11,
            1f
        );
    }

    static Matrix4x4 ToSysMatrix(HmdMatrix44_t hmdMat)
    {
        return new(
            hmdMat.m0,
            hmdMat.m4,
            hmdMat.m8,
            hmdMat.m12,
            hmdMat.m1,
            hmdMat.m5,
            hmdMat.m9,
            hmdMat.m13,
            hmdMat.m2,
            hmdMat.m6,
            hmdMat.m10,
            hmdMat.m14,
            hmdMat.m3,
            hmdMat.m7,
            hmdMat.m11,
            hmdMat.m15
        );
    }

    static uint GetSampleCount(TextureSampleCount sampleCount)
    {
        return sampleCount switch
        {
            TextureSampleCount.Count1 => 1,
            TextureSampleCount.Count2 => 2,
            TextureSampleCount.Count4 => 4,
            TextureSampleCount.Count8 => 8,
            TextureSampleCount.Count16 => 16,
            TextureSampleCount.Count32 => 32,
            TextureSampleCount.Count64 => 64,
            _ => throw new InvalidOperationException(),
        };
    }
}
