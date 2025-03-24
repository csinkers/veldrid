using System;

namespace Veldrid.OpenGLBindings;

internal readonly struct GLsync(IntPtr handle)
{
    public IntPtr Handle { get; } = handle;
}
