using System;
using static Veldrid.MetalBindings.ObjectiveCRuntime;

namespace Veldrid.MetalBindings;

internal readonly struct MTLVertexAttributeDescriptor(IntPtr ptr)
{
    public readonly IntPtr NativePtr = ptr;

    public MTLVertexFormat format
    {
        get => (MTLVertexFormat)uint_objc_msgSend(NativePtr, sel_format);
        set => objc_msgSend(NativePtr, sel_setFormat, (uint)value);
    }

    public UIntPtr offset
    {
        get => UIntPtr_objc_msgSend(NativePtr, sel_offset);
        set => objc_msgSend(NativePtr, sel_setOffset, value);
    }

    public UIntPtr bufferIndex
    {
        get => UIntPtr_objc_msgSend(NativePtr, sel_bufferIndex);
        set => objc_msgSend(NativePtr, sel_setBufferIndex, value);
    }

    static readonly Selector sel_format = "format"u8;
    static readonly Selector sel_setFormat = "setFormat:"u8;
    static readonly Selector sel_offset = "offset"u8;
    static readonly Selector sel_setOffset = "setOffset:"u8;
    static readonly Selector sel_bufferIndex = "bufferIndex"u8;
    static readonly Selector sel_setBufferIndex = "setBufferIndex:"u8;
}
