﻿using System;

namespace Veldrid
{
    /// <summary>
    /// A section of a <see cref="DeviceBuffer"/> that can be used when creating a <see cref="ResourceSet"/> to
    /// make only a subset of the buffer available to shaders.
    /// </summary>
    /// <seealso cref="BindableResource"/>
    public struct DeviceBufferRange : IEquatable<DeviceBufferRange>
    {
        /// <summary>
        /// The underlying <see cref="DeviceBuffer"/> that this range refers to.
        /// </summary>
        public DeviceBuffer Buffer;

        /// <summary>
        /// The offset, in bytes, from the beginning of the buffer that this range starts at.
        /// </summary>
        public uint Offset;

        /// <summary>
        /// The total number of bytes that this range encompasses.
        /// </summary>
        public uint SizeInBytes;

        /// <summary>
        /// Constructs a new <see cref="DeviceBufferRange"/>.
        /// </summary>
        /// <param name="buffer">The underlying <see cref="DeviceBuffer"/> that this range will refer to.</param>
        /// <param name="offset">The offset, in bytes, from the beginning of the buffer that this range will start at.</param>
        /// <param name="sizeInBytes">The total number of bytes that this range will encompass.</param>
        public DeviceBufferRange(DeviceBuffer buffer, uint offset, uint sizeInBytes)
        {
            Buffer = buffer;
            Offset = offset;
            SizeInBytes = sizeInBytes;
        }

        /// <summary>
        /// Element-wise equality.
        /// </summary>
        /// <param name="other">The instance to compare to.</param>
        /// <returns>True if all elements are equal; false otherswise.</returns>
        public readonly bool Equals(DeviceBufferRange other)
        {
            return Buffer == other.Buffer && Offset == other.Offset && SizeInBytes == other.SizeInBytes;
        }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>A 32-bit signed integer that is the hash code for this instance.</returns>
        public override readonly int GetHashCode()
        {
            int bufferHash = Buffer?.GetHashCode() ?? 0;
            return HashHelper.Combine(bufferHash, Offset.GetHashCode(), SizeInBytes.GetHashCode());
        }
    }
}
