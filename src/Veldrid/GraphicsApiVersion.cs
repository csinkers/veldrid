namespace Veldrid;

/// <summary>
/// Represents a graphics API version.
/// </summary>
public readonly struct GraphicsApiVersion(int major, int minor, int subminor, int patch)
{
    /// <summary>
    /// A version representing an unknown graphics API version.
    /// </summary>
    public static GraphicsApiVersion Unknown => default;

    /// <summary>
    /// The major version number.
    /// </summary>
    public int Major { get; } = major;

    /// <summary>
    /// The minor version number.
    /// </summary>
    public int Minor { get; } = minor;

    /// <summary>
    /// The sub-minor version number
    /// </summary>
    public int Subminor { get; } = subminor;

    /// <summary>
    /// The patch version number.
    /// </summary>
    public int Patch { get; } = patch;

    /// <summary>
    /// Whether the version is known.
    /// </summary>
    public bool IsKnown => Major != 0 && Minor != 0 && Subminor != 0 && Patch != 0;

    /// <summary>
    /// Returns a string representation of the version.
    /// </summary>
    public override string ToString() => $"{Major}.{Minor}.{Subminor}.{Patch}";

    /// <summary>
    /// Parses OpenGL version strings with either of following formats:
    /// <list type="bullet">
    ///   <item>
    ///     <description>major_number.minor_number</description>
    ///   </item>
    ///   <item>
    ///     <description>major_number.minor_number.release_number</description>
    ///   </item>
    /// </list>
    /// </summary>
    /// <param name="versionString">The OpenGL version string.</param>
    /// <param name="version">The parsed <see cref="GraphicsApiVersion"/>.</param>
    /// <returns>True whether the parse succeeded; otherwise false.</returns>
    public static bool TryParseGLVersion(string versionString, out GraphicsApiVersion version)
    {
        string[] versionParts = versionString.Split(' ')[0].Split('.');

        if (
            !int.TryParse(versionParts[0], out int major)
            || !int.TryParse(versionParts[1], out int minor)
        )
        {
            version = default;
            return false;
        }

        int releaseNumber = 0;
        if (versionParts.Length == 3)
        {
            if (!int.TryParse(versionParts[2], out releaseNumber))
            {
                version = default;
                return false;
            }
        }

        version = new(major, minor, 0, releaseNumber);
        return true;
    }
}
