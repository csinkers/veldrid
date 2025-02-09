using System;

namespace Veldrid.OpenGLBinding;

public readonly struct GLsync(IntPtr handle)
{
    public IntPtr Handle { get; } = handle;
}
