using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Text;
using ImGuiNET;
using Veldrid.SDL2;

namespace Veldrid;

/// <summary>
/// Can render draw lists produced by ImGui.
/// Also provides functions for updating ImGui input.
/// </summary>
public class ImGuiRenderer : IDisposable
{
    GraphicsDevice _gd;
    readonly Assembly _assembly;
    readonly IntPtr _fontAtlasId = 1;
    ColorSpaceHandling _colorSpaceHandling;

    // Device objects
    DeviceBuffer _vertexBuffer;
    DeviceBuffer _indexBuffer;
    DeviceBuffer _projMatrixBuffer;
    Texture _fontTexture;
    Shader _vertexShader;
    Shader _fragmentShader;
    ResourceLayout _layout;
    ResourceLayout _textureLayout;
    Pipeline _pipeline;
    ResourceSet _mainResourceSet;
    ResourceSet _fontTextureResourceSet;

    int _windowWidth;
    int _windowHeight;
    readonly Vector2 _scaleFactor = Vector2.One;

    // Image trackers
    readonly Dictionary<TextureView, ResourceSetInfo> _setsByView = new();
    readonly Dictionary<Texture, TextureView> _autoViewsByTexture = new();
    readonly Dictionary<IntPtr, ResourceSetInfo> _viewsById = new();
    readonly List<IDisposable> _ownedResources = [];
    int _lastAssignedId = 100;
    bool _frameBegun;
    bool _disposed;

    /// <summary>
    /// Constructs a new ImGuiRenderer.
    /// </summary>
    /// <param name="gd">The GraphicsDevice used to create and update resources.</param>
    /// <param name="outputDescription">The output format.</param>
    /// <param name="width">The initial width of the rendering target. Can be resized.</param>
    /// <param name="height">The initial height of the rendering target. Can be resized.</param>
    /// <param name="colorSpaceHandling">Identifies how the renderer should treat vertex colors.</param>
    public ImGuiRenderer(
        GraphicsDevice gd,
        OutputDescription outputDescription,
        int width,
        int height,
        ColorSpaceHandling colorSpaceHandling = ColorSpaceHandling.Legacy
    )
    {
        _gd = gd;
        _assembly = typeof(ImGuiRenderer).GetTypeInfo().Assembly;
        _colorSpaceHandling = colorSpaceHandling;
        _windowWidth = width;
        _windowHeight = height;

        IntPtr context = ImGui.CreateContext();
        ImGui.SetCurrentContext(context);

        ImGui.GetIO().Fonts.AddFontDefault();
        ImGui.GetIO().Fonts.Flags |= ImFontAtlasFlags.NoBakedLines;

        CreateDeviceResources(gd, outputDescription);

        SetPerFrameImGuiData(1f / 60f);

        ImGui.NewFrame();
        _frameBegun = true;
    }

    public void WindowResized(int width, int height)
    {
        _windowWidth = width;
        _windowHeight = height;
    }

    public void DestroyDeviceObjects()
    {
        _vertexBuffer.Dispose();
        _indexBuffer.Dispose();
        _projMatrixBuffer.Dispose();
        _fontTexture.Dispose();
        _vertexShader.Dispose();
        _fragmentShader.Dispose();
        _layout.Dispose();
        _textureLayout.Dispose();
        _pipeline.Dispose();
        _mainResourceSet.Dispose();
        _fontTextureResourceSet.Dispose();

        foreach (IDisposable resource in _ownedResources)
            resource.Dispose();
    }

