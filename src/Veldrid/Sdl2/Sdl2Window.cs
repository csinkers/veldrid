﻿using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Veldrid.Sdl2.Sdl2Native;

namespace Veldrid.Sdl2;
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

/// <summary>
/// Handler for SDL2 events.
/// </summary>
/// <param name="ev"></param>
public delegate void SDLEventHandler(ref SDL_Event ev);

/// <summary>
/// Represents a window created by SDL2.
/// </summary>
public unsafe class Sdl2Window
{
    /// <summary>Event raised when a file is dragged and dropped on the window</summary>
    public delegate void DropFileAction(DropFileEvent file);
    /// <summary>Event raised when text is dragged and dropped on the window</summary>
    public delegate void DropTextAction(DropTextEvent text);
    /// <summary>Event raised text is input</summary>
    public delegate void TextInputAction(TextInputEvent textInput);
    /// <summary>Event raised when text is edited</summary>
    public delegate void TextEditingAction(TextEditingEvent textEditing);

    readonly List<SDL_Event> _events = [];
    IntPtr _window;
    public uint WindowID { get; private set; }
    bool _exists;

    readonly InputSnapshot _publicSnapshot = new();
    InputSnapshot _privateSnapshot = new();
    readonly InputSnapshot _privateBackbuffer = new();

    // Threaded Sdl2Window flags
    readonly bool _threadedProcessing;

    bool _shouldClose;
    public bool LimitPollRate { get; set; }
    public float PollIntervalInMs { get; set; }

    // Current input states
    // int _currentMouseX;
    // int _currentMouseY;
    // MouseButton _currentMouseDown;
    Vector2 _currentMouseDelta;

    // Cached Sdl2Window state (for threaded processing)
    readonly BufferedValue<Point> _cachedPosition = new();
    readonly BufferedValue<Point> _cachedSize = new();
    string? _cachedWindowTitle;
    bool _newWindowTitleReceived;
    bool _firstMouseEvent = true;
    Func<bool>? _closeRequestedHandler;

    public Sdl2Window(
        string title,
        int x,
        int y,
        int width,
        int height,
        SDL_WindowFlags flags,
        bool threadedProcessing
    )
    {
        SDL_SetHint("SDL_MOUSE_FOCUS_CLICKTHROUGH", "1");
        _threadedProcessing = threadedProcessing;
        if (threadedProcessing)
        {
            using ManualResetEvent mre = new(false);
            WindowParams wp = new()
            {
                Title = title,
                X = x,
                Y = y,
                Width = width,
                Height = height,
                WindowFlags = flags,
                ResetEvent = mre,
            };

            Task.Factory.StartNew(WindowOwnerRoutine, wp, TaskCreationOptions.LongRunning);
            mre.WaitOne();
        }
        else
        {
            _window = SDL_CreateWindow(title, x, y, width, height, flags);
            WindowID = SDL_GetWindowID(_window);
            Sdl2WindowRegistry.RegisterWindow(this);
            PostWindowCreated(flags);
        }
    }

    public Sdl2Window(IntPtr windowHandle, bool threadedProcessing)
    {
        _threadedProcessing = threadedProcessing;
        if (threadedProcessing)
        {
            using ManualResetEvent mre = new(false);
            WindowParams wp = new()
            {
                WindowHandle = windowHandle,
                WindowFlags = 0,
                ResetEvent = mre,
            };

            Task.Factory.StartNew(WindowOwnerRoutine, wp, TaskCreationOptions.LongRunning);
            mre.WaitOne();
        }
        else
        {
            _window = SDL_CreateWindowFrom(windowHandle);
            WindowID = SDL_GetWindowID(_window);
            Sdl2WindowRegistry.RegisterWindow(this);
            PostWindowCreated(0);
        }
    }

    public int X
    {
        get => _cachedPosition.Value.X;
        set => SetWindowPosition(value, Y);
    }
    public int Y
    {
        get => _cachedPosition.Value.Y;
        set => SetWindowPosition(X, value);
    }

