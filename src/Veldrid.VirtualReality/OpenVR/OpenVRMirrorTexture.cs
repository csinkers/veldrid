using System;
using System.Collections.Generic;
using System.Numerics;

namespace Veldrid.VirtualReality.OpenVR;

internal class OpenVRMirrorTexture(OpenVRContext context) : IDisposable
{
    readonly List<IDisposable> _disposables = new();

    readonly Dictionary<OutputDescription, TextureBlitter> _blitters = new();

    ResourceSet? _leftSet;
    ResourceSet? _rightSet;

    public void Render(CommandList cl, Framebuffer fb, MirrorTextureEyeSource source)
    {
        cl.SetFramebuffer(fb);
        TextureBlitter blitter = GetBlitter(fb.OutputDescription);

        switch (source)
        {
            case MirrorTextureEyeSource.BothEyes:
                float width = fb.Width * 0.5f;
                cl.SetViewport(0, new(0, 0, width, fb.Height, 0, 1));
                BlitLeftEye(cl, blitter, width / fb.Height);
                cl.SetViewport(0, new(width, 0, width, fb.Height, 0, 1));
                BlitRightEye(cl, blitter, width / fb.Height);
                break;
            case MirrorTextureEyeSource.LeftEye:
                BlitLeftEye(cl, blitter, (float)fb.Width / fb.Height);
                break;
            case MirrorTextureEyeSource.RightEye:
                BlitRightEye(cl, blitter, (float)fb.Width / fb.Height);
                break;
        }

        cl.SetFullViewports();
    }

    void BlitLeftEye(CommandList cl, TextureBlitter blitter, float viewportAspect)
    {
        GetSampleRatio(
            context.LeftEyeFramebuffer,
            viewportAspect,
            out Vector2 minUV,
            out Vector2 maxUV
        );
        ResourceSet leftEyeSet = GetLeftEyeSet(blitter.ResourceLayout);
        blitter.Render(cl, leftEyeSet, minUV, maxUV);
    }

    void BlitRightEye(CommandList cl, TextureBlitter blitter, float viewportAspect)
    {
        GetSampleRatio(
            context.RightEyeFramebuffer,
            viewportAspect,
            out Vector2 minUV,
            out Vector2 maxUV
        );
        ResourceSet rightEyeSet = GetRightEyeSet(blitter.ResourceLayout);
        blitter.Render(cl, rightEyeSet, minUV, maxUV);
    }

    void GetSampleRatio(
        Framebuffer eyeFB,
        float viewportAspect,
        out Vector2 minUV,
        out Vector2 maxUV
    )
    {
        uint eyeWidth = eyeFB.Width;
        uint eyeHeight = eyeFB.Height;

        uint sampleWidth,
            sampleHeight;
        if (viewportAspect > 1)
        {
            sampleWidth = eyeWidth;
            sampleHeight = (uint)(eyeWidth / viewportAspect);
        }
        else
        {
            sampleHeight = eyeHeight;
            sampleWidth = (uint)(eyeHeight / (1 / viewportAspect));
        }

        float sampleUVWidth = (float)sampleWidth / eyeWidth;
        float sampleUVHeight = (float)sampleHeight / eyeHeight;

        float max = Math.Max(sampleUVWidth, sampleUVHeight);
        sampleUVWidth /= max;
        sampleUVHeight /= max;

        minUV = new(0.5f - sampleUVWidth / 2f, 0.5f - sampleUVHeight / 2f);
        maxUV = new(0.5f + sampleUVWidth / 2f, 0.5f + sampleUVHeight / 2f);
    }

    ResourceSet GetLeftEyeSet(ResourceLayout rl) =>
        _leftSet ??= CreateColorTargetSet(rl, context.LeftEyeFramebuffer);

    ResourceSet GetRightEyeSet(ResourceLayout rl) =>
        _rightSet ??= CreateColorTargetSet(rl, context.RightEyeFramebuffer);

    ResourceSet CreateColorTargetSet(ResourceLayout rl, Framebuffer fb)
    {
        ResourceFactory factory = context.GraphicsDevice.ResourceFactory;
        Texture target = fb.ColorTargets[0].Target;
        TextureView view = factory.CreateTextureView(target);
        _disposables.Add(view);
        ResourceSet rs = factory.CreateResourceSet(
            new(rl, view, context.GraphicsDevice.PointSampler)
        );
        _disposables.Add(rs);

        return rs;
    }

    TextureBlitter GetBlitter(OutputDescription outputDescription)
    {
        if (!_blitters.TryGetValue(outputDescription, out TextureBlitter? ret))
        {
            ret = new(
                context.GraphicsDevice,
                context.GraphicsDevice.ResourceFactory,
                outputDescription,
                srgbOutput: false
            );

            _blitters.Add(outputDescription, ret);
        }

        return ret;
    }

    public void Dispose()
    {
        foreach (IDisposable disposable in _disposables)
            disposable.Dispose();

        foreach (KeyValuePair<OutputDescription, TextureBlitter> kvp in _blitters)
            kvp.Value.Dispose();

        _leftSet?.Dispose();
        _rightSet?.Dispose();
    }
}
