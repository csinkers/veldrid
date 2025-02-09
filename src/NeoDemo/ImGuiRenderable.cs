using System.Diagnostics;
using System.Numerics;

namespace Veldrid.NeoDemo;

public class ImGuiRenderable(int width, int height) : Renderable, IUpdateable
{
    ImGuiRenderer? _imguiRenderer;
    int _width = width;
    int _height = height;

    public void WindowResized(int width, int height)
    {
        _width = width;
        _height = height;
        _imguiRenderer?.WindowResized(_width, _height);
    }

    public override void CreateDeviceObjects(GraphicsDevice gd, CommandList cl, SceneContext sc)
    {
        if (_imguiRenderer == null)
        {
            _imguiRenderer = new(
                gd,
                sc.MainSceneFramebuffer.OutputDescription,
                _width,
                _height,
                ColorSpaceHandling.Linear
            );
        }
        else
        {
            _imguiRenderer.CreateDeviceResources(
                gd,
                sc.MainSceneFramebuffer.OutputDescription,
                ColorSpaceHandling.Linear
            );
        }
    }

    public override void DestroyDeviceObjects() => _imguiRenderer?.DestroyDeviceObjects();

    public override RenderOrderKey GetRenderOrderKey(Vector3 cameraPosition) => new(ulong.MaxValue);

    public override void Render(
        GraphicsDevice gd,
        CommandList cl,
        SceneContext sc,
        RenderPasses renderPass
    )
    {
        Debug.Assert(renderPass == RenderPasses.Overlay);
        _imguiRenderer?.Render(gd, cl);
    }

    public override void UpdatePerFrameResources(
        GraphicsDevice gd,
        CommandList cl,
        SceneContext sc
    ) { }

    public override RenderPasses RenderPasses => RenderPasses.Overlay;

    public void Update(float deltaSeconds)
    {
        if (InputTracker.FrameSnapshot != null)
            _imguiRenderer?.Update(deltaSeconds, InputTracker.FrameSnapshot);
    }
}
