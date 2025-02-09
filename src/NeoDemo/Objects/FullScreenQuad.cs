using System;
using System.Numerics;
using Veldrid.Utilities;

namespace Veldrid.NeoDemo.Objects;

internal class FullScreenQuad : Renderable
{
    DisposeCollector? _disposeCollector;
    Pipeline? _pipeline;
    DeviceBuffer? _ib;
    DeviceBuffer? _vb;

    public bool UseMultipleRenderTargets { get; set; }

    public override void CreateDeviceObjects(GraphicsDevice gd, CommandList cl, SceneContext sc)
    {
        if (gd.SwapchainFramebuffer == null)
            throw new InvalidOperationException("No swapchain on device");

        DisposeCollectorResourceFactory factory = new(gd.ResourceFactory);
        _disposeCollector = factory.DisposeCollector;

        ResourceLayout resourceLayout = factory.CreateResourceLayout(
            new(
                new("SourceTexture", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
                new("SourceSampler", ResourceKind.Sampler, ShaderStages.Fragment)
            )
        );

        (Shader vs, Shader fs) = StaticResourceCache.GetShaders(
            gd,
            gd.ResourceFactory,
            "FullScreenQuad"
        );

        GraphicsPipelineDescription pd = new(
            new(RgbaFloat.Black, BlendAttachmentDescription.OverrideBlend),
            DepthStencilStateDescription.Disabled,
            new(FaceCullMode.Back, PolygonFillMode.Solid, FrontFace.Clockwise, true, false),
            PrimitiveTopology.TriangleList,
            new(
                [
                    new(
                        new VertexElementDescription(
                            "Position",
                            VertexElementSemantic.TextureCoordinate,
                            VertexElementFormat.Float2
                        ),
                        new VertexElementDescription(
                            "TexCoords",
                            VertexElementSemantic.TextureCoordinate,
                            VertexElementFormat.Float2
                        )
                    ),
                ],
                [vs, fs],
                ShaderHelper.GetSpecializations(gd)
            ),
            [resourceLayout],
            gd.SwapchainFramebuffer.OutputDescription
        );
        _pipeline = factory.CreateGraphicsPipeline(pd);

        float[] verts = Util.GetFullScreenQuadVerts(gd);

        _vb = factory.CreateBuffer(new(verts.SizeInBytes(), BufferUsage.VertexBuffer));
        cl.UpdateBuffer(_vb, 0, verts);

        _ib = factory.CreateBuffer(new(s_quadIndices.SizeInBytes(), BufferUsage.IndexBuffer));
        cl.UpdateBuffer(_ib, 0, s_quadIndices);
    }

    public override void DestroyDeviceObjects() => _disposeCollector?.DisposeAll();

    public override RenderOrderKey GetRenderOrderKey(Vector3 cameraPosition) => new();

    public override void Render(
        GraphicsDevice gd,
        CommandList cl,
        SceneContext sc,
        RenderPasses renderPass
    )
    {
        cl.SetPipeline(_pipeline!);
        cl.SetGraphicsResourceSet(
            0,
            UseMultipleRenderTargets ? sc.DuplicatorTargetSet1 : sc.DuplicatorTargetSet0
        );
        cl.SetVertexBuffer(0, _vb!);
        cl.SetIndexBuffer(_ib!, IndexFormat.UInt16);
        cl.DrawIndexed(6, 1, 0, 0, 0);
    }

    public override RenderPasses RenderPasses => RenderPasses.SwapchainOutput;

    public override void UpdatePerFrameResources(
        GraphicsDevice gd,
        CommandList cl,
        SceneContext sc
    ) { }

    static readonly ushort[] s_quadIndices = [0, 1, 2, 0, 2, 3];
}
