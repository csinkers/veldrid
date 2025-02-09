using Veldrid.Utilities;

namespace Veldrid.NeoDemo;

public static class CubeModel
{
    public static readonly VertexPositionNormalTexture[] Vertices =
    [
        // Top
        new(new(-.5f, .5f, -.5f), new(0, 1, 0), new(0, 0)),
        new(new(.5f, .5f, -.5f), new(0, 1, 0), new(1, 0)),
        new(new(.5f, .5f, .5f), new(0, 1, 0), new(1, 1)),
        new(new(-.5f, .5f, .5f), new(0, 1, 0), new(0, 1)),
        // Bottom
        new(new(-.5f, -.5f, .5f), new(0, -1, 0), new(0, 0)),
        new(new(.5f, -.5f, .5f), new(0, -1, 0), new(1, 0)),
        new(new(.5f, -.5f, -.5f), new(0, -1, 0), new(1, 1)),
        new(new(-.5f, -.5f, -.5f), new(0, -1, 0), new(0, 1)),
        // Left
        new(new(-.5f, .5f, -.5f), new(-1, 0, 0), new(0, 0)),
        new(new(-.5f, .5f, .5f), new(-1, 0, 0), new(1, 0)),
        new(new(-.5f, -.5f, .5f), new(-1, 0, 0), new(1, 1)),
        new(new(-.5f, -.5f, -.5f), new(-1, 0, 0), new(0, 1)),
        // Right
        new(new(.5f, .5f, .5f), new(1, 0, 0), new(0, 0)),
        new(new(.5f, .5f, -.5f), new(1, 0, 0), new(1, 0)),
        new(new(.5f, -.5f, -.5f), new(1, 0, 0), new(1, 1)),
        new(new(.5f, -.5f, .5f), new(1, 0, 0), new(0, 1)),
        // Back
        new(new(.5f, .5f, -.5f), new(0, 0, -1), new(0, 0)),
        new(new(-.5f, .5f, -.5f), new(0, 0, -1), new(1, 0)),
        new(new(-.5f, -.5f, -.5f), new(0, 0, -1), new(1, 1)),
        new(new(.5f, -.5f, -.5f), new(0, 0, -1), new(0, 1)),
        // Front
        new(new(-.5f, .5f, .5f), new(0, 0, 1), new(0, 0)),
        new(new(.5f, .5f, .5f), new(0, 0, 1), new(1, 0)),
        new(new(.5f, -.5f, .5f), new(0, 0, 1), new(1, 1)),
        new(new(-.5f, -.5f, .5f), new(0, 0, 1), new(0, 1)),
    ];

    public static readonly ushort[] Indices =
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
}
