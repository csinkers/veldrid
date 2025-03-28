﻿using System;
using Veldrid.OpenGLBindings;
using Veldrid.SPIRV;
using static Veldrid.OpenGL.OpenGLUtil;
using static Veldrid.OpenGLBindings.OpenGLNative;

namespace Veldrid.OpenGL;

internal sealed unsafe class OpenGLShader : Shader, IOpenGLDeferredResource
{
    readonly OpenGLGraphicsDevice _gd;
    readonly ShaderType _shaderType;
    readonly StagingBlock _stagingBlock;

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

    public override bool IsDisposed => _disposeRequested;

    uint _shader;

    public uint Shader => _shader;

    public OpenGLShader(
        OpenGLGraphicsDevice gd,
        ShaderStages stage,
        StagingBlock stagingBlock,
        string entryPoint
    )
        : base(stage, entryPoint)
    {
#if VALIDATE_USAGE
        if (stage == ShaderStages.Compute && !gd.Extensions.ComputeShaders)
        {
            if (gd.BackendType == GraphicsBackend.OpenGLES)
            {
                throw new VeldridException("Compute shaders require OpenGL ES 3.1.");
            }
            else
            {
                throw new VeldridException(
                    "Compute shaders require OpenGL 4.3 or ARB_compute_shader."
                );
            }
        }
#endif
        _gd = gd;
        _shaderType = OpenGLFormats.VdToGLShaderType(stage);
        _stagingBlock = stagingBlock;
    }

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
            if (_gd.Extensions.KHR_Debug)
            {
                SetObjectLabel(ObjectLabelIdentifier.Shader, _shader, _name);
            }
        }
    }

    void CreateGLResources()
    {
        _shader = glCreateShader(_shaderType);
        CheckLastError();

        byte* textPtr = (byte*)_stagingBlock.Data;
        int length = (int)_stagingBlock.SizeInBytes;
        byte** textsPtr = &textPtr;

        glShaderSource(_shader, 1, textsPtr, &length);
        CheckLastError();

        glCompileShader(_shader);
        CheckLastError();

        int compileStatus = 0;
        glGetShaderiv(_shader, ShaderParameter.CompileStatus, &compileStatus);
        CheckLastError();

        if (compileStatus != 1)
        {
            int infoLogLength = 0;
            glGetShaderiv(_shader, ShaderParameter.InfoLogLength, &infoLogLength);
            CheckLastError();

            Span<byte> infoLog = stackalloc byte[4096];
            if (infoLogLength > infoLog.Length)
                infoLog = new byte[infoLogLength];

            uint returnedInfoLength;
            fixed (byte* infoLogPtr = infoLog)
            {
                glGetShaderInfoLog(_shader, (uint)infoLogLength, &returnedInfoLength, infoLogPtr);
                CheckLastError();
            }

            string message = Util.UTF8.GetString(infoLog[..(int)returnedInfoLength]);
            throw new VeldridException(
                $"Unable to compile shader code for shader [{_name}] of type {_shaderType}: {message}"
            );
        }

        _gd.StagingMemoryPool.Free(_stagingBlock);
        Created = true;
    }

    public override void Dispose()
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
        if (Created)
        {
            glDeleteShader(_shader);
            CheckLastError();
        }
        else
        {
            _gd.StagingMemoryPool.Free(_stagingBlock);
        }
    }
}
