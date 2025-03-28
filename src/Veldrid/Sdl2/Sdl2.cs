﻿using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Veldrid.Sdl2;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
/// <summary>
/// Provides access to the native methods of SDL2.
/// </summary>
public static unsafe partial class Sdl2Native
{
    static Lazy<IntPtr> LibHandle { get; } = new(LoadSdl2);

    static IntPtr LoadSdl2()
    {
        string name;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            name = "SDL2.dll";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            name = "libSDL2-2.0.so";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            name = "libsdl2.dylib";
        }
        else
        {
            Debug.WriteLine("Unknown SDL platform. Attempting to load \"SDL2\"");
            name = "SDL2.dll";
        }

        return NativeLibrary.Load(
            name,
            Assembly.GetExecutingAssembly(),
            DllImportSearchPath.SafeDirectories
        );
    }

    /// <summary>
    /// Loads an SDL2 function by the given name.
    /// </summary>
    /// <typeparam name="T">The delegate type of the function to load.</typeparam>
    /// <param name="name">The name of the exported native function.</param>
    /// <returns>A delegate which can be used to invoke the native function.</returns>
    public static T LoadFunction<T>(string name)
    {
        IntPtr export = NativeLibrary.GetExport(LibHandle.Value, name);
        return Marshal.GetDelegateForFunctionPointer<T>(export);
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate byte* SDL_GetError_t();

    static readonly SDL_GetError_t s_sdl_getError = LoadFunction<SDL_GetError_t>("SDL_GetError");

    public static byte* SDL_GetError() => s_sdl_getError();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate void SDL_ClearError_t();

    static readonly SDL_ClearError_t s_sdl_clearError = LoadFunction<SDL_ClearError_t>(
        "SDL_ClearError"
    );

    public static byte* SDL_ClearError()
    {
        s_sdl_clearError();
        return null;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate void SDL_free_t(void* ptr);

    static readonly SDL_free_t s_sdl_free = LoadFunction<SDL_free_t>("SDL_free");

    public static void SDL_free(void* ptr)
    {
        s_sdl_free(ptr);
    }
}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