    [MemberNotNull(
        nameof(_vertexBuffer),
        nameof(_indexBuffer),
        nameof(_projMatrixBuffer),
        nameof(_vertexShader),
        nameof(_fragmentShader),
        nameof(_layout),
        nameof(_textureLayout),
        nameof(_pipeline),
        nameof(_mainResourceSet),
        nameof(_fontTexture),
        nameof(_fontTextureResourceSet)
    )]
    public void CreateDeviceResources(
        GraphicsDevice gd,
        OutputDescription outputDescription,
        ColorSpaceHandling? colorSpaceHandling = null
    )
    {
        _gd = gd;
        _colorSpaceHandling = colorSpaceHandling ?? _colorSpaceHandling;
        ResourceFactory factory = gd.ResourceFactory;
        _vertexBuffer = factory.CreateBuffer(
            new(10000, BufferUsage.VertexBuffer | BufferUsage.DynamicWrite)
        );
        _vertexBuffer.Name = "ImGui.NET Vertex Buffer";

        _indexBuffer = factory.CreateBuffer(
            new(2000, BufferUsage.IndexBuffer | BufferUsage.DynamicWrite)
        );
        _indexBuffer.Name = "ImGui.NET Index Buffer";

        _projMatrixBuffer = factory.CreateBuffer(
            new(64, BufferUsage.UniformBuffer | BufferUsage.DynamicWrite)
        );
        _projMatrixBuffer.Name = "ImGui.NET Projection Buffer";

        byte[] vertexShaderBytes = LoadEmbeddedShaderCode(
            gd.ResourceFactory,
            "imgui-vertex",
            ShaderStages.Vertex,
            _colorSpaceHandling
        );

        byte[] fragmentShaderBytes = LoadEmbeddedShaderCode(
            gd.ResourceFactory,
            "imgui-frag",
            ShaderStages.Fragment,
            _colorSpaceHandling
        );

        _vertexShader = factory.CreateShader(
            new(
                ShaderStages.Vertex,
                vertexShaderBytes,
                _gd.BackendType == GraphicsBackend.Vulkan ? "main" : "VS"
            )
        );
        _vertexShader.Name = "ImGui.NET Vertex Shader";

        _fragmentShader = factory.CreateShader(
            new(
                ShaderStages.Fragment,
                fragmentShaderBytes,
                _gd.BackendType == GraphicsBackend.Vulkan ? "main" : "FS"
            )
        );
        _fragmentShader.Name = "ImGui.NET Fragment Shader";

        VertexLayoutDescription[] vertexLayouts =
        [
            new(
                [
                    new("in_position", VertexElementSemantic.Position, VertexElementFormat.Float2),
                    new(
                        "in_texCoord",
                        VertexElementSemantic.TextureCoordinate,
                        VertexElementFormat.Float2
                    ),
                    new("in_color", VertexElementSemantic.Color, VertexElementFormat.Byte4_Norm),
                ]
            ),
        ];

        _layout = factory.CreateResourceLayout(
            new(
                new("ProjectionMatrixBuffer", ResourceKind.UniformBuffer, ShaderStages.Vertex),
                new("FontSampler", ResourceKind.Sampler, ShaderStages.Fragment)
            )
        );
        _layout.Name = "ImGui.NET Resource Layout";

        _textureLayout = factory.CreateResourceLayout(
            new([new("FontTexture", ResourceKind.TextureReadOnly, ShaderStages.Fragment)])
        );
        _textureLayout.Name = "ImGui.NET Texture Layout";

        GraphicsPipelineDescription pd = new(
            BlendStateDescription.SingleAlphaBlend,
            new(false, false, ComparisonKind.Always),
            new(FaceCullMode.None, PolygonFillMode.Solid, FrontFace.Clockwise, true, true),
            PrimitiveTopology.TriangleList,
            new(
                vertexLayouts,
                [_vertexShader, _fragmentShader],
                [
                    new(0, gd.IsClipSpaceYInverted),
                    new(1, _colorSpaceHandling == ColorSpaceHandling.Legacy),
                ]
            ),
            [_layout, _textureLayout],
            outputDescription,
            ResourceBindingModel.Default
        );

        _pipeline = factory.CreateGraphicsPipeline(pd);
        _pipeline.Name = "ImGui.NET Pipeline";

        _mainResourceSet = factory.CreateResourceSet(
            new(_layout, _projMatrixBuffer, gd.PointSampler)
        );
        _mainResourceSet.Name = "ImGui.NET Main Resource Set";

        RecreateFontDeviceTexture(gd);
    }

