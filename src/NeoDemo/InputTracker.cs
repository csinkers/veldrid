﻿using System;
using System.Collections.Generic;
using System.Numerics;
using Veldrid.SDL2;

namespace Veldrid.NeoDemo;

public static class InputTracker
{
    static readonly HashSet<Key> _currentlyPressedKeys = new();
    static readonly HashSet<Key> _newKeysThisFrame = new();

    static readonly HashSet<MouseButton> _currentlyPressedMouseButtons = new();
    static readonly HashSet<MouseButton> _newMouseButtonsThisFrame = new();

    public static Vector2 MousePosition;
    public static Vector2 MouseDelta;
    public static IInputSnapshot? FrameSnapshot { get; private set; }

    public static bool GetKey(Key key) => _currentlyPressedKeys.Contains(key);

    public static bool GetKeyDown(Key key) => _newKeysThisFrame.Contains(key);

    public static bool GetMouseButton(MouseButton button) =>
        _currentlyPressedMouseButtons.Contains(button);

    public static bool GetMouseButtonDown(MouseButton button) =>
        _newMouseButtonsThisFrame.Contains(button);

    public static void UpdateFrameInput(IInputSnapshot snapshot, Sdl2Window window)
    {
        FrameSnapshot = snapshot;
        _newKeysThisFrame.Clear();
        _newMouseButtonsThisFrame.Clear();

        MousePosition = snapshot.MousePosition;
        MouseDelta = window.MouseDelta;

        ReadOnlySpan<KeyEvent> keyEvents = snapshot.KeyEvents;
        for (int i = 0; i < keyEvents.Length; i++)
        {
            KeyEvent ke = keyEvents[i];
            if (ke.Down)
                KeyDown(ke.Physical);
            else
                KeyUp(ke.Physical);
        }

        ReadOnlySpan<MouseButtonEvent> mouseEvents = snapshot.MouseEvents;
        for (int i = 0; i < mouseEvents.Length; i++)
        {
            MouseButtonEvent me = mouseEvents[i];
            if (me.Down)
                MouseDown(me.MouseButton);
            else
                MouseUp(me.MouseButton);
        }
    }

    static void MouseUp(MouseButton mouseButton)
    {
        _currentlyPressedMouseButtons.Remove(mouseButton);
        _newMouseButtonsThisFrame.Remove(mouseButton);
    }

    static void MouseDown(MouseButton mouseButton)
    {
        if (_currentlyPressedMouseButtons.Add(mouseButton))
            _newMouseButtonsThisFrame.Add(mouseButton);
    }

    static void KeyUp(Key key)
    {
        _currentlyPressedKeys.Remove(key);
        _newKeysThisFrame.Remove(key);
    }

    static void KeyDown(Key key)
    {
        if (_currentlyPressedKeys.Add(key))
            _newKeysThisFrame.Add(key);
    }
}
