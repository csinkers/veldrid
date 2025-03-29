using System.Numerics;

namespace Veldrid.Utilities;
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

/// <summary>
/// A vertex type containing only a position.
/// </summary>
public struct VertexPosition(Vector3 position)
{
    public const byte SizeInBytes = 12;
    public const byte ElementCount = 1;

    public readonly Vector3 Position = position;
}
