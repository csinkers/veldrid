using System;
using static Veldrid.MetalBindings.ObjectiveCRuntime;

namespace Veldrid.MetalBindings;

internal readonly struct MTLRenderPassStencilAttachmentDescriptor(IntPtr nativePtr)
{
    public readonly IntPtr NativePtr = nativePtr;

    public MTLTexture texture
    {
        get => new(IntPtr_objc_msgSend(NativePtr, Selectors.texture));
        set => objc_msgSend(NativePtr, Selectors.setTexture, value.NativePtr);
    }

    public MTLLoadAction loadAction
    {
        get => (MTLLoadAction)uint_objc_msgSend(NativePtr, Selectors.loadAction);
        set => objc_msgSend(NativePtr, Selectors.setLoadAction, (uint)value);
    }

    public MTLStoreAction storeAction
    {
        get => (MTLStoreAction)uint_objc_msgSend(NativePtr, Selectors.storeAction);
        set => objc_msgSend(NativePtr, Selectors.setStoreAction, (uint)value);
    }

    public uint clearStencil
    {
        get => uint_objc_msgSend(NativePtr, sel_clearStencil);
        set => objc_msgSend(NativePtr, sel_setClearStencil, value);
    }

    public UIntPtr slice
    {
        get => UIntPtr_objc_msgSend(NativePtr, Selectors.slice);
        set => objc_msgSend(NativePtr, Selectors.setSlice, value);
    }

    static readonly Selector sel_clearStencil = "clearStencil"u8;
    static readonly Selector sel_setClearStencil = "setClearStencil:"u8;
}
