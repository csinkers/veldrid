using System.Numerics;

namespace Veldrid.Utilities;

public struct RayCastHit<T>(T item, Vector3 location, float distance)
{
    public readonly T Item = item;
    public readonly Vector3 Location = location;
    public readonly float Distance = distance;
}