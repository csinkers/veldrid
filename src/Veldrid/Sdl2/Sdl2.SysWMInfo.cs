﻿using System;
using System.Runtime.InteropServices;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Veldrid.Sdl2;

public static unsafe partial class Sdl2Native
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate int SDL_GetWindowWMInfo_t(SDL_Window Sdl2Window, SDL_SysWMinfo* info);

    static readonly SDL_GetWindowWMInfo_t s_getWindowWMInfo = LoadFunction<SDL_GetWindowWMInfo_t>(
        "SDL_GetWindowWMInfo"
    );

    public static int SDL_GetWMWindowInfo(SDL_Window Sdl2Window, SDL_SysWMinfo* info) =>
        s_getWindowWMInfo(Sdl2Window, info);
}

public struct SDL_SysWMinfo
{
    public SDL_version version;
    public SysWMType subsystem;
    public WindowInfo info;
}

[StructLayout(LayoutKind.Sequential, Size = 100)]
public struct WindowInfo { }

public struct Win32WindowInfo
{
    /// <summary>
    /// The Sdl2Window handle.
    /// </summary>
    public IntPtr Sdl2Window;

    /// <summary>
    /// The Sdl2Window device context.
    /// </summary>
    public IntPtr hdc;

    /// <summary>
    /// The instance handle.
    /// </summary>
    public IntPtr hinstance;
}

public struct X11WindowInfo
{
    public IntPtr display;
    public IntPtr Sdl2Window;
}

public struct WaylandWindowInfo
{
    public IntPtr display;
    public IntPtr surface;
    public IntPtr shellSurface;
}

public struct CocoaWindowInfo
{
    /// <summary>
    /// The NSWindow* Cocoa window.
    /// </summary>
    public IntPtr Window;
}

public struct AndroidWindowInfo
{
    public IntPtr window;
    public IntPtr surface;
}

public enum SysWMType
{
    Unknown,
    Windows,
    X11,
    DirectFB,
    Cocoa,
    UIKit,
    Wayland,
    Mir,
    WinRT,
    Android,
    Vivante,
}
