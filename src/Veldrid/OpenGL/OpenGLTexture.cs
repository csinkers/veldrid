﻿using System;
using System.Diagnostics;
using Veldrid.OpenGLBindings;
using static Veldrid.OpenGL.OpenGLUtil;
using static Veldrid.OpenGLBindings.OpenGLNative;

namespace Veldrid.OpenGL;

internal sealed unsafe class OpenGLTexture : Texture, IOpenGLDeferredResource
{
    readonly OpenGLGraphicsDevice _gd;
    uint _texture;
    readonly uint[] _framebuffers;
    readonly uint[] _pbos;
    readonly uint[] _pboSizes;
    bool _disposeRequested;
    bool _disposed;

    string? _name;
    bool _nameChanged;

    public override string? Name
    {
        get => _name;
        set
        {
            _name = value;
            _nameChanged = true;
        }
    }

    public uint Texture => _texture;

    public OpenGLTexture(OpenGLGraphicsDevice gd, in TextureDescription description)
    {
        _gd = gd;

        Width = description.Width;
        Height = description.Height;
        Depth = description.Depth;
        Format = description.Format;
        MipLevels = description.MipLevels;
        ArrayLayers = description.ArrayLayers;
        Usage = description.Usage;
        Type = description.Type;
        SampleCount = description.SampleCount;

        _framebuffers = new uint[MipLevels * ArrayLayers];
        _pbos = new uint[MipLevels * ArrayLayers];
        _pboSizes = new uint[MipLevels * ArrayLayers];

        GLPixelFormat = OpenGLFormats.VdToGLPixelFormat(Format, Usage);
        GLPixelType = OpenGLFormats.VdToGLPixelType(Format);
        GLInternalFormat = OpenGLFormats.VdToGLPixelInternalFormat(Format, Usage);

        if ((Usage & TextureUsage.Cubemap) == TextureUsage.Cubemap)
        {
            TextureTarget =
                ArrayLayers == 1 ? TextureTarget.TextureCubeMap : TextureTarget.TextureCubeMapArray;
        }
        else if (Type == TextureType.Texture1D)
        {
            TextureTarget =
                ArrayLayers == 1 ? TextureTarget.Texture1D : TextureTarget.Texture1DArray;
        }
        else if (Type == TextureType.Texture2D)
        {
            if (ArrayLayers == 1)
            {
                TextureTarget =
                    SampleCount == TextureSampleCount.Count1
                        ? TextureTarget.Texture2D
                        : TextureTarget.Texture2DMultisample;
            }
            else
            {
                TextureTarget =
                    SampleCount == TextureSampleCount.Count1
                        ? TextureTarget.Texture2DArray
                        : TextureTarget.Texture2DMultisampleArray;
            }
        }
        else
        {
            Debug.Assert(Type == TextureType.Texture3D);
            TextureTarget = TextureTarget.Texture3D;
        }
    }

    public OpenGLTexture(
        OpenGLGraphicsDevice gd,
        uint nativeTexture,
        in TextureDescription description
    )
    {
        _gd = gd;
        _texture = nativeTexture;
        Width = description.Width;
        Height = description.Height;
        Depth = description.Depth;
        Format = description.Format;
        MipLevels = description.MipLevels;
        ArrayLayers = description.ArrayLayers;
        Usage = description.Usage;
        Type = description.Type;
        SampleCount = description.SampleCount;

        _framebuffers = new uint[MipLevels * ArrayLayers];
        _pbos = new uint[MipLevels * ArrayLayers];
        _pboSizes = new uint[MipLevels * ArrayLayers];

        GLPixelFormat = OpenGLFormats.VdToGLPixelFormat(Format, Usage);
        GLPixelType = OpenGLFormats.VdToGLPixelType(Format);
        GLInternalFormat = OpenGLFormats.VdToGLPixelInternalFormat(Format, Usage);

        if ((Usage & TextureUsage.Cubemap) == TextureUsage.Cubemap)
        {
            TextureTarget =
                ArrayLayers == 1 ? TextureTarget.TextureCubeMap : TextureTarget.TextureCubeMapArray;
        }
        else if (Type == TextureType.Texture1D)
        {
            TextureTarget =
                ArrayLayers == 1 ? TextureTarget.Texture1D : TextureTarget.Texture1DArray;
        }
        else if (Type == TextureType.Texture2D)
        {
            if (ArrayLayers == 1)
            {
                TextureTarget =
                    SampleCount == TextureSampleCount.Count1
                        ? TextureTarget.Texture2D
                        : TextureTarget.Texture2DMultisample;
            }
            else
            {
                TextureTarget =
                    SampleCount == TextureSampleCount.Count1
                        ? TextureTarget.Texture2DArray
                        : TextureTarget.Texture2DMultisampleArray;
            }
        }
        else
        {
            Debug.Assert(Type == TextureType.Texture3D);
            TextureTarget = TextureTarget.Texture3D;
        }

        Created = true;
    }

