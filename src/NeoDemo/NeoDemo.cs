﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Numerics;
using ImGuiNET;
using Veldrid.ImageSharp;
using Veldrid.NeoDemo.Objects;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;
using Veldrid.Utilities;

namespace Veldrid.NeoDemo;

public class NeoDemo
{
    Sdl2Window _window;
    GraphicsDevice _gd;
    readonly Scene _scene;
    readonly SceneContext _sc = new();
    bool _windowResized;
    bool _recreateWindow = true;

    const double DesiredFrameLengthSeconds = 1.0 / 60.0;
    public bool LimitFrameRate { get; set; } = false;
    readonly FrameTimeAverager _fta = new(0.666);
    CommandList _frameCommands;

    event Action<int, int>? ResizeHandled;

    readonly string[] _msaaOptions = ["Off", "2x", "4x", "8x", "16x", "32x"];
    int _msaaOption;
    bool _colorRedMask = true;
    bool _colorGreenMask = true;
    bool _colorBlueMask = true;
    bool _colorAlphaMask = true;
    TextureSampleCount? _newSampleCount;
    ColorWriteMask? _newMask;

    readonly Dictionary<string, ImageSharpTexture> _textures = new();
    Sdl2ControllerTracker? _controllerTracker;
    bool _colorSrgb = true;
    readonly FullScreenQuad _fsq;
    static RenderDoc? _renderDoc;
    bool _controllerDebugMenu;
    bool _showImguiDemo;

    public NeoDemo()
    {
        WindowCreateInfo windowCI = new()
        {
            X = 50,
            Y = 50,
            WindowWidth = 960,
            WindowHeight = 540,
            WindowInitialState = WindowState.Normal,
            WindowTitle = "Veldrid NeoDemo",
        };

        GraphicsDeviceOptions gdOptions = new(
            false,
            null,
            false,
            ResourceBindingModel.Improved,
            true,
            true,
            _colorSrgb
        );

#if DEBUG
        gdOptions.Debug = true;
#endif

        VeldridStartup.CreateWindowAndGraphicsDevice(
            windowCI,
            gdOptions,
            //VeldridStartup.GetPlatformDefaultBackend(),
            //GraphicsBackend.Metal,
            //GraphicsBackend.Vulkan,
            //GraphicsBackend.OpenGL,
            //GraphicsBackend.OpenGLES,
            out _window,
            out _gd
        );
        _window.Resized += () => _windowResized = true;

        Sdl2Native.SDL_Init(SDLInitFlags.GameController);
        Sdl2ControllerTracker.CreateDefault(out _controllerTracker);

        _scene = new(_gd, _window, _controllerTracker);

        _sc.SetCurrentScene(_scene);

        ImGuiRenderable igRenderable = new(_window.Width, _window.Height);
        ResizeHandled += (w, h) => igRenderable.WindowResized(w, h);
        _scene.AddRenderable(igRenderable);
        _scene.AddUpdateable(igRenderable);

        Skybox skybox = Skybox.LoadDefaultSkybox();
        _scene.AddRenderable(skybox);

        AddSponzaAtriumObjects();
        if (_sc.Camera != null)
        {
            _sc.Camera.Position = new(-80, 25, -4.3f);
            _sc.Camera.Yaw = -MathF.PI / 2;
            _sc.Camera.Pitch = -MathF.PI / 9;
        }

        ShadowmapDrawer texDrawIndexeder = new(() => _window, () => _sc.NearShadowMapView);
        ResizeHandled += (_, _) => texDrawIndexeder.OnWindowResized();
        texDrawIndexeder.Position = new(10, 25);
        _scene.AddRenderable(texDrawIndexeder);

        ShadowmapDrawer texDrawIndexeder2 = new(() => _window, () => _sc.MidShadowMapView);
        ResizeHandled += (_, _) => texDrawIndexeder2.OnWindowResized();
        texDrawIndexeder2.Position = new(20 + texDrawIndexeder2.Size.X, 25);
        _scene.AddRenderable(texDrawIndexeder2);

        ShadowmapDrawer texDrawIndexeder3 = new(() => _window, () => _sc.FarShadowMapView);
        ResizeHandled += (_, _) => texDrawIndexeder3.OnWindowResized();
        texDrawIndexeder3.Position = new(30 + (texDrawIndexeder3.Size.X * 2), 25);
        _scene.AddRenderable(texDrawIndexeder3);

        ShadowmapDrawer reflectionTexDrawer = new(() => _window, () => _sc.ReflectionColorView);
        ResizeHandled += (_, _) => reflectionTexDrawer.OnWindowResized();
        reflectionTexDrawer.Position = new(40 + (reflectionTexDrawer.Size.X * 3), 25);
        _scene.AddRenderable(reflectionTexDrawer);

        ScreenDuplicator duplicator = new();
        _scene.AddRenderable(duplicator);

        _fsq = new();
        _scene.AddRenderable(_fsq);

        CreateAllObjects();
        ImGui.StyleColorsClassic();
    }