    /// <summary>
    /// Gets or creates a handle for a texture to be drawn with ImGui.
    /// Pass the returned handle to Image() or ImageButton().
    /// </summary>
    public IntPtr GetOrCreateImGuiBinding(ResourceFactory factory, TextureView textureView)
    {
        if (!_setsByView.TryGetValue(textureView, out ResourceSetInfo rsi))
        {
            ResourceSet resourceSet = factory.CreateResourceSet(new(_textureLayout, textureView));
            resourceSet.Name = $"ImGui.NET {textureView.Name} Resource Set";

            rsi = new(GetNextImGuiBindingID(), resourceSet);

            _setsByView.Add(textureView, rsi);
            _viewsById.Add(rsi.ImGuiBinding, rsi);
            _ownedResources.Add(resourceSet);
        }

        return rsi.ImGuiBinding;
    }

    public void RemoveImGuiBinding(TextureView textureView)
    {
        if (_setsByView.Remove(textureView, out ResourceSetInfo rsi))
        {
            _viewsById.Remove(rsi.ImGuiBinding);
            _ownedResources.Remove(rsi.ResourceSet);
            rsi.ResourceSet.Dispose();
        }
    }

    IntPtr GetNextImGuiBindingID()
    {
        int newId = _lastAssignedId++;
        return newId;
    }

    /// <summary>
    /// Gets or creates a handle for a texture to be drawn with ImGui.
    /// Pass the returned handle to Image() or ImageButton().
    /// </summary>
    public IntPtr GetOrCreateImGuiBinding(ResourceFactory factory, Texture texture)
    {
        if (!_autoViewsByTexture.TryGetValue(texture, out TextureView? textureView))
        {
            textureView = factory.CreateTextureView(texture);
            textureView.Name = $"ImGui.NET {texture.Name} View";
            _autoViewsByTexture.Add(texture, textureView);
            _ownedResources.Add(textureView);
        }

        return GetOrCreateImGuiBinding(factory, textureView);
    }

    public void RemoveImGuiBinding(Texture texture)
    {
        if (_autoViewsByTexture.Remove(texture, out TextureView? textureView))
        {
            _ownedResources.Remove(textureView);
            textureView.Dispose();
            RemoveImGuiBinding(textureView);
        }
    }

    /// <summary>
    /// Retrieves the shader texture binding for the given helper handle.
    /// </summary>
    public ResourceSet GetImageResourceSet(IntPtr imGuiBinding)
    {
        if (!_viewsById.TryGetValue(imGuiBinding, out ResourceSetInfo rsi))
        {
            throw new InvalidOperationException(
                "No registered ImGui binding with id " + imGuiBinding.ToString()
            );
        }

        return rsi.ResourceSet;
    }

    public void ClearCachedImageResources()
    {
        foreach (IDisposable resource in _ownedResources)
            resource.Dispose();

        _ownedResources.Clear();
        _setsByView.Clear();
        _viewsById.Clear();
        _autoViewsByTexture.Clear();
        _lastAssignedId = 100;
    }

    byte[] LoadEmbeddedShaderCode(
        ResourceFactory factory,
        string name,
        ShaderStages stage,
        ColorSpaceHandling colorSpaceHandling
    )
    {
        switch (factory.BackendType)
        {
            case GraphicsBackend.Direct3D11:
            {
                if (stage == ShaderStages.Vertex && colorSpaceHandling == ColorSpaceHandling.Legacy)
                    name += "-legacy";
                string resourceName = name + ".hlsl.bytes";
                return GetEmbeddedResourceBytes(resourceName);
            }
            case GraphicsBackend.OpenGL:
            {
                if (stage == ShaderStages.Vertex && colorSpaceHandling == ColorSpaceHandling.Legacy)
                    name += "-legacy";
                string resourceName = name + ".glsl";
                return GetEmbeddedResourceBytes(resourceName);
            }
            case GraphicsBackend.OpenGLES:
            {
                if (stage == ShaderStages.Vertex && colorSpaceHandling == ColorSpaceHandling.Legacy)
                    name += "-legacy";
                string resourceName = name + ".glsles";
                return GetEmbeddedResourceBytes(resourceName);
            }
            case GraphicsBackend.Vulkan:
            {
                string resourceName = name + ".spv";
                return GetEmbeddedResourceBytes(resourceName);
            }
            case GraphicsBackend.Metal:
            {
                string resourceName = name + ".metallib";
                return GetEmbeddedResourceBytes(resourceName);
            }
            default:
                throw new NotImplementedException();
        }
    }

