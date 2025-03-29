namespace Veldrid.Sdl2;

/// <summary>
/// A keyboard event.
/// </summary>
public record struct KeyEvent(
    uint Timestamp,
    uint WindowId,
    bool Down,
    bool Repeat,
    Key Physical,
    VKey Virtual,
    ModifierKeys Modifiers)
{
    /// <summary>
    /// Format the key event as a string.
    /// </summary>
    public override string ToString()
        => $"{Physical}->{Virtual} {(Down ? "Down" : "Up") + (Repeat ? " Repeat" : "")} [{Modifiers}]";
}
