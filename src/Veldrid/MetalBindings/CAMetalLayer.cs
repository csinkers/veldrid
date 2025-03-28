using System;
using static Veldrid.MetalBindings.ObjectiveCRuntime;

namespace Veldrid.MetalBindings;

internal readonly struct CAMetalLayer(IntPtr ptr)
{
    public readonly IntPtr NativePtr = ptr;

    public static CAMetalLayer New() => s_class.AllocInit<CAMetalLayer>();

    public static bool TryCast(IntPtr layerPointer, out CAMetalLayer metalLayer)
    {
        var layerObject = new NSObject(layerPointer);

        if (layerObject.IsKindOfClass(s_class))
        {
            metalLayer = new(layerPointer);
            return true;
        }

        metalLayer = default;
        return false;
    }

    public MTLDevice device
    {
        get => new(IntPtr_objc_msgSend(NativePtr, sel_device));
        set => objc_msgSend(NativePtr, sel_setDevice, value);
    }

    public MTLPixelFormat pixelFormat
    {
        get => (MTLPixelFormat)uint_objc_msgSend(NativePtr, sel_pixelFormat);
        set => objc_msgSend(NativePtr, sel_setPixelFormat, (uint)value);
    }

    public Bool8 framebufferOnly
    {
        get => bool8_objc_msgSend(NativePtr, sel_framebufferOnly);
        set => objc_msgSend(NativePtr, sel_setFramebufferOnly, value);
    }

    public CGSize drawableSize
    {
        get => CGSize_objc_msgSend(NativePtr, sel_drawableSize);
        set => objc_msgSend(NativePtr, sel_setDrawableSize, value);
    }

    public CGRect frame
    {
        get => CGRect_objc_msgSend(NativePtr, "frame"u8);
        set => objc_msgSend(NativePtr, "setFrame:"u8, value);
    }

    public Bool8 opaque
    {
        get => bool8_objc_msgSend(NativePtr, "isOpaque"u8);
        set => objc_msgSend(NativePtr, "setOpaque:"u8, value);
    }

    public CAMetalDrawable nextDrawable() => new(IntPtr_objc_msgSend(NativePtr, sel_nextDrawable));

    public Bool8 displaySyncEnabled
    {
        get => bool8_objc_msgSend(NativePtr, "displaySyncEnabled"u8);
        set => objc_msgSend(NativePtr, "setDisplaySyncEnabled:"u8, value);
    }

    static readonly ObjCClass s_class = new("CAMetalLayer"u8);
    static readonly Selector sel_device = "device"u8;
    static readonly Selector sel_setDevice = "setDevice:"u8;
    static readonly Selector sel_pixelFormat = "pixelFormat"u8;
    static readonly Selector sel_setPixelFormat = "setPixelFormat:"u8;
    static readonly Selector sel_framebufferOnly = "framebufferOnly"u8;
    static readonly Selector sel_setFramebufferOnly = "setFramebufferOnly:"u8;
    static readonly Selector sel_drawableSize = "drawableSize"u8;
    static readonly Selector sel_setDrawableSize = "setDrawableSize:"u8;
    static readonly Selector sel_nextDrawable = "nextDrawable"u8;
}
