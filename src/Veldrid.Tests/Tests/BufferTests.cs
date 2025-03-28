using System;
using System.Linq;
using System.Numerics;
using Veldrid.Tests.Utilities;
using Xunit;

namespace Veldrid.Tests;

public abstract class BufferTestBase<T> : GraphicsDeviceTestBase<T>
    where T : IGraphicsDeviceCreator
{
    [Fact]
    public void CreateBuffer_Succeeds()
    {
        uint expectedSize = 64;
        BufferUsage expectedUsage = BufferUsage.DynamicWrite | BufferUsage.UniformBuffer;

        DeviceBuffer buffer = RF.CreateBuffer(new(expectedSize, expectedUsage));

        Assert.Equal(expectedUsage, buffer.Usage);
        Assert.Equal(expectedSize, buffer.SizeInBytes);
    }

    [Fact]
    public void UpdateBuffer_NonDynamic_Succeeds()
    {
        DeviceBuffer buffer = CreateBuffer(64, BufferUsage.VertexBuffer);
        GD.UpdateBuffer(buffer, 0, Matrix4x4.Identity);
        GD.WaitForIdle();
    }

    [Fact]
    public void UpdateBuffer_Span_Succeeds()
    {
        DeviceBuffer buffer = CreateBuffer(64, BufferUsage.VertexBuffer);
        float[] data = new float[16];
        GD.UpdateBuffer(buffer, 0, (ReadOnlySpan<float>)data);
        GD.WaitForIdle();
    }

    [Fact]
    public void UpdateBuffer_ThenMapRead_Succeeds()
    {
        DeviceBuffer buffer = CreateBuffer(1024, BufferUsage.StagingReadWrite);
        int[] data = Enumerable.Range(0, 256).Select(i => 2 * i).ToArray();
        GD.UpdateBuffer(buffer, 0, data);

        MappedResourceView<int> view = GD.Map<int>(buffer, MapMode.Read);
        for (int i = 0; i < view.Count; i++)
        {
            Assert.Equal(i * 2, view[i]);
        }
    }

    [Fact]
    public unsafe void Staging_Map_WriteThenRead()
    {
        DeviceBuffer buffer = CreateBuffer(256, BufferUsage.StagingReadWrite);
        MappedResource map = GD.Map(buffer, MapMode.Write);
        byte* dataPtr = (byte*)map.Data.ToPointer();
        for (int i = 0; i < map.SizeInBytes; i++)
        {
            dataPtr[i] = (byte)i;
        }
        GD.Unmap(buffer);

        map = GD.Map(buffer, MapMode.Read);
        dataPtr = (byte*)map.Data.ToPointer();
        for (int i = 0; i < map.SizeInBytes; i++)
        {
            Assert.Equal((byte)i, dataPtr[i]);
        }
    }

    [Fact]
    public void Staging_MapGeneric_WriteThenRead()
    {
        DeviceBuffer buffer = CreateBuffer(1024, BufferUsage.StagingReadWrite);
        MappedResourceView<int> view = GD.Map<int>(buffer, MapMode.Write);
        Assert.Equal(256, view.Count);
        for (int i = 0; i < view.Count; i++)
        {
            view[i] = i * 10;
        }
        GD.Unmap(buffer);

        view = GD.Map<int>(buffer, MapMode.Read);
        Assert.Equal(256, view.Count);
        for (int i = 0; i < view.Count; i++)
        {
            view[i] = 1 * 10;
        }
        GD.Unmap(buffer);
    }

    [Fact]
    public void Map_WrongFlags_Throws()
    {
        DeviceBuffer buffer = CreateBuffer(1024, BufferUsage.VertexBuffer);
        Assert.Throws<VeldridException>(() => GD.Map(buffer, MapMode.Read));
        Assert.Throws<VeldridException>(() => GD.Map(buffer, MapMode.Write));
        Assert.Throws<VeldridException>(() => GD.Map(buffer, MapMode.ReadWrite));
    }

    [Fact]
    public void CopyBuffer_Succeeds()
    {
        DeviceBuffer src = CreateBuffer(1024, BufferUsage.StagingWrite);
        int[] data = Enumerable.Range(0, 256).Select(i => 2 * i).ToArray();
        GD.UpdateBuffer(src, 0, data);

        DeviceBuffer dst = CreateBuffer(1024, BufferUsage.StagingRead);

        CommandList copyCL = RF.CreateCommandList();
        copyCL.Begin();
        copyCL.CopyBuffer(src, 0, dst, 0, src.SizeInBytes);
        copyCL.End();
        GD.SubmitCommands(copyCL);
        GD.WaitForIdle();
        src.Dispose();

        MappedResourceView<int> view = GD.Map<int>(dst, MapMode.Read);
        for (int i = 0; i < view.Count; i++)
        {
            Assert.Equal(i * 2, view[i]);
        }
    }

    [Fact]
    public void CopyBuffer_Chain_Succeeds()
    {
        DeviceBuffer src = CreateBuffer(1024, BufferUsage.StagingWrite);
        int[] data = Enumerable.Range(0, 256).Select(i => 2 * i).ToArray();
        GD.UpdateBuffer(src, 0, data);

        DeviceBuffer finalDst = CreateBuffer(1024, BufferUsage.StagingRead);

        for (int chainLength = 2; chainLength <= 10; chainLength += 4)
        {
            DeviceBuffer[] dsts = Enumerable
                .Range(0, chainLength)
                .Select(_ => RF.CreateBuffer(new(1024, BufferUsage.UniformBuffer)))
                .ToArray();

            CommandList copyCL = RF.CreateCommandList();
            copyCL.Begin();
            copyCL.CopyBuffer(src, 0, dsts[0], 0, src.SizeInBytes);
            for (int i = 0; i < chainLength - 1; i++)
            {
                copyCL.CopyBuffer(dsts[i], 0, dsts[i + 1], 0, src.SizeInBytes);
            }
            copyCL.CopyBuffer(dsts[^1], 0, finalDst, 0, src.SizeInBytes);
            copyCL.End();
            GD.SubmitCommands(copyCL);
            GD.WaitForIdle();

            MappedResourceView<int> view = GD.Map<int>(finalDst, MapMode.Read);
            for (int i = 0; i < view.Count; i++)
            {
                Assert.Equal(i * 2, view[i]);
            }
            GD.Unmap(finalDst);
        }
    }

    [SkippableFact]
    public void MapThenUpdate_Fails()
    {
        Skip.If(GD.BackendType == GraphicsBackend.Vulkan); // TODO
        Skip.If(GD.BackendType == GraphicsBackend.Metal); // TODO

        DeviceBuffer buffer = RF.CreateBuffer(new(1024, BufferUsage.StagingReadWrite));
        _ = GD.Map<int>(buffer, MapMode.ReadWrite);
        int[] data = Enumerable.Range(0, 256).Select(i => 2 * i).ToArray();
        Assert.Throws<VeldridMappedResourceException>(() => GD.UpdateBuffer(buffer, 0, data));
    }

    [SkippableFact]
    public void Map_MultipleTimes_Fails()
    {
        Skip.If(GD.BackendType == GraphicsBackend.Vulkan); // TODO
        Skip.If(GD.BackendType == GraphicsBackend.Metal); // TODO

        DeviceBuffer buffer = RF.CreateBuffer(new(1024, BufferUsage.StagingReadWrite));
        _ = GD.Map(buffer, MapMode.ReadWrite);
        Assert.Throws<VeldridMappedResourceException>(() => GD.Map(buffer, MapMode.ReadWrite));
        GD.Unmap(buffer);
    }

    [Fact]
    public void Map_DifferentMode_WriteToReadFails()
    {
        DeviceBuffer buffer = RF.CreateBuffer(new(1024, BufferUsage.StagingRead));
        Assert.Throws<VeldridException>(() => GD.Map(buffer, MapMode.Write));
    }

    [Fact]
    public void Map_DifferentMode_ReadFromWriteFails()
    {
        DeviceBuffer buffer = RF.CreateBuffer(new(1024, BufferUsage.StagingWrite));
        Assert.Throws<VeldridException>(() => GD.Map(buffer, MapMode.Read));
    }

    [Fact]
    public unsafe void UnusualSize()
    {
        DeviceBuffer src = RF.CreateBuffer(new(208, BufferUsage.UniformBuffer));
        DeviceBuffer dst = RF.CreateBuffer(new(208, BufferUsage.StagingRead));

        byte[] data = Enumerable.Range(0, 208).Select(i => (byte)(i * 150)).ToArray();
        GD.UpdateBuffer(src, 0, data);

        CommandList cl = RF.CreateCommandList();
        cl.Begin();
        cl.CopyBuffer(src, 0, dst, 0, src.SizeInBytes);
        cl.End();
        GD.SubmitCommands(cl);
        GD.WaitForIdle();
        MappedResource readMap = GD.Map(dst, MapMode.Read);
        for (int i = 0; i < readMap.SizeInBytes; i++)
        {
            Assert.Equal((byte)(i * 150), ((byte*)readMap.Data)[i]);
        }
    }

    [Fact]
    public void Update_Dynamic_NonZeroOffset()
    {
        DeviceBuffer dynamic = RF.CreateBuffer(
            new(1024, BufferUsage.DynamicWrite | BufferUsage.UniformBuffer)
        );

        byte[] initialData = Enumerable.Range(0, 1024).Select(i => (byte)i).ToArray();
        GD.UpdateBuffer(dynamic, 0, initialData);

        byte[] replacementData = Enumerable.Repeat((byte)255, 512).ToArray();
        CommandList cl = RF.CreateCommandList();
        cl.Begin();
        cl.UpdateBuffer(dynamic, 512, replacementData);
        cl.End();
        GD.SubmitCommands(cl);
        GD.WaitForIdle();

        DeviceBuffer dst = RF.CreateBuffer(new(1024, BufferUsage.StagingRead));

        cl.Begin();
        cl.CopyBuffer(dynamic, 0, dst, 0, dynamic.SizeInBytes);
        cl.End();
        GD.SubmitCommands(cl);
        GD.WaitForIdle();

        MappedResourceView<byte> readView = GD.Map<byte>(dst, MapMode.Read);
        for (uint i = 0; i < 512; i++)
        {
            Assert.Equal((byte)i, readView[i]);
        }

        for (uint i = 512; i < 1024; i++)
        {
            Assert.Equal((byte)255, readView[i]);
        }
    }

    //[Fact]
    public void Dynamic_MapRead_Fails()
    {
        DeviceBuffer dynamic = RF.CreateBuffer(
            new(1024, BufferUsage.DynamicRead | BufferUsage.UniformBuffer)
        );
        Assert.Throws<VeldridException>(() => GD.Map(dynamic, MapMode.ReadWrite));
    }

    [Fact]
    public void CommandList_Update_Staging()
    {
        DeviceBuffer staging = RF.CreateBuffer(new(1024, BufferUsage.StagingRead));
        byte[] data = Enumerable.Range(0, 1024).Select(i => (byte)i).ToArray();

        CommandList cl = RF.CreateCommandList();
        cl.Begin();
        cl.UpdateBuffer(staging, 0, data);
        cl.End();
        GD.SubmitCommands(cl);
        GD.WaitForIdle();

        MappedResourceView<byte> readView = GD.Map<byte>(staging, MapMode.Read);
        for (uint i = 0; i < staging.SizeInBytes; i++)
        {
            Assert.Equal((byte)i, readView[i]);
        }
    }

    [Theory]
    [InlineData(60, BufferUsage.VertexBuffer, 1, 70, BufferUsage.VertexBuffer, 13, 11)]
    [InlineData(60, BufferUsage.StagingWrite, 1, 70, BufferUsage.VertexBuffer, 13, 11)]
    [InlineData(60, BufferUsage.VertexBuffer, 1, 70, BufferUsage.StagingWrite, 13, 11)]
    [InlineData(60, BufferUsage.StagingWrite, 1, 70, BufferUsage.StagingWrite, 13, 11)]
    [InlineData(5, BufferUsage.VertexBuffer, 3, 10, BufferUsage.VertexBuffer, 7, 2)]
    public void Copy_UnalignedRegion(
        uint srcBufferSize,
        BufferUsage srcUsage,
        uint srcCopyOffset,
        uint dstBufferSize,
        BufferUsage dstUsage,
        uint dstCopyOffset,
        uint copySize
    )
    {
        DeviceBuffer src = CreateBuffer(srcBufferSize, srcUsage);
        DeviceBuffer dst = CreateBuffer(dstBufferSize, dstUsage);

        byte[] data = Enumerable.Range(0, (int)srcBufferSize).Select(i => (byte)i).ToArray();
        GD.UpdateBuffer(src, 0, data);

        CommandList cl = RF.CreateCommandList();
        cl.Begin();
        cl.CopyBuffer(src, srcCopyOffset, dst, dstCopyOffset, copySize);
        cl.End();

        GD.SubmitCommands(cl);
        GD.WaitForIdle();

        DeviceBuffer readback = GetReadback(dst);

        MappedResourceView<byte> readView = GD.Map<byte>(readback, MapMode.Read);
        for (uint i = 0; i < copySize; i++)
        {
            byte expected = data[i + srcCopyOffset];
            byte actual = readView[i + dstCopyOffset];
            Assert.Equal(expected, actual);
        }
        GD.Unmap(readback);
    }

    [Theory]
    [InlineData(BufferUsage.VertexBuffer, 13, 5, 1)]
    [InlineData(BufferUsage.StagingWrite, 13, 5, 1)]
    public void CommandList_UpdateNonStaging_Unaligned(
        BufferUsage usage,
        uint bufferSize,
        uint dataSize,
        uint offset
    )
    {
        DeviceBuffer buffer = CreateBuffer(bufferSize, usage);
        byte[] data = Enumerable.Range(0, (int)dataSize).Select(i => (byte)i).ToArray();
        CommandList cl = RF.CreateCommandList();
        cl.Begin();
        cl.UpdateBuffer(buffer, offset, data);
        cl.End();
        GD.SubmitCommands(cl);
        GD.WaitForIdle();

        DeviceBuffer readback = GetReadback(buffer);
        MappedResourceView<byte> readView = GD.Map<byte>(readback, MapMode.Read);
        for (uint i = 0; i < dataSize; i++)
        {
            byte expected = data[i];
            byte actual = readView[i + offset];
            Assert.Equal(expected, actual);
        }
        GD.Unmap(readback);
    }

    [Theory]
    [InlineData(BufferUsage.UniformBuffer | BufferUsage.DynamicWrite)]
    [InlineData(BufferUsage.UniformBuffer)]
    [InlineData(BufferUsage.StagingWrite)]
    public void UpdateUniform_Offset_GraphicsDevice(BufferUsage usage)
    {
        DeviceBuffer buffer = CreateBuffer(128, usage);
        Matrix4x4 mat1 = new(1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1);
        GD.UpdateBuffer(buffer, 0, ref mat1);
        Matrix4x4 mat2 = new(2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2);
        GD.UpdateBuffer(buffer, 64, ref mat2);

        DeviceBuffer readback = GetReadback(buffer);
        MappedResourceView<Matrix4x4> readView = GD.Map<Matrix4x4>(readback, MapMode.Read);
        Assert.Equal(mat1, readView[0]);
        Assert.Equal(mat2, readView[1]);
        GD.Unmap(readback);
    }

    [Theory]
    [InlineData(BufferUsage.UniformBuffer | BufferUsage.DynamicWrite)]
    [InlineData(BufferUsage.UniformBuffer)]
    [InlineData(BufferUsage.StagingWrite)]
    public void UpdateUniform_Offset_CommandList(BufferUsage usage)
    {
        DeviceBuffer buffer = CreateBuffer(128, usage);
        CommandList cl = RF.CreateCommandList();
        cl.Begin();
        Matrix4x4 mat1 = new(1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1);
        cl.UpdateBuffer(buffer, 0, ref mat1);
        Matrix4x4 mat2 = new(2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2);
        cl.UpdateBuffer(buffer, 64, ref mat2);
        cl.End();
        GD.SubmitCommands(cl);
        GD.WaitForIdle();

        DeviceBuffer readback = GetReadback(buffer);
        MappedResourceView<Matrix4x4> readView = GD.Map<Matrix4x4>(readback, MapMode.Read);
        Assert.Equal(mat1, readView[0]);
        Assert.Equal(mat2, readView[1]);
        GD.Unmap(readback);
    }

    [SkippableTheory]
    [InlineData(BufferUsage.UniformBuffer)]
    [InlineData(BufferUsage.UniformBuffer | BufferUsage.DynamicWrite)]
    [InlineData(BufferUsage.VertexBuffer)]
    [InlineData(BufferUsage.VertexBuffer | BufferUsage.DynamicWrite)]
    [InlineData(BufferUsage.IndexBuffer)]
    [InlineData(BufferUsage.IndexBuffer | BufferUsage.DynamicWrite)]
    [InlineData(BufferUsage.IndirectBuffer)]
    [InlineData(BufferUsage.StructuredBufferReadOnly)]
    [InlineData(BufferUsage.StructuredBufferReadOnly | BufferUsage.DynamicWrite)]
    [InlineData(BufferUsage.StructuredBufferReadWrite)]
    [InlineData(BufferUsage.VertexBuffer | BufferUsage.IndexBuffer)]
    [InlineData(BufferUsage.VertexBuffer | BufferUsage.IndexBuffer | BufferUsage.DynamicWrite)]
    [InlineData(BufferUsage.VertexBuffer | BufferUsage.IndexBuffer | BufferUsage.IndirectBuffer)]
    [InlineData(BufferUsage.IndexBuffer | BufferUsage.IndirectBuffer)]
    [InlineData(BufferUsage.StagingWrite)]
    public void CreateBuffer_UsageFlagsCoverage(BufferUsage usage)
    {
        Skip.If((usage & BufferUsage.StructuredBufferReadOnly) != 0);
        Skip.If((usage & BufferUsage.StructuredBufferReadWrite) != 0);

        BufferDescription description = new(64, usage);
        if (
            (usage & BufferUsage.StructuredBufferReadOnly) != 0
            || (usage & BufferUsage.StructuredBufferReadWrite) != 0
        )
        {
            description.StructureByteStride = 16;
        }
        DeviceBuffer buffer = RF.CreateBuffer(description);
        GD.UpdateBuffer(buffer, 0, new Vector4[4]);
        GD.WaitForIdle();
    }

    [Theory]
    [InlineData(BufferUsage.UniformBuffer)]
    [InlineData(BufferUsage.UniformBuffer | BufferUsage.DynamicWrite)]
    [InlineData(BufferUsage.VertexBuffer)]
    [InlineData(BufferUsage.VertexBuffer | BufferUsage.DynamicWrite)]
    [InlineData(BufferUsage.IndexBuffer)]
    [InlineData(BufferUsage.IndexBuffer | BufferUsage.DynamicWrite)]
    [InlineData(BufferUsage.IndirectBuffer)]
    [InlineData(BufferUsage.VertexBuffer | BufferUsage.IndexBuffer)]
    [InlineData(BufferUsage.VertexBuffer | BufferUsage.IndexBuffer | BufferUsage.DynamicWrite)]
    [InlineData(BufferUsage.VertexBuffer | BufferUsage.IndexBuffer | BufferUsage.IndirectBuffer)]
    [InlineData(BufferUsage.IndexBuffer | BufferUsage.IndirectBuffer)]
    [InlineData(BufferUsage.StagingWrite)]
    public void CopyBuffer_ZeroSize(BufferUsage usage)
    {
        DeviceBuffer src = CreateBuffer(1024, usage);
        DeviceBuffer dst = CreateBuffer(1024, usage);

        byte[] initialDataSrc = Enumerable.Range(0, 1024).Select(i => (byte)i).ToArray();
        byte[] initialDataDst = Enumerable.Range(0, 1024).Select(i => (byte)(i * 2)).ToArray();
        GD.UpdateBuffer(src, 0, initialDataSrc);
        GD.UpdateBuffer(dst, 0, initialDataDst);

        CommandList cl = RF.CreateCommandList();
        cl.Begin();
        cl.CopyBuffer(src, 0, dst, 0, 0);
        cl.End();
        GD.SubmitCommands(cl);
        GD.WaitForIdle();

        DeviceBuffer readback = GetReadback(dst);

        MappedResourceView<byte> readMap = GD.Map<byte>(readback, MapMode.Read);
        for (int i = 0; i < 1024; i++)
        {
            Assert.Equal((byte)(i * 2), readMap[i]);
        }
        GD.Unmap(readback);
    }

    [Theory]
    [InlineData(BufferUsage.UniformBuffer, false)]
    [InlineData(BufferUsage.UniformBuffer, true)]
    [InlineData(BufferUsage.UniformBuffer | BufferUsage.DynamicWrite, false)]
    [InlineData(BufferUsage.UniformBuffer | BufferUsage.DynamicWrite, true)]
    [InlineData(BufferUsage.VertexBuffer, false)]
    [InlineData(BufferUsage.VertexBuffer, true)]
    [InlineData(BufferUsage.VertexBuffer | BufferUsage.DynamicWrite, false)]
    [InlineData(BufferUsage.VertexBuffer | BufferUsage.DynamicWrite, true)]
    [InlineData(BufferUsage.IndexBuffer, false)]
    [InlineData(BufferUsage.IndexBuffer, true)]
    [InlineData(BufferUsage.IndirectBuffer, false)]
    [InlineData(BufferUsage.IndirectBuffer, true)]
    [InlineData(BufferUsage.StagingWrite, false)]
    [InlineData(BufferUsage.StagingWrite, true)]
    public unsafe void UpdateBuffer_ZeroSize(BufferUsage usage, bool useCommandListUpdate)
    {
        DeviceBuffer buffer = CreateBuffer(1024, usage);

        byte[] initialData = Enumerable.Range(0, 1024).Select(i => (byte)i).ToArray();
        byte[] otherData = Enumerable.Range(0, 1024).Select(i => (byte)(i + 10)).ToArray();
        GD.UpdateBuffer(buffer, 0, initialData);

        if (useCommandListUpdate)
        {
            CommandList cl = RF.CreateCommandList();
            cl.Begin();
            fixed (byte* dataPtr = otherData)
            {
                cl.UpdateBuffer(buffer, 0, (IntPtr)dataPtr, 0);
            }
            cl.End();
            GD.SubmitCommands(cl);
            GD.WaitForIdle();
        }
        else
        {
            fixed (byte* dataPtr = otherData)
            {
                GD.UpdateBuffer(buffer, 0, (IntPtr)dataPtr, 0);
            }
        }

        DeviceBuffer readback = GetReadback(buffer);

        MappedResourceView<byte> readMap = GD.Map<byte>(readback, MapMode.Read);
        for (int i = 0; i < 1024; i++)
        {
            Assert.Equal((byte)i, readMap[i]);
        }
        GD.Unmap(readback);
    }

    DeviceBuffer CreateBuffer(uint size, BufferUsage usage)
    {
        return RF.CreateBuffer(new(size, usage));
    }
}

#if TEST_OPENGL
[Trait("Backend", "OpenGL")]
public class OpenGLBufferTests : BufferTestBase<OpenGLDeviceCreator> { }
#endif
#if TEST_OPENGLES
[Trait("Backend", "OpenGLES")]
public class OpenGLESBufferTests : BufferTestBase<OpenGLESDeviceCreator> { }
#endif
#if TEST_VULKAN
[Trait("Backend", "Vulkan")]
public class VulkanBufferTests : BufferTestBase<VulkanDeviceCreator> { }
#endif
#if TEST_D3D11
[Trait("Backend", "D3D11")]
public class D3D11BufferTests : BufferTestBase<D3D11DeviceCreator> { }
#endif
#if TEST_METAL
[Trait("Backend", "Metal")]
public class MetalBufferTests : BufferTestBase<MetalDeviceCreator> { }
#endif
