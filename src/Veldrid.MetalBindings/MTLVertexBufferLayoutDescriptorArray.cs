using System;
using static Veldrid.MetalBindings.ObjectiveCRuntime;

namespace Veldrid.MetalBindings;

public struct MTLVertexBufferLayoutDescriptorArray(IntPtr nativePtr)
{
    public readonly IntPtr NativePtr = nativePtr;

    public MTLVertexBufferLayoutDescriptor this[uint index]
    {
        get
        {
            IntPtr value = IntPtr_objc_msgSend(
                NativePtr,
                Selectors.objectAtIndexedSubscript,
                index
            );
            return new(value);
        }
        set =>
            objc_msgSend(NativePtr, Selectors.setObjectAtIndexedSubscript, value.NativePtr, index);
    }
}
