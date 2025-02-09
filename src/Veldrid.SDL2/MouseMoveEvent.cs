using System.Numerics;

namespace Veldrid.SDL2;

public readonly struct MouseMoveEvent(
    uint timestamp,
    uint windowId,
    Vector2 mousePosition,
    Vector2 delta
)
{
    public uint Timestamp { get; } = timestamp;
    public uint WindowID { get; } = windowId;
    public Vector2 MousePosition { get; } = mousePosition;
    public Vector2 Delta { get; } = delta;
}
