using System;
using Veldrid.OpenGLBindings;
using static Veldrid.OpenGL.OpenGLUtil;
using static Veldrid.OpenGLBindings.OpenGLNative;

namespace Veldrid.OpenGL;

/// <summary>
/// A utility class managing the relationships between textures, samplers, and their binding locations.
/// </summary>
internal sealed unsafe class OpenGLTextureSamplerManager
{
    readonly bool _dsaAvailable;
    readonly uint _lastTextureUnit;
    readonly OpenGLTextureView?[] _textureUnitTextures;
    readonly BoundSamplerStateInfo[] _textureUnitSamplers;
    uint _currentActiveUnit;

    public OpenGLTextureSamplerManager(OpenGLExtensions extensions)
    {
        _dsaAvailable = extensions.ARB_DirectStateAccess;
        int maxTextureUnits;
        glGetIntegerv(GetPName.MaxCombinedTextureImageUnits, &maxTextureUnits);
        CheckLastError();

        int maxTextureUnits1 = Math.Max(maxTextureUnits, 8); // OpenGL spec indicates that implementations must support at least 8.
        _textureUnitTextures = new OpenGLTextureView[maxTextureUnits1];
        _textureUnitSamplers = new BoundSamplerStateInfo[maxTextureUnits1];

        _lastTextureUnit = (uint)(maxTextureUnits1 - 1);
    }

    public void SetTexture(uint textureUnit, OpenGLTextureView textureView)
    {
        uint textureID = textureView.GLTargetTexture;

        if (_textureUnitTextures[textureUnit] == textureView)
            return;

        if (_dsaAvailable)
        {
            glBindTextureUnit(textureUnit, textureID);
        }
        else
        {
            SetActiveTextureUnit(textureUnit);

            glBindTexture(textureView.TextureTarget, textureID);
        }
        CheckLastError();

        EnsureSamplerMipmapState(textureUnit, textureView.MipLevels > 1);
        _textureUnitTextures[textureUnit] = textureView;
    }

    public void SetTextureTransient(TextureTarget target, uint texture)
    {
        _textureUnitTextures[_lastTextureUnit] = null;
        SetActiveTextureUnit(_lastTextureUnit);

        glBindTexture(target, texture);
        CheckLastError();
    }

    public void SetSampler(uint textureUnit, OpenGLSampler sampler)
    {
        OpenGLTextureView? texBinding = _textureUnitTextures[textureUnit];
        if (_textureUnitSamplers[textureUnit].Sampler != sampler)
        {
            bool mipmapped = false;
            if (texBinding != null)
            {
                mipmapped = texBinding.MipLevels > 1;
            }

            uint samplerID = mipmapped ? sampler.MipmapSampler : sampler.NoMipmapSampler;
            glBindSampler(textureUnit, samplerID);
            CheckLastError();

            _textureUnitSamplers[textureUnit] = new(sampler, mipmapped);
        }
        else if (texBinding != null)
        {
            EnsureSamplerMipmapState(textureUnit, texBinding.MipLevels > 1);
        }
    }

    void SetActiveTextureUnit(uint textureUnit)
    {
        if (_currentActiveUnit == textureUnit)
            return;

        glActiveTexture(TextureUnit.Texture0 + (int)textureUnit);
        CheckLastError();
        _currentActiveUnit = textureUnit;
    }

    void EnsureSamplerMipmapState(uint textureUnit, bool mipmapped)
    {
        if (
            _textureUnitSamplers[textureUnit].Sampler == null
            || _textureUnitSamplers[textureUnit].Mipmapped == mipmapped
        )
        {
            return;
        }

        OpenGLSampler? sampler = _textureUnitSamplers[textureUnit].Sampler;
        if (sampler != null)
        {
            uint samplerID = mipmapped ? sampler.MipmapSampler : sampler.NoMipmapSampler;
            glBindSampler(textureUnit, samplerID);
            CheckLastError();
        }

        _textureUnitSamplers[textureUnit].Mipmapped = mipmapped;
    }

    struct BoundSamplerStateInfo(OpenGLSampler? sampler, bool mipmapped)
    {
        public readonly OpenGLSampler? Sampler = sampler;
        public bool Mipmapped = mipmapped;
    }
}
