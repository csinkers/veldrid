using System;
using System.Runtime.InteropServices;
using static Veldrid.MetalBindings.ObjectiveCRuntime;

namespace Veldrid.MetalBindings;

[StructLayout(LayoutKind.Sequential)]
internal struct CAMetalDrawable(IntPtr nativePtr)
{
    public readonly IntPtr NativePtr = nativePtr;

    public bool IsNull => NativePtr == IntPtr.Zero;
    public MTLTexture texture => new(IntPtr_objc_msgSend(NativePtr, Selectors.texture));
}
