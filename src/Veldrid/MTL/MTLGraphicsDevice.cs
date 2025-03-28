using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Veldrid.MetalBindings;
using Veldrid.SPIRV;

namespace Veldrid.MTL;

internal sealed unsafe class MTLGraphicsDevice : GraphicsDevice
{
    static readonly Lazy<bool> s_isSupported = new(GetIsSupported);
    static readonly Dictionary<IntPtr, MTLGraphicsDevice> s_aotRegisteredBlocks = new();

    readonly MTLDevice _device;
    readonly MTLCommandQueue _commandQueue;
    readonly bool[] _supportedSampleCounts;
    readonly BackendInfoMetal _metalInfo;

    readonly object _submittedCommandsLock = new();
    readonly Dictionary<MTLCommandBuffer, MTLFence> _submittedCBs = new();
    MTLCommandBuffer _latestSubmittedCB;

    readonly object _resetEventsLock = new();
    readonly List<WaitHandle[]> _resetEvents = [];

    const string UnalignedBufferCopyPipelineMacOSName = "MTL_UnalignedBufferCopy_macOS";
    const string UnalignedBufferCopyPipelineiOSName = "MTL_UnalignedBufferCopy_iOS";
    readonly object _unalignedBufferCopyPipelineLock = new();
    readonly IntPtr _libSystem;
    MTLShader? _unalignedBufferCopyShader;
    MTLComputePipelineState _unalignedBufferCopyPipeline;
    readonly IntPtr _completionBlockDescriptor;
    readonly IntPtr _completionBlockLiteral;

    public MTLDevice Device => _device;
    public MTLCommandQueue CommandQueue => _commandQueue;
    public MTLFeatureSupport MetalFeatures { get; }
    public ResourceBindingModel ResourceBindingModel { get; }

    public MTLGraphicsDevice(GraphicsDeviceOptions options, SwapchainDescription? swapchainDesc)
    {
        VendorName = "Apple";
        BackendType = GraphicsBackend.Metal;
        IsUvOriginTopLeft = true;
        IsDepthRangeZeroToOne = true;
        IsClipSpaceYInverted = false;
        IsDriverDebug = true;

        IsDebug = options.Debug;
        _device = MTLDevice.MTLCreateSystemDefaultDevice();
        DeviceName = _device.name;
        MetalFeatures = new(_device);

        UniformBufferMinOffsetAlignment = MetalFeatures.IsMacOS ? 16u : 256u;
        StructuredBufferMinOffsetAlignment = 16u;

        int major = (int)MetalFeatures.MaxFeatureSet / 10000;
        int minor = (int)MetalFeatures.MaxFeatureSet % 10000;
        ApiVersion = new(major, minor, 0, 0);

        Features = new(
            computeShader: true,
            geometryShader: false,
            tessellationShaders: false,
            multipleViewports: MetalFeatures.IsSupported(MTLFeatureSet.macOS_GPUFamily1_v3),
            samplerLodBias: false,
            drawBaseVertex: MetalFeatures.IsDrawBaseVertexInstanceSupported(),
            drawBaseInstance: MetalFeatures.IsDrawBaseVertexInstanceSupported(),
            drawIndirect: true,
            drawIndirectBaseInstance: true,
            fillModeWireframe: true,
            samplerAnisotropy: true,
            depthClipDisable: true,
            texture1D: true, // TODO: Should be macOS 10.11+ and iOS 11.0+.
            independentBlend: true,
            structuredBuffer: true,
            subsetTextureView: true,
            commandListDebugMarkers: true,
            bufferRangeBinding: true,
            shaderFloat64: false
        );
        ResourceBindingModel = options.ResourceBindingModel;

        _libSystem = NativeLibrary.Load("libSystem.dylib");
        IntPtr concreteGlobalBlock = NativeLibrary.GetExport(_libSystem, "_NSConcreteGlobalBlock");
        delegate* unmanaged[Cdecl]<IntPtr, MTLCommandBuffer, void> completionHandler =
            &OnCommandBufferCompleted_Static;
        _completionBlockDescriptor = Marshal.AllocHGlobal(Unsafe.SizeOf<BlockDescriptor>());
        BlockDescriptor* descriptorPtr = (BlockDescriptor*)_completionBlockDescriptor;
        descriptorPtr->reserved = 0;
        descriptorPtr->Block_size = (ulong)Unsafe.SizeOf<BlockDescriptor>();

        _completionBlockLiteral = Marshal.AllocHGlobal(Unsafe.SizeOf<BlockLiteral>());
        BlockLiteral* blockPtr = (BlockLiteral*)_completionBlockLiteral;
        blockPtr->isa = concreteGlobalBlock;
        blockPtr->flags = 1 << 28 | 1 << 29;
        blockPtr->invoke = (nint)completionHandler;
        blockPtr->descriptor = descriptorPtr;

        lock (s_aotRegisteredBlocks)
        {
            s_aotRegisteredBlocks.Add(_completionBlockLiteral, this);
        }

        ResourceFactory = new MTLResourceFactory(this);
        _commandQueue = _device.newCommandQueue();

        TextureSampleCount[] allSampleCounts = Enum.GetValues<TextureSampleCount>();
        _supportedSampleCounts = new bool[allSampleCounts.Length];
        for (int i = 0; i < allSampleCounts.Length; i++)
        {
            TextureSampleCount count = allSampleCounts[i];
            uint uintValue = FormatHelpers.GetSampleCountUInt32(count);
            if (_device.supportsTextureSampleCount(uintValue))
            {
                _supportedSampleCounts[i] = true;
            }
        }

        if (swapchainDesc != null)
        {
            SwapchainDescription desc = swapchainDesc.Value;
            MainSwapchain = new MTLSwapchain(this, desc);
        }

        _metalInfo = new(this);

        PostDeviceCreated();
    }

