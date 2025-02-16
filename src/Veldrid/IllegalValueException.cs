using System.Diagnostics.CodeAnalysis;

// ReSharper disable UnusedTypeParameter

namespace Veldrid;

internal static class Illegal
{
    [DoesNotReturn]
    internal static void Value<T>() => throw new IllegalValueException<T>();

    [DoesNotReturn]
    internal static TReturn Value<T, TReturn>() => throw new IllegalValueException<T, TReturn>();

    internal class IllegalValueException<T> : VeldridException { }

    internal class IllegalValueException<T, TReturn> : IllegalValueException<T> { }
}
