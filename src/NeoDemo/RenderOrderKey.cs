﻿using System;
using System.Runtime.CompilerServices;

namespace Veldrid.NeoDemo;

public struct RenderOrderKey(ulong value) : IComparable<RenderOrderKey>
{
    public readonly ulong Value = value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RenderOrderKey Create(int materialID, float cameraDistance) =>
        Create((uint)materialID, cameraDistance);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RenderOrderKey Create(uint materialID, float cameraDistance)
    {
        uint cameraDistanceInt = (uint)Math.Min(uint.MaxValue, (cameraDistance * 1000f));

        return new(((ulong)materialID << 32) + cameraDistanceInt);
    }

    public int CompareTo(RenderOrderKey other)
    {
        return Value.CompareTo(other.Value);
    }
}
