namespace Veldrid;

public readonly struct MouseButtonEvent(uint timestamp, uint windowId, MouseButton mouseButton, bool down, byte clicks)
{
    public uint Timestamp { get; } = timestamp;
    public uint WindowID { get; } = windowId;
    public MouseButton MouseButton { get; } = mouseButton;
    public bool Down { get; } = down;
    public byte Clicks { get; } = clicks;
}