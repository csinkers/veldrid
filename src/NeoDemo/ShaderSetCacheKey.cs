﻿using System;
using Veldrid.SPIRV;

namespace Veldrid.NeoDemo;

public struct ShaderSetCacheKey(string name, SpecializationConstant[] specializations)
    : IEquatable<ShaderSetCacheKey>
{
    public string Name { get; } = name;
    public SpecializationConstant[] Specializations { get; } = specializations;

    public bool Equals(ShaderSetCacheKey other)
    {
        return Name.Equals(other.Name) && ArraysEqual(Specializations, other.Specializations);
    }

    public override int GetHashCode()
    {
        int hash = Name.GetHashCode();
        foreach (SpecializationConstant specConst in Specializations)
            hash ^= specConst.GetHashCode();

        return hash;
    }

    static bool ArraysEqual<T>(T[] a, T[] b)
        where T : IEquatable<T>
    {
        if (a.Length != b.Length)
            return false;

        for (int i = 0; i < a.Length; i++)
            if (!a[i].Equals(b[i]))
                return false;

        return true;
    }
}