    byte[] GetEmbeddedResourceBytes(string resourceName)
    {
        using Stream? s = _assembly.GetManifestResourceStream(resourceName);
        if (s == null)
        {
            throw new InvalidOperationException(
                $"Could not find assembly resource \"{resourceName}\"."
                    + $" Valid resource names: {string.Join(", ", _assembly.GetManifestResourceNames())}"
            );
        }

        byte[] ret = new byte[s.Length];
        int offset = 0;
        do
        {
            int read = s.Read(ret, offset, ret.Length - offset);
            offset += read;
            if (read == 0)
                break;
        } while (offset < ret.Length);

        if (offset != ret.Length)
            throw new EndOfStreamException();

        return ret;
    }

    /// <summary>
    /// Recreates the device texture used to render text.
    /// </summary>
    public void RecreateFontDeviceTexture() => RecreateFontDeviceTexture(_gd);

    /// <summary>
    /// Recreates the device texture used to render text.
    /// </summary>
    [MemberNotNull(nameof(_fontTexture))]
    [MemberNotNull(nameof(_fontTextureResourceSet))]
    public unsafe void RecreateFontDeviceTexture(GraphicsDevice gd)
    {
        ImGuiIOPtr io = ImGui.GetIO();
        // Build
        io.Fonts.GetTexDataAsRGBA32(
            out byte* pixels,
            out int width,
            out int height,
            out int bytesPerPixel
        );

        // Store our identifier
        io.Fonts.SetTexID(_fontAtlasId);

        _fontTexture?.Dispose();
        _fontTexture = gd.ResourceFactory.CreateTexture(
            TextureDescription.Texture2D(
                (uint)width,
                (uint)height,
                1,
                1,
                PixelFormat.R8_G8_B8_A8_UNorm,
                TextureUsage.Sampled
            )
        );
        _fontTexture.Name = "ImGui.NET Font Texture";
        gd.UpdateTexture(
            _fontTexture,
            (IntPtr)pixels,
            (uint)(bytesPerPixel * width * height),
            0,
            0,
            0,
            (uint)width,
            (uint)height,
            1,
            0,
            0
        );

        _fontTextureResourceSet?.Dispose();
        _fontTextureResourceSet = gd.ResourceFactory.CreateResourceSet(
            new(_textureLayout, _fontTexture)
        );
        _fontTextureResourceSet.Name = "ImGui.NET Font Texture Resource Set";

        io.Fonts.ClearTexData();
    }

    /// <summary>
    /// Renders the ImGui draw list data.
    /// </summary>
    public void Render(GraphicsDevice gd, CommandList cl)
    {
        if (_frameBegun)
        {
            _frameBegun = false;
            ImGui.Render();
            RenderImDrawData(ImGui.GetDrawData(), gd, cl);
        }
    }

    /// <summary>
    /// Updates ImGui input and IO configuration state.
    /// </summary>
    public void Update(float deltaSeconds, IInputSnapshot snapshot)
    {
        BeginUpdate(deltaSeconds);
        UpdateImGuiInput(snapshot);
        EndUpdate();
    }

    /// <summary>
    /// Called before we handle the input in <see cref="Update(float, IInputSnapshot)"/>.
    /// This render ImGui and update the state.
    /// </summary>
    protected void BeginUpdate(float deltaSeconds)
    {
        if (_frameBegun)
            ImGui.Render();

        SetPerFrameImGuiData(deltaSeconds);
    }

    /// <summary>
    /// Called at the end of <see cref="Update(float, IInputSnapshot)"/>.
    /// This tells ImGui that we are on the next frame.
    /// </summary>
    protected void EndUpdate()
    {
        _frameBegun = true;
        ImGui.NewFrame();
    }

    /// <summary>
    /// Sets per-frame data based on the associated window.
    /// This is called by Update(float).
    /// </summary>
    void SetPerFrameImGuiData(float deltaSeconds)
    {
        ImGuiIOPtr io = ImGui.GetIO();
        io.DisplaySize = new(_windowWidth / _scaleFactor.X, _windowHeight / _scaleFactor.Y);
        io.DisplayFramebufferScale = _scaleFactor;
        io.DeltaTime = deltaSeconds; // DeltaTime is in seconds.
    }

