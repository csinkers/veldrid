﻿namespace Veldrid.NeoDemo.Objects;

public static class CommonMaterials
{
    public static MaterialPropsAndBuffer Brick { get; }
    public static MaterialPropsAndBuffer Vase { get; }
    public static MaterialPropsAndBuffer Reflective { get; }

    static CommonMaterials()
    {
        Brick = new(new() { SpecularIntensity = new(0.2f), SpecularPower = 10f })
        {
            Name = "Brick",
        };
        Vase = new(new() { SpecularIntensity = new(1.0f), SpecularPower = 10f }) { Name = "Vase" };
        Reflective = new(
            new()
            {
                SpecularIntensity = new(0.2f),
                SpecularPower = 10f,
                Reflectivity = 0.3f,
            }
        )
        {
            Name = "Reflective",
        };
    }

    public static void CreateAllDeviceObjects(GraphicsDevice gd, CommandList cl, SceneContext sc)
    {
        Brick.CreateDeviceObjects(gd, cl, sc);
        Vase.CreateDeviceObjects(gd, cl, sc);
        Reflective.CreateDeviceObjects(gd, cl, sc);
    }

    public static void FlushAll(CommandList cl)
    {
        Brick.FlushChanges(cl);
        Vase.FlushChanges(cl);
        Reflective.FlushChanges(cl);
    }

    public static void DestroyAllDeviceObjects()
    {
        Brick.DestroyDeviceObjects();
        Vase.DestroyDeviceObjects();
        Reflective.DestroyDeviceObjects();
    }
}
