﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Veldrid.Tests.Utilities;
using Xunit;

namespace Veldrid.Tests;

[StructLayout(LayoutKind.Sequential)]
internal struct FillValueStruct
{
    /// <summary>
    /// The value we fill the 3d texture with.
    /// </summary>
    public float FillValue;
    public float pad1,
        pad2,
        pad3;

    public FillValueStruct(float fillValue)
    {
        FillValue = fillValue;
        pad1 = pad2 = pad3 = 0;
    }
}

public abstract class ComputeTests<T> : GraphicsDeviceTestBase<T>
    where T : IGraphicsDeviceCreator
{
    [Fact]
    public void ComputeShader3dTexture()
    {
        // Just a dumb compute shader that fills a 3D texture with the same value from a uniform multiplied by the depth.
        string shaderText =
            @"
#version 450
layout(set = 0, binding = 0, rgba32f) uniform image3D TextureToFill;
layout(set = 0, binding = 1) uniform FillValueBuffer
{
    float FillValue;
    float Padding1;
    float Padding2;
    float Padding3;
};
layout(local_size_x = 16, local_size_y = 16, local_size_z = 1) in;
void main()
{
    ivec3 textureCoordinate = ivec3(gl_GlobalInvocationID.xyz);
    float dataToStore = FillValue * (textureCoordinate.z + 1);

    imageStore(TextureToFill, textureCoordinate, vec4(dataToStore));
}
";

        const float FillValue = 42.42f;
        const uint OutputTextureSize = 32;

        using Shader computeShader = RF.CreateFromSpirv(
            new(ShaderStages.Compute, Encoding.ASCII.GetBytes(shaderText), "main")
        );

        using ResourceLayout computeLayout = RF.CreateResourceLayout(
            new(
                new ResourceLayoutElementDescription(
                    "TextureToFill",
                    ResourceKind.TextureReadWrite,
                    ShaderStages.Compute
                ),
                new ResourceLayoutElementDescription(
                    "FillValueBuffer",
                    ResourceKind.UniformBuffer,
                    ShaderStages.Compute
                )
            )
        );

        using Pipeline computePipeline = RF.CreateComputePipeline(
            new(computeShader, computeLayout, 16, 16, 1)
        );

        using DeviceBuffer fillValueBuffer = RF.CreateBuffer(
            new((uint)Marshal.SizeOf<FillValueStruct>(), BufferUsage.UniformBuffer)
        );

        // Create our output texture.
        using Texture computeTargetTexture = RF.CreateTexture(
            TextureDescription.Texture3D(
                OutputTextureSize,
                OutputTextureSize,
                OutputTextureSize,
                1,
                PixelFormat.R32_G32_B32_A32_Float,
                TextureUsage.Sampled | TextureUsage.Storage
            )
        );

        using TextureView computeTargetTextureView = RF.CreateTextureView(computeTargetTexture);

        using ResourceSet computeResourceSet = RF.CreateResourceSet(
            new(computeLayout, computeTargetTextureView, fillValueBuffer)
        );

        using CommandList cl = RF.CreateCommandList();
        cl.Begin();

        cl.UpdateBuffer(fillValueBuffer, 0, new FillValueStruct(FillValue));

        // Use the compute shader to fill the texture.
        cl.SetPipeline(computePipeline);
        cl.SetComputeResourceSet(0, computeResourceSet);
        const uint GroupDivisorXY = 16;
        cl.Dispatch(
            OutputTextureSize / GroupDivisorXY,
            OutputTextureSize / GroupDivisorXY,
            OutputTextureSize
        );

        cl.End();
        GD.SubmitCommands(cl);
        GD.WaitForIdle();

        // Read back from our texture and make sure it has been properly filled.
        for (uint depth = 0; depth < computeTargetTexture.Depth; depth++)
        {
            RgbaFloat expectedFillValue = new(new(FillValue * (depth + 1)));
            int notFilledCount = CountTexelsNotFilledAtDepth(
                GD,
                computeTargetTexture,
                expectedFillValue,
                depth
            );

            Assert.Equal(0, notFilledCount);
        }
    }

    /// <summary>
    /// Returns the number of texels in the texture that DO NOT match the fill value.
    /// </summary>
    static int CountTexelsNotFilledAtDepth<TexelType>(
        GraphicsDevice device,
        Texture texture,
        TexelType fillValue,
        uint depth
    )
        where TexelType : unmanaged
    {
        ResourceFactory factory = device.ResourceFactory;

        // We need to create a staging texture and copy into it.
        TextureDescription description = new(
            texture.Width,
            texture.Height,
            depth: 1,
            texture.MipLevels,
            texture.ArrayLayers,
            texture.Format,
            TextureUsage.Staging,
            texture.Type,
            texture.SampleCount
        );

        using Texture staging = factory.CreateTexture(description);

        using CommandList cl = factory.CreateCommandList();
        cl.Begin();

        cl.CopyTexture(
            texture,
            srcX: 0,
            srcY: 0,
            srcZ: depth,
            srcMipLevel: 0,
            srcBaseArrayLayer: 0,
            staging,
            dstX: 0,
            dstY: 0,
            dstZ: 0,
            dstMipLevel: 0,
            dstBaseArrayLayer: 0,
            staging.Width,
            staging.Height,
            depth: 1,
            layerCount: 1
        );

        cl.End();
        device.SubmitCommands(cl);
        device.WaitForIdle();

        try
        {
            MappedResourceView<TexelType> mapped = device.Map<TexelType>(staging, MapMode.Read);

            int notFilledCount = 0;
            for (int y = 0; y < staging.Height; y++)
            {
                for (int x = 0; x < staging.Width; x++)
                {
                    TexelType actual = mapped[x, y];
                    if (!fillValue.Equals(actual))
                    {
                        notFilledCount++;
                    }
                }
            }

            return notFilledCount;
        }
        finally
        {
            device.Unmap(staging);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BasicComputeTestParams
    {
        public uint Width;
        public uint Height;
        uint _padding1;
        uint _padding2;
    }

    [SkippableFact]
    public void BasicCompute()
    {
        SkipIfNotComputeShader();

        ResourceLayout layout = RF.CreateResourceLayout(
            new(
                new ResourceLayoutElementDescription(
                    "Params",
                    ResourceKind.UniformBuffer,
                    ShaderStages.Compute
                ),
                new ResourceLayoutElementDescription(
                    "Source",
                    ResourceKind.StructuredBufferReadWrite,
                    ShaderStages.Compute
                ),
                new ResourceLayoutElementDescription(
                    "Destination",
                    ResourceKind.StructuredBufferReadWrite,
                    ShaderStages.Compute
                )
            )
        );

        uint width = 1024;
        uint height = 1024;
        DeviceBuffer paramsBuffer = RF.CreateBuffer(
            new((uint)Unsafe.SizeOf<BasicComputeTestParams>(), BufferUsage.UniformBuffer)
        );
        DeviceBuffer sourceBuffer = RF.CreateBuffer(
            new(width * height * sizeof(float), BufferUsage.StructuredBufferReadWrite)
        );
        DeviceBuffer destinationBuffer = RF.CreateBuffer(
            new(width * height * sizeof(float), BufferUsage.StructuredBufferReadWrite)
        );

        GD.UpdateBuffer(
            paramsBuffer,
            0,
            new BasicComputeTestParams { Width = width, Height = height }
        );

        float[] sourceData = new float[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int index = y * (int)width + x;
            sourceData[index] = index;
        }
        GD.UpdateBuffer(sourceBuffer, 0, sourceData);

        ResourceSet rs = RF.CreateResourceSet(
            new(layout, paramsBuffer, sourceBuffer, destinationBuffer)
        );

        Pipeline pipeline = RF.CreateComputePipeline(
            new(TestShaders.LoadCompute(RF, "BasicComputeTest"), layout, 16, 16, 1)
        );

        CommandList cl = RF.CreateCommandList();
        cl.Begin();
        cl.SetPipeline(pipeline);
        cl.SetComputeResourceSet(0, rs);
        cl.Dispatch(width / 16, width / 16, 1);
        cl.End();
        GD.SubmitCommands(cl);
        GD.WaitForIdle();

        DeviceBuffer sourceReadback = GetReadback(sourceBuffer);
        DeviceBuffer destinationReadback = GetReadback(destinationBuffer);

        MappedResourceView<float> sourceReadView = GD.Map<float>(sourceReadback, MapMode.Read);
        MappedResourceView<float> destinationReadView = GD.Map<float>(
            destinationReadback,
            MapMode.Read
        );

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * (int)width + x;
                Assert.Equal(2 * sourceData[index], sourceReadView[index]);
            }
        }

        Assert.Equal(sourceData, destinationReadView.AsSpan());

        GD.Unmap(sourceReadback);
        GD.Unmap(destinationReadback);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ComputeTextureCopyParams
    {
        public uint Width;
        public uint Height;
        uint _padding1;
        uint _padding2;
    }

    [SkippableFact]
    public void ComputeTextureCopy()
    {
        SkipIfNotComputeShader();

        ResourceLayout layout = RF.CreateResourceLayout(
            new(
                new ResourceLayoutElementDescription(
                    "Params",
                    ResourceKind.UniformBuffer,
                    ShaderStages.Compute
                ),
                new ResourceLayoutElementDescription(
                    "Src",
                    ResourceKind.TextureReadWrite,
                    ShaderStages.Compute
                ),
                new ResourceLayoutElementDescription(
                    "Dst",
                    ResourceKind.TextureReadWrite,
                    ShaderStages.Compute
                )
            )
        );

        uint width = 1024;
        uint height = 1024;
        DeviceBuffer paramsBuffer = RF.CreateBuffer(
            new((uint)Unsafe.SizeOf<ComputeTextureCopyParams>(), BufferUsage.UniformBuffer)
        );
        Texture srcTexture = RF.CreateTexture(
            TextureDescription.Texture2D(
                width,
                height,
                1,
                1,
                PixelFormat.R32_Float,
                TextureUsage.Storage
            )
        );
        Texture dstTexture = RF.CreateTexture(
            TextureDescription.Texture2D(
                width,
                height,
                1,
                1,
                PixelFormat.R32_Float,
                TextureUsage.Storage
            )
        );

        GD.UpdateBuffer(
            paramsBuffer,
            0,
            new BasicComputeTestParams { Width = width, Height = height }
        );

        float[] srcData = new float[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * (int)width + x;
                srcData[index] = index;
            }
        }
        GD.UpdateTexture(srcTexture, srcData, 0, 0, 0, width, height, 1, 0, 0);

        ResourceSet rs = RF.CreateResourceSet(new(layout, paramsBuffer, srcTexture, dstTexture));

        Pipeline pipeline = RF.CreateComputePipeline(
            new(TestShaders.LoadCompute(RF, "ComputeTextureCopy"), layout, 16, 16, 1)
        );

        CommandList cl = RF.CreateCommandList();
        cl.Begin();
        cl.SetPipeline(pipeline);
        cl.SetComputeResourceSet(0, rs);
        cl.Dispatch(width / 16, width / 16, 1);
        cl.End();
        GD.SubmitCommands(cl);
        GD.WaitForIdle();

        Texture srcReadback = GetReadback(srcTexture);
        Texture dstReadback = GetReadback(dstTexture);

        MappedResourceView<float> srcReadView = GD.Map<float>(srcReadback, MapMode.Read);
        MappedResourceView<float> dstReadView = GD.Map<float>(dstReadback, MapMode.Read);

        Assert.Equal(srcData, srcReadView.AsSpan());
        Assert.Equal(srcData, dstReadView.AsSpan());

        GD.Unmap(srcReadback);
        GD.Unmap(dstReadback);
    }

    [SkippableFact]
    public void ComputeCubemapGeneration()
    {
        SkipIfNotComputeShader();
        Skip.If(GD.GetD3D11Info(out _), "D3D11 doesn't support Storage Cubemaps");

        const int TexSize = 32;
        const uint MipLevels = 1;

        TextureDescription texDesc = TextureDescription.Texture2D(
            TexSize,
            TexSize,
            MipLevels,
            1,
            PixelFormat.R8_G8_B8_A8_UNorm,
            TextureUsage.Sampled | TextureUsage.Storage | TextureUsage.Cubemap
        );
        Texture computeOutput = RF.CreateTexture(texDesc);

        Vector4[] faceColors =
        [
            new(0 * 42),
            new(1 * 42),
            new(2 * 42),
            new(3 * 42),
            new(4 * 42),
            new(5 * 42),
        ];

        ResourceLayout computeLayout = RF.CreateResourceLayout(
            new(
                new ResourceLayoutElementDescription(
                    "ComputeOutput",
                    ResourceKind.TextureReadWrite,
                    ShaderStages.Compute
                )
            )
        );
        ResourceSet computeSet = RF.CreateResourceSet(new(computeLayout, computeOutput));

        Pipeline computePipeline = RF.CreateComputePipeline(
            new(TestShaders.LoadCompute(RF, "ComputeCubemapGenerator"), computeLayout, 32, 32, 1)
        );

        CommandList cl = RF.CreateCommandList();
        cl.Begin();
        cl.SetPipeline(computePipeline);
        cl.SetComputeResourceSet(0, computeSet);
        cl.Dispatch(TexSize / 32, TexSize / 32, 6);
        cl.End();
        GD.SubmitCommands(cl);
        GD.WaitForIdle();

        using (Texture readback = GetReadback(computeOutput))
        {
            for (uint mip = 0; mip < MipLevels; mip++)
            {
                for (uint face = 0; face < 6; face++)
                {
                    uint subresource = readback.CalculateSubresource(mip, face);
                    int mipSize = (TexSize >> (int)mip);
                    RgbaByte expectedColor = new(
                        (byte)faceColors[face].X,
                        (byte)faceColors[face].Y,
                        (byte)faceColors[face].Z,
                        (byte)faceColors[face].Z
                    );
                    MappedResourceView<RgbaByte> readView = GD.Map<RgbaByte>(
                        readback,
                        MapMode.Read,
                        subresource
                    );
                    for (int y = 0; y < mipSize; y++)
                    for (int x = 0; x < mipSize; x++)
                    {
                        Assert.Equal(expectedColor, readView[x, y]);
                    }
                    GD.Unmap(readback, subresource);
                }
            }
        }
    }

    [SkippableFact]
    public void ComputeCubemapBindSingleTextureMipLevelOutput()
    {
        SkipIfNotComputeShader();
        Skip.If(GD.GetD3D11Info(out _), "D3D11 doesn't support Storage Cubemaps");

        const int TexSize = 128;
        const uint MipLevels = 7;

        const uint BoundMipLevel = 2;

        TextureDescription texDesc = TextureDescription.Texture2D(
            TexSize,
            TexSize,
            MipLevels,
            1,
            PixelFormat.R8_G8_B8_A8_UNorm,
            TextureUsage.Sampled | TextureUsage.Storage | TextureUsage.Cubemap
        );
        Texture computeOutput = RF.CreateTexture(texDesc);

        TextureView computeOutputMipLevel = RF.CreateTextureView(
            new TextureViewDescription(computeOutput, BoundMipLevel, 1, 0, 1)
        );

        Vector4[] faceColors =
        [
            new(0 * 42),
            new(1 * 42),
            new(2 * 42),
            new(3 * 42),
            new(4 * 42),
            new(5 * 42),
        ];

        ResourceLayout computeLayout = RF.CreateResourceLayout(
            new(
                new ResourceLayoutElementDescription(
                    "ComputeOutput",
                    ResourceKind.TextureReadWrite,
                    ShaderStages.Compute
                )
            )
        );
        ResourceSet computeSet = RF.CreateResourceSet(new(computeLayout, computeOutputMipLevel));

        Pipeline computePipeline = RF.CreateComputePipeline(
            new(TestShaders.LoadCompute(RF, "ComputeCubemapGenerator"), computeLayout, 32, 32, 1)
        );

        using (Texture readback = GetReadback(computeOutput))
        {
            for (uint mip = 0; mip < MipLevels; mip++)
            {
                for (uint face = 0; face < 6; face++)
                {
                    uint subresource = readback.CalculateSubresource(mip, face);
                    uint mipSize = (uint)(TexSize / (1 << (int)mip));
                    RgbaByte expectedColor = RgbaByte.Clear;
                    MappedResourceView<RgbaByte> readView = GD.Map<RgbaByte>(
                        readback,
                        MapMode.Read,
                        subresource
                    );
                    for (int y = 0; y < mipSize; y++)
                    for (int x = 0; x < mipSize; x++)
                    {
                        Assert.Equal(expectedColor, readView[x, y]);
                    }
                    GD.Unmap(readback, subresource);
                }
            }
        }

        CommandList cl = RF.CreateCommandList();
        cl.Begin();
        cl.SetPipeline(computePipeline);
        cl.SetComputeResourceSet(0, computeSet);
        cl.Dispatch((TexSize >> (int)BoundMipLevel) / 32, (TexSize >> (int)BoundMipLevel) / 32, 6);
        cl.End();
        GD.SubmitCommands(cl);
        GD.WaitForIdle();

        using (Texture readback = GetReadback(computeOutput))
        {
            for (uint mip = 0; mip < MipLevels; mip++)
            {
                for (uint face = 0; face < 6; face++)
                {
                    uint subresource = readback.CalculateSubresource(mip, face);
                    uint mipSize = (uint)(TexSize / (1 << (int)mip));
                    RgbaByte expectedColor =
                        mip == BoundMipLevel
                            ? new(
                                (byte)faceColors[face].X,
                                (byte)faceColors[face].Y,
                                (byte)faceColors[face].Z,
                                (byte)faceColors[face].Z
                            )
                            : RgbaByte.Clear;
                    MappedResourceView<RgbaByte> readView = GD.Map<RgbaByte>(
                        readback,
                        MapMode.Read,
                        subresource
                    );
                    for (int y = 0; y < mipSize; y++)
                    for (int x = 0; x < mipSize; x++)
                    {
                        Assert.Equal(expectedColor, readView[x, y]);
                    }
                    GD.Unmap(readback, subresource);
                }
            }
        }
    }

    [SkippableTheory]
    [MemberData(nameof(FillBuffer_WithOffsetsData))]
    public void FillBuffer_WithOffsets(
        uint srcSetMultiple,
        uint srcBindingMultiple,
        uint dstSetMultiple,
        uint dstBindingMultiple,
        bool combinedLayout
    )
    {
        SkipIfNotComputeShader();
        Skip.If(
            !GD.Features.BufferRangeBinding
                && (
                    srcSetMultiple != 0
                    || srcBindingMultiple != 0
                    || dstSetMultiple != 0
                    || dstBindingMultiple != 0
                )
        );

        Debug.Assert((GD.StructuredBufferMinOffsetAlignment % sizeof(uint)) == 0);

        uint valueCount = 512;
        uint dataSize = valueCount * sizeof(uint);
        uint totalSrcAlignment =
            GD.StructuredBufferMinOffsetAlignment * (srcSetMultiple + srcBindingMultiple);
        uint totalDstAlignment =
            GD.StructuredBufferMinOffsetAlignment * (dstSetMultiple + dstBindingMultiple);

        DeviceBuffer copySrc = RF.CreateBuffer(
            new(totalSrcAlignment + dataSize, BufferUsage.StructuredBufferReadOnly)
        );
        DeviceBuffer copyDst = RF.CreateBuffer(
            new(totalDstAlignment + dataSize, BufferUsage.StructuredBufferReadWrite)
        );

        ResourceLayout[] layouts;
        ResourceSet[] sets;

        DeviceBufferRange srcRange = new(
            copySrc,
            srcSetMultiple * GD.StructuredBufferMinOffsetAlignment,
            dataSize
        );
        DeviceBufferRange dstRange = new(
            copyDst,
            dstSetMultiple * GD.StructuredBufferMinOffsetAlignment,
            dataSize
        );

        if (combinedLayout)
        {
            layouts =
            [
                RF.CreateResourceLayout(
                    new(
                        new ResourceLayoutElementDescription(
                            "CopySrc",
                            ResourceKind.StructuredBufferReadOnly,
                            ShaderStages.Compute,
                            ResourceLayoutElementOptions.DynamicBinding
                        ),
                        new ResourceLayoutElementDescription(
                            "CopyDst",
                            ResourceKind.StructuredBufferReadWrite,
                            ShaderStages.Compute,
                            ResourceLayoutElementOptions.DynamicBinding
                        )
                    )
                ),
            ];
            sets = [RF.CreateResourceSet(new(layouts[0], srcRange, dstRange))];
        }
        else
        {
            layouts =
            [
                RF.CreateResourceLayout(
                    new(
                        new ResourceLayoutElementDescription(
                            "CopySrc",
                            ResourceKind.StructuredBufferReadOnly,
                            ShaderStages.Compute,
                            ResourceLayoutElementOptions.DynamicBinding
                        )
                    )
                ),
                RF.CreateResourceLayout(
                    new(
                        new ResourceLayoutElementDescription(
                            "CopyDst",
                            ResourceKind.StructuredBufferReadWrite,
                            ShaderStages.Compute,
                            ResourceLayoutElementOptions.DynamicBinding
                        )
                    )
                ),
            ];
            sets =
            [
                RF.CreateResourceSet(new(layouts[0], srcRange)),
                RF.CreateResourceSet(new(layouts[1], dstRange)),
            ];
        }

        Pipeline pipeline = RF.CreateComputePipeline(
            new(
                TestShaders.LoadCompute(
                    RF,
                    combinedLayout ? "FillBuffer" : "FillBuffer_SeparateLayout"
                ),
                layouts,
                1,
                1,
                1
            )
        );

        uint[] srcData = Enumerable
            .Range(0, (int)copySrc.SizeInBytes / sizeof(uint))
            .Select(i => (uint)i)
            .ToArray();
        GD.UpdateBuffer(copySrc, 0, srcData);

        CommandList cl = RF.CreateCommandList();
        cl.Begin();
        cl.SetPipeline(pipeline);
        if (combinedLayout)
        {
            uint[] offsets =
            [
                srcBindingMultiple * GD.StructuredBufferMinOffsetAlignment,
                dstBindingMultiple * GD.StructuredBufferMinOffsetAlignment,
            ];
            cl.SetComputeResourceSet(0, sets[0], offsets);
        }
        else
        {
            uint offset = srcBindingMultiple * GD.StructuredBufferMinOffsetAlignment;
            ReadOnlySpan<uint> offsets = MemoryMarshal.CreateReadOnlySpan(ref offset, 1);
            cl.SetComputeResourceSet(0, sets[0], offsets);
            offset = dstBindingMultiple * GD.StructuredBufferMinOffsetAlignment;
            cl.SetComputeResourceSet(1, sets[1], offsets);
        }
        cl.Dispatch(512, 1, 1);
        cl.End();
        GD.SubmitCommands(cl);
        GD.WaitForIdle();

        DeviceBuffer readback = GetReadback(copyDst);

        MappedResourceView<uint> readView = GD.Map<uint>(readback, MapMode.Read);
        for (uint i = 0; i < valueCount; i++)
        {
            uint srcIndex = totalSrcAlignment / sizeof(uint) + i;
            uint expected = srcData[(int)srcIndex];

            uint dstIndex = totalDstAlignment / sizeof(uint) + i;
            uint actual = readView[dstIndex];

            Assert.Equal(expected, actual);
        }
        GD.Unmap(readback);
    }

    public static IEnumerable<object[]> FillBuffer_WithOffsetsData()
    {
        foreach (uint srcSetMultiple in new uint[] { 0, 2, 10 })
        foreach (uint srcBindingMultiple in new uint[] { 0, 2, 10 })
        foreach (uint dstSetMultiple in new uint[] { 0, 2, 10 })
        foreach (uint dstBindingMultiple in new uint[] { 0, 2, 10 })
        foreach (bool combinedLayout in new[] { false, true })
        {
            yield return
            [
                srcSetMultiple,
                srcBindingMultiple,
                dstSetMultiple,
                dstBindingMultiple,
                combinedLayout,
            ];
        }
    }

    [SkippableTheory]
    [InlineData(BufferUsage.IndirectBuffer)]
    [InlineData(BufferUsage.IndexBuffer)]
    [InlineData(BufferUsage.VertexBuffer)]
    public unsafe void FillIndirectBuffer(BufferUsage dstUsage)
    {
        SkipIfNotComputeShader();

        ResourceLayout layout = RF.CreateResourceLayout(
            new(
                new ResourceLayoutElementDescription(
                    "Params",
                    ResourceKind.StructuredBufferReadOnly,
                    ShaderStages.Compute
                ),
                new ResourceLayoutElementDescription(
                    "Destination",
                    ResourceKind.StructuredBufferReadWrite,
                    ShaderStages.Compute
                )
            )
        );

        DeviceBuffer paramsBuffer = RF.CreateBuffer(
            new((uint)sizeof(IndirectDrawIndexedArguments), BufferUsage.StructuredBufferReadOnly)
        );

        DeviceBuffer dstBuffer = RF.CreateBuffer(
            new(
                (uint)sizeof(IndirectDrawIndexedArguments),
                dstUsage | BufferUsage.StructuredBufferReadWrite
            )
        );

        IndirectDrawIndexedArguments paramsValue = new()
        {
            FirstIndex = 1,
            FirstInstance = 2,
            IndexCount = 3,
            InstanceCount = 4,
            VertexOffset = 5,
        };
        GD.UpdateBuffer(paramsBuffer, 0, paramsValue);

        ResourceSet rs = RF.CreateResourceSet(new(layout, paramsBuffer, dstBuffer));

        Pipeline pipeline = RF.CreateComputePipeline(
            new(TestShaders.LoadCompute(RF, "FillIndirectBufferComputeTest"), layout, 1, 1, 1)
        );

        CommandList cl = RF.CreateCommandList();
        cl.Begin();
        cl.SetPipeline(pipeline);
        cl.SetComputeResourceSet(0, rs);
        cl.Dispatch(1, 1, 1);
        cl.End();
        GD.SubmitCommands(cl);
        GD.WaitForIdle();

        DeviceBuffer paramsReadback = GetReadback(paramsBuffer);
        DeviceBuffer dstReadback = GetReadback(dstBuffer);

        var paramsView = GD.Map<IndirectDrawIndexedArguments>(paramsReadback, MapMode.Read);
        var dstView = GD.Map<IndirectDrawIndexedArguments>(dstReadback, MapMode.Read);

        var paramsSpan = paramsView.AsSpan();
        var dstSpan = dstView.AsSpan();
        Assert.True(paramsSpan.SequenceEqual(dstSpan));

        GD.Unmap(dstReadback);
    }
}

#if TEST_OPENGL
[Trait("Backend", "OpenGL")]
public class OpenGLComputeTests : ComputeTests<OpenGLDeviceCreator> { }
#endif
#if TEST_OPENGLES
[Trait("Backend", "OpenGLES")]
public class OpenGLESComputeTests : ComputeTests<OpenGLESDeviceCreator> { }
#endif
#if TEST_VULKAN
[Trait("Backend", "Vulkan")]
public class VulkanComputeTests : ComputeTests<VulkanDeviceCreator> { }
#endif
#if TEST_D3D11
[Trait("Backend", "D3D11")]
public class D3D11ComputeTests : ComputeTests<D3D11DeviceCreator> { }
#endif
#if TEST_METAL
[Trait("Backend", "Metal")]
public class MetalComputeTests : RenderTests<MetalDeviceCreator> { }
#endif
