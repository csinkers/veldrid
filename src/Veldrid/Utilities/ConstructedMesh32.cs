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
    /// The first index array of the mesh.
    /// </summary>
    public uint[] Indices { get; }

    /// <summary>
    /// The number of indices in the mesh.
    /// </summary>
    public override int IndexCount => Indices.Length;

    /// <summary>
    /// The format of the indices in the mesh.
    /// </summary>
    public override IndexFormat IndexFormat => IndexFormat.UInt32;

    /// <summary>
    /// Constructs a new <see cref="ConstructedMesh32"/>.
    /// </summary>
    /// <param name="vertices">The vertices.</param>
    /// <param name="indices">The indices.</param>
    /// <param name="materialName">The name of the associated MTL <see cref="MaterialDefinition"/>.</param>
    public ConstructedMesh32(
        VertexPositionNormalTexture[] vertices,
        uint[] indices,
        string? materialName
    )
        : base(vertices, materialName)
    {
        Indices = indices ?? throw new ArgumentNullException(nameof(indices));
    }

    /// <inheritdoc />
    public override DeviceBuffer CreateIndexBuffer(ResourceFactory factory, CommandList cl)
    {
        DeviceBuffer ib = factory.CreateBuffer(
            new((uint)Indices.Length * sizeof(uint), BufferUsage.IndexBuffer)
        );

        cl.UpdateBuffer(ib, 0, Indices);
        return ib;
    }

    /// <inheritdoc />
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

            if (!ray.Intersects(v0, v1, v2, out float newDistance))
                continue;

            if (newDistance < distance)
            {
                distance = newDistance;

                if (any)
                    return true;
            }

            result = true;
        }

        return result;
    }

    /// <summary>
    /// Casts a ray against the mesh.
    /// </summary>
    public RayEnumerator RayCast(Ray ray) => new(this, ray);

    /// <summary>
    /// An enumerator for ray casting against a mesh.
    /// </summary>
    public struct RayEnumerator(ConstructedMesh32 mesh, Ray ray) : IEnumerator<float>
    {
        int _indexOffset = 0;

        /// <summary>
        /// The mesh to ray cast against.
        /// </summary>
        public ConstructedMesh32 Mesh { get; } = mesh ?? throw new ArgumentNullException(nameof(mesh));

        /// <summary>
        /// The ray to cast.
        /// </summary>
        public Ray Ray { get; } = ray;

        /// <summary>
        /// The current hit's distance from the ray origin.
        /// </summary>
        public float Current { get; private set; } = 0;

        object IEnumerator.Current => Current;

        /// <summary>
        /// Advances the enumerator to the next hit.
        /// </summary>
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

            Current = 0;
            return false;
        }

        /// <summary>
        /// Resets the enumerator.
        /// </summary>
        public void Reset()
        {
            Current = 0;
            _indexOffset = 0;
        }

        /// <summary>
        /// Gets the enumerator.
        /// </summary>
        public RayEnumerator GetEnumerator() => this;

        /// <summary>
        /// Disposes the enumerator.
        /// </summary>
        public void Dispose() { }
    }
}
