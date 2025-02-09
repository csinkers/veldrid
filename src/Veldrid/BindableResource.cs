using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Veldrid;

/// <summary>
/// A resource which can be bound in a <see cref="ResourceSet"/> and used in a shader.
/// </summary>
/// <remarks>
/// See <see cref="DeviceBuffer"/>, <see cref="DeviceBufferRange"/>, <see cref="Texture"/>, <see cref="TextureView"/>
/// and <see cref="Sampler"/>.
/// </remarks>
public readonly struct BindableResource : IEquatable<BindableResource>
{
    /// <summary>
    /// The kind of resource this instance represents.
    /// </summary>
    public BindableResourceKind Kind { get; }

    /// <summary>
    /// The resource object.
    /// </summary>
    public object? Resource { get; }

    BindableResource(BindableResourceKind kind, object? resource)
    {
        Debug.Assert(
            kind != BindableResourceKind.Null || resource == null,
            "Non-null resource for Null kind."
        );

        Kind = resource != null ? kind : BindableResourceKind.Null;
        Resource = resource;
    }

    [MemberNotNullWhen(true, nameof(Resource))]
    bool IsNotNull
    {
        get
        {
            if (Kind != BindableResourceKind.Null)
            {
                Debug.Assert(Resource != null);
                return true;
            }
            return false;
        }
    }

    /// <summary>Casts the underlying object to a <see cref="Texture"/>.</summary>
    public Texture GetTexture() => As<Texture>(BindableResourceKind.Texture);

    /// <summary>Casts the underlying object to a <see cref="TextureView"/>.</summary>
    public TextureView GetTextureView() => As<TextureView>(BindableResourceKind.TextureView);

    /// <summary>Casts the underlying object to a <see cref="DeviceBuffer"/>.</summary>
    public DeviceBuffer GetDeviceBuffer() => As<DeviceBuffer>(BindableResourceKind.DeviceBuffer);

    /// <summary>Casts the underlying object to a <see cref="DeviceBufferRange"/>.</summary>
    public DeviceBufferRange GetDeviceBufferRange() =>
        Unbox<DeviceBufferRange>(BindableResourceKind.DeviceBufferRange);

    /// <summary>Casts the underlying object to a <see cref="Sampler"/>.</summary>
    public Sampler GetSampler() => As<Sampler>(BindableResourceKind.Sampler);

    /// <summary>
    /// Determines whether the specified <see cref="BindableResource"/> is equal to the current <see cref="BindableResource"/>.
    /// </summary>
    public bool Equals(BindableResource other) => // We can check Kind as a fast path, which also saves us from doing actual null checks.
        Kind == other.Kind && IsNotNull && Resource.Equals(other.Resource);

    /// <summary>
    /// Gets the hash code for this instance.
    /// </summary>
    public override int GetHashCode() => Resource?.GetHashCode() ?? 0; // Including the Kind in the hash is pointless since Resource must be of the right kind.

    T As<T>(BindableResourceKind kind)
        where T : class
    {
        if (Kind == kind)
        {
            Debug.Assert(Resource is T, "Resource is not of the correct Kind.");
            return Unsafe.As<T>(Resource)!;
        }

        ThrowMismatch(kind, Kind);
        return null!;
    }

    T Unbox<T>(BindableResourceKind kind)
        where T : struct
    {
        if (Kind == kind)
        {
            Debug.Assert(Resource is T, "Resource is not of the correct Kind.");
            return Unsafe.Unbox<T>(Resource!);
        }

        ThrowMismatch(kind, Kind);
        return default!;
    }

    /// <summary>
    /// Converts the specified <see cref="Texture"/> to a <see cref="BindableResource"/>.
    /// </summary>
    public static implicit operator BindableResource(Texture? texture) =>
        new(BindableResourceKind.Texture, texture);

    /// <summary>
    /// Converts the specified <see cref="TextureView"/> to a <see cref="BindableResource"/>.
    /// </summary>
    public static implicit operator BindableResource(TextureView? textureView) =>
        new(BindableResourceKind.TextureView, textureView);

    /// <summary>
    /// Converts the specified <see cref="DeviceBuffer"/> to a <see cref="BindableResource"/>.
    /// </summary>
    public static implicit operator BindableResource(DeviceBuffer? deviceBuffer) =>
        new(BindableResourceKind.DeviceBuffer, deviceBuffer);

    /// <summary>
    /// Converts the specified <see cref="DeviceBufferRange"/> to a <see cref="BindableResource"/>.
    /// </summary>
    public static implicit operator BindableResource(DeviceBufferRange deviceBufferRange) =>
        new(BindableResourceKind.DeviceBufferRange, deviceBufferRange);

    /// <summary>
    /// Converts the specified <see cref="Sampler"/> to a <see cref="BindableResource"/>.
    /// </summary>
    public static implicit operator BindableResource(Sampler? sampler) =>
        new(BindableResourceKind.Sampler, sampler);

    static void ThrowMismatch(BindableResourceKind expected, BindableResourceKind actual) =>
        throw new VeldridException(
            $"Resource type mismatch. Expected {expected} but got {actual}."
        );
}
