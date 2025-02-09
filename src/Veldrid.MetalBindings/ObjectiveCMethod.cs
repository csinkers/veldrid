using System;

namespace Veldrid.MetalBindings;

public struct ObjectiveCMethod(IntPtr ptr)
{
    public readonly IntPtr NativePtr = ptr;

    public static implicit operator IntPtr(ObjectiveCMethod method) => method.NativePtr;

    public static implicit operator ObjectiveCMethod(IntPtr ptr) => new(ptr);

    public Selector GetSelector() => ObjectiveCRuntime.method_getName(this);

    public string GetName() => GetSelector().Name;
}
