﻿using System;
using System.Collections.Generic;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace Veldrid.D3D11;

internal sealed class D3D11Buffer : DeviceBuffer
{
    readonly ID3D11Device _device;
    readonly ID3D11Buffer _buffer;
    readonly object _accessViewLock = new();
    readonly Dictionary<OffsetSizePair, ID3D11ShaderResourceView> _srvs = new();
    readonly Dictionary<OffsetSizePair, ID3D11UnorderedAccessView> _uavs = new();
    readonly uint _structureByteStride;
    readonly bool _rawBuffer;
    string? _name;

    public override bool IsDisposed => _buffer.NativePointer == IntPtr.Zero;

    public ID3D11Buffer Buffer => _buffer;

    public D3D11Buffer(ID3D11Device device, in BufferDescription desc)
        : base(desc)
    {
        _device = device;
        _structureByteStride = desc.StructureByteStride;
        _rawBuffer = desc.RawBuffer;

        Vortice.Direct3D11.BufferDescription bd = new(
            (int)desc.SizeInBytes,
            D3D11Formats.VdToD3D11BindFlags(desc.Usage)
        );

        if (
            (desc.Usage & BufferUsage.StructuredBufferReadOnly)
                == BufferUsage.StructuredBufferReadOnly
            || (desc.Usage & BufferUsage.StructuredBufferReadWrite)
                == BufferUsage.StructuredBufferReadWrite
        )
        {
            if (desc.RawBuffer)
            {
                bd.MiscFlags |= ResourceOptionFlags.BufferAllowRawViews;
            }
            else
            {
                bd.MiscFlags |= ResourceOptionFlags.BufferStructured;
                bd.StructureByteStride = (int)desc.StructureByteStride;
            }
        }
        if ((desc.Usage & BufferUsage.IndirectBuffer) == BufferUsage.IndirectBuffer)
        {
            bd.MiscFlags |= ResourceOptionFlags.DrawIndirectArguments;
        }

        if ((desc.Usage & BufferUsage.DynamicReadWrite) != 0)
        {
            bd.Usage = ResourceUsage.Dynamic;

            if ((desc.Usage & BufferUsage.DynamicWrite) != 0)
                bd.CPUAccessFlags |= CpuAccessFlags.Write;
            if ((desc.Usage & BufferUsage.DynamicRead) != 0)
                bd.CPUAccessFlags |= CpuAccessFlags.Read;
        }
        else if ((desc.Usage & BufferUsage.StagingReadWrite) != 0)
        {
            bd.Usage = ResourceUsage.Staging;

            if ((desc.Usage & BufferUsage.StagingWrite) != 0)
                bd.CPUAccessFlags |= CpuAccessFlags.Write;
            if ((desc.Usage & BufferUsage.StagingRead) != 0)
                bd.CPUAccessFlags |= CpuAccessFlags.Read;
        }

        _buffer =
            desc.InitialData == IntPtr.Zero
                ? device.CreateBuffer(bd)
                : device.CreateBuffer(bd, desc.InitialData);
    }

    public override string? Name
    {
        get => _name;
        set
        {
            _name = value;
            Buffer.DebugName = value!;
            foreach (KeyValuePair<OffsetSizePair, ID3D11ShaderResourceView> kvp in _srvs)
                kvp.Value.DebugName = value + "_SRV";

            foreach (KeyValuePair<OffsetSizePair, ID3D11UnorderedAccessView> kvp in _uavs)
                kvp.Value.DebugName = value + "_UAV";
        }
    }

    public override void Dispose()
    {
        foreach (KeyValuePair<OffsetSizePair, ID3D11ShaderResourceView> kvp in _srvs)
            kvp.Value.Dispose();

        foreach (KeyValuePair<OffsetSizePair, ID3D11UnorderedAccessView> kvp in _uavs)
            kvp.Value.Dispose();

        _buffer.Dispose();
    }

    internal ID3D11ShaderResourceView GetShaderResourceView(uint offset, uint size)
    {
        lock (_accessViewLock)
        {
            OffsetSizePair pair = new(offset, size);
            if (!_srvs.TryGetValue(pair, out ID3D11ShaderResourceView? srv))
            {
                srv = CreateShaderResourceView(offset, size);
                _srvs.Add(pair, srv);
            }

            return srv;
        }
    }

    internal ID3D11UnorderedAccessView GetUnorderedAccessView(uint offset, uint size)
    {
        lock (_accessViewLock)
        {
            OffsetSizePair pair = new(offset, size);
            if (!_uavs.TryGetValue(pair, out ID3D11UnorderedAccessView? uav))
            {
                uav = CreateUnorderedAccessView(offset, size);
                _uavs.Add(pair, uav);
            }

            return uav;
        }
    }

    ID3D11ShaderResourceView CreateShaderResourceView(uint offset, uint size)
    {
        if (_rawBuffer)
        {
            ShaderResourceViewDescription srvDesc = new(
                _buffer,
                Format.R32_Typeless,
                (int)offset / 4,
                (int)size / 4,
                BufferExtendedShaderResourceViewFlags.Raw
            );

            return _device.CreateShaderResourceView(_buffer, srvDesc);
        }
        else
        {
            ShaderResourceViewDescription srvDesc = new()
            {
                ViewDimension = ShaderResourceViewDimension.Buffer,
            };
            srvDesc.Buffer.NumElements = (int)(size / _structureByteStride);
            srvDesc.Buffer.ElementOffset = (int)(offset / _structureByteStride);
            return _device.CreateShaderResourceView(_buffer, srvDesc);
        }
    }

    ID3D11UnorderedAccessView CreateUnorderedAccessView(uint offset, uint size)
    {
        if (_rawBuffer)
        {
            UnorderedAccessViewDescription uavDesc = new(
                _buffer,
                Format.R32_Typeless,
                (int)offset / 4,
                (int)size / 4,
                BufferUnorderedAccessViewFlags.Raw
            );

            return _device.CreateUnorderedAccessView(_buffer, uavDesc);
        }
        else
        {
            UnorderedAccessViewDescription uavDesc = new(
                _buffer,
                Format.Unknown,
                (int)(offset / _structureByteStride),
                (int)(size / _structureByteStride)
            );

            return _device.CreateUnorderedAccessView(_buffer, uavDesc);
        }
    }

    readonly struct OffsetSizePair(uint offset, uint size) : IEquatable<OffsetSizePair>
    {
        public readonly uint Offset = offset;
        public readonly uint Size = size;

        public bool Equals(OffsetSizePair other) =>
            Offset.Equals(other.Offset) && Size.Equals(other.Size);

        public override int GetHashCode() =>
            HashHelper.Combine(Offset.GetHashCode(), Size.GetHashCode());
    }
}
