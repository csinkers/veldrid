using System;
using System.Runtime.InteropServices;
using static Veldrid.MetalBindings.ObjectiveCRuntime;

namespace Veldrid.MetalBindings;

[StructLayout(LayoutKind.Sequential)]
internal readonly unsafe struct MTLTexture(IntPtr ptr)
{
    public readonly IntPtr NativePtr = ptr;

    public bool IsNull => NativePtr == IntPtr.Zero;

    public void replaceRegion(
        MTLRegion region,
        UIntPtr mipmapLevel,
        UIntPtr slice,
        void* pixelBytes,
        UIntPtr bytesPerRow,
        UIntPtr bytesPerImage
    )
    {
        objc_msgSend(
            NativePtr,
            sel_replaceRegion,
            region,
            mipmapLevel,
            slice,
            (IntPtr)pixelBytes,
            bytesPerRow,
            bytesPerImage
        );
    }

    public MTLTexture newTextureView(
        MTLPixelFormat pixelFormat,
        MTLTextureType textureType,
        NSRange levelRange,
        NSRange sliceRange
    )
    {
        IntPtr ret = IntPtr_objc_msgSend(
            NativePtr,
            sel_newTextureView,
            (uint)pixelFormat,
            (uint)textureType,
            levelRange,
            sliceRange
        );
        return new(ret);
    }

    static readonly Selector sel_replaceRegion =
        "replaceRegion:mipmapLevel:slice:withBytes:bytesPerRow:bytesPerImage:"u8;
    static readonly Selector sel_newTextureView =
        "newTextureViewWithPixelFormat:textureType:levels:slices:"u8;
}
