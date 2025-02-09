using System;
using System.Runtime.CompilerServices;

namespace Veldrid;

/// <summary>
/// Extension methods for the <see cref="BufferUsage"/> enum.
/// </summary>
public static class BufferUsageExtensions
{
    /// <summary>
    /// Returns a string representation of the given <see cref="BufferUsage"/> value.
    /// </summary>
    [SkipLocalsInit]
    public static string ToDisplayString(this BufferUsage usage)
    {
        const string separator = " | ";
        Span<char> buffer = stackalloc char[64];

        int offset = 0;
        string type = GetTypeFlagString(usage);
        type.CopyTo(buffer);
        offset += type.Length;

        string dynamic = GetDynamicFlagString(usage);
        if (dynamic.Length > 0)
        {
            if (offset != 0)
            {
                separator.CopyTo(buffer[offset..]);
                offset += separator.Length;
            }

            dynamic.CopyTo(buffer[offset..]);
            offset += dynamic.Length;
        }

        string staging = GetStagingFlagString(usage);

        if (staging.Length > 0)
        {
            if (offset != 0)
            {
                separator.CopyTo(buffer[offset..]);
                offset += separator.Length;
            }

            staging.CopyTo(buffer[offset..]);
            offset += staging.Length;
        }

        return buffer[..offset].ToString();
    }

    static string GetStagingFlagString(BufferUsage usage)
    {
        if ((usage & BufferUsage.StagingReadWrite) == BufferUsage.StagingReadWrite)
            return "StagingRW";

        if ((usage & BufferUsage.StagingRead) == BufferUsage.StagingRead)
            return "StagingRead";

        if ((usage & BufferUsage.StagingWrite) == BufferUsage.StagingWrite)
            return "StagingWrite";

        return "";
    }

    static string GetDynamicFlagString(BufferUsage usage)
    {
        if ((usage & BufferUsage.DynamicReadWrite) == BufferUsage.DynamicReadWrite)
            return "DynamicRW";

        if ((usage & BufferUsage.DynamicRead) == BufferUsage.DynamicRead)
            return "DynamicRead";

        if ((usage & BufferUsage.DynamicWrite) == BufferUsage.DynamicWrite)
            return "DynamicWrite";

        return "";
    }

    static string GetTypeFlagString(BufferUsage usage)
    {
        if ((usage & BufferUsage.VertexBuffer) == BufferUsage.VertexBuffer)
            return "Vertex";

        if ((usage & BufferUsage.IndexBuffer) == BufferUsage.IndexBuffer)
            return "Index";

        if ((usage & BufferUsage.UniformBuffer) == BufferUsage.UniformBuffer)
            return "Uniform";

        if ((usage & BufferUsage.StructuredBufferReadOnly) == BufferUsage.StructuredBufferReadOnly)
            return "Struct";

        if (
            (usage & BufferUsage.StructuredBufferReadWrite) == BufferUsage.StructuredBufferReadWrite
        )
            return "StructRW";

        if ((usage & BufferUsage.IndirectBuffer) == BufferUsage.IndirectBuffer)
            return "Indirect";

        return "";
    }
}
