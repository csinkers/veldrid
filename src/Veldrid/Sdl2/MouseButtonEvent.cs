namespace Veldrid.Sdl2;

/// <summary>
/// A mouse button up/down event
/// </summary>
public record struct MouseButtonEvent(
    uint Timestamp,
    uint WindowId,
    MouseButton MouseButton,
    bool Down,
    byte Clicks
);

