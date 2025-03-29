using System.Numerics;

namespace Veldrid.Utilities;
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

public struct FrustumCorners
{
    public Vector3 NearTopLeft;
    public Vector3 NearTopRight;
    public Vector3 NearBottomLeft;
    public Vector3 NearBottomRight;
    public Vector3 FarTopLeft;
    public Vector3 FarTopRight;
    public Vector3 FarBottomLeft;
    public Vector3 FarBottomRight;
}
