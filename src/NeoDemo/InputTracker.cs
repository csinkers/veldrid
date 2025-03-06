using System.Collections.Generic;
using System.Numerics;
using Veldrid.SDL2;

namespace Veldrid.NeoDemo;

public static class InputTracker
{
    static readonly HashSet<Key> CurrentlyPressedKeys = new();
    static readonly HashSet<Key> NewKeysThisFrame = new();

    static readonly HashSet<MouseButton> CurrentlyPressedMouseButtons = new();
    static readonly HashSet<MouseButton> NewMouseButtonsThisFrame = new();

    public static Vector2 MousePosition;
    public static Vector2 MouseDelta;
    public static InputSnapshot? FrameSnapshot { get; private set; }

    public static bool GetKey(Key key) => CurrentlyPressedKeys.Contains(key);

    public static bool GetKeyDown(Key key) => NewKeysThisFrame.Contains(key);

    public static bool GetMouseButton(MouseButton button) =>
        CurrentlyPressedMouseButtons.Contains(button);

    public static bool GetMouseButtonDown(MouseButton button) =>
        NewMouseButtonsThisFrame.Contains(button);

    public static void UpdateFrameInput(InputSnapshot snapshot, Sdl2Window window)
    {
        FrameSnapshot = snapshot;
        NewKeysThisFrame.Clear();
        NewMouseButtonsThisFrame.Clear();

        MousePosition = snapshot.MousePosition;
        MouseDelta = window.MouseDelta;

        foreach (KeyEvent ke in snapshot.KeyEvents)
        {
            if (ke.Down)
                KeyDown(ke.Physical);
            else
                KeyUp(ke.Physical);
        }

        foreach (MouseButtonEvent me in snapshot.MouseEvents)
        {
            if (me.Down)
                MouseDown(me.MouseButton);
            else
                MouseUp(me.MouseButton);
        }
    }

    static void MouseUp(MouseButton mouseButton)
    {
        CurrentlyPressedMouseButtons.Remove(mouseButton);
        NewMouseButtonsThisFrame.Remove(mouseButton);
    }

    static void MouseDown(MouseButton mouseButton)
    {
        if (CurrentlyPressedMouseButtons.Add(mouseButton))
            NewMouseButtonsThisFrame.Add(mouseButton);
    }

    static void KeyUp(Key key)
    {
        CurrentlyPressedKeys.Remove(key);
        NewKeysThisFrame.Remove(key);
    }

    static void KeyDown(Key key)
    {
        if (CurrentlyPressedKeys.Add(key))
            NewKeysThisFrame.Add(key);
    }
}
