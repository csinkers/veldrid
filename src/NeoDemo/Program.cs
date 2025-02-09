using Veldrid.SDL2;

namespace Veldrid.NeoDemo;

internal class Program
{
    static unsafe void Main(string[] args)
    {
        SDL_version version;
        Sdl2Native.SDL_GetVersion(&version);
        new NeoDemo().Run();
    }
}
