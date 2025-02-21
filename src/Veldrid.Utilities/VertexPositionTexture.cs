﻿using System.Numerics;

namespace Veldrid.Utilities;

public struct VertexPositionTexture(Vector3 position, Vector2 texCoords)
{
    public const byte SizeInBytes = 20;
    public const byte TextureCoordinatesOffset = 12;
    public const byte ElementCount = 2;

    public readonly Vector3 Position = position;
    public readonly Vector2 TextureCoordinates = texCoords;
}
