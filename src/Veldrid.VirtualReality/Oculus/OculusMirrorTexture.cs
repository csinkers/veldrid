using System;
using System.Collections.Generic;
using System.Numerics;
using TerraFX.Interop.Vulkan;
using static Veldrid.VirtualReality.Oculus.LibOvrNative;

namespace Veldrid.VirtualReality.Oculus;

internal class OculusMirrorTexture(OculusContext oculusContext) : IDisposable
{
    public static readonly Guid s_d3d11Tex2DGuid = new("6f15aaf2-d208-4e89-9ab4-489535d34f9c");

    readonly Dictionary<OutputDescription, TextureBlitter> _blitters = new();

    (uint width, uint height, MirrorTextureEyeSource source) _texProperties;
    ovrMirrorTexture _ovrMirrorTex;
    Texture? _vdMirrorTex;
    TextureView? _vdMirrorTexView;
    ResourceSet? _set;
    Texture? _vkTrueMirrorTex;

    void CreateMirrorTex(uint width, uint height, MirrorTextureEyeSource source)
    {
        _set?.Dispose();
        _set = null;
        _vdMirrorTexView?.Dispose();
        if (_vkTrueMirrorTex != null)
            _vdMirrorTex?.Dispose();

        if (!_ovrMirrorTex.IsNull)
            ovr_DestroyMirrorTexture(oculusContext.Session, _ovrMirrorTex);

        switch (oculusContext.GraphicsDevice.BackendType)
        {
            case GraphicsBackend.Direct3D11:
                CreateMirrorTextureD3D11(width, height, source);
                break;
            case GraphicsBackend.Vulkan:
                CreateMirrorTextureVulkan(width, height, source);
                break;
            case GraphicsBackend.OpenGL:
            case GraphicsBackend.OpenGLES:
                CreateMirrorTextureGL(width, height, source);
                break;
            default:
                throw new VeldridException("This backend does not support VR.");
        }

        _texProperties = (width, height, source);
    }

    unsafe void CreateMirrorTextureGL(uint width, uint height, MirrorTextureEyeSource source)
    {
        GraphicsDevice gd = oculusContext.GraphicsDevice;

        uint glID = 0;

        ovrMirrorTextureDesc desc;
        desc.Format = ovrTextureFormat.R8G8B8A8_UNORM_SRGB;
        desc.Width = (int)width;
        desc.Height = (int)height;
        desc.MiscFlags = ovrTextureMiscFlags.DX_Typeless;
        desc.MirrorOptions =
            source == MirrorTextureEyeSource.LeftEye ? ovrMirrorOptions.LeftEyeOnly
            : source == MirrorTextureEyeSource.RightEye ? ovrMirrorOptions.RightEyeOnly
            : ovrMirrorOptions.Default;

        gd.GetOpenGLInfo()
            .ExecuteOnGLThread(() =>
            {
                ovrMirrorTextureDesc localDesc = desc;
                localDesc.MiscFlags &= ~(
                    ovrTextureMiscFlags.DX_Typeless | ovrTextureMiscFlags.AllowGenerateMips
                );
                ovrMirrorTexture localTex;
                ovrResult result = ovr_CreateMirrorTextureWithOptionsGL(
                    oculusContext.Session,
                    &localDesc,
                    &localTex
                );
                if (result != ovrResult.Success)
                {
                    return;
                }
                _ovrMirrorTex = localTex;

                uint localID;
                result = ovr_GetMirrorTextureBufferGL(
                    oculusContext.Session,
                    _ovrMirrorTex,
                    &localID
                );
                if (result != ovrResult.Success)
                {
                    return;
                }
                glID = localID;
            });

        if (_ovrMirrorTex.IsNull)
            throw new VeldridException("Failed to create OpenGL Mirror Texture");

        _vdMirrorTex = gd.ResourceFactory.CreateTexture(
            glID,
            OculusUtil.GetVeldridTextureDescription(desc)
        );

        _vdMirrorTexView = oculusContext.GraphicsDevice.ResourceFactory.CreateTextureView(
            _vdMirrorTex
        );
    }

