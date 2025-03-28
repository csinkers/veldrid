﻿using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Veldrid.Sdl2;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
public static unsafe partial class Sdl2Native
{
    /// <summary>
    /// A special sentinel value indicating that a newly-created window should be centered in the screen.
    /// </summary>
    public const int SDL_WINDOWPOS_CENTERED = 0x2FFF0000;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate SDL_Window SDL_CreateWindow_t(
        byte* title,
        int x,
        int y,
        int w,
        int h,
        SDL_WindowFlags flags
    );

    static readonly SDL_CreateWindow_t s_sdl_createWindow = LoadFunction<SDL_CreateWindow_t>(
        "SDL_CreateWindow"
    );

    [SkipLocalsInit]
    public static SDL_Window SDL_CreateWindow(
        ReadOnlySpan<char> title,
        int x,
        int y,
        int w,
        int h,
        SDL_WindowFlags flags
    )
    {
        Span<byte> titleBuffer = stackalloc byte[4096];

        IntPtr ptr = Utilities.GetNullTerminatedUtf8(title, ref titleBuffer);
        try
        {
            fixed (byte* titlePtr = titleBuffer)
            {
                return s_sdl_createWindow(titlePtr, x, y, w, h, flags);
            }
        }
        finally
        {
            Utilities.FreeUtf8(ptr);
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate SDL_Window SDL_CreateWindowFrom_t(IntPtr data);

    static readonly SDL_CreateWindowFrom_t s_sdl_createWindowFrom =
        LoadFunction<SDL_CreateWindowFrom_t>("SDL_CreateWindowFrom");

    public static SDL_Window SDL_CreateWindowFrom(IntPtr data) => s_sdl_createWindowFrom(data);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate void SDL_DestroyWindow_t(SDL_Window SDL2Window);

    static readonly SDL_DestroyWindow_t s_sdl_destroyWindow = LoadFunction<SDL_DestroyWindow_t>(
        "SDL_DestroyWindow"
    );

    public static void SDL_DestroyWindow(SDL_Window Sdl2Window) => s_sdl_destroyWindow(Sdl2Window);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate void SDL_GetWindowSize_t(SDL_Window SDL2Window, int* w, int* h);

    static readonly SDL_GetWindowSize_t s_getWindowSize = LoadFunction<SDL_GetWindowSize_t>(
        "SDL_GetWindowSize"
    );

    public static void SDL_GetWindowSize(SDL_Window Sdl2Window, int* w, int* h) =>
        s_getWindowSize(Sdl2Window, w, h);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate void SDL_GetWindowPosition_t(SDL_Window SDL2Window, int* x, int* y);

    static readonly SDL_GetWindowPosition_t s_getWindowPosition =
        LoadFunction<SDL_GetWindowPosition_t>("SDL_GetWindowPosition");

    public static void SDL_GetWindowPosition(SDL_Window Sdl2Window, int* x, int* y) =>
        s_getWindowPosition(Sdl2Window, x, y);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate void SDL_SetWindowPosition_t(SDL_Window SDL2Window, int x, int y);

    static readonly SDL_SetWindowPosition_t s_setWindowPosition =
        LoadFunction<SDL_SetWindowPosition_t>("SDL_SetWindowPosition");

    public static void SDL_SetWindowPosition(SDL_Window Sdl2Window, int x, int y) =>
        s_setWindowPosition(Sdl2Window, x, y);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate void SDL_SetWindowSize_t(SDL_Window SDL2Window, int w, int h);

    static readonly SDL_SetWindowSize_t s_setWindowSize = LoadFunction<SDL_SetWindowSize_t>(
        "SDL_SetWindowSize"
    );

    public static void SDL_SetWindowSize(SDL_Window Sdl2Window, int w, int h) =>
        s_setWindowSize(Sdl2Window, w, h);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate string SDL_GetWindowTitle_t(SDL_Window SDL2Window);

    static readonly SDL_GetWindowTitle_t s_getWindowTitle = LoadFunction<SDL_GetWindowTitle_t>(
        "SDL_GetWindowTitle"
    );

    public static string SDL_GetWindowTitle(SDL_Window Sdl2Window) => s_getWindowTitle(Sdl2Window);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate void SDL_SetWindowTitle_t(SDL_Window SDL2Window, byte* title);

    static readonly SDL_SetWindowTitle_t s_setWindowTitle = LoadFunction<SDL_SetWindowTitle_t>(
        "SDL_SetWindowTitle"
    );

    [SkipLocalsInit]
    public static void SDL_SetWindowTitle(SDL_Window Sdl2Window, ReadOnlySpan<char> title)
    {
        Span<byte> titleBuffer = stackalloc byte[4096];

        IntPtr ptr = Utilities.GetNullTerminatedUtf8(title, ref titleBuffer);
        try
        {
            fixed (byte* titlePtr = titleBuffer)
            {
                s_setWindowTitle(Sdl2Window, titlePtr);
            }
        }
        finally
        {
            Utilities.FreeUtf8(ptr);
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate SDL_WindowFlags SDL_GetWindowFlags_t(SDL_Window SDL2Window);

    static readonly SDL_GetWindowFlags_t s_getWindowFlags = LoadFunction<SDL_GetWindowFlags_t>(
        "SDL_GetWindowFlags"
    );

    public static SDL_WindowFlags SDL_GetWindowFlags(SDL_Window Sdl2Window) =>
        s_getWindowFlags(Sdl2Window);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate void SDL_SetWindowBordered_t(SDL_Window SDL2Window, uint bordered);

    static readonly SDL_SetWindowBordered_t s_setWindowBordered =
        LoadFunction<SDL_SetWindowBordered_t>("SDL_SetWindowBordered");

    public static void SDL_SetWindowBordered(SDL_Window Sdl2Window, uint bordered) =>
        s_setWindowBordered(Sdl2Window, bordered);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate void SDL_MaximizeWindow_t(SDL_Window SDL2Window);

    static readonly SDL_MaximizeWindow_t s_maximizeWindow = LoadFunction<SDL_MaximizeWindow_t>(
        "SDL_MaximizeWindow"
    );

    public static void SDL_MaximizeWindow(SDL_Window Sdl2Window) => s_maximizeWindow(Sdl2Window);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate void SDL_MinimizeWindow_t(SDL_Window SDL2Window);

    static readonly SDL_MinimizeWindow_t s_minimizeWindow = LoadFunction<SDL_MinimizeWindow_t>(
        "SDL_MinimizeWindow"
    );

    public static void SDL_MinimizeWindow(SDL_Window Sdl2Window) => s_minimizeWindow(Sdl2Window);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate void SDL_RaiseWindow_t(SDL_Window SDL2Window);

    static readonly SDL_RaiseWindow_t s_raiseWindow = LoadFunction<SDL_RaiseWindow_t>(
        "SDL_RaiseWindow"
    );

    public static void SDL_RaiseWindow(SDL_Window Sdl2Window) => s_raiseWindow(Sdl2Window);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate int SDL_SetWindowFullscreen_t(SDL_Window Sdl2Window, SDL_FullscreenMode mode);

    static readonly SDL_SetWindowFullscreen_t s_setWindowFullscreen =
        LoadFunction<SDL_SetWindowFullscreen_t>("SDL_SetWindowFullscreen");

    public static int SDL_SetWindowFullscreen(SDL_Window Sdl2Window, SDL_FullscreenMode mode) =>
        s_setWindowFullscreen(Sdl2Window, mode);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate void SDL_ShowWindow_t(SDL_Window SDL2Window);

    static readonly SDL_ShowWindow_t s_showWindow = LoadFunction<SDL_ShowWindow_t>(
        "SDL_ShowWindow"
    );

    public static void SDL_ShowWindow(SDL_Window Sdl2Window) => s_showWindow(Sdl2Window);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate void SDL_HideWindow_t(SDL_Window SDL2Window);

    static readonly SDL_HideWindow_t s_hideWindow = LoadFunction<SDL_HideWindow_t>(
        "SDL_HideWindow"
    );

    public static void SDL_HideWindow(SDL_Window Sdl2Window) => s_hideWindow(Sdl2Window);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate uint SDL_GetWindowID_t(SDL_Window SDL2Window);

    static readonly SDL_GetWindowID_t s_getWindowID = LoadFunction<SDL_GetWindowID_t>(
        "SDL_GetWindowID"
    );

    public static uint SDL_GetWindowID(SDL_Window Sdl2Window) => s_getWindowID(Sdl2Window);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate int SDL_SetWindowOpacity_t(SDL_Window window, float opacity);

    static readonly SDL_SetWindowOpacity_t s_setWindowOpacity =
        LoadFunction<SDL_SetWindowOpacity_t>("SDL_SetWindowOpacity");

    public static int SDL_SetWindowOpacity(SDL_Window Sdl2Window, float opacity) =>
        s_setWindowOpacity(Sdl2Window, opacity);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate int SDL_GetWindowOpacity_t(SDL_Window window, float* opacity);

    static readonly SDL_GetWindowOpacity_t s_getWindowOpacity =
        LoadFunction<SDL_GetWindowOpacity_t>("SDL_GetWindowOpacity");

    public static int SDL_GetWindowOpacity(SDL_Window Sdl2Window, float* opacity) =>
        s_getWindowOpacity(Sdl2Window, opacity);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate void SDL_SetWindowResizable_t(SDL_Window window, uint resizable);

    static readonly SDL_SetWindowResizable_t s_setWindowResizable =
        LoadFunction<SDL_SetWindowResizable_t>("SDL_SetWindowResizable");

    public static void SDL_SetWindowResizable(SDL_Window window, uint resizable) =>
        s_setWindowResizable(window, resizable);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate int SDL_GetDisplayBounds_t(int displayIndex, Rectangle* rect);

    static readonly SDL_GetDisplayBounds_t s_sdl_getDisplayBounds =
        LoadFunction<SDL_GetDisplayBounds_t>("SDL_GetDisplayBounds");

    public static int SDL_GetDisplayBounds(int displayIndex, Rectangle* rect) =>
        s_sdl_getDisplayBounds(displayIndex, rect);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate int SDL_GetDisplayDPI_t(int displayIndex, float* ddpi, float* hdpi, float* vdpi);

    static readonly SDL_GetDisplayDPI_t s_sdl_getDisplayDPI = LoadFunction<SDL_GetDisplayDPI_t>(
        "SDL_GetDisplayDPI"
    );

    public static int SDL_GetDisplayDPI(int displayIndex, float* ddpi, float* hdpi, float* vdpi) =>
        s_sdl_getDisplayDPI(displayIndex, ddpi, hdpi, vdpi);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate int SDL_GL_GetDrawableSize_t(SDL_Window window, int* w, int* h);

    static readonly SDL_GL_GetDrawableSize_t s_sdl_gl_getDrawableSize =
        LoadFunction<SDL_GL_GetDrawableSize_t>("SDL_GL_GetDrawableSize");

    public static int SDL_GL_GetDrawableSize(SDL_Window window, int* w, int* h) =>
        s_sdl_gl_getDrawableSize(window, w, h);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate int SDL_GetWindowDisplayIndex_t(SDL_Window window);

    static readonly SDL_GetWindowDisplayIndex_t s_sdl_getWindowDisplayIndex =
        LoadFunction<SDL_GetWindowDisplayIndex_t>("SDL_GetWindowDisplayIndex");

    public static int SDL_GetWindowDisplayIndex(SDL_Window window) =>
        s_sdl_getWindowDisplayIndex(window);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate int SDL_GetCurrentDisplayMode_t(int displayIndex, SDL_DisplayMode* mode);

    static readonly SDL_GetCurrentDisplayMode_t s_sdl_getCurrentDisplayMode =
        LoadFunction<SDL_GetCurrentDisplayMode_t>("SDL_GetCurrentDisplayMode");

    public static int SDL_GetCurrentDisplayMode(int displayIndex, SDL_DisplayMode* mode) =>
        s_sdl_getCurrentDisplayMode(displayIndex, mode);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate int SDL_GetDesktopDisplayMode_t(int displayIndex, SDL_DisplayMode* mode);

    static readonly SDL_GetDesktopDisplayMode_t s_sdl_getDesktopDisplayMode =
        LoadFunction<SDL_GetDesktopDisplayMode_t>("SDL_GetDesktopDisplayMode");

    public static int SDL_GetDesktopDisplayMode(int displayIndex, SDL_DisplayMode* mode) =>
        s_sdl_getDesktopDisplayMode(displayIndex, mode);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate int SDL_GetNumVideoDisplays_t();

    static readonly SDL_GetNumVideoDisplays_t s_sdl_getNumVideoDisplays =
        LoadFunction<SDL_GetNumVideoDisplays_t>("SDL_GetNumVideoDisplays");

    public static int SDL_GetNumVideoDisplays() => s_sdl_getNumVideoDisplays();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    delegate bool SDL_SetHint_t(string name, string value);

    static readonly SDL_SetHint_t s_sdl_setHint = LoadFunction<SDL_SetHint_t>("SDL_SetHint");

    public static bool SDL_SetHint(string name, string value) => s_sdl_setHint(name, value);
}

[Flags]
public enum SDL_WindowFlags : uint
{
    /// <summary>
    /// fullscreen Sdl2Window.
    /// </summary>
    Fullscreen = 0x00000001,

    /// <summary>
    /// Sdl2Window usable with OpenGL context.
    /// </summary>
    OpenGL = 0x00000002,

    /// <summary>
    /// Sdl2Window is visible.
    /// </summary>
    Shown = 0x00000004,

    /// <summary>
    /// Sdl2Window is not visible.
    /// </summary>
    Hidden = 0x00000008,

    /// <summary>
    /// no Sdl2Window decoration.
    /// </summary>
    Borderless = 0x00000010,

    /// <summary>
    /// Sdl2Window can be resized.
    /// </summary>
    Resizable = 0x00000020,

    /// <summary>
    /// Sdl2Window is minimized.
    /// </summary>
    Minimized = 0x00000040,

    /// <summary>
    /// Sdl2Window is maximized.
    /// </summary>
    Maximized = 0x00000080,

    /// <summary>
    /// Sdl2Window has grabbed input focus.
    /// </summary>
    InputGrabbed = 0x00000100,

    /// <summary>
    /// Sdl2Window has input focus.
    /// </summary>
    InputFocus = 0x00000200,

    /// <summary>
    /// Sdl2Window has mouse focus.
    /// </summary>
    MouseFocus = 0x00000400,
    FullScreenDesktop = (Fullscreen | 0x00001000),

    /// <summary>
    /// Sdl2Window not created by SDL.
    /// </summary>
    Foreign = 0x00000800,

    /// <summary>
    /// Sdl2Window should be created in high-DPI mode if supported.
    /// </summary>
    AllowHighDpi = 0x00002000,

    /// <summary>
    /// Sdl2Window has mouse captured (unrelated to InputGrabbed).
    /// </summary>
    MouseCapture = 0x00004000,

    /// <summary>
    /// Sdl2Window should always be above others.
    /// </summary>
    AlwaysOnTop = 0x00008000,

    /// <summary>
    /// Sdl2Window should not be added to the taskbar.
    /// </summary>
    SkipTaskbar = 0x00010000,

    /// <summary>
    /// Sdl2Window should be treated as a utility Sdl2Window.
    /// </summary>
    Utility = 0x00020000,

    /// <summary>
    /// Sdl2Window should be treated as a tooltip.
    /// </summary>
    Tooltip = 0x00040000,

    /// <summary>
    /// Sdl2Window should be treated as a popup menu.
    /// </summary>
    PopupMenu = 0x00080000,
}

public enum SDL_FullscreenMode : uint
{
    Windowed = 0,
    Fullscreen = 0x00000001,
    FullScreenDesktop = (Fullscreen | 0x00001000),
}

#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
public unsafe struct SDL_DisplayMode
{
    public uint format;
    public int w;
    public int h;
    public int refresh_rate;
    public void* driverdata;
}
#pragma warning restore CS0649 // Field is never assigned to, and will always have its default value
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
