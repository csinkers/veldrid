﻿using System.Numerics;

namespace Veldrid.NeoDemo.Objects;

internal class MirrorMesh
{
    public static Plane Plane { get; set; } = new(Vector3.UnitY, 0);
}
