namespace Veldrid.SDL2;

public readonly struct MouseState(int x, int y, MouseButton mouseDown)
{
    public int X { get; } = x;
    public int Y { get; } = y;
    public MouseButton MouseDown { get; } = mouseDown;

    public bool IsButtonDown(MouseButton button)
    {
        return (MouseDown & button) != 0;
    }
}
