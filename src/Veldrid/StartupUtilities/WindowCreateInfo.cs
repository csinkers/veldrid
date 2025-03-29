using Veldrid.Sdl2;

namespace Veldrid.StartupUtilities;
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

public struct WindowCreateInfo(
    int x,
    int y,
    int windowWidth,
    int windowHeight,
    WindowState windowInitialState,
    string windowTitle
)
{
    public int X = x;
    public int Y = y;
    public int WindowWidth = windowWidth;
    public int WindowHeight = windowHeight;
    public WindowState WindowInitialState = windowInitialState;
    public string WindowTitle = windowTitle;
}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