    public override bool IsDisposed => _disposeRequested;

    public GLPixelFormat GLPixelFormat { get; }
    public GLPixelType GLPixelType { get; }
    public PixelInternalFormat GLInternalFormat { get; }
    public TextureTarget TextureTarget { get; internal set; }

    public bool Created { get; private set; }

    public void EnsureResourcesCreated()
    {
        if (!Created)
            CreateGLResources();

        if (_nameChanged)
        {
            _nameChanged = false;
            if (_gd.Extensions.KHR_Debug)
            {
                SetObjectLabel(ObjectLabelIdentifier.Texture, _texture, _name);
            }
        }
    }

    void CreateGLResources()
    {
        bool dsa = _gd.Extensions.ARB_DirectStateAccess;
        if (dsa)
        {
            uint texture;
            glCreateTextures(TextureTarget, 1, &texture);
            CheckLastError();
            _texture = texture;
        }
        else
        {
            uint texture;
            glGenTextures(1, &texture);
            CheckLastError();
            _texture = texture;

            _gd.TextureSamplerManager.SetTextureTransient(TextureTarget, _texture);
        }

        if (TextureTarget == TextureTarget.Texture1D)
        {
            if (dsa)
            {
                glTextureStorage1D(
                    _texture,
                    MipLevels,
                    OpenGLFormats.VdToGLSizedInternalFormat(Format, Usage),
                    Width
                );
                CheckLastError();
            }
            else if (_gd.Extensions.TextureStorage)
            {
                glTexStorage1D(
                    TextureTarget.Texture1D,
                    MipLevels,
                    OpenGLFormats.VdToGLSizedInternalFormat(Format, Usage),
                    Width
                );
                CheckLastError();
            }
            else
            {
                uint levelWidth = Width;
                for (int currentLevel = 0; currentLevel < MipLevels; currentLevel++)
                {
                    // Set size, load empty data into texture
                    glTexImage1D(
                        TextureTarget.Texture1D,
                        currentLevel,
                        GLInternalFormat,
                        levelWidth,
                        0, // border
                        GLPixelFormat,
                        GLPixelType,
                        null
                    );
                    CheckLastError();

                    levelWidth = Math.Max(1, levelWidth / 2);
                }
            }
        }
        else if (
            TextureTarget == TextureTarget.Texture2D
            || TextureTarget == TextureTarget.Texture1DArray
        )
        {
            uint heightOrArrayLayers =
                TextureTarget == TextureTarget.Texture2D ? Height : ArrayLayers;
            if (dsa)
            {
                glTextureStorage2D(
                    _texture,
                    MipLevels,
                    OpenGLFormats.VdToGLSizedInternalFormat(Format, Usage),
                    Width,
                    heightOrArrayLayers
                );
                CheckLastError();
            }
            else if (_gd.Extensions.TextureStorage)
            {
                glTexStorage2D(
                    TextureTarget,
                    MipLevels,
                    OpenGLFormats.VdToGLSizedInternalFormat(Format, Usage),
                    Width,
                    heightOrArrayLayers
                );
                CheckLastError();
            }
            else
            {
                uint levelWidth = Width;
                uint levelHeight = heightOrArrayLayers;
                for (int currentLevel = 0; currentLevel < MipLevels; currentLevel++)
                {
                    // Set size, load empty data into texture
                    glTexImage2D(
                        TextureTarget,
                        currentLevel,
                        GLInternalFormat,
                        levelWidth,
                        levelHeight,
                        0, // border
                        GLPixelFormat,
                        GLPixelType,
                        null
                    );
                    CheckLastError();

                    levelWidth = Math.Max(1, levelWidth / 2);
                    if (TextureTarget == TextureTarget.Texture2D)
                    {
                        levelHeight = Math.Max(1, levelHeight / 2);
                    }
                }
            }
        }
        else if (TextureTarget == TextureTarget.Texture2DArray)
        {
            if (dsa)
            {
                glTextureStorage3D(
                    _texture,
                    MipLevels,
                    OpenGLFormats.VdToGLSizedInternalFormat(Format, Usage),
                    Width,
                    Height,
                    ArrayLayers
                );
                CheckLastError();
            }
            else if (_gd.Extensions.TextureStorage)
            {
                glTexStorage3D(
                    TextureTarget.Texture2DArray,
                    MipLevels,
                    OpenGLFormats.VdToGLSizedInternalFormat(Format, Usage),
                    Width,
                    Height,
                    ArrayLayers
                );
                CheckLastError();
            }
            else
            {
                uint levelWidth = Width;
                uint levelHeight = Height;
                for (int currentLevel = 0; currentLevel < MipLevels; currentLevel++)
                {
                    glTexImage3D(
                        TextureTarget.Texture2DArray,
                        currentLevel,
                        GLInternalFormat,
                        levelWidth,
                        levelHeight,
                        ArrayLayers,
                        0, // border
                        GLPixelFormat,
                        GLPixelType,
                        null
                    );
                    CheckLastError();

                    levelWidth = Math.Max(1, levelWidth / 2);
                    levelHeight = Math.Max(1, levelHeight / 2);
                }
            }
        }
        else if (TextureTarget == TextureTarget.Texture2DMultisample)
        {
            if (dsa)
            {
                glTextureStorage2DMultisample(
                    _texture,
                    FormatHelpers.GetSampleCountUInt32(SampleCount),
                    OpenGLFormats.VdToGLSizedInternalFormat(Format, Usage),
                    Width,
                    Height,
                    false
                );
                CheckLastError();
            }
            else
            {
                if (_gd.Extensions.TextureStorageMultisample)
                {
                    glTexStorage2DMultisample(
                        TextureTarget.Texture2DMultisample,
                        FormatHelpers.GetSampleCountUInt32(SampleCount),
                        OpenGLFormats.VdToGLSizedInternalFormat(Format, Usage),
                        Width,
                        Height,
                        false
                    );
                    CheckLastError();
                }
                else
                {
                    glTexImage2DMultiSample(
                        TextureTarget.Texture2DMultisample,
                        FormatHelpers.GetSampleCountUInt32(SampleCount),
                        GLInternalFormat,
                        Width,
                        Height,
                        false
                    );
                }
                CheckLastError();
            }
        }
        else if (TextureTarget == TextureTarget.Texture2DMultisampleArray)
        {
            if (dsa)
            {
                glTextureStorage3DMultisample(
                    _texture,
                    FormatHelpers.GetSampleCountUInt32(SampleCount),
                    OpenGLFormats.VdToGLSizedInternalFormat(Format, Usage),
                    Width,
                    Height,
                    ArrayLayers,
                    false
                );
                CheckLastError();
            }
            else
            {
                if (_gd.Extensions.TextureStorageMultisample)
                {
                    glTexStorage3DMultisample(
                        TextureTarget.Texture2DMultisampleArray,
                        FormatHelpers.GetSampleCountUInt32(SampleCount),
                        OpenGLFormats.VdToGLSizedInternalFormat(Format, Usage),
                        Width,
                        Height,
                        ArrayLayers,
                        false
                    );
                }
                else
                {
                    glTexImage3DMultisample(
                        TextureTarget.Texture2DMultisampleArray,
                        FormatHelpers.GetSampleCountUInt32(SampleCount),
                        GLInternalFormat,
                        Width,
                        Height,
                        ArrayLayers,
                        false
                    );
                    CheckLastError();
                }
            }
        }
        else if (TextureTarget == TextureTarget.TextureCubeMap)
        {
            if (dsa)
            {
                glTextureStorage2D(
                    _texture,
                    MipLevels,
                    OpenGLFormats.VdToGLSizedInternalFormat(Format, Usage),
                    Width,
                    Height
                );
                CheckLastError();
            }
            else if (_gd.Extensions.TextureStorage)
            {
                glTexStorage2D(
                    TextureTarget.TextureCubeMap,
                    MipLevels,
                    OpenGLFormats.VdToGLSizedInternalFormat(Format, Usage),
                    Width,
                    Height
                );
                CheckLastError();
            }
            else
            {
                uint levelWidth = Width;
                uint levelHeight = Height;
                for (int currentLevel = 0; currentLevel < MipLevels; currentLevel++)
                {
                    for (int face = 0; face < 6; face++)
                    {
                        // Set size, load empty data into texture
                        glTexImage2D(
                            TextureTarget.TextureCubeMapPositiveX + face,
                            currentLevel,
                            GLInternalFormat,
                            levelWidth,
                            levelHeight,
                            0, // border
                            GLPixelFormat,
                            GLPixelType,
                            null
                        );
                        CheckLastError();
                    }

                    levelWidth = Math.Max(1, levelWidth / 2);
                    levelHeight = Math.Max(1, levelHeight / 2);
                }
            }
        }
        else if (TextureTarget == TextureTarget.TextureCubeMapArray)
        {
            if (dsa)
            {
                glTextureStorage3D(
                    _texture,
                    MipLevels,
                    OpenGLFormats.VdToGLSizedInternalFormat(Format, Usage),
                    Width,
                    Height,
                    ArrayLayers * 6
                );
                CheckLastError();
            }
            else if (_gd.Extensions.TextureStorage)
            {
                glTexStorage3D(
                    TextureTarget.TextureCubeMapArray,
                    MipLevels,
                    OpenGLFormats.VdToGLSizedInternalFormat(Format, Usage),
                    Width,
                    Height,
                    ArrayLayers * 6
                );
                CheckLastError();
            }
            else
            {
                uint levelWidth = Width;
                uint levelHeight = Height;
                for (int currentLevel = 0; currentLevel < MipLevels; currentLevel++)
                {
                    for (int face = 0; face < 6; face++)
                    {
                        // Set size, load empty data into texture
                        glTexImage3D(
                            TextureTarget.Texture2DArray,
                            currentLevel,
                            GLInternalFormat,
                            levelWidth,
                            levelHeight,
                            ArrayLayers * 6,
                            0, // border
                            GLPixelFormat,
                            GLPixelType,
                            null
                        );
                        CheckLastError();
                    }

                    levelWidth = Math.Max(1, levelWidth / 2);
                    levelHeight = Math.Max(1, levelHeight / 2);
                }
            }
        }
        else if (TextureTarget == TextureTarget.Texture3D)
        {
            if (dsa)
            {
                glTextureStorage3D(
                    _texture,
                    MipLevels,
                    OpenGLFormats.VdToGLSizedInternalFormat(Format, Usage),
                    Width,
                    Height,
                    Depth
                );
                CheckLastError();
            }
            else if (_gd.Extensions.TextureStorage)
            {
                glTexStorage3D(
                    TextureTarget.Texture3D,
                    MipLevels,
                    OpenGLFormats.VdToGLSizedInternalFormat(Format, Usage),
                    Width,
                    Height,
                    Depth
                );
                CheckLastError();
            }
            else
            {
                uint levelWidth = Width;
                uint levelHeight = Height;
                uint levelDepth = Depth;
                for (int currentLevel = 0; currentLevel < MipLevels; currentLevel++)
                {
                    for (int face = 0; face < 6; face++)
                    {
                        // Set size, load empty data into texture
                        glTexImage3D(
                            TextureTarget.Texture3D,
                            currentLevel,
                            GLInternalFormat,
                            levelWidth,
                            levelHeight,
                            levelDepth,
                            0, // border
                            GLPixelFormat,
                            GLPixelType,
                            null
                        );
                        CheckLastError();
                    }

                    levelWidth = Math.Max(1, levelWidth / 2);
                    levelHeight = Math.Max(1, levelHeight / 2);
                    levelDepth = Math.Max(1, levelDepth / 2);
                }
            }
        }
        else
        {
            throw new VeldridException("Invalid texture target: " + TextureTarget);
        }

        Created = true;
    }

