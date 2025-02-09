using System.Numerics;

namespace Veldrid.Sdl2;

public readonly struct MouseWheelEvent(uint timestamp, uint windowId, Vector2 wheelDelta)
{
    public uint Timestamp { get; } = timestamp;
    public uint WindowID { get; } = windowId;
    public Vector2 WheelDelta { get; } = wheelDelta;
}