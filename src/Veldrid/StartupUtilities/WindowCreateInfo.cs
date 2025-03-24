using Veldrid.SDL2;

namespace Veldrid.StartupUtilities;

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