    void AddSponzaAtriumObjects()
    {
        Console.WriteLine("Loading sponza objects");
        ObjFile atriumFile;
        using (
            FileStream objStream = File.OpenRead(
                AssetHelper.GetPath("Models/SponzaAtrium/sponza.obj")
            )
        )
        {
            atriumFile = new ObjParser().Parse(objStream);
        }

        MtlFile atriumMtls;
        using (
            FileStream mtlStream = File.OpenRead(
                AssetHelper.GetPath("Models/SponzaAtrium/sponza.mtl")
            )
        )
        {
            atriumMtls = new MtlParser().Parse(mtlStream);
        }

        Console.WriteLine($"Loading {atriumFile.MeshGroups.Length} mesh groups");
        int groupOffset = 0;
        int loadedDiffuses = 0;
        int loadedAlphaMaps = 0;
        foreach (ObjFile.MeshGroup group in atriumFile.MeshGroups)
        {
            double progress = Math.Floor(
                (groupOffset + 1.0) / atriumFile.MeshGroups.Length * 100.0
            );
            Console.WriteLine($"[{progress:0}%] Group {groupOffset++}: " + group.Name);

            Vector3 scale = new(0.1f);
            ConstructedMesh mesh = atriumFile.GetMesh16(group);
            MaterialDefinition? materialDef =
                mesh.MaterialName != null ? atriumMtls.Definitions[mesh.MaterialName] : null;
            ImageSharpTexture? overrideTextureData = null;
            ImageSharpTexture? alphaTexture = null;
            MaterialPropsAndBuffer materialProps = CommonMaterials.Brick;

            if (materialDef != null)
            {
                if (materialDef.DiffuseTexture != null)
                {
                    string texturePath = AssetHelper.GetPath(
                        "Models/SponzaAtrium/" + materialDef.DiffuseTexture
                    );
                    if (!_textures.ContainsKey(texturePath))
                    {
                        Console.WriteLine("Loading diffuse: " + materialDef.DiffuseTexture);
                        loadedDiffuses++;
                    }
                    overrideTextureData = LoadTexture(texturePath, true);
                }

                if (materialDef.AlphaMap != null)
                {
                    string texturePath = AssetHelper.GetPath(
                        "Models/SponzaAtrium/" + materialDef.AlphaMap
                    );
                    if (!_textures.ContainsKey(texturePath))
                    {
                        Console.WriteLine("Loading alpha map: " + materialDef.AlphaMap);
                        loadedAlphaMaps++;
                    }
                    alphaTexture = LoadTexture(texturePath, false);
                }

                if (materialDef.Name.Contains("vase"))
                {
                    materialProps = CommonMaterials.Vase;
                }
            }

            if (group.Name == "sponza_117")
            {
                MirrorMesh.Plane = Plane.CreateFromVertices(
                    atriumFile.Positions[group.Faces[0].Vertex0.PositionIndex] * scale.X,
                    atriumFile.Positions[group.Faces[0].Vertex1.PositionIndex] * scale.Y,
                    atriumFile.Positions[group.Faces[0].Vertex2.PositionIndex] * scale.Z
                );
                materialProps = CommonMaterials.Reflective;
            }

            AddTexturedMesh(
                mesh,
                overrideTextureData,
                alphaTexture,
                materialProps,
                Vector3.Zero,
                Quaternion.Identity,
                scale,
                group.Name
            );
        }

        Console.WriteLine($"Loaded {loadedDiffuses} diffuse textures");
        Console.WriteLine($"Loaded {loadedAlphaMaps} alpha map textures");
    }

