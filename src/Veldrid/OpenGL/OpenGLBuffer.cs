﻿using System;
using System.Diagnostics;
using Veldrid.OpenGLBindings;
using static Veldrid.OpenGL.OpenGLUtil;
using static Veldrid.OpenGLBindings.OpenGLNative;

namespace Veldrid.OpenGL;

internal sealed class OpenGLBuffer : DeviceBuffer, IOpenGLDeferredResource
{
    readonly OpenGLGraphicsDevice _gd;
    uint _buffer;
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

    public uint Buffer => _buffer;
    public bool Created { get; private set; }
    public bool CanBufferSubData { get; private set; }

    public override bool IsDisposed => _disposeRequested;

    public OpenGLBuffer(OpenGLGraphicsDevice gd, in BufferDescription desc)
        : base(desc)
    {
        _gd = gd;

        if (desc.InitialData != IntPtr.Zero)
        {
            gd.CreateBuffer(this, desc.InitialData);
        }
    }

    public void EnsureResourcesCreated()
    {
        if (!Created)
            CreateGLResources(IntPtr.Zero);

        if (_nameChanged)
            UpdateObjectLabel();
    }

    void UpdateObjectLabel()
    {
        _nameChanged = false;

        if (_gd.Extensions.KHR_Debug)
            SetObjectLabel(ObjectLabelIdentifier.Buffer, _buffer, _name);
    }

    public static BufferStorageMask GetStorageMask(BufferUsage usage)
    {
        BufferStorageMask storageMask = 0;

        if ((usage & BufferUsage.StagingRead) != 0 || (usage & BufferUsage.DynamicRead) != 0)
        {
            storageMask |= BufferStorageMask.MapRead;
            storageMask |= BufferStorageMask.ClientStorage;
            storageMask |= BufferStorageMask.DynamicStorage;
        }

        if ((usage & BufferUsage.StagingWrite) != 0 || (usage & BufferUsage.DynamicWrite) != 0)
        {
            storageMask |= BufferStorageMask.MapWrite;
            storageMask |= BufferStorageMask.ClientStorage;
            storageMask |= BufferStorageMask.DynamicStorage;
        }

        return storageMask;
    }

    public static BufferUsageHint GetUsageHint(BufferUsage usage)
    {
        if ((usage & BufferUsage.StagingRead) != 0)
            return BufferUsageHint.StreamRead;

        if ((usage & BufferUsage.StagingWrite) != 0)
            return BufferUsageHint.StreamCopy;

        if ((usage & BufferUsage.DynamicRead) != 0)
            return BufferUsageHint.DynamicRead;

        if ((usage & BufferUsage.DynamicWrite) != 0)
            return BufferUsageHint.DynamicDraw;

        return BufferUsageHint.StaticDraw;
    }

    public unsafe void CreateGLResources(IntPtr initialData)
    {
        Debug.Assert(!Created);

        BufferStorageMask mask = GetStorageMask(Usage);

        if (_gd.Extensions.ARB_DirectStateAccess)
        {
            uint buffer;
            glCreateBuffers(1, &buffer);
            CheckLastError();
            _buffer = buffer;

            if (mask != 0 && _gd.Extensions.ARB_buffer_storage)
            {
                glNamedBufferStorage(_buffer, SizeInBytes, (void*)initialData, mask);
                CanBufferSubData = (mask & BufferStorageMask.DynamicStorage) != 0;
            }
            else
            {
                BufferUsageHint hint = GetUsageHint(Usage);
                glNamedBufferData(_buffer, SizeInBytes, (void*)initialData, hint);
                CanBufferSubData = true;
            }
            CheckLastError();
        }
        else
        {
            uint buffer;
            glGenBuffers(1, &buffer);
            CheckLastError();
            _buffer = buffer;

            glBindBuffer(BufferTarget.CopyWriteBuffer, _buffer);
            CheckLastError();

            if (mask != 0 && _gd.Extensions.ARB_buffer_storage)
            {
                glBufferStorage(
                    BufferTarget.CopyWriteBuffer,
                    SizeInBytes,
                    (void*)initialData,
                    mask
                );
                CanBufferSubData = (mask & BufferStorageMask.DynamicStorage) != 0;
            }
            else
            {
                BufferUsageHint hint = GetUsageHint(Usage);
                glBufferData(BufferTarget.CopyWriteBuffer, SizeInBytes, (void*)initialData, hint);
                CanBufferSubData = true;
            }
            CheckLastError();
        }

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

    public unsafe void DestroyGLResources()
    {
        uint buffer = _buffer;
        glDeleteBuffers(1, &buffer);
        CheckLastError();
        _buffer = buffer;
    }
}
