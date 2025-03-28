using System;
using System.Runtime.InteropServices;
using static Veldrid.MetalBindings.ObjectiveCRuntime;

namespace Veldrid.MetalBindings;

[StructLayout(LayoutKind.Sequential)]
internal readonly struct MTLRenderCommandEncoder(IntPtr ptr)
{
    public readonly IntPtr NativePtr = ptr;

    public bool IsNull => NativePtr == IntPtr.Zero;

    public void setRenderPipelineState(MTLRenderPipelineState pipelineState) =>
        objc_msgSend(NativePtr, sel_setRenderPipelineState, pipelineState.NativePtr);

    public void setVertexBuffer(MTLBuffer buffer, UIntPtr offset, UIntPtr index) =>
        objc_msgSend(NativePtr, sel_setVertexBuffer, buffer.NativePtr, offset, index);

    public void setFragmentBuffer(MTLBuffer buffer, UIntPtr offset, UIntPtr index) =>
        objc_msgSend(NativePtr, sel_setFragmentBuffer, buffer.NativePtr, offset, index);

    public void setVertexTexture(MTLTexture texture, UIntPtr index) =>
        objc_msgSend(NativePtr, sel_setVertexTexture, texture.NativePtr, index);

    public void setFragmentTexture(MTLTexture texture, UIntPtr index) =>
        objc_msgSend(NativePtr, sel_setFragmentTexture, texture.NativePtr, index);

    public void setVertexSamplerState(MTLSamplerState sampler, UIntPtr index) =>
        objc_msgSend(NativePtr, sel_setVertexSamplerState, sampler.NativePtr, index);

    public void setFragmentSamplerState(MTLSamplerState sampler, UIntPtr index) =>
        objc_msgSend(NativePtr, sel_setFragmentSamplerState, sampler.NativePtr, index);

    public void drawPrimitives(
        MTLPrimitiveType primitiveType,
        UIntPtr vertexStart,
        UIntPtr vertexCount,
        UIntPtr instanceCount,
        UIntPtr baseInstance
    ) =>
        objc_msgSend(
            NativePtr,
            sel_drawPrimitives0,
            primitiveType,
            vertexStart,
            vertexCount,
            instanceCount,
            baseInstance
        );

    public void drawPrimitives(
        MTLPrimitiveType primitiveType,
        UIntPtr vertexStart,
        UIntPtr vertexCount,
        UIntPtr instanceCount
    ) =>
        objc_msgSend(
            NativePtr,
            sel_drawPrimitives2,
            primitiveType,
            vertexStart,
            vertexCount,
            instanceCount
        );

    public void drawPrimitives(
        MTLPrimitiveType primitiveType,
        MTLBuffer indirectBuffer,
        UIntPtr indirectBufferOffset
    ) =>
        objc_msgSend(
            NativePtr,
            sel_drawPrimitives1,
            primitiveType,
            indirectBuffer,
            indirectBufferOffset
        );

    public void drawIndexedPrimitives(
        MTLPrimitiveType primitiveType,
        UIntPtr indexCount,
        MTLIndexType indexType,
        MTLBuffer indexBuffer,
        UIntPtr indexBufferOffset,
        UIntPtr instanceCount
    ) =>
        objc_msgSend(
            NativePtr,
            sel_drawIndexedPrimitives0,
            primitiveType,
            indexCount,
            indexType,
            indexBuffer.NativePtr,
            indexBufferOffset,
            instanceCount
        );

    public void drawIndexedPrimitives(
        MTLPrimitiveType primitiveType,
        UIntPtr indexCount,
        MTLIndexType indexType,
        MTLBuffer indexBuffer,
        UIntPtr indexBufferOffset,
        UIntPtr instanceCount,
        IntPtr baseVertex,
        UIntPtr baseInstance
    ) =>
        objc_msgSend(
            NativePtr,
            sel_drawIndexedPrimitives1,
            primitiveType,
            indexCount,
            indexType,
            indexBuffer.NativePtr,
            indexBufferOffset,
            instanceCount,
            baseVertex,
            baseInstance
        );

    public void drawIndexedPrimitives(
        MTLPrimitiveType primitiveType,
        MTLIndexType indexType,
        MTLBuffer indexBuffer,
        UIntPtr indexBufferOffset,
        MTLBuffer indirectBuffer,
        UIntPtr indirectBufferOffset
    ) =>
        objc_msgSend(
            NativePtr,
            sel_drawIndexedPrimitives2,
            primitiveType,
            indexType,
            indexBuffer,
            indexBufferOffset,
            indirectBuffer,
            indirectBufferOffset
        );

    public void setViewport(MTLViewport viewport) =>
        objc_msgSend(NativePtr, sel_setViewport, viewport);

    public unsafe void setViewports(MTLViewport* viewports, UIntPtr count) =>
        objc_msgSend(NativePtr, sel_setViewports, viewports, count);

    public void setScissorRect(MTLScissorRect scissorRect) =>
        objc_msgSend(NativePtr, sel_setScissorRect, scissorRect);

    public unsafe void setScissorRects(MTLScissorRect* scissorRects, UIntPtr count) =>
        objc_msgSend(NativePtr, sel_setScissorRects, scissorRects, count);

    public void setCullMode(MTLCullMode cullMode) =>
        objc_msgSend(NativePtr, sel_setCullMode, (uint)cullMode);

    public void setFrontFacing(MTLWinding frontFaceWinding) =>
        objc_msgSend(NativePtr, sel_setFrontFacingWinding, (uint)frontFaceWinding);

    public void setDepthStencilState(MTLDepthStencilState depthStencilState) =>
        objc_msgSend(NativePtr, sel_setDepthStencilState, depthStencilState.NativePtr);

    public void setDepthClipMode(MTLDepthClipMode depthClipMode) =>
        objc_msgSend(NativePtr, sel_setDepthClipMode, (uint)depthClipMode);

    public void endEncoding() => objc_msgSend(NativePtr, sel_endEncoding);

    public void setStencilReferenceValue(uint stencilReference) =>
        objc_msgSend(NativePtr, sel_setStencilReferenceValue, stencilReference);

    public void setBlendColor(float red, float green, float blue, float alpha) =>
        objc_msgSend(NativePtr, sel_setBlendColor, red, green, blue, alpha);

    public void setTriangleFillMode(MTLTriangleFillMode fillMode) =>
        objc_msgSend(NativePtr, sel_setTriangleFillMode, (uint)fillMode);

    public void pushDebugGroup(NSString @string) =>
        objc_msgSend(NativePtr, Selectors.pushDebugGroup, @string.NativePtr);

    public void popDebugGroup() => objc_msgSend(NativePtr, Selectors.popDebugGroup);

    public void insertDebugSignpost(NSString @string) =>
        objc_msgSend(NativePtr, Selectors.insertDebugSignpost, @string.NativePtr);

    static readonly Selector sel_setRenderPipelineState = "setRenderPipelineState:"u8;
    static readonly Selector sel_setVertexBuffer = "setVertexBuffer:offset:atIndex:"u8;
    static readonly Selector sel_setFragmentBuffer = "setFragmentBuffer:offset:atIndex:"u8;
    static readonly Selector sel_setVertexTexture = "setVertexTexture:atIndex:"u8;
    static readonly Selector sel_setFragmentTexture = "setFragmentTexture:atIndex:"u8;
    static readonly Selector sel_setVertexSamplerState = "setVertexSamplerState:atIndex:"u8;
    static readonly Selector sel_setFragmentSamplerState = "setFragmentSamplerState:atIndex:"u8;
    static readonly Selector sel_drawPrimitives0 =
        "drawPrimitives:vertexStart:vertexCount:instanceCount:baseInstance:"u8;
    static readonly Selector sel_drawPrimitives1 =
        "drawPrimitives:indirectBuffer:indirectBufferOffset:"u8;
    static readonly Selector sel_drawPrimitives2 =
        "drawPrimitives:vertexStart:vertexCount:instanceCount:"u8;
    static readonly Selector sel_drawIndexedPrimitives0 =
        "drawIndexedPrimitives:indexCount:indexType:indexBuffer:indexBufferOffset:instanceCount:"u8;
    static readonly Selector sel_drawIndexedPrimitives1 =
        "drawIndexedPrimitives:indexCount:indexType:indexBuffer:indexBufferOffset:instanceCount:baseVertex:baseInstance:"u8;
    static readonly Selector sel_drawIndexedPrimitives2 =
        "drawIndexedPrimitives:indexType:indexBuffer:indexBufferOffset:indirectBuffer:indirectBufferOffset:"u8;
    static readonly Selector sel_setViewport = "setViewport:"u8;
    static readonly Selector sel_setViewports = "setViewports:count:"u8;
    static readonly Selector sel_setScissorRect = "setScissorRect:"u8;
    static readonly Selector sel_setScissorRects = "setScissorRects:count:"u8;
    static readonly Selector sel_setCullMode = "setCullMode:"u8;
    static readonly Selector sel_setFrontFacingWinding = "setFrontFacingWinding:"u8;
    static readonly Selector sel_setDepthStencilState = "setDepthStencilState:"u8;
    static readonly Selector sel_setDepthClipMode = "setDepthClipMode:"u8;
    static readonly Selector sel_endEncoding = "endEncoding"u8;
    static readonly Selector sel_setStencilReferenceValue = "setStencilReferenceValue:"u8;
    static readonly Selector sel_setBlendColor = "setBlendColorRed:green:blue:alpha:"u8;
    static readonly Selector sel_setTriangleFillMode = "setTriangleFillMode:"u8;
}
