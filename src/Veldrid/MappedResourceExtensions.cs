using System;
using System.Runtime.InteropServices;

namespace Veldrid;

/// <summary>
/// Extension methods for <see cref="MappedResourceView{T}"/>.
/// </summary>
public static class MappedResourceExtensions
{
    /// <summary>
    /// Returns a <see cref="Span{T}"/> which represents the data in the <see cref="MappedResourceView{T}"/>.
    /// </summary>
    public static Span<T> AsSpan<T>(this MappedResourceView<T> resource)
        where T : unmanaged => MemoryMarshal.CreateSpan(ref resource[0], resource.Count);

    /// <summary>
    /// Returns a <see cref="Span{T}"/> which represents the data in the <see cref="MappedResourceView{T}"/>.
    /// </summary>
    public static Span<T> AsSpan<T>(this MappedResourceView<T> resource, uint start)
        where T : unmanaged =>
        start >= (uint)resource.Count
            ? throw new ArgumentOutOfRangeException(nameof(start))
            : MemoryMarshal.CreateSpan(ref resource[start], resource.Count - (int)start);

    /// <summary>
    /// Returns a <see cref="Span{T}"/> which represents the data in the <see cref="MappedResourceView{T}"/>.
    /// </summary>
    public static Span<T> AsSpan<T>(this MappedResourceView<T> resource, int start)
        where T : unmanaged => resource.AsSpan((uint)start);
}
