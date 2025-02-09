using System;
using System.Runtime.InteropServices;
using static Veldrid.MetalBindings.ObjectiveCRuntime;

namespace Veldrid.MetalBindings;

[StructLayout(LayoutKind.Sequential)]
public struct MTLRenderPassColorAttachmentDescriptorArray(IntPtr nativePtr)
{
    public readonly IntPtr NativePtr = nativePtr;

    public MTLRenderPassColorAttachmentDescriptor this[uint index]
    {
        get
        {
            IntPtr value = IntPtr_objc_msgSend(
                NativePtr,
                Selectors.objectAtIndexedSubscript,
                (UIntPtr)index
            );
            return new(value);
        }
        set
        {
            objc_msgSend(
                NativePtr,
                Selectors.setObjectAtIndexedSubscript,
                value.NativePtr,
                (UIntPtr)index
            );
        }
    }
}