    void OnCommandBufferCompleted(MTLCommandBuffer cb)
    {
        lock (_submittedCommandsLock)
        {
            if (_submittedCBs.Remove(cb, out MTLFence? fence))
            {
                fence.Set();
            }

            if (_latestSubmittedCB.NativePtr == cb.NativePtr)
            {
                _latestSubmittedCB = default;
            }
        }

        ObjectiveCRuntime.release(cb.NativePtr);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    static void OnCommandBufferCompleted_Static(IntPtr block, MTLCommandBuffer cb)
    {
        lock (s_aotRegisteredBlocks)
        {
            if (s_aotRegisteredBlocks.TryGetValue(block, out MTLGraphicsDevice? gd))
            {
                gd.OnCommandBufferCompleted(cb);
            }
        }
    }

    private protected override void SubmitCommandsCore(CommandList commandList, Fence? fence)
    {
        MTLCommandList mtlCL = Util.AssertSubtype<CommandList, MTLCommandList>(commandList);

        mtlCL.CommandBuffer.addCompletedHandler(_completionBlockLiteral);
        lock (_submittedCommandsLock)
        {
            if (fence != null)
            {
                MTLFence mtlFence = Util.AssertSubtype<Fence, MTLFence>(fence);
                _submittedCBs.Add(mtlCL.CommandBuffer, mtlFence);
            }

            _latestSubmittedCB = mtlCL.Commit();
        }
    }

    public override TextureSampleCount GetSampleCountLimit(PixelFormat format, bool depthFormat)
    {
        for (int i = _supportedSampleCounts.Length - 1; i >= 0; i--)
        {
            if (_supportedSampleCounts[i])
            {
                return (TextureSampleCount)i;
            }
        }

        return TextureSampleCount.Count1;
    }

    private protected override bool GetPixelFormatSupportCore(
        PixelFormat format,
        TextureType type,
        TextureUsage usage,
        out PixelFormatProperties properties
    )
    {
        if (!MTLFormats.IsFormatSupported(format, usage, MetalFeatures))
        {
            properties = default;
            return false;
        }

        uint sampleCounts = 0;

        for (int i = 0; i < _supportedSampleCounts.Length; i++)
        {
            if (_supportedSampleCounts[i])
            {
                sampleCounts |= (uint)(1 << i);
            }
        }

        MTLFeatureSet maxFeatureSet = MetalFeatures.MaxFeatureSet;
        uint maxArrayLayer = MTLFormats.GetMaxTextureVolume(maxFeatureSet);
        uint maxWidth;
        uint maxHeight;
        uint maxDepth;
        if (type == TextureType.Texture1D)
        {
            maxWidth = MTLFormats.GetMaxTexture1DWidth(maxFeatureSet);
            maxHeight = 1;
            maxDepth = 1;
        }
        else if (type == TextureType.Texture2D)
        {
            uint maxDimensions;
            if ((usage & TextureUsage.Cubemap) != 0)
            {
                maxDimensions = MTLFormats.GetMaxTextureCubeDimensions(maxFeatureSet);
            }
            else
            {
                maxDimensions = MTLFormats.GetMaxTexture2DDimensions(maxFeatureSet);
            }

            maxWidth = maxDimensions;
            maxHeight = maxDimensions;
            maxDepth = 1;
        }
        else if (type == TextureType.Texture3D)
        {
            maxWidth = maxArrayLayer;
            maxHeight = maxArrayLayer;
            maxDepth = maxArrayLayer;
            maxArrayLayer = 1;
        }
        else
        {
            Unsafe.SkipInit(out properties);
            return Illegal.Value<TextureType, bool>();
        }

        properties = new(maxWidth, maxHeight, maxDepth, uint.MaxValue, maxArrayLayer, sampleCounts);
        return true;
    }

    private protected override void SwapBuffersCore(Swapchain swapchain)
    {
        MTLSwapchain mtlSC = Util.AssertSubtype<Swapchain, MTLSwapchain>(swapchain);
        IntPtr currentDrawablePtr = mtlSC.CurrentDrawable.NativePtr;
        if (currentDrawablePtr != IntPtr.Zero)
        {
            using (NSAutoreleasePool.Begin())
            {
                MTLCommandBuffer submitCB = _commandQueue.commandBuffer();
                submitCB.presentDrawable(currentDrawablePtr);
                submitCB.commit();
            }
        }

        mtlSC.GetNextDrawable();
    }

    private protected override void UpdateBufferCore(
        DeviceBuffer buffer,
        uint bufferOffsetInBytes,
        IntPtr source,
        uint sizeInBytes
    )
    {
        MTLBuffer mtlBuffer = Util.AssertSubtype<DeviceBuffer, MTLBuffer>(buffer);
        void* destPtr = mtlBuffer.DeviceBuffer.contents();
        byte* destOffsetPtr = (byte*)destPtr + bufferOffsetInBytes;
        Unsafe.CopyBlock(destOffsetPtr, source.ToPointer(), sizeInBytes);
    }

    private protected override void UpdateTextureCore(
        Texture texture,
        IntPtr source,
        uint sizeInBytes,
        uint x,
        uint y,
        uint z,
        uint width,
        uint height,
        uint depth,
        uint mipLevel,
        uint arrayLayer
    )
    {
        MTLTexture mtlTex = Util.AssertSubtype<Texture, MTLTexture>(texture);
        if (mtlTex.StagingBuffer.IsNull)
        {
            Texture stagingTex = ResourceFactory.CreateTexture(
                new(width, height, depth, 1, 1, texture.Format, TextureUsage.Staging, texture.Type)
            );
            UpdateTexture(stagingTex, source, sizeInBytes, 0, 0, 0, width, height, depth, 0, 0);
            CommandList cl = ResourceFactory.CreateCommandList();
            cl.Begin();
            cl.CopyTexture(
                stagingTex,
                0,
                0,
                0,
                0,
                0,
                texture,
                x,
                y,
                z,
                mipLevel,
                arrayLayer,
                width,
                height,
                depth,
                1
            );
            cl.End();
            SubmitCommands(cl);

            cl.Dispose();
            stagingTex.Dispose();
        }
        else
        {
            mtlTex.GetSubresourceLayout(
                mipLevel,
                arrayLayer,
                out uint dstRowPitch,
                out uint dstDepthPitch
            );
            ulong dstOffset = Util.ComputeSubresourceOffset(mtlTex, mipLevel, arrayLayer);
            uint srcRowPitch = FormatHelpers.GetRowPitch(width, texture.Format);
            uint srcDepthPitch = FormatHelpers.GetDepthPitch(srcRowPitch, height, texture.Format);
            Util.CopyTextureRegion(
                source.ToPointer(),
                0,
                0,
                0,
                srcRowPitch,
                srcDepthPitch,
                (byte*)mtlTex.StagingBuffer.contents() + dstOffset,
                x,
                y,
                z,
                dstRowPitch,
                dstDepthPitch,
                width,
                height,
                depth,
                texture.Format
            );
        }
    }

    private protected override void WaitForIdleCore()
    {
        MTLCommandBuffer lastCB;
        lock (_submittedCommandsLock)
        {
            lastCB = _latestSubmittedCB;
            ObjectiveCRuntime.retain(lastCB.NativePtr);
        }

        if (lastCB.NativePtr != IntPtr.Zero && lastCB.status != MTLCommandBufferStatus.Completed)
        {
            lastCB.waitUntilCompleted();
        }

        ObjectiveCRuntime.release(lastCB.NativePtr);
    }

    private protected override MappedResource MapCore(
        MappableResource resource,
        uint offsetInBytes,
        uint sizeInBytes,
        MapMode mode,
        uint subresource
    )
    {
        if (resource is MTLBuffer buffer)
        {
            return MapBuffer(buffer, offsetInBytes, sizeInBytes, mode);
        }
        else
        {
            MTLTexture texture = Util.AssertSubtype<MappableResource, MTLTexture>(resource);
            return MapTexture(texture, offsetInBytes, sizeInBytes, mode, subresource);
        }
    }

    MappedResource MapBuffer(MTLBuffer buffer, uint offsetInBytes, uint sizeInBytes, MapMode mode)
    {
        byte* data = (byte*)buffer.DeviceBuffer.contents() + offsetInBytes;
        return new(buffer, mode, (IntPtr)data, offsetInBytes, sizeInBytes, 0, 0, 0);
    }

    MappedResource MapTexture(
        MTLTexture texture,
        uint offsetInBytes,
        uint sizeInBytes,
        MapMode mode,
        uint subresource
    )
    {
        Debug.Assert(!texture.StagingBuffer.IsNull);
        byte* data = (byte*)texture.StagingBuffer.contents() + offsetInBytes;
        Util.GetMipLevelAndArrayLayer(texture, subresource, out uint mipLevel, out uint arrayLayer);
        texture.GetSubresourceLayout(mipLevel, arrayLayer, out uint rowPitch, out uint depthPitch);
        ulong offset = Util.ComputeSubresourceOffset(texture, mipLevel, arrayLayer);
        byte* offsetPtr = data + offset;
        return new(
            texture,
            mode,
            (IntPtr)offsetPtr,
            offsetInBytes,
            sizeInBytes,
            subresource,
            rowPitch,
            depthPitch
        );
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!_unalignedBufferCopyPipeline.IsNull)
        {
            _unalignedBufferCopyShader?.Dispose();
            ObjectiveCRuntime.release(_unalignedBufferCopyPipeline.NativePtr);
        }
        MainSwapchain?.Dispose();
        ObjectiveCRuntime.release(_commandQueue.NativePtr);
        ObjectiveCRuntime.release(_device.NativePtr);

        lock (s_aotRegisteredBlocks)
        {
            s_aotRegisteredBlocks.Remove(_completionBlockLiteral);
        }

        NativeLibrary.Free(_libSystem);
        Marshal.FreeHGlobal(_completionBlockDescriptor);
        Marshal.FreeHGlobal(_completionBlockLiteral);
    }

