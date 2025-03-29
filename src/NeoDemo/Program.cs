using Veldrid.Sdl2;

namespace Veldrid.NeoDemo;

internal class Program
{
    static unsafe void Main()
    {
        SDL_version version;
        Sdl2Native.SDL_GetVersion(&version);
        new NeoDemo().Run();
    }
}
