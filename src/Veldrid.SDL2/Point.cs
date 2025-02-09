using System;
using System.Diagnostics;

namespace Veldrid;

[DebuggerDisplay("{DebuggerDisplayString,nq}")]
public struct Point(int x, int y) : IEquatable<Point>
{
    public readonly int X = x;
    public readonly int Y = y;

    public readonly bool Equals(Point other) => X.Equals(other.X) && Y.Equals(other.Y);

    public override readonly bool Equals(object? obj) => obj is Point p && Equals(p);

    public override readonly int GetHashCode() => HashCode.Combine(X, Y);

    public override readonly string ToString() => $"({X}, {Y})";

    public static bool operator ==(Point left, Point right) => left.Equals(right);

    public static bool operator !=(Point left, Point right) => !left.Equals(right);

    string DebuggerDisplayString => ToString();
}
