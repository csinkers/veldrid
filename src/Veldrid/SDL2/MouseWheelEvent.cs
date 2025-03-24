using System.Numerics;

namespace Veldrid.SDL2;

/// <summary>
/// Represents a mouse wheel event.
/// </summary>
public readonly struct MouseWheelEvent(uint Timestamp, uint WindowId, Vector2 WheelDelta);

