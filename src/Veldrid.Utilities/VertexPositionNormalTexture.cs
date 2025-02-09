using System.Numerics;

namespace Veldrid.Utilities;

public struct VertexPositionNormalTexture(Vector3 position, Vector3 normal, Vector2 texCoords)
{
    public const byte SizeInBytes = 32;
    public const byte NormalOffset = 12;
    public const byte TextureCoordinatesOffset = 24;
    public const byte ElementCount = 3;

    public readonly Vector3 Position = position;
    public readonly Vector3 Normal = normal;
    public readonly Vector2 TextureCoordinates = texCoords;
}