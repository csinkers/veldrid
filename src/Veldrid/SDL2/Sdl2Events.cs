using System.Collections.Generic;
using static Veldrid.SDL2.Sdl2Native;

namespace Veldrid.SDL2;

/// <summary>
/// Handles SDL2 event processor subscriptions
/// </summary>
public static class Sdl2Events
{
    static readonly object s_lock = new();
    static readonly List<SDLEventHandler> s_processors = new();

    /// <summary>
    /// Subscribes the specified event processor to the SDL2 event loop.
    /// </summary>
    /// <param name="processor">The event processor to subscribe.</param>
    public static void Subscribe(SDLEventHandler processor)
    {
        lock (s_lock)
        {
            s_processors.Add(processor);
        }
    }

    /// <summary>
    /// Unsubscribes the specified event processor from the SDL2 event loop.
    /// </summary>
    /// <param name="processor">The event processor to unsubscribe.</param>
    public static void Unsubscribe(SDLEventHandler processor)
    {
        lock (s_lock)
        {
            s_processors.Remove(processor);
        }
    }

    /// <summary>
    /// Pumps the SDL2 event loop, and calls all registered event processors for each event.
    /// </summary>
    public static unsafe void ProcessEvents()
    {
        SDL_Event ev;
        while (SDL_PollEvent(&ev) == 1)
        {
            lock (s_lock)
            {
                foreach (SDLEventHandler processor in s_processors)
                {
                    processor(ref ev);
                }
            }
        }
    }
}
