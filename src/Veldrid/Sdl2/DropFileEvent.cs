using System;

namespace Veldrid.Sdl2;

/// <summary>
/// Event for when a file is dropped on the window.
/// </summary>
public readonly ref struct DropFileEvent(
    ReadOnlySpan<byte> fileNameUtf8,
    uint timestamp,
    uint windowId
)
{
    /// <summary>
    /// The dropped file name in UTF8.
    /// </summary>
    public ReadOnlySpan<byte> FileNameUtf8 { get; } = fileNameUtf8;

    /// <summary>
    /// Timestamp of the event.
    /// </summary>
    public uint Timestamp { get; } = timestamp;

    /// <summary>
    /// The window that was dropped on, if any.
    /// </summary>
    public uint WindowID { get; } = windowId;
}
