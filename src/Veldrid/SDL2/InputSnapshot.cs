using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Text;

namespace Veldrid.SDL2;

/// <summary>
/// Represents a snapshot of input state at a particular moment in time.
/// </summary>
public class InputSnapshot
{
    /// <summary>
    /// The list of input events representing printable characters entered by keyboard, IME etc.
    /// </summary>
    public List<Rune> InputEvents { get; } = [];

    /// <summary>
    /// The list of key events representing key presses and releases.
    /// </summary>
    public List<KeyEvent> KeyEvents { get; } = [];

    /// <summary>
    /// The list of mouse events representing mouse button presses and releases.
    /// </summary>
    public List<MouseButtonEvent> MouseEvents { get; } = [];

    /// <summary>
    /// The current position of the mouse cursor.
    /// </summary>
    public Vector2 MousePosition { get; set; }

    /// <summary>
    /// The current scroll wheel delta.
    /// </summary>
    public Vector2 WheelDelta { get; set; }

    /// <summary>
    /// The current mouse buttons that are down.
    /// </summary>
    public MouseButton MouseDown { get; set; }

    internal void Clear()
    {
        InputEvents.Clear();
        KeyEvents.Clear();
        MouseEvents.Clear();
        WheelDelta = Vector2.Zero;
    }

    /// <summary>
    /// Copies the state of this InputSnapshot to another InputSnapshot.
    /// </summary>
    public void CopyTo(InputSnapshot other)
    {
        Debug.Assert(this != other);

        other.InputEvents.Clear();
        other.InputEvents.AddRange(InputEvents);

        other.MouseEvents.Clear();
        other.MouseEvents.AddRange(MouseEvents);

        other.KeyEvents.Clear();
        other.KeyEvents.AddRange(KeyEvents);

        other.MousePosition = MousePosition;
        other.WheelDelta = WheelDelta;
        other.MouseDown = MouseDown;
    }
}
