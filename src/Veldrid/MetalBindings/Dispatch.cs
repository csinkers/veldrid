using System;
using System.Runtime.InteropServices;

namespace Veldrid.MetalBindings;

internal static unsafe class Dispatch
{
    const string LibdispatchLocation = @"/usr/lib/system/libdispatch.dylib";

    [DllImport(LibdispatchLocation)]
    public static extern DispatchQueue dispatch_get_global_queue(
        QualityOfServiceLevel identifier,
        ulong flags
    );

    [DllImport(LibdispatchLocation)]
    public static extern DispatchData dispatch_data_create(
        void* buffer,
        UIntPtr size,
        DispatchQueue queue,
        IntPtr destructorBlock
    );

    [DllImport(LibdispatchLocation)]
    public static extern void dispatch_release(IntPtr nativePtr);
}

internal enum QualityOfServiceLevel : long
{
    QOS_CLASS_USER_INTERACTIVE = 0x21,
    QOS_CLASS_USER_INITIATED = 0x19,
    QOS_CLASS_DEFAULT = 0x15,
    QOS_CLASS_UTILITY = 0x11,
    QOS_CLASS_BACKGROUND = 0x9,
    QOS_CLASS_UNSPECIFIED = 0,
}

#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
internal struct DispatchQueue
{
    public IntPtr NativePtr;
}

internal struct DispatchData
{
    public IntPtr NativePtr;
}
#pragma warning restore CS0649 // Field is never assigned to, and will always have its default value