    public int Width
    {
        get => GetWindowSize().X;
        set => SetWindowSize(value, Height);
    }
    public int Height
    {
        get => GetWindowSize().Y;
        set => SetWindowSize(Width, value);
    }

    public IntPtr Handle => GetUnderlyingWindowHandle();

    public string? Title
    {
        get => _cachedWindowTitle;
        set => SetWindowTitle(value);
    }

    void SetWindowTitle(string? value)
    {
        _cachedWindowTitle = value;
        _newWindowTitleReceived = true;
    }

    public WindowState WindowState
    {
        get
        {
            SDL_WindowFlags flags = SDL_GetWindowFlags(_window);
            if (
                ((flags & SDL_WindowFlags.FullScreenDesktop) == SDL_WindowFlags.FullScreenDesktop)
                || (
                    (flags & (SDL_WindowFlags.Borderless | SDL_WindowFlags.Fullscreen))
                    == (SDL_WindowFlags.Borderless | SDL_WindowFlags.Fullscreen)
                )
            )
            {
                return WindowState.BorderlessFullScreen;
            }
            else if ((flags & SDL_WindowFlags.Minimized) == SDL_WindowFlags.Minimized)
            {
                return WindowState.Minimized;
            }
            else if ((flags & SDL_WindowFlags.Fullscreen) == SDL_WindowFlags.Fullscreen)
            {
                return WindowState.FullScreen;
            }
            else if ((flags & SDL_WindowFlags.Maximized) == SDL_WindowFlags.Maximized)
            {
                return WindowState.Maximized;
            }
            else if ((flags & SDL_WindowFlags.Hidden) == SDL_WindowFlags.Hidden)
            {
                return WindowState.Hidden;
            }

            return WindowState.Normal;
        }
        set
        {
            switch (value)
            {
                case WindowState.Normal:
                    SDL_SetWindowFullscreen(_window, SDL_FullscreenMode.Windowed);
                    break;
                case WindowState.FullScreen:
                    SDL_SetWindowFullscreen(_window, SDL_FullscreenMode.Fullscreen);
                    break;
                case WindowState.Maximized:
                    SDL_MaximizeWindow(_window);
                    break;
                case WindowState.Minimized:
                    SDL_MinimizeWindow(_window);
                    break;
                case WindowState.BorderlessFullScreen:
                    SDL_SetWindowFullscreen(_window, SDL_FullscreenMode.FullScreenDesktop);
                    break;
                case WindowState.Hidden:
                    SDL_HideWindow(_window);
                    break;
                default:
                    throw new InvalidOperationException("Illegal WindowState value: " + value);
            }
        }
    }

    public bool Exists => _exists;

    public bool Visible
    {
        get => (SDL_GetWindowFlags(_window) & SDL_WindowFlags.Shown) != 0;
        set
        {
            if (value)
            {
                SDL_ShowWindow(_window);
            }
            else
            {
                SDL_HideWindow(_window);
            }
        }
    }

    public Vector2 ScaleFactor => Vector2.One;

    public Rectangle Bounds => new(_cachedPosition, GetWindowSize());

    public bool CursorVisible
    {
        get { return SDL_ShowCursor(SDL_QUERY) == 1; }
        set
        {
            int toggle = value ? SDL_ENABLE : SDL_DISABLE;
            SDL_ShowCursor(toggle);
        }
    }

    public float Opacity
    {
        get
        {
            float opacity = float.NaN;
            if (SDL_GetWindowOpacity(_window, &opacity) == 0)
            {
                return opacity;
            }
            return float.NaN;
        }
        set { SDL_SetWindowOpacity(_window, value); }
    }

    /// <summary>
    /// Returns whether the window is currently focused.
    /// </summary>
    public bool Focused => (SDL_GetWindowFlags(_window) & SDL_WindowFlags.InputFocus) != 0;

