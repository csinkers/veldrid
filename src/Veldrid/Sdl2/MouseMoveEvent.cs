using System.Numerics;

namespace Veldrid.Sdl2;

/// <summary>
/// Represents a mouse move event.
/// </summary>
public record struct MouseMoveEvent(
    uint Timestamp,
    uint WindowId,
    Vector2 MousePosition,
    Vector2 Delta
);

