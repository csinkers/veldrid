using System;

namespace Veldrid.Sdl2;
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

/// <summary>
/// A transparent wrapper over a pointer representing an SDL Sdl2Window object.
/// </summary>
public struct SDL_Window(IntPtr pointer)
{
    /// <summary>
    /// The native SDL_Window pointer.
    /// </summary>
    public readonly IntPtr NativePointer = pointer;

    public static implicit operator IntPtr(SDL_Window Sdl2Window) => Sdl2Window.NativePointer;

    public static implicit operator SDL_Window(IntPtr pointer) => new(pointer);
}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
