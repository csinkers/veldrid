using System;

namespace Veldrid.SDL2;

/// <summary>
/// Event fired when text is dropped on a window.
/// </summary>
public ref struct DropTextEvent(ReadOnlySpan<byte> textUtf8, uint timestamp, uint windowId)
{
    /// <summary>
    /// The dropped text in UTF8.
    /// </summary>
    public ReadOnlySpan<byte> TextUtf8 { get; } = textUtf8;

    /// <summary>
    /// Timestamp of the event.
    /// </summary>
    public uint Timestamp { get; } = timestamp;

    /// <summary>
    /// The window that was dropped on, if any.
    /// </summary>
    public uint WindowID { get; } = windowId;
}