    ImageSharpTexture LoadTexture(string texturePath, bool mipmap) // Plz don't call this with the same texturePath and different mipmap values.
    {
        if (!_textures.TryGetValue(texturePath, out ImageSharpTexture? tex))
        {
            tex = new(texturePath, mipmap, true);
            _textures.Add(texturePath, tex);
        }

        return tex;
    }

    void AddTexturedMesh(
        ConstructedMesh meshData,
        ImageSharpTexture? texData,
        ImageSharpTexture? alphaTexData,
        MaterialPropsAndBuffer materialProps,
        Vector3 position,
        Quaternion rotation,
        Vector3 scale,
        string name
    )
    {
        TexturedMesh mesh = new(name, meshData, texData, alphaTexData, materialProps);
        mesh.Transform.Position = position;
        mesh.Transform.Rotation = rotation;
        mesh.Transform.Scale = scale;
        _scene.AddRenderable(mesh);
    }

    public void Run()
    {
        long previousFrameTicks = 0;
        Stopwatch sw = new();
        sw.Start();
        while (_window.Exists)
        {
            long currentFrameTicks = sw.ElapsedTicks;
            double deltaSeconds =
                (currentFrameTicks - previousFrameTicks) / (double)Stopwatch.Frequency;

            while (LimitFrameRate && deltaSeconds < DesiredFrameLengthSeconds)
            {
                currentFrameTicks = sw.ElapsedTicks;
                deltaSeconds =
                    (currentFrameTicks - previousFrameTicks) / (double)Stopwatch.Frequency;
            }

            previousFrameTicks = currentFrameTicks;

            Sdl2Events.ProcessEvents();
            InputSnapshot snapshot = _window.PumpEvents();
            InputTracker.UpdateFrameInput(snapshot, _window);
            Update((float)deltaSeconds);
            if (!_window.Exists)
                break;

            Draw();
        }

        DestroyAllObjects();
        _gd.Dispose();
    }

