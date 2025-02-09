using System;
using static Veldrid.MetalBindings.ObjectiveCRuntime;

namespace Veldrid.MetalBindings;

public struct MTLPipelineBufferDescriptorArray(IntPtr nativePtr)
{
    public readonly IntPtr NativePtr = nativePtr;

    public MTLPipelineBufferDescriptor this[uint index]
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
