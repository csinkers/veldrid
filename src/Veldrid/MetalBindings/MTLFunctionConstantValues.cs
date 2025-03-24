using System;

namespace Veldrid.MetalBindings;

internal readonly struct MTLFunctionConstantValues(IntPtr nativePtr)
{
    public readonly IntPtr NativePtr = nativePtr;

    public static MTLFunctionConstantValues New()
    {
        return s_class.AllocInit<MTLFunctionConstantValues>();
    }

    public unsafe void setConstantValuetypeatIndex(void* value, MTLDataType type, UIntPtr index)
    {
        ObjectiveCRuntime.objc_msgSend(
            NativePtr,
            sel_setConstantValuetypeatIndex,
            value,
            (uint)type,
            index
        );
    }

    static readonly ObjCClass s_class = new("MTLFunctionConstantValues"u8);

    static readonly Selector sel_setConstantValuetypeatIndex = "setConstantValue:type:atIndex:"u8;
}
