﻿using System;
using Veldrid.OpenGLBindings;
using static Veldrid.OpenGL.OpenGLUtil;
using static Veldrid.OpenGLBindings.OpenGLNative;

namespace Veldrid.OpenGL;

internal sealed unsafe class OpenGLFramebuffer(
    OpenGLGraphicsDevice gd,
    in FramebufferDescription description
) : Framebuffer(description.DepthTarget, description.ColorTargets), IOpenGLDeferredResource
{
    uint _framebuffer;

    string? _name;
    bool _nameChanged;
    bool _disposeRequested;
    bool _disposed;

    public override string? Name
    {
        get => _name;
        set
        {
            _name = value;
            _nameChanged = true;
        }
    }

    public uint Framebuffer => _framebuffer;
    public bool Created { get; private set; }

    public override bool IsDisposed => _disposeRequested;

    public void EnsureResourcesCreated()
    {
        if (!Created)
        {
            CreateGLResources();
        }
        if (_nameChanged)
        {
            _nameChanged = false;
            if (gd.Extensions.KHR_Debug)
            {
                SetObjectLabel(ObjectLabelIdentifier.Framebuffer, _framebuffer, _name);
            }
        }
    }

    public void CreateGLResources()
    {
        uint fb;
        glGenFramebuffers(1, &fb);
        CheckLastError();
        _framebuffer = fb;

        glBindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);
        CheckLastError();

        ReadOnlySpan<FramebufferAttachment> colorTargets = ColorTargets;
        uint colorCount = (uint)colorTargets.Length;
        if (colorCount > 0)
        {
            for (int i = 0; i < colorTargets.Length; i++)
            {
                FramebufferAttachment colorAttachment = colorTargets[i];
                OpenGLTexture glTex = Util.AssertSubtype<Texture, OpenGLTexture>(
                    colorAttachment.Target
                );
                glTex.EnsureResourcesCreated();

                gd.TextureSamplerManager.SetTextureTransient(glTex.TextureTarget, glTex.Texture);

                TextureTarget textureTarget = GetTextureTarget(glTex, colorAttachment.ArrayLayer);

                if (glTex.ArrayLayers == 1)
                {
                    glFramebufferTexture2D(
                        FramebufferTarget.Framebuffer,
                        GLFramebufferAttachment.ColorAttachment0 + i,
                        textureTarget,
                        glTex.Texture,
                        (int)colorAttachment.MipLevel
                    );
                }
                else
                {
                    glFramebufferTextureLayer(
                        FramebufferTarget.Framebuffer,
                        GLFramebufferAttachment.ColorAttachment0 + i,
                        glTex.Texture,
                        (int)colorAttachment.MipLevel,
                        (int)colorAttachment.ArrayLayer
                    );
                }
                CheckLastError();
            }

            DrawBuffersEnum* bufs = stackalloc DrawBuffersEnum[(int)colorCount];
            for (int i = 0; i < colorCount; i++)
            {
                bufs[i] = DrawBuffersEnum.ColorAttachment0 + i;
            }
            glDrawBuffers(colorCount, bufs);
            CheckLastError();
        }

        if (DepthTarget != null)
        {
            FramebufferAttachment depthTargetValue = DepthTarget.GetValueOrDefault();

            OpenGLTexture glDepthTex = Util.AssertSubtype<Texture, OpenGLTexture>(
                depthTargetValue.Target
            );
            glDepthTex.EnsureResourcesCreated();
            TextureTarget depthTarget = glDepthTex.TextureTarget;
            uint depthTextureID = glDepthTex.Texture;

            gd.TextureSamplerManager.SetTextureTransient(depthTarget, glDepthTex.Texture);

            depthTarget = GetTextureTarget(glDepthTex, depthTargetValue.ArrayLayer);

            GLFramebufferAttachment framebufferAttachment = GLFramebufferAttachment.DepthAttachment;
            if (FormatHelpers.IsStencilFormat(glDepthTex.Format))
            {
                framebufferAttachment = GLFramebufferAttachment.DepthStencilAttachment;
            }

            if (glDepthTex.ArrayLayers == 1)
            {
                glFramebufferTexture2D(
                    FramebufferTarget.Framebuffer,
                    framebufferAttachment,
                    depthTarget,
                    depthTextureID,
                    (int)depthTargetValue.MipLevel
                );
            }
            else
            {
                glFramebufferTextureLayer(
                    FramebufferTarget.Framebuffer,
                    framebufferAttachment,
                    glDepthTex.Texture,
                    (int)depthTargetValue.MipLevel,
                    (int)depthTargetValue.ArrayLayer
                );
            }
            CheckLastError();
        }

        FramebufferErrorCode errorCode = glCheckFramebufferStatus(FramebufferTarget.Framebuffer);
        CheckLastError();
        if (errorCode != FramebufferErrorCode.FramebufferComplete)
        {
            throw new VeldridException("Framebuffer was not successfully created: " + errorCode);
        }

        Created = true;
    }

    public override void Dispose()
    {
        if (!_disposeRequested)
        {
            _disposeRequested = true;
            gd.EnqueueDisposal(this);
        }
    }

    public void DestroyGLResources()
    {
        if (_disposed)
            return;

        _disposed = true;
        uint framebuffer = _framebuffer;
        glDeleteFramebuffers(1, &framebuffer);
        CheckLastError();
        _framebuffer = framebuffer;
    }
}
