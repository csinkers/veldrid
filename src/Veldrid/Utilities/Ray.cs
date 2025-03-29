using System;
using System.Numerics;

namespace Veldrid.Utilities;

/// <summary>
/// Represents a ray in 3D space.
/// </summary>
/// <param name="origin">The position where the ray starts</param>
/// <param name="direction">The direction of the ray</param>
public struct Ray(Vector3 origin, Vector3 direction)
{
    /// <summary>
    /// The position where the ray starts.
    /// </summary>
    public Vector3 Origin = origin;

    /// <summary>
    /// The direction of the ray.
    /// </summary>
    public Vector3 Direction = direction;

    /// <summary>
    /// Gets the point at a given distance along the ray.
    /// </summary>
    public Vector3 GetPoint(float distance) => Origin + Direction * distance;

    /// <summary>
    /// Determines whether the ray intersects a <see cref="BoundingBox"/>.
    /// </summary>
    public bool Intersects(BoundingBox box, out float distance)
    {
        Vector3 dirFactor = new Vector3(1f) / Direction;
        Vector3 max = (box.Max - Origin) * dirFactor;
        Vector3 min = (box.Min - Origin) * dirFactor;
        Vector3 tminv = Vector3.Min(min, max);
        Vector3 tmaxv = Vector3.Max(min, max);

        float tmax = MathF.Min(MathF.Min(tmaxv.X, tmaxv.Y), tmaxv.Z);
        distance = tmax;

        // ray is intersecting AABB, but the whole AABB is behind us
        if (tmax < 0)
            return false;

        float tmin = MathF.Max(MathF.Max(tminv.X, tminv.Y), tminv.Z);

        // ray doesn't intersect AABB
        if (tmin > tmax)
            return false;

        distance = tmin;
        return true;
    }

    /// <summary>
    /// Determines whether the ray intersects a <see cref="BoundingBox"/>.
    /// </summary>
    public bool Intersects(BoundingBox box) => Intersects(box, out _);

    /// <summary>
    /// Applies a matrix transformation to the ray.
    /// </summary>
    public static Ray Transform(Ray ray, Matrix4x4 mat) =>
        new(
            Vector3.Transform(ray.Origin, mat),
            Vector3.Normalize(Vector3.TransformNormal(ray.Direction, mat))
        );

    /// <summary>
    /// Ray-Triangle Intersection, using the Möller–Trumbore intersection algorithm.
    /// </summary>
    /// <remarks>
    /// https:// en.wikipedia.org/wiki/M%C3%B6ller%E2%80%93Trumbore_intersection_algorithm
    /// </remarks>
    public bool Intersects(Vector3 V1, Vector3 V2, Vector3 V3, out float distance)
    {
        const float EPSILON = 1E-6f;

        // Find vectors for two edges sharing V1
        Vector3 e1 = V2 - V1;
        Vector3 e2 = V3 - V1;

        // Begin calculating determinant - also used to calculate u parameter
        Vector3 p = Vector3.Cross(Direction, e2);

        // if determinant is near zero, ray lies in plane of triangle or ray is parallel to plane of triangle
        float det = Vector3.Dot(e1, p);

        // NOT CULLIN
        if (det is > -EPSILON and < EPSILON)
        {
            distance = 0f;
            return false;
        }

        float invDet = 1.0f / det;

        // calculate distance from V1 to ray origin
        Vector3 T = Origin - V1;

        // Calculate u parameter and test bound
        float u = Vector3.Dot(T, p) * invDet;
        // The intersection lies outside the triangle
        if (u is < 0.0f or > 1.0f)
        {
            distance = 0f;
            return false;
        }

        // Prepare to test v parameter
        Vector3 q = Vector3.Cross(T, e1);

        // Calculate V parameter and test bound
        float v = Vector3.Dot(Direction, q) * invDet;
        // The intersection lies outside the triangle
        if (v < 0.0f || u + v > 1.0f)
        {
            distance = 0f;
            return false;
        }

        float t = Vector3.Dot(e2, q) * invDet;

        if (t > EPSILON)
        {
            // ray intersection
            distance = t;
            return true;
        }

        // No hit, no win
        distance = 0f;
        return false;
    }
}
