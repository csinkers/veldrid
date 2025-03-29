using System.Numerics;

namespace Veldrid.Utilities;

/// <summary>
/// Represents a ray intersection with an object.
/// </summary>
public struct RayCastHit<T>(T item, Vector3 location, float distance)
{
    /// <summary>
    /// The item that was hit.
    /// </summary>
    public readonly T Item = item;

    /// <summary>
    /// The location of the hit.
    /// </summary>
    public readonly Vector3 Location = location;

    /// <summary>
    /// The distance from the ray origin to the hit location.
    /// </summary>
    public readonly float Distance = distance;
}
