using System.Numerics;
using System.Runtime.CompilerServices;

namespace Veldrid.Utilities;
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

public struct BoundingFrustum(
    Plane left,
    Plane right,
    Plane bottom,
    Plane top,
    Plane near,
    Plane far
)
{
    public Plane Left = left;
    public Plane Right = right;
    public Plane Bottom = bottom;
    public Plane Top = top;
    public Plane Near = near;
    public Plane Far = far;

    public BoundingFrustum(in Matrix4x4 m)
        : this(
            Plane.Normalize(new(m.M14 + m.M11, m.M24 + m.M21, m.M34 + m.M31, m.M44 + m.M41)),
            Plane.Normalize(new(m.M14 - m.M11, m.M24 - m.M21, m.M34 - m.M31, m.M44 - m.M41)),
            Plane.Normalize(new(m.M14 + m.M12, m.M24 + m.M22, m.M34 + m.M32, m.M44 + m.M42)),
            Plane.Normalize(new(m.M14 - m.M12, m.M24 - m.M22, m.M34 - m.M32, m.M44 - m.M42)),
            Plane.Normalize(new(m.M13, m.M23, m.M33, m.M43)),
            Plane.Normalize(new(m.M14 - m.M13, m.M24 - m.M23, m.M34 - m.M33, m.M44 - m.M43))
        )
    {
        // Plane computations: http://gamedevs.org/uploads/fast-extraction-viewing-frustum-planes-from-world-view-projection-matrix.pdf
    }

    public readonly ContainmentType Contains(Vector3 point)
    {
        ref Plane planes = ref Unsafe.AsRef(in Left);

        for (nuint i = 0; i < 6; i++)
            if (Plane.DotCoordinate(Unsafe.Add(ref planes, i), point) < 0)
                return ContainmentType.Disjoint;

        return ContainmentType.Contains;
    }

    public readonly ContainmentType Contains(BoundingSphere sphere)
    {
        ref Plane planes = ref Unsafe.AsRef(in Left);

        ContainmentType result = ContainmentType.Contains;
        for (nuint i = 0; i < 6; i++)
        {
            float distance = Plane.DotCoordinate(Unsafe.Add(ref planes, i), sphere.Center);
            if (distance < -sphere.Radius)
                return ContainmentType.Disjoint;

            if (distance < sphere.Radius)
                result = ContainmentType.Intersects;
        }

        return result;
    }

    public readonly ContainmentType Contains(BoundingBox box)
    {
        ref Plane planes = ref Unsafe.AsRef(in Left);

        ContainmentType result = ContainmentType.Contains;
        for (nuint i = 0; i < 6; i++)
        {
            Plane plane = Unsafe.Add(ref planes, i);

            // Approach: http://zach.in.tu-clausthal.de/teaching/cg_literatur/lighthouse3d_view_frustum_culling/index.html

            Vector3 positive = new(box.Min.X, box.Min.Y, box.Min.Z);
            Vector3 negative = new(box.Max.X, box.Max.Y, box.Max.Z);

            if (plane.Normal.X >= 0)
            {
                positive.X = box.Max.X;
                negative.X = box.Min.X;
            }
            if (plane.Normal.Y >= 0)
            {
                positive.Y = box.Max.Y;
                negative.Y = box.Min.Y;
            }
            if (plane.Normal.Z >= 0)
            {
                positive.Z = box.Max.Z;
                negative.Z = box.Min.Z;
            }

            // If the positive vertex is outside (behind plane), the box is disjoint.
            float positiveDistance = Plane.DotCoordinate(plane, positive);
            if (positiveDistance < 0)
                return ContainmentType.Disjoint;

            // If the negative vertex is outside (behind plane), the box is intersecting.
            // Because the above check failed, the positive vertex is in front of the plane,
            // and the negative vertex is behind. Thus, the box is intersecting this plane.
            float negativeDistance = Plane.DotCoordinate(plane, negative);
            if (negativeDistance < 0)
                result = ContainmentType.Intersects;
        }

        return result;
    }

    public readonly unsafe ContainmentType Contains(in BoundingFrustum other)
    {
        int pointsContained = 0;
        other.GetCorners(out FrustumCorners corners);
        Vector3* cornersPtr = (Vector3*)&corners;
        for (nuint i = 0; i < 8; i++)
            if (Contains(cornersPtr[i]) != ContainmentType.Disjoint)
                pointsContained++;

        return pointsContained switch
        {
            8 => ContainmentType.Contains,
            0 => ContainmentType.Disjoint,
            _ => ContainmentType.Intersects
        };
    }

    public readonly FrustumCorners GetCorners()
    {
        GetCorners(out FrustumCorners corners);
        return corners;
    }

    public readonly void GetCorners(out FrustumCorners corners)
    {
        PlaneIntersection(Near, Top, Left, out corners.NearTopLeft);
        PlaneIntersection(Near, Top, Right, out corners.NearTopRight);
        PlaneIntersection(Near, Bottom, Left, out corners.NearBottomLeft);
        PlaneIntersection(Near, Bottom, Right, out corners.NearBottomRight);
        PlaneIntersection(Far, Top, Left, out corners.FarTopLeft);
        PlaneIntersection(Far, Top, Right, out corners.FarTopRight);
        PlaneIntersection(Far, Bottom, Left, out corners.FarBottomLeft);
        PlaneIntersection(Far, Bottom, Right, out corners.FarBottomRight);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void PlaneIntersection(Plane p1, Plane p2, Plane p3, out Vector3 intersection)
    {
        // Formula: http://geomalgorithms.com/a05-_intersect-1.html
        // The formula assumes that there is only a single intersection point.
        // Because of the way the frustum planes are constructed, this should be guaranteed.
        intersection =
            (
                -(p1.D * Vector3.Cross(p2.Normal, p3.Normal))
                - (p2.D * Vector3.Cross(p3.Normal, p1.Normal))
                - (p3.D * Vector3.Cross(p1.Normal, p2.Normal))
            ) / Vector3.Dot(p1.Normal, Vector3.Cross(p2.Normal, p3.Normal));
    }
}
