namespace Veldrid;

/// <summary>
/// Specifies the kind of resource in a <see cref="BindableResource"/>
/// </summary>
public enum BindableResourceKind : byte
{
    /// <summary>
    /// No resource is bound.
    /// </summary>
    Null,

    /// <summary>
    /// A texture.
    /// </summary>
    Texture,

    /// <summary>
    /// A view into a texture.
    /// </summary>
    TextureView,

    /// <summary>
    /// A buffer.
    /// </summary>
    DeviceBuffer,

    /// <summary>
    /// A range within a buffer.
    /// </summary>
    DeviceBufferRange,

    /// <summary>
    /// A sampler.
    /// </summary>
    Sampler,
}
