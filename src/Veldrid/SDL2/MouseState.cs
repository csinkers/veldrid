namespace Veldrid.SDL2;

/// <summary>
/// The current state of the mouse
/// </summary>
public record struct MouseState(int X, int Y, MouseButton MouseDown)
{
    /// <summary>
    /// Check if a particular button is pressed
    /// </summary>
    public bool IsButtonDown(MouseButton button) => (MouseDown & button) != 0;
}