    void Update(float deltaSeconds)
    {
        _fta.AddTime(deltaSeconds);
        _scene.Update(deltaSeconds);

        if (ImGui.BeginMainMenuBar())
        {
            if (ImGui.BeginMenu("Settings"))
            {
                if (ImGui.BeginMenu("Graphics Backend"))
                {
                    if (
                        ImGui.MenuItem(
                            "Vulkan",
                            string.Empty,
                            _gd.BackendType == GraphicsBackend.Vulkan,
                            GraphicsDevice.IsBackendSupported(GraphicsBackend.Vulkan)
                        )
                    )
                    {
                        ChangeBackend(GraphicsBackend.Vulkan);
                    }
                    if (
                        ImGui.MenuItem(
                            "OpenGL",
                            string.Empty,
                            _gd.BackendType == GraphicsBackend.OpenGL,
                            GraphicsDevice.IsBackendSupported(GraphicsBackend.OpenGL)
                        )
                    )
                    {
                        ChangeBackend(GraphicsBackend.OpenGL);
                    }
                    if (
                        ImGui.MenuItem(
                            "OpenGL ES",
                            string.Empty,
                            _gd.BackendType == GraphicsBackend.OpenGLES,
                            GraphicsDevice.IsBackendSupported(GraphicsBackend.OpenGLES)
                        )
                    )
                    {
                        ChangeBackend(GraphicsBackend.OpenGLES);
                    }
                    if (
                        ImGui.MenuItem(
                            "Direct3D 11",
                            string.Empty,
                            _gd.BackendType == GraphicsBackend.Direct3D11,
                            GraphicsDevice.IsBackendSupported(GraphicsBackend.Direct3D11)
                        )
                    )
                    {
                        ChangeBackend(GraphicsBackend.Direct3D11);
                    }
                    if (
                        ImGui.MenuItem(
                            "Metal",
                            string.Empty,
                            _gd.BackendType == GraphicsBackend.Metal,
                            GraphicsDevice.IsBackendSupported(GraphicsBackend.Metal)
                        )
                    )
                    {
                        ChangeBackend(GraphicsBackend.Metal);
                    }
                    ImGui.EndMenu();
                }
                if (ImGui.BeginMenu("MSAA"))
                {
                    if (ImGui.Combo("MSAA", ref _msaaOption, _msaaOptions, _msaaOptions.Length))
                    {
                        ChangeMsaa(_msaaOption);
                    }

                    ImGui.EndMenu();
                }
                if (ImGui.BeginMenu("Color mask"))
                {
                    if (ImGui.Checkbox("Red", ref _colorRedMask))
                        UpdateColorMask();
                    if (ImGui.Checkbox("Green", ref _colorGreenMask))
                        UpdateColorMask();
                    if (ImGui.Checkbox("Blue", ref _colorBlueMask))
                        UpdateColorMask();
                    if (ImGui.Checkbox("Alpha", ref _colorAlphaMask))
                        UpdateColorMask();

                    ImGui.EndMenu();
                }
                bool threadedRendering = _scene.ThreadedRendering;
                if (
                    ImGui.MenuItem(
                        "Render with multiple threads",
                        string.Empty,
                        threadedRendering,
                        true
                    )
                )
                {
                    _scene.ThreadedRendering = !_scene.ThreadedRendering;
                }
                bool tinted = _fsq.UseMultipleRenderTargets;
                if (ImGui.MenuItem("Tinted output", string.Empty, tinted, true))
                {
                    _fsq.UseMultipleRenderTargets = !tinted;
                }

                ImGui.EndMenu();
            }
            if (ImGui.BeginMenu("Window"))
            {
                bool isFullscreen = _window.WindowState == WindowState.BorderlessFullScreen;
                if (ImGui.MenuItem("Fullscreen", "F11", isFullscreen, true))
                {
                    ToggleFullscreenState();
                }
                if (
                    ImGui.MenuItem(
                        "Always Recreate Sdl2Window",
                        string.Empty,
                        _recreateWindow,
                        true
                    )
                )
                {
                    _recreateWindow = !_recreateWindow;
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(
                        "Causes a new OS window to be created whenever the graphics backend is switched. This is much safer, and is the default."
                    );
                }
                if (ImGui.MenuItem("sRGB Swapchain Format", string.Empty, _colorSrgb, true))
                {
                    _colorSrgb = !_colorSrgb;
                    ChangeBackend(_gd.BackendType);
                }
                bool vsync = _gd.SyncToVerticalBlank;
                if (ImGui.MenuItem("VSync", string.Empty, vsync, true))
                {
                    _gd.SyncToVerticalBlank = !_gd.SyncToVerticalBlank;
                }
                bool resizable = _window.Resizable;
                if (ImGui.MenuItem("Resizable Window", string.Empty, resizable))
                {
                    _window.Resizable = !_window.Resizable;
                }
                bool bordered = _window.BorderVisible;
                if (ImGui.MenuItem("Visible Window Border", string.Empty, bordered))
                {
                    _window.BorderVisible = !_window.BorderVisible;
                }

                ImGui.EndMenu();
            }
            if (ImGui.BeginMenu("Materials"))
            {
                if (ImGui.BeginMenu("Brick"))
                {
                    DrawIndexedMaterialMenu(CommonMaterials.Brick);
                    ImGui.EndMenu();
                }
                if (ImGui.BeginMenu("Vase"))
                {
                    DrawIndexedMaterialMenu(CommonMaterials.Vase);
                    ImGui.EndMenu();
                }
                if (ImGui.BeginMenu("Reflective"))
                {
                    DrawIndexedMaterialMenu(CommonMaterials.Reflective);
                    ImGui.EndMenu();
                }

                ImGui.EndMenu();
            }
            if (ImGui.BeginMenu("Debug"))
            {
                if (ImGui.MenuItem("Refresh Device Objects"))
                {
                    RefreshDeviceObjects(1);
                }
                if (ImGui.MenuItem("Refresh Device Objects (10 times)"))
                {
                    RefreshDeviceObjects(10);
                }
                if (ImGui.MenuItem("Refresh Device Objects (100 times)"))
                {
                    RefreshDeviceObjects(100);
                }
                if (_controllerTracker != null)
                {
                    if (ImGui.MenuItem("Controller State"))
                    {
                        _controllerDebugMenu = true;
                    }
                }
                else
                {
                    if (ImGui.MenuItem("Connect to Controller"))
                    {
                        Sdl2ControllerTracker.CreateDefault(out _controllerTracker);
                        _scene.Camera.Controller = _controllerTracker;
                    }
                }
                if (ImGui.MenuItem("Show ImGui Demo", string.Empty, _showImguiDemo, true))
                {
                    _showImguiDemo = !_showImguiDemo;
                }

                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu("RenderDoc"))
            {
                if (_renderDoc == null)
                {
                    if (ImGui.MenuItem("Load"))
                    {
                        if (RenderDoc.Load(out _renderDoc))
                        {
                            ChangeBackend(_gd.BackendType, forceRecreateWindow: true);
                        }
                    }
                }
                else
                {
                    if (ImGui.MenuItem("Trigger Capture"))
                    {
                        _renderDoc.TriggerCapture();
                    }
                    if (ImGui.BeginMenu("Options"))
                    {
                        bool allowVsync = _renderDoc.AllowVSync;
                        if (ImGui.Checkbox("Allow VSync", ref allowVsync))
                        {
                            _renderDoc.AllowVSync = allowVsync;
                        }
                        bool validation = _renderDoc.APIValidation;
                        if (ImGui.Checkbox("API Validation", ref validation))
                        {
                            _renderDoc.APIValidation = validation;
                        }
                        int delayForDebugger = (int)_renderDoc.DelayForDebugger;
                        if (ImGui.InputInt("Debugger Delay", ref delayForDebugger))
                        {
                            delayForDebugger = Math.Clamp(delayForDebugger, 0, int.MaxValue);
                            _renderDoc.DelayForDebugger = (uint)delayForDebugger;
                        }
                        bool verifyBufferAccess = _renderDoc.VerifyBufferAccess;
                        if (ImGui.Checkbox("Verify Buffer Access", ref verifyBufferAccess))
                        {
                            _renderDoc.VerifyBufferAccess = verifyBufferAccess;
                        }
                        bool overlayEnabled = _renderDoc.OverlayEnabled;
                        if (ImGui.Checkbox("Overlay Visible", ref overlayEnabled))
                        {
                            _renderDoc.OverlayEnabled = overlayEnabled;
                        }
                        bool overlayFrameRate = _renderDoc.OverlayFrameRate;
                        if (ImGui.Checkbox("Overlay Frame Rate", ref overlayFrameRate))
                        {
                            _renderDoc.OverlayFrameRate = overlayFrameRate;
                        }
                        bool overlayFrameNumber = _renderDoc.OverlayFrameNumber;
                        if (ImGui.Checkbox("Overlay Frame Number", ref overlayFrameNumber))
                        {
                            _renderDoc.OverlayFrameNumber = overlayFrameNumber;
                        }
                        bool overlayCaptureList = _renderDoc.OverlayCaptureList;
                        if (ImGui.Checkbox("Overlay Capture List", ref overlayCaptureList))
                        {
                            _renderDoc.OverlayCaptureList = overlayCaptureList;
                        }
                        ImGui.EndMenu();
                    }
                    if (ImGui.MenuItem("Launch Replay UI"))
                    {
                        _renderDoc.LaunchReplayUI();
                    }
                }
                ImGui.EndMenu();
            }

            if (_controllerDebugMenu)
            {
                if (
                    ImGui.Begin(
                        "Controller State",
                        ref _controllerDebugMenu,
                        ImGuiWindowFlags.NoCollapse
                    )
                )
                {
                    if (_controllerTracker != null)
                    {
                        ImGui.Columns(2);
                        ImGui.Text($"Name: {_controllerTracker.ControllerName}");
                        foreach (
                            SDL_GameControllerAxis axis in Enum.GetValues<SDL_GameControllerAxis>()
                        )
                        {
                            ImGui.Text($"{axis}: {_controllerTracker.GetAxis(axis)}");
                        }
                        ImGui.NextColumn();
                        foreach (
                            SDL_GameControllerButton button in Enum.GetValues<SDL_GameControllerButton>()
                        )
                        {
                            ImGui.Text($"{button}: {_controllerTracker.IsPressed(button)}");
                        }
                    }
                    else
                    {
                        ImGui.Text("No controller detected.");
                    }
                }
                ImGui.End();
            }

            ImGui.Text(
                _fta.CurrentAverageFramesPerSecond.ToString("000.0 fps / ")
                    + _fta.CurrentAverageFrameTimeMilliseconds.ToString("#00.00 ms")
            );

            ImGui.EndMainMenuBar();
        }

        if (InputTracker.GetKeyDown(Key.F11))
        {
            ToggleFullscreenState();
        }

        if (InputTracker.GetKeyDown(Key.Keypad6))
        {
            _window.X += 10;
        }
        if (InputTracker.GetKeyDown(Key.Keypad4))
        {
            _window.X -= 10;
        }
        if (InputTracker.GetKeyDown(Key.Keypad8))
        {
            _window.Y += 10;
        }
        if (InputTracker.GetKeyDown(Key.Keypad2))
        {
            _window.Y -= 10;
        }

        _window.Title = $"NeoDemo ({_gd.DeviceName}, {_gd.BackendType.ToString()})";

        if (_showImguiDemo)
        {
            ImGui.ShowDemoWindow(ref _showImguiDemo);
        }
    }

