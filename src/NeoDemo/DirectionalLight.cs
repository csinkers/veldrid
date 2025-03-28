﻿using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Veldrid.NeoDemo;

public class DirectionalLight
{
    RgbaFloat _color = RgbaFloat.White;
    public Transform Transform { get; } = new();

    public Vector3 Direction => Transform.Forward;

    public event Action<RgbaFloat>? ColorChanged;

    public RgbaFloat Color
    {
        get => _color;
        set
        {
            _color = value;
            ColorChanged?.Invoke(value);
        }
    }

    public DirectionalLight()
    {
        Vector3 lightDir = Vector3.Normalize(new(0.15f, -1f, -0.15f));
        Transform.Rotation = Util.FromToRotation(-Vector3.UnitZ, lightDir);
    }

    internal DirectionalLightInfo GetInfo()
    {
        return new() { Direction = Transform.Forward, Color = Color.ToVector4() };
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct DirectionalLightInfo
{
    public Vector3 Direction;
    float _padding;
    public Vector4 Color;
}
