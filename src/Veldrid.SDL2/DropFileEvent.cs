using System;

namespace Veldrid.Sdl2;

public readonly ref struct DropFileEvent(ReadOnlySpan<byte> fileNameUtf8, uint timestamp, uint windowId)
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