using System;
using System.Runtime.InteropServices;
using static Veldrid.MetalBindings.ObjectiveCRuntime;

namespace Veldrid.MetalBindings;

[StructLayout(LayoutKind.Sequential)]
public readonly struct MTLLibrary(IntPtr ptr)
{
    public readonly IntPtr NativePtr = ptr;

    public MTLFunction newFunctionWithName(string name)
    {
        NSString nameNSS = NSString.New(name);
        IntPtr function = IntPtr_objc_msgSend(NativePtr, sel_newFunctionWithName, nameNSS);
        release(nameNSS.NativePtr);
        return new(function);
    }

    public unsafe MTLFunction newFunctionWithNameConstantValues(
        string name,
        MTLFunctionConstantValues constantValues
    )
    {
        NSString nameNSS = NSString.New(name);
        NSError error;
        IntPtr function = IntPtr_objc_msgSend(
            NativePtr,
            sel_newFunctionWithNameConstantValues,
            nameNSS.NativePtr,
            constantValues.NativePtr,
            &error
        );
        release(nameNSS.NativePtr);

        if (function == IntPtr.Zero)
        {
            throw new($"Failed to create MTLFunction: {error.localizedDescription}");
        }

        return new(function);
    }

    static readonly Selector sel_newFunctionWithName = "newFunctionWithName:"u8;
    static readonly Selector sel_newFunctionWithNameConstantValues =
        "newFunctionWithName:constantValues:error:"u8;
}
