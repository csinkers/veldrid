﻿using System;
using System.Collections.Generic;

namespace Veldrid.Tests.Utilities;

internal class RgbaFloatFuzzyComparer : IEqualityComparer<RgbaFloat>
{
    public static RgbaFloatFuzzyComparer Instance = new();

    public bool Equals(RgbaFloat x, RgbaFloat y)
    {
        return FuzzyEquals(x.R, y.R)
            && FuzzyEquals(x.G, y.G)
            && FuzzyEquals(x.B, y.B)
            && FuzzyEquals(x.A, y.A);
    }

    bool FuzzyEquals(float x, float y)
    {
        return Math.Abs(x - y) < 1e-5;
    }

    public int GetHashCode(RgbaFloat obj)
    {
        return obj.GetHashCode();
    }
}