    public uint GetFramebuffer(uint mipLevel, uint arrayLayer)
    {
        Debug.Assert(!FormatHelpers.IsCompressedFormat(Format));
        Debug.Assert(Created);

        uint subresource = CalculateSubresource(mipLevel, arrayLayer);
        if (_framebuffers[subresource] == 0)
        {
            FramebufferTarget framebufferTarget =
                SampleCount == TextureSampleCount.Count1
                    ? FramebufferTarget.DrawFramebuffer
                    : FramebufferTarget.ReadFramebuffer;

            uint fb;
            glGenFramebuffers(1, &fb);
            CheckLastError();
            _framebuffers[subresource] = fb;

            glBindFramebuffer(framebufferTarget, _framebuffers[subresource]);
            CheckLastError();

            _gd.TextureSamplerManager.SetTextureTransient(TextureTarget, Texture);

            if (
                TextureTarget == TextureTarget.Texture2D
                || TextureTarget == TextureTarget.Texture2DMultisample
            )
            {
                glFramebufferTexture2D(
                    framebufferTarget,
                    GLFramebufferAttachment.ColorAttachment0,
                    TextureTarget,
                    Texture,
                    (int)mipLevel
                );
                CheckLastError();
            }
            else if (
                TextureTarget == TextureTarget.Texture2DArray
                || TextureTarget == TextureTarget.Texture2DMultisampleArray
                || TextureTarget == TextureTarget.Texture3D
            )
            {
                glFramebufferTextureLayer(
                    framebufferTarget,
                    GLFramebufferAttachment.ColorAttachment0,
                    Texture,
                    (int)mipLevel,
                    (int)arrayLayer
                );
                CheckLastError();
            }

            FramebufferErrorCode errorCode = glCheckFramebufferStatus(framebufferTarget);
            if (errorCode != FramebufferErrorCode.FramebufferComplete)
            {
                throw new VeldridException("Failed to create texture copy FBO: " + errorCode);
            }
        }

        return _framebuffers[subresource];
    }

