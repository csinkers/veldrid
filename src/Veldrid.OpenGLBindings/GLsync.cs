using System;

namespace Veldrid.OpenGLBindings;

public readonly struct GLsync(IntPtr handle)
{
    public IntPtr Handle { get; } = handle;
}
