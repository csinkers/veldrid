﻿#if !EXCLUDE_OPENGL_BACKEND
using System;
using System.Collections.ObjectModel;
using System.Linq;
using Veldrid.OpenGL;
using Veldrid.OpenGLBindings;

namespace Veldrid;

/// <summary>
/// Delegate type for OpenGL debugger logging callbacks
/// </summary>
public unsafe delegate void OpenGLDebugMessageCallback(
    uint source,
    uint type,
    uint id,
    uint severity,
    uint length,
    byte* message
);

/// <summary>
/// Exposes OpenGL-specific functionality,
/// useful for interoperating with native components which interface directly with OpenGL.
/// Can only be used on <see cref="GraphicsBackend.OpenGL"/> or <see cref="GraphicsBackend.OpenGLES"/>.
/// </summary>
public class BackendInfoOpenGL
{
    readonly OpenGLGraphicsDevice _gd;
    readonly ReadOnlyCollection<string> _extensions;

    internal BackendInfoOpenGL(OpenGLGraphicsDevice gd)
    {
        _gd = gd;
        _extensions = new(gd.Extensions.ToArray());
    }

    /// <summary>
    /// Gets the Version string of this OpenGL implementation.
    /// </summary>
    /// <remarks>
    /// The string begins with a version number. The version number uses one of these forms:
    /// <list type="bullet">
    ///   <item>
    ///     <description>major_number.minor_number</description>
    ///   </item>
    ///   <item>
    ///     <description>major_number.minor_number.release_number</description>
    ///   </item>
    /// </list>
    /// <para>
    /// Vendor-specific information may follow the version number.
    /// Its format depends on the implementation, but a space always separates the version number and the vendor-specific information.
    /// </para>
    /// </remarks>
    public string Version => _gd.Version;

    /// <summary>
    /// Gets the Shader Language Version string of this OpenGL implementation.
    /// </summary>
    /// <remarks>
    /// The string begins with a version number. The version number uses one of these forms:
    /// <list type="bullet">
    ///   <item>
    ///     <description>major_number.minor_number</description>
    ///   </item>
    ///   <item>
    ///     <description>major_number.minor_number.release_number</description>
    ///   </item>
    /// </list>
    /// <para>
    /// Vendor-specific information may follow the version number.
    /// Its format depends on the implementation, but a space always separates the version number and the vendor-specific information.
    /// </para>
    /// </remarks>
    public string ShadingLanguageVersion => _gd.ShadingLanguageVersion;

    /// <summary>
    /// Gets a collection of available OpenGL extensions.
    /// </summary>
    public ReadOnlyCollection<string> Extensions => _extensions;

    /// <summary>
    /// An event which is invoked when the OpenGL implementation reports a debug message.
    /// </summary>
    /// <remarks>
    /// <see cref="GraphicsDeviceOptions.Debug"/> must have been true to enable this.
    /// </remarks>
    public event OpenGLDebugMessageCallback? DebugProc;

    /// <summary>
    /// Executes the given delegate in the OpenGL device's main execution thread.
    /// In the delegate, OpenGL commands can be executed directly.
    /// This method does not return until the delegate's execution is fully completed.
    /// </summary>
    public void ExecuteOnGLThread(Action action) => ExecuteOnGLThread(action, true);

    /// <summary>
    /// Executes the given delegate in the OpenGL device's main execution thread.
    /// In the delegate, OpenGL commands can be executed directly.
    /// </summary>
    public void ExecuteOnGLThread(Action action, bool wait) => _gd.ExecuteOnGLThread(action, wait);

    /// <summary>
    /// Executes a glFlush and a glFinish command, and waits for their completion.
    /// </summary>
    public void FlushAndFinish() => _gd.FlushAndFinish();

    /// <summary>
    /// Gets the name of the OpenGL texture object wrapped by the given Veldrid Texture.
    /// </summary>
    /// <returns>The Veldrid Texture's underlying OpenGL texture name.</returns>
    public uint GetTextureName(Texture texture) =>
        Util.AssertSubtype<Texture, OpenGLTexture>(texture).Texture;

    /// <summary>
    /// Sets the texture target of the OpenGL texture object wrapped by the given Veldrid Texture to to a custom value.
    /// This could be used to set platform specific texture target values like Veldrid.OpenGLBinding.TextureTarget.TextureExternalOes.
    /// </summary>
    public void SetTextureTarget(Texture texture, uint textureTarget)
    {
        Util.AssertSubtype<Texture, OpenGLTexture>(texture).TextureTarget =
            (TextureTarget)textureTarget;
    }

    internal unsafe bool InvokeDebugProc(
        DebugSource source,
        DebugType type,
        uint id,
        DebugSeverity severity,
        uint length,
        byte* message
    )
    {
        OpenGLDebugMessageCallback? debugProc = DebugProc;
        if (debugProc != null)
        {
            debugProc.Invoke((uint)source, (uint)type, id, (uint)severity, length, message);
            return true;
        }

        return false;
    }
}
#endif
