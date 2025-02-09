using System;
using System.Text;

namespace Veldrid.Sdl2;

public readonly ref struct TextInputEvent(uint timestamp, uint windowId, ReadOnlySpan<Rune> runes)
{
    public uint Timestamp { get; } = timestamp;
    public uint WindowID { get; } = windowId;
    public ReadOnlySpan<Rune> Runes { get; } = runes;
}
