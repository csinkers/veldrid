﻿using System.Runtime.InteropServices;

namespace Veldrid.Sdl2;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
public static unsafe partial class Sdl2Native
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate SDL_Renderer SDL_CreateRenderer_t(SDL_Window SDL2Window, int index, uint flags);

    static readonly SDL_CreateRenderer_t s_sdl_createRenderer = LoadFunction<SDL_CreateRenderer_t>(
        "SDL_CreateRenderer"
    );

    public static SDL_Renderer SDL_CreateRenderer(SDL_Window Sdl2Window, int index, uint flags) =>
        s_sdl_createRenderer(Sdl2Window, index, flags);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate void SDL_DestroyRenderer_t(SDL_Renderer renderer);

    static readonly SDL_DestroyRenderer_t s_sdl_destroyRenderer =
        LoadFunction<SDL_DestroyRenderer_t>("SDL_DestroyRenderer");

    public static void SDL_DestroyRenderer(SDL_Renderer renderer) =>
        s_sdl_destroyRenderer(renderer);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate int SDL_SetRenderDrawColor_t(SDL_Renderer renderer, byte r, byte g, byte b, byte a);

    static readonly SDL_SetRenderDrawColor_t s_sdl_setRenderDrawColor =
        LoadFunction<SDL_SetRenderDrawColor_t>("SDL_SetRenderDrawColor");

    public static int SDL_SetRenderDrawColor(
        SDL_Renderer renderer,
        byte r,
        byte g,
        byte b,
        byte a
    ) => s_sdl_setRenderDrawColor(renderer, r, g, b, a);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate int SDL_RenderClear_t(SDL_Renderer renderer);

    static readonly SDL_RenderClear_t s_sdl_renderClear = LoadFunction<SDL_RenderClear_t>(
        "SDL_RenderClear"
    );

    public static int SDL_RenderClear(SDL_Renderer renderer) => s_sdl_renderClear(renderer);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate int SDL_RenderFillRect_t(SDL_Renderer renderer, void* rect);

    static readonly SDL_RenderFillRect_t s_sdl_renderFillRect = LoadFunction<SDL_RenderFillRect_t>(
        "SDL_RenderFillRect"
    );

    public static int SDL_RenderFillRect(SDL_Renderer renderer, void* rect) =>
        s_sdl_renderFillRect(renderer, rect);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate int SDL_RenderPresent_t(SDL_Renderer renderer);

    static readonly SDL_RenderPresent_t s_sdl_renderPresent = LoadFunction<SDL_RenderPresent_t>(
        "SDL_RenderPresent"
    );

    public static int SDL_RenderPresent(SDL_Renderer renderer) => s_sdl_renderPresent(renderer);
}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