    public override bool GetMetalInfo(out BackendInfoMetal info)
    {
        info = _metalInfo;
        return true;
    }

    private protected override void UnmapCore(MappableResource resource, uint subresource) { }

    public override bool WaitForFence(Fence fence, ulong nanosecondTimeout)
    {
        return Util.AssertSubtype<Fence, MTLFence>(fence).Wait(nanosecondTimeout);
    }

    public override bool WaitForFences(Fence[] fences, bool waitAll, ulong nanosecondTimeout)
    {
        int msTimeout;
        if (nanosecondTimeout == ulong.MaxValue)
        {
            msTimeout = -1;
        }
        else
        {
            msTimeout = (int)Math.Min(nanosecondTimeout / 1_000_000, int.MaxValue);
        }

        WaitHandle[] events = GetResetEventArray(fences.Length);
        for (int i = 0; i < fences.Length; i++)
        {
            events[i] = Util.AssertSubtype<Fence, MTLFence>(fences[i]).ResetEvent;
        }
        bool result;
        if (waitAll)
        {
            result = WaitHandle.WaitAll(events, msTimeout);
        }
        else
        {
            int index = WaitHandle.WaitAny(events, msTimeout);
            result = index != WaitHandle.WaitTimeout;
        }

        ReturnResetEventArray(events);

        return result;
    }

