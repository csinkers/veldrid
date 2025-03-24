using System.Numerics;

namespace Veldrid.SDL2;

/// <summary>
/// Represents a mouse wheel event.
/// </summary>
public record struct MouseWheelEvent(uint Timestamp, uint WindowId, Vector2 WheelDelta);

