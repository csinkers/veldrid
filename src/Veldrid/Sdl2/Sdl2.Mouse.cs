﻿using System;
using System.Runtime.InteropServices;

namespace Veldrid.Sdl2;
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

/// <summary>
/// A transparent wrapper over a pointer representing an SDL Sdl2Cursor object.
/// </summary>
public struct SDL_Cursor(IntPtr pointer)
{
    /// <summary>
    /// The native SDL_Cursor pointer.
    /// </summary>
    public readonly IntPtr NativePointer = pointer;

    public static implicit operator IntPtr(SDL_Cursor Sdl2Cursor) => Sdl2Cursor.NativePointer;

    public static implicit operator SDL_Cursor(IntPtr pointer) => new(pointer);
}

/// <summary>
/// Cursor types for SDL_CreateSystemCursor().
/// </summary>
public enum SDL_SystemCursor
{
    Arrow,
    IBeam,
    Wait,
    Crosshair,
    WaitArrow,
    SizeNWSE,
    SizeNESW,
    SizeWE,
    SizeNS,
    SizeAll,
    No,
    Hand,
}

public static partial class Sdl2Native
{
    public const int SDL_QUERY = -1;
    public const int SDL_DISABLE = 0;
    public const int SDL_ENABLE = 1;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate int SDL_ShowCursor_t(int toggle);

    static readonly SDL_ShowCursor_t s_sdl_showCursor = LoadFunction<SDL_ShowCursor_t>(
        "SDL_ShowCursor"
    );

    /// <summary>
    /// Toggle whether or not the cursor should be shown.
    /// </summary>
    public static int SDL_ShowCursor(int toggle) => s_sdl_showCursor(toggle);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate void SDL_WarpMouseInWindow_t(SDL_Window window, int x, int y);

    static readonly SDL_WarpMouseInWindow_t s_sdl_warpMouseInWindow =
        LoadFunction<SDL_WarpMouseInWindow_t>("SDL_WarpMouseInWindow");

    /// <summary>
    /// Move mouse position to the given position in the window.
    /// </summary>
    public static void SDL_WarpMouseInWindow(SDL_Window window, int x, int y) =>
        s_sdl_warpMouseInWindow(window, x, y);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate int SDL_SetRelativeMouseMode_t(bool enabled);

    static readonly SDL_SetRelativeMouseMode_t s_sdl_setRelativeMouseMode =
        LoadFunction<SDL_SetRelativeMouseMode_t>("SDL_SetRelativeMouseMode");

    /// <summary>
    /// Enable/disable relative mouse mode.
    /// If enabled mouse cursor will be hidden and only relative
    /// mouse motion events will be delivered, mouse position will not change.
    /// </summary>
    /// <returns>
    /// Returns 0 on success or a negative error code on failure; call SDL_GetError() for more information.
    /// If relative mode is not supported this returns -1.
    /// </returns>
    public static int SDL_SetRelativeMouseMode(bool enabled) => s_sdl_setRelativeMouseMode(enabled);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate int SDL_CaptureMouse_t(bool enabled);

    static readonly SDL_CaptureMouse_t s_sdl_captureMouse = LoadFunction<SDL_CaptureMouse_t>(
        "SDL_CaptureMouse"
    );

    /// <summary>
    /// Enable/disable capture mouse.
    /// If enabled mouse will also be tracked outside the window.
    /// </summary>
    /// <returns>
    /// Returns 0 on success or -1 if not supported; call SDL_GetError() for more information.
    /// </returns>
    public static int SDL_CaptureMouse(bool enabled) => s_sdl_captureMouse(enabled);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate void SDL_SetWindowGrab_t(SDL_Window window, bool grabbed);

    static readonly SDL_SetWindowGrab_t s_sdl_setWindowGrabbed = LoadFunction<SDL_SetWindowGrab_t>(
        "SDL_SetWindowGrab"
    );

    /// <summary>
    /// Enable/disable window grab mouse.
    /// If enabled mouse will be contained inside of window.
    /// </summary>
    public static void SDL_SetWindowGrab(SDL_Window window, bool grabbed) =>
        s_sdl_setWindowGrabbed(window, grabbed);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate SDL_Cursor SDL_CreateSystemCursor_t(SDL_SystemCursor id);

    static readonly SDL_CreateSystemCursor_t s_sdl_createSystemCursor =
        LoadFunction<SDL_CreateSystemCursor_t>("SDL_CreateSystemCursor");

    /// <summary>
    /// Create a system cursor.
    /// </summary>
    public static SDL_Cursor SDL_CreateSystemCursor(SDL_SystemCursor id) =>
        s_sdl_createSystemCursor(id);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate void SDL_FreeCursor_t(SDL_Cursor cursor);

    static readonly SDL_FreeCursor_t s_sdl_freeCursor = LoadFunction<SDL_FreeCursor_t>(
        "SDL_FreeCursor"
    );

    /// <summary>
    /// Free a cursor created with SDL_CreateCursor(), SDL_CreateColorCursor() or SDL_CreateSystemCursor().
    /// </summary>
    public static void SDL_FreeCursor(SDL_Cursor cursor) => s_sdl_freeCursor(cursor);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate SDL_Cursor SDL_GetDefaultCursor_t();

    static readonly SDL_GetDefaultCursor_t s_sdl_getDefaultCursor =
        LoadFunction<SDL_GetDefaultCursor_t>("SDL_GetDefaultCursor");

    /// <summary>
    /// Get the default cursor.
    /// </summary>
    public static SDL_Cursor SDL_GetDefaultCursor() => s_sdl_getDefaultCursor();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate void SDL_SetCursor_t(SDL_Cursor cursor);

    static readonly SDL_SetCursor_t s_sdl_setCursor = LoadFunction<SDL_SetCursor_t>(
        "SDL_SetCursor"
    );

    /// <summary>
    /// Set the active cursor.
    /// </summary>
    public static void SDL_SetCursor(SDL_Cursor cursor) => s_sdl_setCursor(cursor);
}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
