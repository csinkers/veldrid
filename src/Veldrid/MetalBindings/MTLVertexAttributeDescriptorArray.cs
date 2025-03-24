using System;
using static Veldrid.MetalBindings.ObjectiveCRuntime;

namespace Veldrid.MetalBindings;

internal struct MTLVertexAttributeDescriptorArray(IntPtr nativePtr)
{
    public readonly IntPtr NativePtr = nativePtr;

    public MTLVertexAttributeDescriptor this[uint index]
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
