﻿using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Veldrid.Utilities;
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

public struct BoundingSphere(Vector3 center, float radius)
{
    public Vector3 Center = center;
    public readonly float Radius = radius;

    public readonly override string ToString() => $"Center:{Center}, Radius:{Radius}";
    public readonly bool Contains(Vector3 point) => (Center - point).LengthSquared() <= Radius * Radius;

    public static float GetMaxDistanceSquared(
        Vector3 center,
        ReadOnlySpan<byte> pointBytes,
        int pointStride
    )
    {
        nuint stride = (nuint)pointStride;
        nuint pointCount = (nuint)pointBytes.Length / stride;
        if (pointCount < 1)
        {
            return 0;
        }

        ref byte ptr = ref MemoryMarshal.GetReference(pointBytes);
        ref byte endPtr = ref Unsafe.Add(ref ptr, pointCount * stride);

        float maxDistanceSquared = 0;

        while (Unsafe.IsAddressLessThan(ref ptr, ref endPtr))
        {
            Vector3 point = Unsafe.ReadUnaligned<Vector3>(ref ptr);
            ptr = ref Unsafe.Add(ref ptr, stride);

            float distSq = Vector3.DistanceSquared(center, point);
            if (distSq > maxDistanceSquared)
            {
                maxDistanceSquared = distSq;
            }
        }

        return maxDistanceSquared;
    }

    public static Vector3 GetCenter(ReadOnlySpan<byte> pointBytes, int pointStride)
    {
        nuint stride = (nuint)pointStride;
        nuint pointCount = (nuint)pointBytes.Length / stride;
        if (pointCount < 1)
        {
            return Vector3.Zero;
        }

        ref byte ptr = ref MemoryMarshal.GetReference(pointBytes);
        ref byte endPtr = ref Unsafe.Add(ref ptr, pointCount * stride);

        Vector3 center = Vector3.Zero;

        while (Unsafe.IsAddressLessThan(ref ptr, ref endPtr))
        {
            Vector3 point = Unsafe.ReadUnaligned<Vector3>(ref ptr);
            ptr = ref Unsafe.Add(ref ptr, stride);

            center += point;
        }

        center /= pointCount;
        return center;
    }

    public static BoundingSphere CreateFromPoints(
        Vector3 center,
        ReadOnlySpan<byte> pointBytes,
        int pointStride)
    {
        float maxDistanceSquared = GetMaxDistanceSquared(center, pointBytes, pointStride);
        return new(center, MathF.Sqrt(maxDistanceSquared));
    }

    public static BoundingSphere CreateFromPoints(ReadOnlySpan<byte> pointBytes, int pointStride)
    {
        Vector3 center = GetCenter(pointBytes, pointStride);
        float radiusSquared = GetMaxDistanceSquared(center, pointBytes, pointStride);
        return new(center, MathF.Sqrt(radiusSquared));
    }
}
