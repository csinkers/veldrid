using System.Numerics;

namespace Veldrid.NeoDemo;

public struct ClipPlaneInfo(Plane clipPlane, bool enabled)
{
    public Vector4 ClipPlane = new(clipPlane.Normal, clipPlane.D);
    public int Enabled = enabled ? 1 : 0;
}