    unsafe void CreateMirrorTextureVulkan(uint width, uint height, MirrorTextureEyeSource source)
    {
        GraphicsDevice gd = oculusContext.GraphicsDevice;

        ovrMirrorTextureDesc desc;
        desc.Format = ovrTextureFormat.R8G8B8A8_UNORM_SRGB;
        desc.Width = (int)width;
        desc.Height = (int)height;
        desc.MiscFlags = ovrTextureMiscFlags.DX_Typeless;
        desc.MirrorOptions =
            source == MirrorTextureEyeSource.LeftEye ? ovrMirrorOptions.LeftEyeOnly
            : source == MirrorTextureEyeSource.RightEye ? ovrMirrorOptions.RightEyeOnly
            : ovrMirrorOptions.Default;

        desc.MiscFlags &= ~(ovrTextureMiscFlags.DX_Typeless);

        ovrMirrorTexture mirrorTex;
        ovrResult result = ovr_CreateMirrorTextureWithOptionsVk(
            oculusContext.Session,
            gd.GetVulkanInfo().Device,
            &desc,
            &mirrorTex
        );

        if (result != ovrResult.Success)
            throw new VeldridException($"Failed to create Vulkan mirror texture: {result}");

        _ovrMirrorTex = mirrorTex;

        ulong vkImage;
        result = ovr_GetMirrorTextureBufferVk(oculusContext.Session, mirrorTex, &vkImage);

        if (result != ovrResult.Success)
            throw new VeldridException($"Failed to get Vulkan Mirror Texture image: {result}.");

        _vkTrueMirrorTex = gd.ResourceFactory.CreateTexture(
            vkImage,
            OculusUtil.GetVeldridTextureDescription(desc)
        );
        gd.GetVulkanInfo()
            .OverrideImageLayout(
                _vkTrueMirrorTex,
                (uint)VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL
            );

        _vdMirrorTex = gd.ResourceFactory.CreateTexture(
            TextureDescription.Texture2D(
                _vkTrueMirrorTex.Width,
                _vkTrueMirrorTex.Height,
                _vkTrueMirrorTex.MipLevels,
                _vkTrueMirrorTex.ArrayLayers,
                _vkTrueMirrorTex.Format,
                TextureUsage.Sampled
            )
        );
        _vdMirrorTexView = oculusContext.GraphicsDevice.ResourceFactory.CreateTextureView(
            _vdMirrorTex
        );
    }

    unsafe void CreateMirrorTextureD3D11(uint width, uint height, MirrorTextureEyeSource source)
    {
        ovrMirrorTextureDesc desc;
        desc.Format = ovrTextureFormat.R8G8B8A8_UNORM_SRGB;
        desc.Width = (int)width;
        desc.Height = (int)height;
        desc.MiscFlags = ovrTextureMiscFlags.DX_Typeless;
        desc.MirrorOptions =
            source == MirrorTextureEyeSource.LeftEye ? ovrMirrorOptions.LeftEyeOnly
            : source == MirrorTextureEyeSource.RightEye ? ovrMirrorOptions.RightEyeOnly
            : ovrMirrorOptions.Default;

        ovrMirrorTexture mirrorTexture;
        ovrResult result = ovr_CreateMirrorTextureWithOptionsDX(
            oculusContext.Session,
            oculusContext.GraphicsDevice.GetD3D11Info().Device,
            &desc,
            &mirrorTexture
        );
        if (result != ovrResult.Success)
            throw new VeldridException($"Failed to create DX mirror texture: {result}");

        _ovrMirrorTex = mirrorTexture;

        IntPtr mirrord3d11Tex;
        result = ovr_GetMirrorTextureBufferDX(
            oculusContext.Session,
            mirrorTexture,
            s_d3d11Tex2DGuid,
            &mirrord3d11Tex
        );
        if (result != ovrResult.Success)
            throw new VeldridException($"Failed to get D3D11 mirror texture handle: {result}");

        _vdMirrorTex = oculusContext.GraphicsDevice.ResourceFactory.CreateTexture(
            (ulong)mirrord3d11Tex,
            OculusUtil.GetVeldridTextureDescription(desc)
        );

        _vdMirrorTexView = oculusContext.GraphicsDevice.ResourceFactory.CreateTextureView(
            _vdMirrorTex
        );
    }

    public void Render(CommandList cl, Framebuffer fb, MirrorTextureEyeSource source)
    {
        EnsureMirrorTexture(fb.Width, fb.Height, source);
        TextureBlitter blitter = GetBlitter(fb.OutputDescription);
        ResourceSet set = GetResourceSet(blitter);

        if (_vkTrueMirrorTex != null && _vdMirrorTex != null)
            cl.CopyTexture(_vkTrueMirrorTex, _vdMirrorTex);

        cl.SetFramebuffer(fb);
        blitter.Render(cl, set, Vector2.Zero, Vector2.One);
    }

    void EnsureMirrorTexture(uint width, uint height, MirrorTextureEyeSource source)
    {
        if (_texProperties != (width, height, source))
            CreateMirrorTex(width, height, source);
    }

    ResourceSet GetResourceSet(TextureBlitter blitter) =>
        _set ??= oculusContext.GraphicsDevice.ResourceFactory.CreateResourceSet(
            new(blitter.ResourceLayout, _vdMirrorTexView, oculusContext.GraphicsDevice.PointSampler)
        );

    TextureBlitter GetBlitter(OutputDescription outputDescription)
    {
        if (!_blitters.TryGetValue(outputDescription, out TextureBlitter? ret))
        {
            ret = new(
                oculusContext.GraphicsDevice,
                oculusContext.GraphicsDevice.ResourceFactory,
                outputDescription,
                srgbOutput: false
            );

            _blitters.Add(outputDescription, ret);
        }

        return ret;
    }

    public void Dispose()
    {
        foreach (KeyValuePair<OutputDescription, TextureBlitter> kvp in _blitters)
            kvp.Value.Dispose();

        _set?.Dispose();
        _vdMirrorTex?.Dispose();
        _vdMirrorTexView?.Dispose();
        ovr_DestroyMirrorTexture(oculusContext.Session, _ovrMirrorTex);
    }
}
