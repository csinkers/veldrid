using System;
using static Veldrid.MetalBindings.ObjectiveCRuntime;

namespace Veldrid.MetalBindings;

internal readonly struct MTLVertexDescriptor(IntPtr nativePtr)
{
    public readonly IntPtr NativePtr = nativePtr;

    public MTLVertexBufferLayoutDescriptorArray layouts =>
        new(IntPtr_objc_msgSend(NativePtr, sel_layouts));

    public MTLVertexAttributeDescriptorArray attributes =>
        new(IntPtr_objc_msgSend(NativePtr, sel_attributes));

    static readonly Selector sel_layouts = "layouts"u8;
    static readonly Selector sel_attributes = "attributes"u8;
}