    static bool TryMapKey(Key key, out ImGuiKey result)
    {
        result = MapKey(key);
        return result != ImGuiKey.None;
    }

    static ImGuiKey MapKey(Key key) =>
        key switch
        {
            Key.A => ImGuiKey.A,
            Key.B => ImGuiKey.B,
            Key.C => ImGuiKey.C,
            Key.D => ImGuiKey.D,
            Key.E => ImGuiKey.E,
            Key.F => ImGuiKey.F,
            Key.G => ImGuiKey.G,
            Key.H => ImGuiKey.H,
            Key.I => ImGuiKey.I,
            Key.J => ImGuiKey.J,
            Key.K => ImGuiKey.K,
            Key.L => ImGuiKey.L,
            Key.M => ImGuiKey.M,
            Key.N => ImGuiKey.N,
            Key.O => ImGuiKey.O,
            Key.P => ImGuiKey.P,
            Key.Q => ImGuiKey.Q,
            Key.R => ImGuiKey.R,
            Key.S => ImGuiKey.S,
            Key.T => ImGuiKey.T,
            Key.U => ImGuiKey.U,
            Key.V => ImGuiKey.V,
            Key.W => ImGuiKey.W,
            Key.X => ImGuiKey.X,
            Key.Y => ImGuiKey.Y,
            Key.Z => ImGuiKey.Z,
            Key.F1 => ImGuiKey.F1,
            Key.F2 => ImGuiKey.F2,
            Key.F3 => ImGuiKey.F3,
            Key.F4 => ImGuiKey.F4,
            Key.F5 => ImGuiKey.F5,
            Key.F6 => ImGuiKey.F6,
            Key.F7 => ImGuiKey.F7,
            Key.F8 => ImGuiKey.F8,
            Key.F9 => ImGuiKey.F9,
            Key.F10 => ImGuiKey.F10,
            Key.F11 => ImGuiKey.F11,
            Key.F12 => ImGuiKey.F12,
            Key.Keypad0 => ImGuiKey.Keypad0,
            Key.Keypad1 => ImGuiKey.Keypad1,
            Key.Keypad2 => ImGuiKey.Keypad2,
            Key.Keypad3 => ImGuiKey.Keypad3,
            Key.Keypad4 => ImGuiKey.Keypad4,
            Key.Keypad5 => ImGuiKey.Keypad5,
            Key.Keypad6 => ImGuiKey.Keypad6,
            Key.Keypad7 => ImGuiKey.Keypad7,
            Key.Keypad8 => ImGuiKey.Keypad8,
            Key.Keypad9 => ImGuiKey.Keypad9,
            Key.Num0 => ImGuiKey._0,
            Key.Num1 => ImGuiKey._1,
            Key.Num2 => ImGuiKey._2,
            Key.Num3 => ImGuiKey._3,
            Key.Num4 => ImGuiKey._4,
            Key.Num5 => ImGuiKey._5,
            Key.Num6 => ImGuiKey._6,
            Key.Num7 => ImGuiKey._7,
            Key.Num8 => ImGuiKey._8,
            Key.Num9 => ImGuiKey._9,
            Key.LeftShift => ImGuiKey.ModShift,
            Key.RightShift => ImGuiKey.ModShift,
            Key.LeftControl => ImGuiKey.ModCtrl,
            Key.RightControl => ImGuiKey.ModCtrl,
            Key.LeftAlt => ImGuiKey.ModAlt,
            Key.RightAlt => ImGuiKey.ModAlt,
            Key.LeftGui => ImGuiKey.ModSuper,
            Key.RightGui => ImGuiKey.ModSuper,
            Key.Menu => ImGuiKey.Menu,
            Key.Up => ImGuiKey.UpArrow,
            Key.Down => ImGuiKey.DownArrow,
            Key.Left => ImGuiKey.LeftArrow,
            Key.Right => ImGuiKey.RightArrow,
            Key.Return => ImGuiKey.Enter,
            Key.Escape => ImGuiKey.Escape,
            Key.Space => ImGuiKey.Space,
            Key.Tab => ImGuiKey.Tab,
            Key.Backspace => ImGuiKey.Backspace,
            Key.Insert => ImGuiKey.Insert,
            Key.Delete => ImGuiKey.Delete,
            Key.PageUp => ImGuiKey.PageUp,
            Key.PageDown => ImGuiKey.PageDown,
            Key.Home => ImGuiKey.Home,
            Key.End => ImGuiKey.End,
            Key.CapsLock => ImGuiKey.CapsLock,
            Key.ScrollLock => ImGuiKey.ScrollLock,
            Key.PrintScreen => ImGuiKey.PrintScreen,
            Key.Pause => ImGuiKey.Pause,
            Key.NumLockClear => ImGuiKey.NumLock,
            Key.KeypadDivide => ImGuiKey.KeypadDivide,
            Key.KeypadMultiply => ImGuiKey.KeypadMultiply,
            Key.KeypadMemorySubtract => ImGuiKey.KeypadSubtract,
            Key.KeypadMemoryAdd => ImGuiKey.KeypadAdd,
            Key.KeypadDecimal => ImGuiKey.KeypadDecimal,
            Key.KeypadEnter => ImGuiKey.KeypadEnter,
            Key.Grave => ImGuiKey.GraveAccent,
            Key.Minus => ImGuiKey.Minus,
            Key.KeypadPlus => ImGuiKey.Equal,
            Key.LeftBracket => ImGuiKey.LeftBracket,
            Key.RightBracket => ImGuiKey.RightBracket,
            Key.Semicolon => ImGuiKey.Semicolon,
            Key.Apostrophe => ImGuiKey.Apostrophe,
            Key.Comma => ImGuiKey.Comma,
            Key.Period => ImGuiKey.Period,
            Key.Slash => ImGuiKey.Slash,
            Key.Backslash => ImGuiKey.Backslash,
            Key.NonUsBackslash => ImGuiKey.Backslash,
            _ => ImGuiKey.None,
        };

