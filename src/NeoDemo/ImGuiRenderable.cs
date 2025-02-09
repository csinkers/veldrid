﻿using System;
using System.Diagnostics;
using System.Numerics;
using Veldrid.Sdl2;

namespace Veldrid.NeoDemo;

public class ImGuiRenderable(int width, int height) : Renderable, IUpdateable
{
    ImGuiRenderer _imguiRenderer;

    public void WindowResized(int width, int height) => _imguiRenderer.WindowResized(width, height);

    public override void CreateDeviceObjects(GraphicsDevice gd, CommandList cl, SceneContext sc)
    {
        if (_imguiRenderer == null)
        {
            _imguiRenderer = new ImGuiRenderer(gd, sc.MainSceneFramebuffer.OutputDescription, width, height, ColorSpaceHandling.Linear);
        }
        else
        {
            _imguiRenderer.CreateDeviceResources(gd, sc.MainSceneFramebuffer.OutputDescription, ColorSpaceHandling.Linear);
        }
    }

    public override void DestroyDeviceObjects()
    {
        _imguiRenderer.DestroyDeviceObjects();
    }

    public override RenderOrderKey GetRenderOrderKey(Vector3 cameraPosition)
    {
        return new RenderOrderKey(ulong.MaxValue);
    }

    public override void Render(GraphicsDevice gd, CommandList cl, SceneContext sc, RenderPasses renderPass)
    {
        Debug.Assert(renderPass == RenderPasses.Overlay);
        _imguiRenderer.Render(gd, cl);
    }

    public override void UpdatePerFrameResources(GraphicsDevice gd, CommandList cl, SceneContext sc)
    {
    }

    public override RenderPasses RenderPasses => RenderPasses.Overlay;

    public void Update(float deltaSeconds)
    {
        _imguiRenderer.Update(deltaSeconds, InputTracker.FrameSnapshot);
    }
}