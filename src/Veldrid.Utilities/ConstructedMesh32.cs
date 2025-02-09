using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;

namespace Veldrid.Utilities;

/// <summary>
/// A standalone mesh created from information from an <see cref="ObjFile"/>.
/// </summary>
public class ConstructedMesh32 : ConstructedMesh
{
    /// <summary>
    /// The the first index array of the mesh.
    /// </summary>
    public uint[] Indices { get; }

    public override int IndexCount => Indices.Length;

    public override IndexFormat IndexFormat => IndexFormat.UInt32;

    /// <summary>
    /// Constructs a new <see cref="ConstructedMesh32"/>.
    /// </summary>
    /// <param name="vertices">The vertices.</param>
    /// <param name="indices">The indices.</param>
    /// <param name="materialName">The name of the associated MTL <see cref="MaterialDefinition"/>.</param>
    public ConstructedMesh32(VertexPositionNormalTexture[] vertices, uint[] indices, string? materialName) :
        base(vertices, materialName)
    {
        Indices = indices ?? throw new ArgumentNullException(nameof(indices));
    }

    public override DeviceBuffer CreateIndexBuffer(ResourceFactory factory, CommandList cl)
    {
        DeviceBuffer ib = factory.CreateBuffer(new BufferDescription((uint)Indices.Length * sizeof(uint), BufferUsage.IndexBuffer));
        cl.UpdateBuffer(ib, 0, Indices);
        return ib;
    }

    public override bool RayCast(Ray ray, bool any, out float distance)
    {
        VertexPositionNormalTexture[] vertices = Vertices;
        uint[] indices = Indices;

        distance = float.MaxValue;
        bool result = false;
        for (int i = 0; i < Indices.Length - 2; i += 3)
        {
            Vector3 v0 = vertices[indices[i + 0]].Position;
            Vector3 v1 = vertices[indices[i + 1]].Position;
            Vector3 v2 = vertices[indices[i + 2]].Position;

            if (ray.Intersects(v0, v1, v2, out float newDistance))
            {
                if (newDistance < distance)
                {
                    distance = newDistance;

                    if (any)
                    {
                        return true;
                    }
                }
                result = true;
            }
        }

        return result;
    }

    public RayEnumerator RayCast(Ray ray)
    {
        return new RayEnumerator(this, ray);
    }

    public struct RayEnumerator(ConstructedMesh32 mesh, Ray ray) : IEnumerator<float>
    {
        int _indexOffset = 0;

        public ConstructedMesh32 Mesh { get; } = mesh ?? throw new ArgumentNullException(nameof(mesh));
        public Ray Ray { get; } = ray;

        public float Current { get; private set; } = default;
        object? IEnumerator.Current => Current;

        public bool MoveNext()
        {
            VertexPositionNormalTexture[] vertices = Mesh.Vertices;
            uint[] indices = Mesh.Indices;

            for (; _indexOffset < indices.Length - 2; _indexOffset += 3)
            {
                Vector3 v0 = vertices[indices[_indexOffset + 0]].Position;
                Vector3 v1 = vertices[indices[_indexOffset + 1]].Position;
                Vector3 v2 = vertices[indices[_indexOffset + 2]].Position;

                if (Ray.Intersects(v0, v1, v2, out float distance))
                {
                    Current = distance;
                    return true;
                }
            }

            Current = default;
            return false;
        }

        public void Reset()
        {
            Current = default;
            _indexOffset = 0;
        }

        public RayEnumerator GetEnumerator()
        {
            return this;
        }

        public void Dispose()
        {
        }
    }
}