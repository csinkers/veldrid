using System;
using System.Runtime.InteropServices;
using static Veldrid.MetalBindings.ObjectiveCRuntime;

namespace Veldrid.MetalBindings;

[StructLayout(LayoutKind.Sequential)]
internal readonly struct MTLRenderPassDescriptor
{
    static readonly ObjCClass s_class = new("MTLRenderPassDescriptor"u8);

    public readonly IntPtr NativePtr;

    public static MTLRenderPassDescriptor New() => s_class.AllocInit<MTLRenderPassDescriptor>();

    public MTLRenderPassColorAttachmentDescriptorArray colorAttachments =>
        new(IntPtr_objc_msgSend(NativePtr, sel_colorAttachments));

    public MTLRenderPassDepthAttachmentDescriptor depthAttachment =>
        new(IntPtr_objc_msgSend(NativePtr, sel_depthAttachment));

    public MTLRenderPassStencilAttachmentDescriptor stencilAttachment =>
        new(IntPtr_objc_msgSend(NativePtr, sel_stencilAttachment));

    static readonly Selector sel_colorAttachments = "colorAttachments"u8;
    static readonly Selector sel_depthAttachment = "depthAttachment"u8;
    static readonly Selector sel_stencilAttachment = "stencilAttachment"u8;
}