    void ChangeMsaa(int msaaOption)
    {
        TextureSampleCount sampleCount = (TextureSampleCount)msaaOption;
        _newSampleCount = sampleCount;
    }

    void UpdateColorMask()
    {
        ColorWriteMask mask = ColorWriteMask.None;

        if (_colorRedMask)
            mask |= ColorWriteMask.Red;
        if (_colorGreenMask)
            mask |= ColorWriteMask.Green;
        if (_colorBlueMask)
            mask |= ColorWriteMask.Blue;
        if (_colorAlphaMask)
            mask |= ColorWriteMask.Alpha;

        _newMask = mask;
    }

    void RefreshDeviceObjects(int numTimes)
    {
        Stopwatch sw = Stopwatch.StartNew();
        for (int i = 0; i < numTimes; i++)
        {
            DestroyAllObjects();
            CreateAllObjects();
        }
        sw.Stop();
        Console.WriteLine(
            $"Refreshing resources {numTimes} times took {sw.Elapsed.TotalSeconds} seconds."
        );
    }

    void DrawIndexedMaterialMenu(MaterialPropsAndBuffer propsAndBuffer)
    {
        MaterialProperties props = propsAndBuffer.Properties;
        if (
            ImGui.SliderFloat(
                "Intensity",
                ref props.SpecularIntensity.X,
                0f,
                10f,
                props.SpecularIntensity.X.ToString(CultureInfo.InvariantCulture)
            )
            | ImGui.SliderFloat(
                "Power",
                ref props.SpecularPower,
                0f,
                1000f,
                props.SpecularPower.ToString(CultureInfo.InvariantCulture)
            )
            | ImGui.SliderFloat(
                "Reflectivity",
                ref props.Reflectivity,
                0f,
                1f,
                props.Reflectivity.ToString(CultureInfo.InvariantCulture)
            )
        )
        {
            props.SpecularIntensity = new(props.SpecularIntensity.X);
            propsAndBuffer.Properties = props;
        }
    }

