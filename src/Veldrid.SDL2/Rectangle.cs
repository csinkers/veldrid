using System;
using System.Numerics;

namespace Veldrid;

public struct Rectangle(int x, int y, int width, int height) : IEquatable<Rectangle>
{
    public int X = x;
    public int Y = y;
    public int Width = width;
    public int Height = height;

    public Rectangle(Point topLeft, Point size)
        : this(topLeft.X, topLeft.Y, size.X, size.Y) { }

    public readonly int Left => X;
    public readonly int Right => X + Width;
    public readonly int Top => Y;
    public readonly int Bottom => Y + Height;

    public readonly Vector2 Position => new(X, Y);
    public readonly Vector2 Size => new(Width, Height);

    public readonly bool Contains(Point p) => Contains(p.X, p.Y);

    public readonly bool Contains(int x, int y)
    {
        return (X <= x && (X + Width) > x) && (Y <= y && (Y + Height) > y);
    }

    public readonly bool Equals(Rectangle other)
    {
        return X.Equals(other.X)
            && Y.Equals(other.Y)
            && Width.Equals(other.Width)
            && Height.Equals(other.Height);
    }

    public override readonly bool Equals(object? obj) => obj is Rectangle r && Equals(r);

    public override readonly int GetHashCode() => HashCode.Combine(X, Y, Width, Height);

    public static bool operator ==(Rectangle left, Rectangle right) => left.Equals(right);

    public static bool operator !=(Rectangle left, Rectangle right) => !left.Equals(right);
}
