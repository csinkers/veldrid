using System;
using System.Text;

namespace Veldrid.Sdl2;

public readonly ref struct TextEditingEvent(
    uint timestamp,
    uint windowId,
    ReadOnlySpan<Rune> runes,
    int offset,
    int length
)
{
    public uint Timestamp { get; } = timestamp;
    public uint WindowID { get; } = windowId;
    public ReadOnlySpan<Rune> Runes { get; } = runes;
    public int Offset { get; } = offset;
    public int Length { get; } = length;
}