    public uint GetPixelBuffer(uint subresource)
    {
        Debug.Assert(Created);
        if (_pbos[subresource] == 0)
        {
            uint pb;
            glGenBuffers(1, &pb);
            CheckLastError();
            _pbos[subresource] = pb;

            glBindBuffer(BufferTarget.CopyWriteBuffer, _pbos[subresource]);
            CheckLastError();

            uint dataSize = Width * Height * FormatSizeHelpers.GetSizeInBytes(Format);
            glBufferData(BufferTarget.CopyWriteBuffer, dataSize, null, BufferUsageHint.StaticCopy);
            CheckLastError();
            _pboSizes[subresource] = dataSize;
        }

        return _pbos[subresource];
    }

    public uint GetPixelBufferSize(uint subresource)
    {
        Debug.Assert(Created);
        Debug.Assert(_pbos[subresource] != 0);
        return _pboSizes[subresource];
    }

    private protected override void DisposeCore()
    {
        if (!_disposeRequested)
        {
            _disposeRequested = true;
            _gd.EnqueueDisposal(this);
        }
    }

    public void DestroyGLResources()
    {
        if (_disposed)
            return;

        _disposed = true;
        uint tex = _texture;
        glDeleteTextures(1, &tex);
        CheckLastError();
        _texture = tex;

        for (int i = 0; i < _framebuffers.Length; i++)
        {
            uint fb = _framebuffers[i];
            if (fb != 0)
            {
                glDeleteFramebuffers(1, &fb);
                _framebuffers[i] = fb;
            }
        }

        for (int i = 0; i < _pbos.Length; i++)
        {
            uint pb = _pbos[i];
            if (pb != 0)
            {
                glDeleteBuffers(1, &pb);
                _pbos[i] = pb;
            }
        }
    }
}