    static void UpdateImGuiInput(IInputSnapshot snapshot)
    {
        ImGuiIOPtr io = ImGui.GetIO();

        Vector2 mousePos = snapshot.MousePosition;
        io.AddMousePosEvent(mousePos.X, mousePos.Y);

        MouseButton snapMouseDown = snapshot.MouseDown;
        io.AddMouseButtonEvent(0, (snapMouseDown & MouseButton.Left) != 0);
        io.AddMouseButtonEvent(1, (snapMouseDown & MouseButton.Right) != 0);
        io.AddMouseButtonEvent(2, (snapMouseDown & MouseButton.Middle) != 0);
        io.AddMouseButtonEvent(3, (snapMouseDown & MouseButton.Button1) != 0);
        io.AddMouseButtonEvent(4, (snapMouseDown & MouseButton.Button2) != 0);

        Vector2 wheelDelta = snapshot.WheelDelta;
        io.AddMouseWheelEvent(wheelDelta.X, wheelDelta.Y);

        foreach (Rune rune in snapshot.InputEvents)
            io.AddInputCharacter((uint)rune.Value);

        foreach (KeyEvent keyEvent in snapshot.KeyEvents)
            if (TryMapKey(keyEvent.Physical, out ImGuiKey imguikey))
                io.AddKeyEvent(imguikey, keyEvent.Down);
    }

