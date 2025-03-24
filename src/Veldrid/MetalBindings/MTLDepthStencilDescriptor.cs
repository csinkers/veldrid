using System;
using static Veldrid.MetalBindings.ObjectiveCRuntime;

namespace Veldrid.MetalBindings;

internal readonly struct MTLDepthStencilDescriptor(IntPtr ptr)
{
    public readonly IntPtr NativePtr = ptr;

    public MTLCompareFunction depthCompareFunction
    {
        get => (MTLCompareFunction)uint_objc_msgSend(NativePtr, sel_depthCompareFunction);
        set => objc_msgSend(NativePtr, sel_setDepthCompareFunction, (uint)value);
    }

    public Bool8 depthWriteEnabled
    {
        get => bool8_objc_msgSend(NativePtr, sel_isDepthWriteEnabled);
        set => objc_msgSend(NativePtr, sel_setDepthWriteEnabled, value);
    }

    public MTLStencilDescriptor backFaceStencil
    {
        get => new(IntPtr_objc_msgSend(NativePtr, sel_backFaceStencil));
        set => objc_msgSend(NativePtr, sel_setBackFaceStencil, value.NativePtr);
    }

    public MTLStencilDescriptor frontFaceStencil
    {
        get => new(IntPtr_objc_msgSend(NativePtr, sel_frontFaceStencil));
        set => objc_msgSend(NativePtr, sel_setFrontFaceStencil, value.NativePtr);
    }

    static readonly Selector sel_depthCompareFunction = "depthCompareFunction"u8;
    static readonly Selector sel_setDepthCompareFunction = "setDepthCompareFunction:"u8;
    static readonly Selector sel_isDepthWriteEnabled = "isDepthWriteEnabled"u8;
    static readonly Selector sel_setDepthWriteEnabled = "setDepthWriteEnabled:"u8;
    static readonly Selector sel_backFaceStencil = "backFaceStencil"u8;
    static readonly Selector sel_setBackFaceStencil = "setBackFaceStencil:"u8;
    static readonly Selector sel_frontFaceStencil = "frontFaceStencil"u8;
    static readonly Selector sel_setFrontFaceStencil = "setFrontFaceStencil:"u8;
}
