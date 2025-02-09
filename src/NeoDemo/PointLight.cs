using System.Numerics;
using System.Runtime.InteropServices;

namespace Veldrid.NeoDemo;

[StructLayout(LayoutKind.Sequential)]
public struct PointLightInfo
{
    public Vector3 Position;
    float _padding0;
    public Vector3 Color;
    public float _padding1;
    public float Range;
    float _padding2;
    float _padding3;
    float _padding4;
}

[StructLayout(LayoutKind.Sequential)]
public struct PointLightsInfo
{
    public PointLightInfo[] PointLights;
    public int NumActiveLights;
    float _padding0;
    float _padding1;
    float _padding2;

    public Blittable GetBlittable()
    {
        return new()
        {
            NumActiveLights = NumActiveLights,
            PointLights0 = PointLights[0],
            PointLights1 = PointLights[1],
            PointLights2 = PointLights[2],
            PointLights3 = PointLights[3],
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Blittable
    {
        public PointLightInfo PointLights0;
        public PointLightInfo PointLights1;
        public PointLightInfo PointLights2;
        public PointLightInfo PointLights3;
        public int NumActiveLights;

        float _padding0;
        float _padding1;
        float _padding2;
    }
}