    void ToggleFullscreenState()
    {
        bool isFullscreen = _window.WindowState == WindowState.BorderlessFullScreen;
        _window.WindowState = isFullscreen ? WindowState.Normal : WindowState.BorderlessFullScreen;
    }

    void Draw()
    {
        Debug.Assert(_window.Exists);
        int width = _window.Width;
        int height = _window.Height;

        if (_windowResized)
        {
            _windowResized = false;

            _gd.ResizeMainWindow((uint)width, (uint)height);
            _scene.Camera.WindowResized(width, height);
            ResizeHandled?.Invoke(width, height);
            CommandList cl = _gd.ResourceFactory.CreateCommandList();
            cl.Begin();
            _sc.RecreateWindowSizedResources(_gd, cl);
            cl.End();
            _gd.SubmitCommands(cl);
            cl.Dispose();
        }

        if (_newSampleCount != null)
        {
            _sc.MainSceneSampleCount = _newSampleCount.Value;
            _newSampleCount = null;
            DestroyAllObjects();
            CreateAllObjects();
        }

        if (_newMask != null)
        {
            _sc.MainSceneMask = _newMask.Value;
            _newMask = null;
            DestroyAllObjects();
            CreateAllObjects();
        }

        _frameCommands.Begin();

        CommonMaterials.FlushAll(_frameCommands);

        _scene.RenderAllStages(_gd, _frameCommands, _sc);
        _gd.SwapBuffers();
    }