    /// <summary>
    /// Gets or sets a value indicating whether the window is resizable.
    /// </summary>
    public bool Resizable
    {
        get => (SDL_GetWindowFlags(_window) & SDL_WindowFlags.Resizable) != 0;
        set => SDL_SetWindowResizable(_window, value ? 1u : 0u);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the window has a border.
    /// </summary>
    public bool BorderVisible
    {
        get => (SDL_GetWindowFlags(_window) & SDL_WindowFlags.Borderless) == 0;
        set => SDL_SetWindowBordered(_window, value ? 1u : 0u);
    }

    public IntPtr SdlWindowHandle => _window;

    public event Action? Resized;
    public event Action? Closing;
    public event Action? Closed;
    public event Action? FocusLost;
    public event Action? FocusGained;
    public event Action? Shown;
    public event Action? Hidden;
    public event Action? MouseEntered;
    public event Action? MouseLeft;
    public event Action? Exposed;
    public event Action? KeyMapChanged;
    public event Action<Point>? Moved;
    public event Action<MouseWheelEvent>? MouseWheel;
    public event Action<MouseMoveEvent>? MouseMove;
    public event Action<MouseButtonEvent>? MouseDown;
    public event Action<MouseButtonEvent>? MouseUp;
    public event Action<KeyEvent>? KeyDown;
    public event Action<KeyEvent>? KeyUp;
    public event TextInputAction? TextInput;
    public event TextEditingAction? TextEditing;
    public event Action? DropBegin;
    public event Action? DropComplete;
    public event DropFileAction? DropFile;
    public event DropTextAction? DropText;

    public Point ClientToScreen(Point p)
    {
        Point position = _cachedPosition;
        return new(p.X + position.X, p.Y + position.Y);
    }

    public void SetMousePosition(Vector2 position) =>
        SetMousePosition((int)position.X, (int)position.Y);

    public void SetMousePosition(int x, int y)
    {
        if (_exists)
        {
            SDL_WarpMouseInWindow(_window, x, y);
            // _currentMouseX = x;
            // _currentMouseY = y;
        }
    }

    public Vector2 MouseDelta => _currentMouseDelta;

    public void SetCloseRequestedHandler(Func<bool>? handler)
    {
        _closeRequestedHandler = handler;
    }

    public void Close()
    {
        if (_threadedProcessing)
        {
            _shouldClose = true;
        }
        else
        {
            CloseCore();
        }
    }

    bool CloseCore()
    {
        if (_closeRequestedHandler?.Invoke() ?? false)
        {
            _shouldClose = false;
            return false;
        }

        Sdl2WindowRegistry.RemoveWindow(this);
        Closing?.Invoke();
        SDL_DestroyWindow(_window);
        _exists = false;
        Closed?.Invoke();

        return true;
    }

    void WindowOwnerRoutine(object? state)
    {
        WindowParams wp = (WindowParams)state!;
        _window = wp.Create();
        WindowID = SDL_GetWindowID(_window);
        Sdl2WindowRegistry.RegisterWindow(this);
        PostWindowCreated(wp.WindowFlags);
        wp.ResetEvent!.Set();

        double previousPollTimeMs = 0;
        Stopwatch sw = new();
        sw.Start();

        while (_exists)
        {
            if (_shouldClose && CloseCore())
            {
                return;
            }

            // double currentTick = sw.ElapsedTicks;
            double currentTimeMs = sw.ElapsedTicks * (1000.0 / Stopwatch.Frequency);
            if (LimitPollRate && currentTimeMs - previousPollTimeMs < PollIntervalInMs)
            {
                Thread.Sleep(0);
            }
            else
            {
                previousPollTimeMs = currentTimeMs;
                ProcessEvents(null);
            }
        }
    }

    void PostWindowCreated(SDL_WindowFlags flags)
    {
        RefreshCachedPosition();
        RefreshCachedSize();
        if ((flags & SDL_WindowFlags.Shown) == SDL_WindowFlags.Shown)
        {
            SDL_ShowWindow(_window);
        }

        _exists = true;
    }

    // Called by Sdl2EventProcessor when an event for this window is encountered.
    internal void AddEvent(SDL_Event ev)
    {
        _events.Add(ev);
    }

    public InputSnapshot PumpEvents()
    {
        _currentMouseDelta = new();
        if (_threadedProcessing)
        {
            InputSnapshot snapshot = Interlocked.Exchange(
                ref _privateSnapshot,
                _privateBackbuffer
            );
            snapshot.CopyTo(_publicSnapshot);
            snapshot.Clear();
        }
        else
        {
            ProcessEvents(null);
            _privateSnapshot.CopyTo(_publicSnapshot);
            _privateSnapshot.Clear();
        }

        return _publicSnapshot;
    }

    void ProcessEvents(SDLEventHandler? eventHandler)
    {
        CheckNewWindowTitle();

        Sdl2Events.ProcessEvents();
        Span<SDL_Event> events = CollectionsMarshal.AsSpan(_events);
        for (int i = 0; i < events.Length; i++)
        {
            ref SDL_Event ev = ref events[i];
            if (eventHandler == null)
            {
                HandleEvent(ref ev);
            }
            else
            {
                eventHandler(ref ev);
            }
        }
        _events.Clear();
    }

    public void PumpEvents(SDLEventHandler? eventHandler)
    {
        ProcessEvents(eventHandler);
    }

    void HandleEvent(ref SDL_Event ev)
    {
        switch (ev.type)
        {
            case SDL_EventType.Quit:
                Close();
                break;
            case SDL_EventType.Terminating:
                Close();
                break;
            case SDL_EventType.WindowEvent:
                SDL_WindowEvent windowEvent = Unsafe.As<SDL_Event, SDL_WindowEvent>(ref ev);
                HandleWindowEvent(windowEvent);
                break;
            case SDL_EventType.KeyDown:
            case SDL_EventType.KeyUp:
                SDL_KeyboardEvent keyboardEvent = Unsafe.As<SDL_Event, SDL_KeyboardEvent>(ref ev);
                HandleKeyboardEvent(keyboardEvent);
                break;
            case SDL_EventType.TextEditing:
                SDL_TextEditingEvent textEditingEvent = Unsafe.As<SDL_Event, SDL_TextEditingEvent>(
                    ref ev
                );
                HandleTextEditingEvent(textEditingEvent);
                break;
            case SDL_EventType.TextInput:
                SDL_TextInputEvent textInputEvent = Unsafe.As<SDL_Event, SDL_TextInputEvent>(
                    ref ev
                );
                HandleTextInputEvent(textInputEvent);
                break;
            case SDL_EventType.KeyMapChanged:
                KeyMapChanged?.Invoke();
                break;
            case SDL_EventType.MouseMotion:
                SDL_MouseMotionEvent mouseMotionEvent = Unsafe.As<SDL_Event, SDL_MouseMotionEvent>(
                    ref ev
                );
                HandleMouseMotionEvent(mouseMotionEvent);
                break;
            case SDL_EventType.MouseButtonDown:
            case SDL_EventType.MouseButtonUp:
                SDL_MouseButtonEvent mouseButtonEvent = Unsafe.As<SDL_Event, SDL_MouseButtonEvent>(
                    ref ev
                );
                HandleMouseButtonEvent(mouseButtonEvent);
                break;
            case SDL_EventType.MouseWheel:
                SDL_MouseWheelEvent mouseWheelEvent = Unsafe.As<SDL_Event, SDL_MouseWheelEvent>(
                    ref ev
                );
                HandleMouseWheelEvent(mouseWheelEvent);
                break;
            case SDL_EventType.DropBegin:
                DropBegin?.Invoke();
                break;
            case SDL_EventType.DropComplete:
                DropComplete?.Invoke();
                break;
            case SDL_EventType.DropFile:
            case SDL_EventType.DropText:
                SDL_DropEvent dropEvent = Unsafe.As<SDL_Event, SDL_DropEvent>(ref ev);
                HandleDropEvent(dropEvent);
                break;
        }
    }

    void CheckNewWindowTitle()
    {
        if (_newWindowTitleReceived)
        {
            _newWindowTitleReceived = false;
            SDL_SetWindowTitle(_window, _cachedWindowTitle);
        }
    }

    static int ParseTextEvent(ReadOnlySpan<byte> utf8, Span<Rune> runes)
    {
        int byteCount = utf8.IndexOf((byte)0);
        if (byteCount != -1)
            utf8 = utf8[..byteCount];

        int runeCount = 0;
        while (Rune.DecodeFromUtf8(utf8, out Rune rune, out int consumed) == OperationStatus.Done)
        {
            runes[runeCount++] = rune;
            utf8 = utf8[consumed..];
        }

        return runeCount;
    }

    void HandleTextInputEvent(SDL_TextInputEvent textInputEvent)
    {
        ReadOnlySpan<byte> utf8 = new(textInputEvent.text, SDL_TextInputEvent.MaxTextSize);
        Span<Rune> runes = stackalloc Rune[SDL_TextInputEvent.MaxTextSize];
        runes = runes[..ParseTextEvent(utf8, runes)];

        InputSnapshot snapshot = _privateSnapshot;
        for (int i = 0; i < runes.Length; i++)
        {
            snapshot.InputEvents.Add(runes[i]);
        }

        TextInputEvent inputEvent = new(textInputEvent.timestamp, textInputEvent.windowID, runes);
        TextInput?.Invoke(inputEvent);
    }

    void HandleTextEditingEvent(SDL_TextEditingEvent textEditingEvent)
    {
        ReadOnlySpan<byte> utf8 = new(textEditingEvent.text, SDL_TextEditingEvent.MaxTextSize);
        Span<Rune> runes = stackalloc Rune[SDL_TextEditingEvent.MaxTextSize];
        runes = runes[..ParseTextEvent(utf8, runes)];

        TextEditingEvent editingEvent = new(
            textEditingEvent.timestamp,
            textEditingEvent.windowID,
            runes,
            textEditingEvent.start,
            textEditingEvent.length
        );
        TextEditing?.Invoke(editingEvent);
    }

    void HandleMouseWheelEvent(SDL_MouseWheelEvent mouseWheelEvent)
    {
        Vector2 delta = new(mouseWheelEvent.x, mouseWheelEvent.y);

        InputSnapshot snapshot = _privateSnapshot;
        snapshot.WheelDelta += delta;

        MouseWheelEvent wheelEvent = new(
            mouseWheelEvent.timestamp,
            mouseWheelEvent.windowID,
            delta
        );
        MouseWheel?.Invoke(wheelEvent);
    }

    void HandleDropEvent(SDL_DropEvent dropEvent)
    {
        if (dropEvent.file != null)
        {
            int characters = 0;
            while (dropEvent.file[characters] != 0)
            {
                characters++;
            }

            ReadOnlySpan<byte> utf8 = new(dropEvent.file, characters);
            try
            {
                if (dropEvent.type == SDL_EventType.DropFile)
                {
                    DropFile?.Invoke(new(utf8, dropEvent.timestamp, dropEvent.windowID));
                }
                else if (dropEvent.type == SDL_EventType.DropText)
                {
                    DropText?.Invoke(new(utf8, dropEvent.timestamp, dropEvent.windowID));
                }
            }
            finally
            {
                SDL_free(dropEvent.file);
            }
        }
    }

    void HandleMouseButtonEvent(SDL_MouseButtonEvent mouseButtonEvent)
    {
        MouseButton button = MapMouseButton(mouseButtonEvent.button);
        bool down = mouseButtonEvent.state == 1;

        InputSnapshot snapshot = _privateSnapshot;
        if (down)
        {
            // _currentMouseDown |= button;
            snapshot.MouseDown |= button;
        }
        else
        {
            // _currentMouseDown &= ~button;
            snapshot.MouseDown &= ~button;
        }

        MouseButtonEvent mouseEvent = new(
            mouseButtonEvent.timestamp,
            mouseButtonEvent.windowID,
            button,
            down,
            mouseButtonEvent.clicks
        );
        snapshot.MouseEvents.Add(mouseEvent);

        if (down)
        {
            MouseDown?.Invoke(mouseEvent);
        }
        else
        {
            MouseUp?.Invoke(mouseEvent);
        }
    }

    static MouseButton MapMouseButton(SDL_MouseButton button)
    {
        return button switch
        {
            SDL_MouseButton.Left => MouseButton.Left,
            SDL_MouseButton.Middle => MouseButton.Middle,
            SDL_MouseButton.Right => MouseButton.Right,
            SDL_MouseButton.X1 => MouseButton.Button1,
            SDL_MouseButton.X2 => MouseButton.Button2,
            _ => MouseButton.Left,
        };
    }

    void HandleMouseMotionEvent(SDL_MouseMotionEvent mouseMotionEvent)
    {
        Vector2 mousePos = new(mouseMotionEvent.x, mouseMotionEvent.y);
        Vector2 delta = new(mouseMotionEvent.xrel, mouseMotionEvent.yrel);
        // _currentMouseX = (int)mousePos.X;
        // _currentMouseY = (int)mousePos.Y;
        _privateSnapshot.MousePosition = mousePos;

        if (!_firstMouseEvent)
        {
            _currentMouseDelta += delta;

            MouseMoveEvent motionEvent = new(
                mouseMotionEvent.timestamp,
                mouseMotionEvent.windowID,
                mousePos,
                delta
            );
            MouseMove?.Invoke(motionEvent);
        }
        _firstMouseEvent = false;
    }

    void HandleKeyboardEvent(SDL_KeyboardEvent keyboardEvent)
    {
        KeyEvent keyEvent = new(
            keyboardEvent.timestamp,
            keyboardEvent.windowID,
            keyboardEvent.state == 1,
            keyboardEvent.repeat == 1,
            (Key)keyboardEvent.keysym.scancode,
            (VKey)keyboardEvent.keysym.sym,
            (ModifierKeys)keyboardEvent.keysym.mod
        );

        _privateSnapshot.KeyEvents.Add(keyEvent);
        if (keyEvent.Down)
        {
            KeyDown?.Invoke(keyEvent);
        }
        else
        {
            KeyUp?.Invoke(keyEvent);
        }
    }

    void HandleWindowEvent(SDL_WindowEvent windowEvent)
    {
        switch (windowEvent.@event)
        {
            case SDL_WindowEventID.Resized:
            case SDL_WindowEventID.SizeChanged:
            case SDL_WindowEventID.Minimized:
            case SDL_WindowEventID.Maximized:
            case SDL_WindowEventID.Restored:
                HandleResizedMessage();
                break;
            case SDL_WindowEventID.FocusGained:
                FocusGained?.Invoke();
                break;
            case SDL_WindowEventID.FocusLost:
                FocusLost?.Invoke();
                break;
            case SDL_WindowEventID.Close:
                Close();
                break;
            case SDL_WindowEventID.Shown:
                Shown?.Invoke();
                break;
            case SDL_WindowEventID.Hidden:
                Hidden?.Invoke();
                break;
            case SDL_WindowEventID.Enter:
                MouseEntered?.Invoke();
                break;
            case SDL_WindowEventID.Leave:
                MouseLeft?.Invoke();
                break;
            case SDL_WindowEventID.Exposed:
                Exposed?.Invoke();
                break;
            case SDL_WindowEventID.Moved:
                _cachedPosition.Value = new(windowEvent.data1, windowEvent.data2);
                Moved?.Invoke(new(windowEvent.data1, windowEvent.data2));
                break;
            default:
                Debug.WriteLine("Unhandled SDL WindowEvent: " + windowEvent.@event);
                break;
        }
    }

    void HandleResizedMessage()
    {
        RefreshCachedSize();
        Resized?.Invoke();
    }

    void RefreshCachedSize()
    {
        int w,
            h;
        SDL_GetWindowSize(_window, &w, &h);
        _cachedSize.Value = new(w, h);
    }

    void RefreshCachedPosition()
    {
        int x,
            y;
        SDL_GetWindowPosition(_window, &x, &y);
        _cachedPosition.Value = new(x, y);
    }

    /*
    MouseState GetCurrentMouseState()
    {
        return new(_currentMouseX, _currentMouseY, _currentMouseDown);
    }
    */

    public Point ScreenToClient(Point p)
    {
        Point position = _cachedPosition;
        return new(p.X - position.X, p.Y - position.Y);
    }

    void SetWindowPosition(int x, int y)
    {
        SDL_SetWindowPosition(_window, x, y);
        _cachedPosition.Value = new(x, y);
    }

    Point GetWindowSize()
    {
        return _cachedSize;
    }

    void SetWindowSize(int width, int height)
    {
        SDL_SetWindowSize(_window, width, height);
        _cachedSize.Value = new(width, height);
    }

    IntPtr GetUnderlyingWindowHandle()
    {
        SDL_SysWMinfo wmInfo;
        SDL_GetVersion(&wmInfo.version);
        SDL_GetWMWindowInfo(_window, &wmInfo);
        switch (wmInfo.subsystem)
        {
            case SysWMType.Windows:
                Win32WindowInfo win32Info = Unsafe.Read<Win32WindowInfo>(&wmInfo.info);
                return win32Info.Sdl2Window;
            case SysWMType.X11:
                X11WindowInfo x11Info = Unsafe.Read<X11WindowInfo>(&wmInfo.info);
                return x11Info.Sdl2Window;
            case SysWMType.Wayland:
                WaylandWindowInfo waylandInfo = Unsafe.Read<WaylandWindowInfo>(&wmInfo.info);
                return waylandInfo.surface;
            case SysWMType.Cocoa:
                CocoaWindowInfo cocoaInfo = Unsafe.Read<CocoaWindowInfo>(&wmInfo.info);
                return cocoaInfo.Window;
            case SysWMType.Android:
                AndroidWindowInfo androidInfo = Unsafe.Read<AndroidWindowInfo>(&wmInfo.info);
                return androidInfo.window;
            default:
                return _window;
        }
    }

    class WindowParams
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string? Title { get; set; }
        public SDL_WindowFlags WindowFlags { get; set; }

        public IntPtr WindowHandle { get; set; }

        public ManualResetEvent? ResetEvent { get; set; }

        public SDL_Window Create() => WindowHandle != IntPtr.Zero
            ? SDL_CreateWindowFrom(WindowHandle)
            : SDL_CreateWindow(Title, X, Y, Width, Height, WindowFlags);
    }
}

/// <summary>
/// Helper to allow atomic updating of a value-type.
/// </summary>
/// <typeparam name="T"></typeparam>
[DebuggerDisplay("{DebuggerDisplayString,nq}")]
internal class BufferedValue<T>
    where T : struct
{
    public T Value
    {
        get => _current.Value;
        set
        {
            _back.Value = value;
            _back = Interlocked.Exchange(ref _current, _back);
        }
    }

    ValueHolder _current = new();
    ValueHolder _back = new();

    /// <summary>
    /// Implicitly convert a BufferedValue to its value.
    /// </summary>
    /// <param name="bv"></param>
    public static implicit operator T(BufferedValue<T> bv) => bv.Value;

    string DebuggerDisplayString => $"{_current.Value}";

    class ValueHolder
    {
        public T Value;
    }
}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
