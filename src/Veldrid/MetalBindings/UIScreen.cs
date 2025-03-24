using System;
using static Veldrid.MetalBindings.ObjectiveCRuntime;

namespace Veldrid.MetalBindings;

internal readonly struct UIScreen(IntPtr ptr)
{
    public readonly IntPtr NativePtr = ptr;

    public CGFloat nativeScale => CGFloat_objc_msgSend(NativePtr, "nativeScale"u8);

    public static UIScreen mainScreen =>
        new(IntPtr_objc_msgSend(new ObjCClass("UIScreen"u8), "mainScreen"u8));
}
