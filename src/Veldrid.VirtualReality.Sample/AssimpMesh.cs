using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using Assimp;
using Veldrid.ImageSharp;
using Veldrid.SPIRV;
using Matrix4x4 = System.Numerics.Matrix4x4;

namespace Veldrid.VirtualReality.Sample;

internal class AssimpMesh : IDisposable
{
    readonly GraphicsDevice _gd;
    readonly List<IDisposable> _disposables = [];
    readonly List<MeshPiece> _meshPieces = [];
    readonly Pipeline _pipeline;
    readonly DeviceBuffer _wvpBuffer;
    readonly ResourceSet _rs;

    public AssimpMesh(
        GraphicsDevice gd,
        OutputDescription outputs,
        string meshPath,
        string texturePath
    )
    {
        _gd = gd;
        ResourceFactory factory = gd.ResourceFactory;

        Shader[] shaders = factory.CreateFromSpirv(
            new(ShaderStages.Vertex, Encoding.ASCII.GetBytes(VertexGlsl), "main"),
            new ShaderDescription(
                ShaderStages.Fragment,
                Encoding.ASCII.GetBytes(FragmentGlsl),
                "main"
            )
        );
        _disposables.Add(shaders[0]);
        _disposables.Add(shaders[1]);

        ResourceLayout rl = factory.CreateResourceLayout(
            new(
                new ResourceLayoutElementDescription(
                    "WVP",
                    ResourceKind.UniformBuffer,
                    ShaderStages.Vertex
                ),
                new ResourceLayoutElementDescription(
                    "Input",
                    ResourceKind.TextureReadOnly,
                    ShaderStages.Fragment
                ),
                new ResourceLayoutElementDescription(
                    "InputSampler",
                    ResourceKind.Sampler,
                    ShaderStages.Fragment
                )
            )
        );
        _disposables.Add(rl);

        VertexLayoutDescription positionLayoutDesc = new(
            [new("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3)]
        );

        VertexLayoutDescription texCoordLayoutDesc = new(
            [new("UV", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2)]
        );

        _pipeline = factory.CreateGraphicsPipeline(
            new(
                BlendStateDescription.SingleOverrideBlend,
                DepthStencilStateDescription.DepthOnlyLessEqual,
                RasterizerStateDescription.CullNone,
                PrimitiveTopology.TriangleList,
                new([positionLayoutDesc, texCoordLayoutDesc], [shaders[0], shaders[1]]),
                rl,
                outputs
            )
        );
        _disposables.Add(_pipeline);

        _wvpBuffer = factory.CreateBuffer(
            new(64 * 3, BufferUsage.UniformBuffer | BufferUsage.DynamicWrite)
        );
        _disposables.Add(_wvpBuffer);

        Texture texture = new ImageSharpTexture(texturePath, true, true).CreateDeviceTexture(
            gd,
            factory
        );
        TextureView view = factory.CreateTextureView(texture);
        _disposables.Add(texture);
        _disposables.Add(view);

        _rs = factory.CreateResourceSet(new(rl, _wvpBuffer, view, gd.Aniso4xSampler));
        _disposables.Add(_rs);

        AssimpContext ac = new();
        Scene scene = ac.ImportFile(meshPath);

        foreach (Mesh mesh in scene.Meshes)
        {
            DeviceBuffer positions = CreateDeviceBuffer(mesh.Vertices, BufferUsage.VertexBuffer);
            DeviceBuffer texCoords = CreateDeviceBuffer(
                mesh.TextureCoordinateChannels[0].Select(v3 => new Vector2(v3.X, v3.Y)).ToArray(),
                BufferUsage.VertexBuffer
            );
            DeviceBuffer indices = CreateDeviceBuffer(
                mesh.GetUnsignedIndices(),
                BufferUsage.IndexBuffer
            );

            _meshPieces.Add(new(positions, texCoords, indices));
        }
    }

    public DeviceBuffer CreateDeviceBuffer<T>(IList<T> list, BufferUsage usage)
        where T : unmanaged
    {
        DeviceBuffer buffer = _gd.ResourceFactory.CreateBuffer(
            new((uint)(Unsafe.SizeOf<T>() * list.Count), usage)
        );
        _disposables.Add(buffer);
        _gd.UpdateBuffer(buffer, 0, list.ToArray());
        return buffer;
    }

    public void Render(CommandList cl, UBO ubo)
    {
        cl.UpdateBuffer(_wvpBuffer, 0, ubo);
        cl.SetPipeline(_pipeline);
        foreach (MeshPiece piece in _meshPieces)
        {
            cl.SetVertexBuffer(0, piece.Positions);
            cl.SetVertexBuffer(1, piece.TexCoords);
            cl.SetIndexBuffer(piece.Indices, IndexFormat.UInt32);
            cl.SetGraphicsResourceSet(0, _rs);
            cl.DrawIndexed(piece.IndexCount);
        }
    }

    const string VertexGlsl =
        @"
#version 450

layout (set = 0, binding = 0) uniform WVP
{
    mat4 Proj;
    mat4 View;
    mat4 World;
};

layout (location = 0) in vec3 vsin_Position;
layout (location = 1) in vec2 vsin_UV;

layout (location = 0) out vec2 fsin_UV;

void main()
{
    gl_Position = Proj * View * World * vec4(vsin_Position, 1);
    fsin_UV = vsin_UV;
}
";

    const string FragmentGlsl =
        @"
#version 450

layout(set = 0, binding = 1) uniform texture2D Input;
layout(set = 0, binding = 2) uniform sampler InputSampler;

layout(location = 0) in vec2 fsin_UV;
layout(location = 0) out vec4 fsout_Color0;

layout(constant_id = 100) const bool ClipSpaceInvertedY = true;
layout(constant_id = 102) const bool ReverseDepthRange = true;

void main()
{
    vec2 uv = fsin_UV;
    uv.y = 1 - uv.y;

    fsout_Color0 = texture(sampler2D(Input, InputSampler), uv);
}
";

    public void Dispose()
    {
        foreach (IDisposable disposable in _disposables)
        {
            disposable.Dispose();
        }
        _disposables.Clear();
    }
}

internal class MeshPiece(DeviceBuffer positions, DeviceBuffer texCoords, DeviceBuffer indices)
{
    public DeviceBuffer Positions { get; } = positions;
    public DeviceBuffer TexCoords { get; } = texCoords;
    public DeviceBuffer Indices { get; } = indices;
    public uint IndexCount { get; } = indices.SizeInBytes / sizeof(uint);
}

internal struct UBO(Matrix4x4 projection, Matrix4x4 view, Matrix4x4 world)
{
    public Matrix4x4 Projection = projection;
    public Matrix4x4 View = view;
    public Matrix4x4 World = world;
}