    unsafe void RenderImDrawData(ImDrawDataPtr drawData, GraphicsDevice gd, CommandList cl)
    {
        uint vertexOffsetInVertices = 0;
        uint indexOffsetInElements = 0;

        if (drawData.CmdListsCount == 0)
            return;

        uint totalVbSize = (uint)(drawData.TotalVtxCount * sizeof(ImDrawVert));
        if (totalVbSize > _vertexBuffer.SizeInBytes)
        {
            string? name = _vertexBuffer.Name;
            _vertexBuffer.Dispose();
            _vertexBuffer = gd.ResourceFactory.CreateBuffer(
                new((uint)(totalVbSize * 1.5f), BufferUsage.VertexBuffer | BufferUsage.DynamicWrite)
            );
            _vertexBuffer.Name = name;
        }

        uint totalIbSize = (uint)(drawData.TotalIdxCount * sizeof(ushort));
        if (totalIbSize > _indexBuffer.SizeInBytes)
        {
            string? name = _indexBuffer.Name;
            _indexBuffer.Dispose();
            _indexBuffer = gd.ResourceFactory.CreateBuffer(
                new((uint)(totalIbSize * 1.5f), BufferUsage.IndexBuffer | BufferUsage.DynamicWrite)
            );
            _indexBuffer.Name = name;
        }

        for (int i = 0; i < drawData.CmdListsCount; i++)
        {
            ImDrawListPtr cmdList = drawData.CmdLists[i];

            cl.UpdateBuffer(
                _vertexBuffer,
                vertexOffsetInVertices * (uint)sizeof(ImDrawVert),
                cmdList.VtxBuffer.Data,
                (uint)(cmdList.VtxBuffer.Size * sizeof(ImDrawVert))
            );

            cl.UpdateBuffer(
                _indexBuffer,
                indexOffsetInElements * sizeof(ushort),
                cmdList.IdxBuffer.Data,
                (uint)(cmdList.IdxBuffer.Size * sizeof(ushort))
            );

            vertexOffsetInVertices += (uint)cmdList.VtxBuffer.Size;
            indexOffsetInElements += (uint)cmdList.IdxBuffer.Size;
        }

        // Setup orthographic projection matrix into our constant buffer
        {
            ImGuiIOPtr io = ImGui.GetIO();

            Matrix4x4 mvp = Matrix4x4.CreateOrthographicOffCenter(
                0f,
                io.DisplaySize.X,
                io.DisplaySize.Y,
                0.0f,
                -1.0f,
                1.0f
            );

            cl.UpdateBuffer(_projMatrixBuffer, 0, ref mvp);
        }

        cl.SetVertexBuffer(0, _vertexBuffer);
        cl.SetIndexBuffer(_indexBuffer, IndexFormat.UInt16);
        cl.SetPipeline(_pipeline);
        cl.SetGraphicsResourceSet(0, _mainResourceSet);

        drawData.ScaleClipRects(ImGui.GetIO().DisplayFramebufferScale);

        // Render command lists
        int vtxOffset = 0;
        int idxOffset = 0;
        for (int n = 0; n < drawData.CmdListsCount; n++)
        {
            ImDrawListPtr cmdList = drawData.CmdLists[n];
            for (int i = 0; i < cmdList.CmdBuffer.Size; i++)
            {
                ImDrawCmdPtr pcmd = cmdList.CmdBuffer[i];
                if (pcmd.UserCallback != IntPtr.Zero)
                    throw new NotImplementedException("No ImGui user callback specified");

                if (pcmd.TextureId != IntPtr.Zero)
                {
                    cl.SetGraphicsResourceSet(
                        1,
                        pcmd.TextureId == _fontAtlasId
                            ? _fontTextureResourceSet
                            : GetImageResourceSet(pcmd.TextureId)
                    );
                }

                cl.SetScissorRect(
                    0,
                    (uint)pcmd.ClipRect.X,
                    (uint)pcmd.ClipRect.Y,
                    (uint)(pcmd.ClipRect.Z - pcmd.ClipRect.X),
                    (uint)(pcmd.ClipRect.W - pcmd.ClipRect.Y)
                );

                cl.DrawIndexed(
                    pcmd.ElemCount,
                    1,
                    pcmd.IdxOffset + (uint)idxOffset,
                    (int)(pcmd.VtxOffset + vtxOffset),
                    0
                );
            }

            idxOffset += cmdList.IdxBuffer.Size;
            vtxOffset += cmdList.VtxBuffer.Size;
        }
    }

    struct ResourceSetInfo(IntPtr imGuiBinding, ResourceSet resourceSet)
    {
        public readonly IntPtr ImGuiBinding = imGuiBinding;
        public readonly ResourceSet ResourceSet = resourceSet;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
            DestroyDeviceObjects();

        _disposed = true;
    }

    /// <summary>
    /// Frees all graphics resources used by the renderer.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