    WaitHandle[] GetResetEventArray(int length)
    {
        lock (_resetEventsLock)
        {
            for (int i = _resetEvents.Count - 1; i > 0; i--)
            {
                WaitHandle[] array = _resetEvents[i];
                if (array.Length == length)
                {
                    _resetEvents.RemoveAt(i);
                    return array;
                }
            }
        }

        return new WaitHandle[length];
    }

    void ReturnResetEventArray(WaitHandle[] array)
    {
        lock (_resetEventsLock)
        {
            _resetEvents.Add(array);
        }
    }

    public override void ResetFence(Fence fence)
    {
        Util.AssertSubtype<Fence, MTLFence>(fence).Reset();
    }

    internal static bool IsSupported() => s_isSupported.Value;

    static bool GetIsSupported()
    {
        bool result = false;
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                if (RuntimeInformation.OSDescription.Contains("Darwin"))
                {
                    NSArray allDevices = MTLDevice.MTLCopyAllDevices();
                    result |= (ulong)allDevices.count > 0;
                    ObjectiveCRuntime.release(allDevices.NativePtr);
                }
                else
                {
                    MTLDevice defaultDevice = MTLDevice.MTLCreateSystemDefaultDevice();
                    if (defaultDevice.NativePtr != IntPtr.Zero)
                    {
                        result = true;
                        ObjectiveCRuntime.release(defaultDevice.NativePtr);
                    }
                }
            }
        }
        catch
        {
            result = false;
        }

        return result;
    }

    internal MTLComputePipelineState GetUnalignedBufferCopyPipeline()
    {
        lock (_unalignedBufferCopyPipelineLock)
        {
            if (_unalignedBufferCopyPipeline.IsNull)
            {
                MTLComputePipelineDescriptor descriptor =
                    MTLUtil.AllocInit<MTLComputePipelineDescriptor>(
                        "MTLComputePipelineDescriptor"u8
                    );
                MTLPipelineBufferDescriptor buffer0 = descriptor.buffers[0];
                buffer0.mutability = MTLMutability.Mutable;
                MTLPipelineBufferDescriptor buffer1 = descriptor.buffers[1];
                buffer1.mutability = MTLMutability.Mutable;

                Debug.Assert(_unalignedBufferCopyShader == null);
                string name = MetalFeatures.IsMacOS
                    ? UnalignedBufferCopyPipelineMacOSName
                    : UnalignedBufferCopyPipelineiOSName;
                using (
                    Stream? resourceStream =
                        typeof(MTLGraphicsDevice).Assembly.GetManifestResourceStream(name)
                )
                {
                    if (resourceStream == null)
                    {
                        throw new($"Missing required shader manifest resource \"{name}\".");
                    }

                    byte[] data = new byte[resourceStream.Length];
                    using MemoryStream ms = new(data);
                    resourceStream.CopyTo(ms);
                    ShaderDescription shaderDesc = new(ShaderStages.Compute, data, "copy_bytes");
                    _unalignedBufferCopyShader = new(shaderDesc, this);
                }

                descriptor.computeFunction = _unalignedBufferCopyShader.Function;
                _unalignedBufferCopyPipeline = _device.newComputePipelineStateWithDescriptor(
                    descriptor
                );
                ObjectiveCRuntime.release(descriptor.NativePtr);
            }

            return _unalignedBufferCopyPipeline;
        }
    }
}
