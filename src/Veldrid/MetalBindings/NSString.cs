using System;
using System.Runtime.InteropServices;
using static Veldrid.MetalBindings.ObjectiveCRuntime;

namespace Veldrid.MetalBindings;

internal readonly unsafe struct NSString(IntPtr ptr)
{
    public readonly IntPtr NativePtr = ptr;

    public static implicit operator IntPtr(NSString nss) => nss.NativePtr;

    public static NSString New(ReadOnlySpan<char> s)
    {
        NSString nss = s_class.Alloc<NSString>();

        // initWithCharacters crashes if the pointer is null.
        if (s.IsEmpty)
        {
            s = string.Empty;
        }

        fixed (char* utf16Ptr = s)
        {
            UIntPtr length = (UIntPtr)s.Length;
            IntPtr newString = IntPtr_objc_msgSend(
                nss,
                sel_initWithCharacters,
                (IntPtr)utf16Ptr,
                length
            );
            return new(newString);
        }
    }

    public string GetValue()
    {
        byte* utf8Ptr = bytePtr_objc_msgSend(NativePtr, sel_utf8String);
        return MTLUtil.GetUtf8String(utf8Ptr);
    }

    public ReadOnlySpan<byte> GetValueUtf8()
    {
        byte* utf8Ptr = bytePtr_objc_msgSend(NativePtr, sel_utf8String);
        return MemoryMarshal.CreateReadOnlySpanFromNullTerminated(utf8Ptr);
    }

    static readonly ObjCClass s_class = new("NSString"u8);
    static readonly Selector sel_initWithCharacters = "initWithCharacters:length:"u8;
    static readonly Selector sel_utf8String = "UTF8String"u8;
}
