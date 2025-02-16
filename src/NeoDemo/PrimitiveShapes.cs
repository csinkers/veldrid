using System.Numerics;
using Veldrid.Utilities;

namespace Veldrid.NeoDemo;

public static class PrimitiveShapes
{
    public static ConstructedMesh16 Plane(int width, int height, int uvUnit)
    {
        int hw = width / 2;
        int hh = height / 2;
        float halfWidth = hh;
        float halfHeight = hw;

        int uvScaleX = width / uvUnit;
        int uvScaleY = height / uvUnit;

        Vector2 uvScale = new(uvScaleX, uvScaleY);

        VertexPositionNormalTexture[] vertices =
        [
            new(new(-halfWidth, 0, -halfHeight), Vector3.UnitY, new Vector2(0, 0) * uvScale),
            new(new(halfWidth, 0, -halfHeight), Vector3.UnitY, new Vector2(1, 0) * uvScale),
            new(new(halfWidth, 0, halfHeight), Vector3.UnitY, new Vector2(1, 1) * uvScale),
            new(new(-halfWidth, 0, halfHeight), Vector3.UnitY, new Vector2(0, 1) * uvScale),
        ];

        ushort[] indices = [0, 1, 2, 0, 2, 3];

        return new(vertices, indices, null);
    }

    internal static ConstructedMesh16 Box(float width, float height, float depth, float uvUnit)
    {
        float halfWidth = width / 2;
        float halfHeight = height / 2;
        float halfDepth = depth / 2;

        Vector2 uvScale = new(width / uvUnit, height / uvUnit);

        VertexPositionNormalTexture[] vertices =
        [
            // Top
            new(
                new(-halfWidth, +halfHeight, -halfDepth),
                new(0, 1, 0),
                new Vector2(0, 0) * uvScale
            ),
            new(
                new(+halfWidth, +halfHeight, -halfDepth),
                new(0, 1, 0),
                new Vector2(1, 0) * uvScale
            ),
            new(
                new(+halfWidth, +halfHeight, +halfDepth),
                new(0, 1, 0),
                new Vector2(1, 1) * uvScale
            ),
            new(
                new(-halfWidth, +halfHeight, +halfDepth),
                new(0, 1, 0),
                new Vector2(0, 1) * uvScale
            ),
            // Bottom
            new(
                new(-halfWidth, -halfHeight, +halfDepth),
                new(0, -1, 0),
                new Vector2(0, 0) * uvScale
            ),
            new(
                new(+halfWidth, -halfHeight, +halfDepth),
                new(0, -1, 0),
                new Vector2(1, 0) * uvScale
            ),
            new(
                new(+halfWidth, -halfHeight, -halfDepth),
                new(0, -1, 0),
                new Vector2(1, 1) * uvScale
            ),
            new(
                new(-halfWidth, -halfHeight, -halfDepth),
                new(0, -1, 0),
                new Vector2(0, 1) * uvScale
            ),
            // Left
            new(
                new(-halfWidth, +halfHeight, -halfDepth),
                new(-1, 0, 0),
                new Vector2(0, 0) * uvScale
            ),
            new(
                new(-halfWidth, +halfHeight, +halfDepth),
                new(-1, 0, 0),
                new Vector2(1, 0) * uvScale
            ),
            new(
                new(-halfWidth, -halfHeight, +halfDepth),
                new(-1, 0, 0),
                new Vector2(1, 1) * uvScale
            ),
            new(
                new(-halfWidth, -halfHeight, -halfDepth),
                new(-1, 0, 0),
                new Vector2(0, 1) * uvScale
            ),
            // Right
            new(
                new(+halfWidth, +halfHeight, +halfDepth),
                new(1, 0, 0),
                new Vector2(0, 0) * uvScale
            ),
            new(
                new(+halfWidth, +halfHeight, -halfDepth),
                new(1, 0, 0),
                new Vector2(1, 0) * uvScale
            ),
            new(
                new(+halfWidth, -halfHeight, -halfDepth),
                new(1, 0, 0),
                new Vector2(1, 1) * uvScale
            ),
            new(
                new(+halfWidth, -halfHeight, +halfDepth),
                new(1, 0, 0),
                new Vector2(0, 1) * uvScale
            ),
            // Back
            new(
                new(+halfWidth, +halfHeight, -halfDepth),
                new(0, 0, -1),
                new Vector2(0, 0) * uvScale
            ),
            new(
                new(-halfWidth, +halfHeight, -halfDepth),
                new(0, 0, -1),
                new Vector2(1, 0) * uvScale
            ),
            new(
                new(-halfWidth, -halfHeight, -halfDepth),
                new(0, 0, -1),
                new Vector2(1, 1) * uvScale
            ),
            new(
                new(+halfWidth, -halfHeight, -halfDepth),
                new(0, 0, -1),
                new Vector2(0, 1) * uvScale
            ),
            // Front
            new(
                new(-halfWidth, +halfHeight, +halfDepth),
                new(0, 0, 1),
                new Vector2(0, 0) * uvScale
            ),
            new(
                new(+halfWidth, +halfHeight, +halfDepth),
                new(0, 0, 1),
                new Vector2(1, 0) * uvScale
            ),
            new(
                new(+halfWidth, -halfHeight, +halfDepth),
                new(0, 0, 1),
                new Vector2(1, 1) * uvScale
            ),
            new(
                new(-halfWidth, -halfHeight, +halfDepth),
                new(0, 0, 1),
                new Vector2(0, 1) * uvScale
            ),
        ];

        ushort[] indices =
        [
            0,
            1,
            2,
            0,
            2,
            3,
            4,
            5,
            6,
            4,
            6,
            7,
            8,
            9,
            10,
            8,
            10,
            11,
            12,
            13,
            14,
            12,
            14,
            15,
            16,
            17,
            18,
            16,
            18,
            19,
            20,
            21,
            22,
            20,
            22,
            23,
        ];

        return new(vertices, indices, null);
    }
}
