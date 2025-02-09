using System;
using static Veldrid.MetalBindings.ObjectiveCRuntime;

namespace Veldrid.MetalBindings;

public readonly struct MTLRenderPassColorAttachmentDescriptor(IntPtr ptr)
{
    public readonly IntPtr NativePtr = ptr;

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

    public MTLTexture resolveTexture
    {
        get => new(IntPtr_objc_msgSend(NativePtr, Selectors.resolveTexture));
        set => objc_msgSend(NativePtr, Selectors.setResolveTexture, value.NativePtr);
    }

    public MTLClearColor clearColor
    {
        get
        {
            if (ObjectiveCRuntime.UseStret<MTLClearColor>())
            {
                return objc_msgSend_stret<MTLClearColor>(NativePtr, sel_clearColor);
            }
            else
            {
                return MTLClearColor_objc_msgSend(NativePtr, sel_clearColor);
            }
        }
        set => objc_msgSend(NativePtr, sel_setClearColor, value);
    }

    public UIntPtr slice
    {
        get => UIntPtr_objc_msgSend(NativePtr, Selectors.slice);
        set => objc_msgSend(NativePtr, Selectors.setSlice, value);
    }

    public UIntPtr level
    {
        get => UIntPtr_objc_msgSend(NativePtr, Selectors.level);
        set => objc_msgSend(NativePtr, Selectors.setLevel, value);
    }

    static readonly Selector sel_clearColor = "clearColor"u8;
    static readonly Selector sel_setClearColor = "setClearColor:"u8;
}
