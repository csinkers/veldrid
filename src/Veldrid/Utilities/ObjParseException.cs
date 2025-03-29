using System;

namespace Veldrid.Utilities;

/// <summary>
/// An parsing error for Wavefront OBJ files.
/// </summary>
public class ObjParseException : Exception
{
    /// <summary>
    /// Creates a new <see cref="ObjParseException"/>.
    /// </summary>
    public ObjParseException(string message)
        : base(message) { }

    /// <summary>
    /// Creates a new <see cref="ObjParseException"/>.
    /// </summary>
    public ObjParseException(string message, Exception innerException)
        : base(message, innerException) { }
}
