using System;
using System.Numerics;

namespace Veldrid.Sdl2;

/// <summary>
/// Represents a rectangle for SDL2.
/// </summary>
public struct Rectangle(int x, int y, int width, int height) : IEquatable<Rectangle>
{
    /// <summary>
    /// The X coordinate of the rectangle.
    /// </summary>
    public readonly int X = x;
    /// <summary>
    /// The Y coordinate of the rectangle.
    /// </summary>
    public readonly int Y = y;
    /// <summary>
    /// The width of the rectangle.
    /// </summary>
    public readonly int Width = width;
    /// <summary>
    /// The height of the rectangle.
    /// </summary>
    public readonly int Height = height;

    /// <summary>
    /// Constructs a new <see cref="Rectangle"/> with the specified position and size.
    /// </summary>
    public Rectangle(Point topLeft, Point size)
        : this(topLeft.X, topLeft.Y, size.X, size.Y) { }

    /// <summary>
    /// The left edge of the rectangle.
    /// </summary>
    public readonly int Left => X;
    /// <summary>
    /// The right edge of the rectangle.
    /// </summary>
    public readonly int Right => X + Width;
    /// <summary>
    /// The top edge of the rectangle.
    /// </summary>
    public readonly int Top => Y;
    /// <summary>
    /// The bottom edge of the rectangle.
    /// </summary>
    public readonly int Bottom => Y + Height;

    /// <summary>
    /// The position of the rectangle.
    /// </summary>
    public readonly Vector2 Position => new(X, Y);
    /// <summary>
    /// The size of the rectangle.
    /// </summary>
    public readonly Vector2 Size => new(Width, Height);

    /// <summary>
    /// Check if the rectangle contains the specified point.
    /// </summary>
    public readonly bool Contains(Point p) => Contains(p.X, p.Y);

    /// <summary>
    /// Check if the rectangle contains the specified point.
    /// </summary>
    public readonly bool Contains(int x, int y) => X <= x && X + Width > x && Y <= y && Y + Height > y;

    /// <summary>
    /// Compare two <see cref="Rectangle"/>s for equality.
    /// </summary>
    public readonly bool Equals(Rectangle other) =>
        X.Equals(other.X)
        && Y.Equals(other.Y)
        && Width.Equals(other.Width)
        && Height.Equals(other.Height);

    /// <summary>
    /// Compare two <see cref="Rectangle"/>s for equality.
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public readonly override bool Equals(object? obj) => obj is Rectangle r && Equals(r);

    /// <summary>
    /// Get the hash code for the <see cref="Rectangle"/>.
    /// </summary>
    /// <returns></returns>
    public readonly override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);

    /// <summary>
    /// Compare two <see cref="Rectangle"/>s for equality.
    /// </summary>
    public static bool operator ==(Rectangle left, Rectangle right) => left.Equals(right);

    /// <summary>
    /// Compare two <see cref="Rectangle"/>s for inequality.
    /// </summary>
    public static bool operator !=(Rectangle left, Rectangle right) => !left.Equals(right);
}
