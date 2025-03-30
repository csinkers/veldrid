using System;
using System.Diagnostics;

namespace Veldrid;

/// <summary>
/// Represents a 2D point for SDL2.
/// </summary>
[DebuggerDisplay("{DebuggerDisplayString,nq}")]
public struct Point(int x, int y) : IEquatable<Point>
{
    /// <summary>
    /// The X coordinate of the point.
    /// </summary>
    public readonly int X = x;
    /// <summary>
    /// The Y coordinate of the point.
    /// </summary>
    public readonly int Y = y;

    /// <summary>
    /// Compare two <see cref="Point"/>s for equality.
    /// </summary>
    /// <param name="other"></param>
    /// <returns></returns>
    public readonly bool Equals(Point other) => X.Equals(other.X) && Y.Equals(other.Y);

    /// <summary>
    /// Compare two <see cref="Point"/>s for equality.
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public readonly override bool Equals(object? obj) => obj is Point p && Equals(p);

    /// <summary>
    /// Get the hash code for the <see cref="Point"/>.
    /// </summary>
    /// <returns></returns>
    public readonly override int GetHashCode() => HashCode.Combine(X, Y);

    /// <summary>
    /// Get a string representation of the <see cref="Point"/>.
    /// </summary>
    public readonly override string ToString() => $"({X}, {Y})";

    /// <summary>
    /// Compare two <see cref="Point"/>s for equality.
    /// </summary>
    public static bool operator ==(Point left, Point right) => left.Equals(right);

    /// <summary>
    /// Compare two <see cref="Point"/>s for inequality.
    /// </summary>
    public static bool operator !=(Point left, Point right) => !left.Equals(right);

    string DebuggerDisplayString => ToString();
}
