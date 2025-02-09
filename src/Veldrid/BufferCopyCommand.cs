using System.Diagnostics;

namespace Veldrid;

/// <summary>
/// Represents a copy operation between a source and destination buffer.
/// </summary>
[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public struct BufferCopyCommand(ulong readOffset, ulong writeOffset, ulong length)
{
    /// <summary>
    /// An offset into the source at which the copy region begins.
    /// </summary>
    public readonly ulong ReadOffset = readOffset;

    /// <summary>
    /// An offset into the destination at which the data will be copied.
    /// </summary>
    public readonly ulong WriteOffset = writeOffset;

    /// <summary>
    /// The number of bytes to copy.
    /// </summary>
    public readonly ulong Length = length;

    /// <summary>
    /// Returns a string representation of the command.
    /// </summary>
    public readonly override string ToString() =>
        $"Copy {Length} from {ReadOffset} to {WriteOffset}";

    readonly string GetDebuggerDisplay() => ToString();
}
