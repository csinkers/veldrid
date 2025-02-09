using System;

namespace Veldrid;

internal readonly struct MappedResourceCacheKey(MappableResource resource, uint subresource)
    : IEquatable<MappedResourceCacheKey>
{
    public readonly MappableResource Resource = resource ?? throw new ArgumentNullException(nameof(resource));
    public readonly uint Subresource = subresource;

    public bool Equals(MappedResourceCacheKey other)
    {
        return Resource.Equals(other.Resource)
               && Subresource.Equals(other.Subresource);
    }

    public override int GetHashCode()
    {
        return HashHelper.Combine(Resource.GetHashCode(), Subresource.GetHashCode());
    }
}