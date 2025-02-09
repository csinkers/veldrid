using System;
using System.Runtime.InteropServices;
using static Veldrid.MetalBindings.ObjectiveCRuntime;

namespace Veldrid.MetalBindings;

public readonly unsafe struct MTLDevice(IntPtr nativePtr)
{
    const string MetalFramework = "/System/Library/Frameworks/Metal.framework/Metal";

    public readonly IntPtr NativePtr = nativePtr;

    public static implicit operator IntPtr(MTLDevice device) => device.NativePtr;

    public string name => string_objc_msgSend(NativePtr, sel_name);

    public MTLSize maxThreadsPerThreadgroup
    {
        get
        {
            if (UseStret<MTLSize>())
            {
                return objc_msgSend_stret<MTLSize>(this, sel_maxThreadsPerThreadgroup);
            }
            else
            {
                return MTLSize_objc_msgSend(this, sel_maxThreadsPerThreadgroup);
            }
        }
    }

    public MTLLibrary newLibraryWithSource(string source, MTLCompileOptions options)
    {
        NSString sourceNSS = NSString.New(source);

        NSError error;
        IntPtr library = IntPtr_objc_msgSend(
            NativePtr,
            sel_newLibraryWithSource,
            sourceNSS,
            options,
            &error
        );

        release(sourceNSS.NativePtr);

        if (library == IntPtr.Zero)
        {
            throw new("Shader compilation failed: " + error.localizedDescription);
        }

        return new(library);
    }

    public MTLLibrary newLibraryWithData(DispatchData data)
    {
        NSError error;
        IntPtr library = IntPtr_objc_msgSend(
            NativePtr,
            sel_newLibraryWithData,
            data.NativePtr,
            &error
        );

        if (library == IntPtr.Zero)
        {
            throw new("Unable to load Metal library: " + error.localizedDescription);
        }

        return new(library);
    }

    public MTLRenderPipelineState newRenderPipelineStateWithDescriptor(
        MTLRenderPipelineDescriptor desc
    )
    {
        NSError error;
        IntPtr ret = IntPtr_objc_msgSend(
            NativePtr,
            sel_newRenderPipelineStateWithDescriptor,
            desc.NativePtr,
            &error
        );

        if (error.NativePtr != IntPtr.Zero)
        {
            throw new("Failed to create new MTLRenderPipelineState: " + error.localizedDescription);
        }

        return new(ret);
    }

    public MTLComputePipelineState newComputePipelineStateWithDescriptor(
        MTLComputePipelineDescriptor descriptor
    )
    {
        NSError error;
        IntPtr ret = IntPtr_objc_msgSend(
            NativePtr,
            sel_newComputePipelineStateWithDescriptor,
            descriptor,
            0,
            IntPtr.Zero,
            &error
        );

        if (error.NativePtr != IntPtr.Zero)
        {
            throw new("Failed to create new MTLRenderPipelineState: " + error.localizedDescription);
        }

        return new(ret);
    }

    public MTLCommandQueue newCommandQueue() =>
        new(IntPtr_objc_msgSend(NativePtr, sel_newCommandQueue));

    public MTLBuffer newBuffer(void* pointer, nuint length, MTLResourceOptions options)
    {
        IntPtr buffer = IntPtr_objc_msgSend(
            NativePtr,
            sel_newBufferWithBytes,
            pointer,
            length,
            options
        );
        return new(buffer);
    }

    public MTLBuffer newBufferWithLengthOptions(nuint length, MTLResourceOptions options)
    {
        IntPtr buffer = IntPtr_objc_msgSend(NativePtr, sel_newBufferWithLength, length, options);
        return new(buffer);
    }

    public MTLTexture newTextureWithDescriptor(MTLTextureDescriptor descriptor) =>
        objc_msgSend<MTLTexture>(NativePtr, sel_newTextureWithDescriptor, descriptor.NativePtr);

    public MTLSamplerState newSamplerStateWithDescriptor(MTLSamplerDescriptor descriptor) =>
        objc_msgSend<MTLSamplerState>(
            NativePtr,
            sel_newSamplerStateWithDescriptor,
            descriptor.NativePtr
        );

    public MTLDepthStencilState newDepthStencilStateWithDescriptor(
        MTLDepthStencilDescriptor descriptor
    ) =>
        objc_msgSend<MTLDepthStencilState>(
            NativePtr,
            sel_newDepthStencilStateWithDescriptor,
            descriptor.NativePtr
        );

    public Bool8 supportsTextureSampleCount(UIntPtr sampleCount) =>
        bool8_objc_msgSend(NativePtr, sel_supportsTextureSampleCount, sampleCount);

    public Bool8 supportsFeatureSet(MTLFeatureSet featureSet) =>
        bool8_objc_msgSend(NativePtr, sel_supportsFeatureSet, (uint)featureSet);

    public Bool8 isDepth24Stencil8PixelFormatSupported =>
        bool8_objc_msgSend(NativePtr, sel_isDepth24Stencil8PixelFormatSupported);

    [DllImport(MetalFramework)]
    public static extern MTLDevice MTLCreateSystemDefaultDevice();

    [DllImport(MetalFramework)]
    public static extern NSArray MTLCopyAllDevices();

    static readonly Selector sel_name = "name"u8;
    static readonly Selector sel_maxThreadsPerThreadgroup = "maxThreadsPerThreadgroup"u8;
    static readonly Selector sel_newLibraryWithSource = "newLibraryWithSource:options:error:"u8;
    static readonly Selector sel_newLibraryWithData = "newLibraryWithData:error:"u8;
    static readonly Selector sel_newRenderPipelineStateWithDescriptor =
        "newRenderPipelineStateWithDescriptor:error:"u8;
    static readonly Selector sel_newComputePipelineStateWithDescriptor =
        "newComputePipelineStateWithDescriptor:options:reflection:error:"u8;
    static readonly Selector sel_newCommandQueue = "newCommandQueue"u8;
    static readonly Selector sel_newBufferWithBytes = "newBufferWithBytes:length:options:"u8;
    static readonly Selector sel_newBufferWithLength = "newBufferWithLength:options:"u8;
    static readonly Selector sel_newTextureWithDescriptor = "newTextureWithDescriptor:"u8;
    static readonly Selector sel_newSamplerStateWithDescriptor = "newSamplerStateWithDescriptor:"u8;
    static readonly Selector sel_newDepthStencilStateWithDescriptor =
        "newDepthStencilStateWithDescriptor:"u8;
    static readonly Selector sel_supportsTextureSampleCount = "supportsTextureSampleCount:"u8;
    static readonly Selector sel_supportsFeatureSet = "supportsFeatureSet:"u8;
    static readonly Selector sel_isDepth24Stencil8PixelFormatSupported =
        "isDepth24Stencil8PixelFormatSupported"u8;
}
