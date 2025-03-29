using System;
using System.Numerics;

namespace Veldrid.Utilities;

/// <summary>
/// Various utility methods for working with Veldrid.
/// </summary>
public static class VdUtilities
{
    /// <summary>
    /// Creates a <see cref="Quaternion"/> which rotates from one direction to another.
    /// Code adapted from https://bitbucket.org/sinbad/ogre/src/9db75e3ba05c/OgreMain/include/OgreVector3.h
    /// Based on Stan Melax's article in Game Programming Gems
    /// </summary>
    public static Quaternion FromToRotation(
        Vector3 from,
        Vector3 to,
        Vector3 fallbackAxis = default)
    {
        Vector3 v0 = Vector3.Normalize(from);
        Vector3 v1 = Vector3.Normalize(to);

        float d = Vector3.Dot(v0, v1);
        if (d >= 1.0f) // If dot == 1, vectors are the same
            return Quaternion.Identity;

        if (d < 1e-6f - 1.0f)
        {
            if (fallbackAxis != Vector3.Zero)
                return Quaternion.CreateFromAxisAngle(fallbackAxis, (float)Math.PI); // rotate 180 degrees about the fallback axis

            // Generate an axis
            Vector3 axis = Vector3.Cross(Vector3.UnitX, from);
            if (axis.LengthSquared() == 0) // pick another if collinear
                axis = Vector3.Cross(Vector3.UnitY, from);

            axis = Vector3.Normalize(axis);
            return Quaternion.CreateFromAxisAngle(axis, (float)Math.PI);
        }

        float s = (float)Math.Sqrt((1 + d) * 2);
        float invs = 1.0f / s;

        Vector3 c = Vector3.Cross(v0, v1);

        Quaternion q = new(
            c.X * invs,
            c.Y * invs,
            c.Z * invs,
            s * 0.5f);

        return Quaternion.Normalize(q);
    }
}
