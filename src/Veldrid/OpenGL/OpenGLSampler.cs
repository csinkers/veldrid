﻿using Veldrid.OpenGLBinding;
using static Veldrid.OpenGL.OpenGLUtil;
using static Veldrid.OpenGLBinding.OpenGLNative;

namespace Veldrid.OpenGL;

internal sealed unsafe class OpenGLSampler(
    OpenGLGraphicsDevice gd,
    in SamplerDescription description
) : Sampler, OpenGLDeferredResource
{
    readonly SamplerDescription _description = description;
    InternalSamplerState _noMipmapState;
    InternalSamplerState _mipmapState;
    bool _disposeRequested;

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

    public override bool IsDisposed => _disposeRequested;

    public uint NoMipmapSampler => _noMipmapState.Sampler;
    public uint MipmapSampler => _mipmapState.Sampler;

    public bool Created { get; private set; }

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
                SetObjectLabel(
                    ObjectLabelIdentifier.Sampler,
                    _noMipmapState.Sampler,
                    $"{_name}_WithoutMipmap"
                );
                SetObjectLabel(
                    ObjectLabelIdentifier.Sampler,
                    _mipmapState.Sampler,
                    $"{_name}_WithMipmap"
                );
            }
        }
    }

    void CreateGLResources()
    {
        GraphicsBackend backendType = gd.BackendType;
        _noMipmapState.CreateGLResources(_description, false, backendType);
        _mipmapState.CreateGLResources(_description, true, backendType);
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
        _mipmapState.DestroyGLResources();
        _noMipmapState.DestroyGLResources();
    }

    struct InternalSamplerState
    {
        uint _sampler;

        public readonly uint Sampler => _sampler;

        public void CreateGLResources(
            SamplerDescription description,
            bool mipmapped,
            GraphicsBackend backend
        )
        {
            uint sampler;
            glGenSamplers(1, &sampler);
            CheckLastError();
            _sampler = sampler;

            glSamplerParameteri(
                _sampler,
                SamplerParameterName.TextureWrapS,
                (int)OpenGLFormats.VdToGLTextureWrapMode(description.AddressModeU)
            );
            CheckLastError();
            glSamplerParameteri(
                _sampler,
                SamplerParameterName.TextureWrapT,
                (int)OpenGLFormats.VdToGLTextureWrapMode(description.AddressModeV)
            );
            CheckLastError();
            glSamplerParameteri(
                _sampler,
                SamplerParameterName.TextureWrapR,
                (int)OpenGLFormats.VdToGLTextureWrapMode(description.AddressModeW)
            );
            CheckLastError();

            if (
                description.AddressModeU == SamplerAddressMode.Border
                || description.AddressModeV == SamplerAddressMode.Border
                || description.AddressModeW == SamplerAddressMode.Border
            )
            {
                RgbaFloat borderColor = ToRgbaFloat(description.BorderColor);
                glSamplerParameterfv(
                    _sampler,
                    SamplerParameterName.TextureBorderColor,
                    (float*)&borderColor
                );
                CheckLastError();
            }

            glSamplerParameterf(
                _sampler,
                SamplerParameterName.TextureMinLod,
                description.MinimumLod
            );
            CheckLastError();
            glSamplerParameterf(
                _sampler,
                SamplerParameterName.TextureMaxLod,
                description.MaximumLod
            );
            CheckLastError();
            if (backend == GraphicsBackend.OpenGL && description.LodBias != 0)
            {
                glSamplerParameterf(
                    _sampler,
                    SamplerParameterName.TextureLodBias,
                    description.LodBias
                );
                CheckLastError();
            }

            if (description.Filter == SamplerFilter.Anisotropic)
            {
                glSamplerParameterf(
                    _sampler,
                    SamplerParameterName.TextureMaxAnisotropyExt,
                    description.MaximumAnisotropy
                );
                CheckLastError();
                glSamplerParameteri(
                    _sampler,
                    SamplerParameterName.TextureMinFilter,
                    mipmapped
                        ? (int)TextureMinFilter.LinearMipmapLinear
                        : (int)TextureMinFilter.Linear
                );
                CheckLastError();
                glSamplerParameteri(
                    _sampler,
                    SamplerParameterName.TextureMagFilter,
                    (int)TextureMagFilter.Linear
                );
                CheckLastError();
            }
            else
            {
                OpenGLFormats.VdToGLTextureMinMagFilter(
                    description.Filter,
                    mipmapped,
                    out TextureMinFilter min,
                    out TextureMagFilter mag
                );
                glSamplerParameteri(_sampler, SamplerParameterName.TextureMinFilter, (int)min);
                CheckLastError();
                glSamplerParameteri(_sampler, SamplerParameterName.TextureMagFilter, (int)mag);
                CheckLastError();
            }

            if (description.ComparisonKind != null)
            {
                glSamplerParameteri(
                    _sampler,
                    SamplerParameterName.TextureCompareMode,
                    (int)TextureCompareMode.CompareRefToTexture
                );
                CheckLastError();
                glSamplerParameteri(
                    _sampler,
                    SamplerParameterName.TextureCompareFunc,
                    (int)OpenGLFormats.VdToGLDepthFunction(description.ComparisonKind.Value)
                );
                CheckLastError();
            }
        }

        public void DestroyGLResources()
        {
            uint sampler = _sampler;
            glDeleteSamplers(1, &sampler);
            CheckLastError();
            _sampler = sampler;
        }

        static RgbaFloat ToRgbaFloat(SamplerBorderColor borderColor)
        {
            return borderColor switch
            {
                SamplerBorderColor.TransparentBlack => new(0, 0, 0, 0),
                SamplerBorderColor.OpaqueBlack => new(0, 0, 0, 1),
                SamplerBorderColor.OpaqueWhite => new(1, 1, 1, 1),
                _ => Illegal.Value<SamplerBorderColor, RgbaFloat>(),
            };
        }
    }
}
