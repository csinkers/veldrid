using System.Numerics;

namespace Veldrid.Utilities;
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

/// <summary>
/// A vertex type containing a position and texture coordinates.
/// </summary>
public struct VertexPositionTexture(Vector3 position, Vector2 texCoords)
{
    public const byte SizeInBytes = 20;
    public const byte TextureCoordinatesOffset = 12;
    public const byte ElementCount = 2;

    public readonly Vector3 Position = position;
    public readonly Vector2 TextureCoordinates = texCoords;
}
