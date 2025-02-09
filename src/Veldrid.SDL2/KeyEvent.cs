namespace Veldrid;

public readonly struct KeyEvent(
    uint timestamp,
    uint windowId,
    bool down,
    bool repeat,
    Key physical,
    VKey @virtual,
    ModifierKeys modifiers
)
{
    public uint Timestamp { get; } = timestamp;
    public uint WindowID { get; } = windowId;
    public bool Down { get; } = down;
    public bool Repeat { get; } = repeat;
    public Key Physical { get; } = physical;
    public VKey Virtual { get; } = @virtual;
    public ModifierKeys Modifiers { get; } = modifiers;

    public override string ToString()
    {
        return $"{Physical}->{Virtual} {(Down ? "Down" : "Up") + (Repeat ? " Repeat" : "")} [{Modifiers}]";
    }
}
