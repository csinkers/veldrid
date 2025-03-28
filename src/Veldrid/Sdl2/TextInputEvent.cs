﻿using System;
using System.Text;

namespace Veldrid.Sdl2;
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

public readonly ref struct TextInputEvent(uint timestamp, uint windowId, ReadOnlySpan<Rune> runes)
{
    public uint Timestamp { get; } = timestamp;
    public uint WindowID { get; } = windowId;
    public ReadOnlySpan<Rune> Runes { get; } = runes;
}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