    void ChangeBackend(GraphicsBackend backend) => ChangeBackend(backend, false);

    void ChangeBackend(GraphicsBackend backend, bool forceRecreateWindow)
    {
        DestroyAllObjects();
        bool syncToVBlank = _gd.SyncToVerticalBlank;
        _gd.Dispose();

        if (_recreateWindow || forceRecreateWindow)
        {
            WindowCreateInfo windowCI = new()
            {
                X = _window.X,
                Y = _window.Y,
                WindowWidth = _window.Width,
                WindowHeight = _window.Height,
                WindowInitialState = _window.WindowState,
                WindowTitle = "Veldrid NeoDemo",
            };

            _window.Close();

            _window = VeldridStartup.CreateWindow(ref windowCI);
            _window.Resized += () => _windowResized = true;
        }

        GraphicsDeviceOptions gdOptions = new(
            false,
            null,
            syncToVBlank,
            ResourceBindingModel.Improved,
            true,
            true,
            _colorSrgb
        );
#if DEBUG
        gdOptions.Debug = true;
#endif
        _gd = VeldridStartup.CreateGraphicsDevice(_window, gdOptions, backend);

        _scene.Camera.UpdateBackend(_gd, _window);

        CreateAllObjects();
    }

    void DestroyAllObjects()
    {
        _gd.WaitForIdle();
        _frameCommands.Dispose();
        _sc.DestroyDeviceObjects();
        _scene.DestroyAllDeviceObjects();
        CommonMaterials.DestroyAllDeviceObjects();
        StaticResourceCache.DestroyAllDeviceObjects();
        _gd.WaitForIdle();
    }

    [MemberNotNull(nameof(_frameCommands))]
    void CreateAllObjects()
    {
        _frameCommands = _gd.ResourceFactory.CreateCommandList();
        _frameCommands.Name = "Frame Commands List";
        CommandList initCL = _gd.ResourceFactory.CreateCommandList();
        initCL.Name = "Recreation Initialization Command List";
        initCL.Begin();
        _sc.CreateDeviceObjects(_gd, initCL, _sc);
        CommonMaterials.CreateAllDeviceObjects(_gd, initCL, _sc);
        _scene.CreateAllDeviceObjects(_gd, initCL, _sc);
        initCL.End();
        _gd.SubmitCommands(initCL);
        initCL.Dispose();
    }
}
