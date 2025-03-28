﻿using System;
using Veldrid.MetalBindings;

namespace Veldrid.MTL;

internal sealed class MTLSwapchain : Swapchain
{
    readonly MTLSwapchainFramebuffer _framebuffer;
    readonly CAMetalLayer _metalLayer;
    readonly MTLGraphicsDevice _gd;
    readonly UIView _uiView; // Valid only when a UIViewSwapchainSource is used.
    bool _syncToVerticalBlank;
    bool _disposed;

    CAMetalDrawable _drawable;

    public override Framebuffer Framebuffer => _framebuffer;
    public override bool SyncToVerticalBlank
    {
        get => _syncToVerticalBlank;
        set
        {
            if (_syncToVerticalBlank != value)
            {
                SetSyncToVerticalBlank(value);
            }
        }
    }

    public override string? Name { get; set; }

    public override bool IsDisposed => _disposed;

    public CAMetalDrawable CurrentDrawable => _drawable;

    public MTLSwapchain(MTLGraphicsDevice gd, in SwapchainDescription description)
    {
        _gd = gd;
        _syncToVerticalBlank = description.SyncToVerticalBlank;

        uint width;
        uint height;

        SwapchainSource source = description.Source;
        if (source is NSWindowSwapchainSource nsWindowSource)
        {
            NSWindow nswindow = new(nsWindowSource.NSWindow);
            NSView contentView = nswindow.contentView;
            CGSize windowContentSize = contentView.frame.size;
            width = (uint)windowContentSize.width;
            height = (uint)windowContentSize.height;

            if (!CAMetalLayer.TryCast(contentView.layer, out _metalLayer))
            {
                _metalLayer = CAMetalLayer.New();
                contentView.wantsLayer = true;
                contentView.layer = _metalLayer.NativePtr;
            }
        }
        else if (source is NSViewSwapchainSource nsViewSource)
        {
            NSView contentView = new(nsViewSource.NSView);
            CGSize windowContentSize = contentView.frame.size;
            width = (uint)windowContentSize.width;
            height = (uint)windowContentSize.height;

            if (!CAMetalLayer.TryCast(contentView.layer, out _metalLayer))
            {
                _metalLayer = CAMetalLayer.New();
                contentView.wantsLayer = true;
                contentView.layer = _metalLayer.NativePtr;
            }
        }
        else if (source is UIViewSwapchainSource uiViewSource)
        {
            UIScreen mainScreen = UIScreen.mainScreen;
            CGFloat nativeScale = mainScreen.nativeScale;

            _uiView = new(uiViewSource.UIView);
            CGSize viewSize = _uiView.frame.size;
            width = (uint)(viewSize.width * nativeScale);
            height = (uint)(viewSize.height * nativeScale);

            if (!CAMetalLayer.TryCast(_uiView.layer, out _metalLayer))
            {
                _metalLayer = CAMetalLayer.New();
                _metalLayer.frame = _uiView.frame;
                _metalLayer.opaque = true;
                _uiView.layer.addSublayer(_metalLayer.NativePtr);
            }
        }
        else
        {
            throw new VeldridException(
                "A Metal Swapchain can only be created from an NSWindow, NSView, or UIView."
            );
        }

        PixelFormat format = description.ColorSrgb
            ? PixelFormat.B8_G8_R8_A8_UNorm_SRgb
            : PixelFormat.B8_G8_R8_A8_UNorm;

        _metalLayer.device = _gd.Device;
        _metalLayer.pixelFormat = MTLFormats.VdToMTLPixelFormat(format, default);
        _metalLayer.framebufferOnly = true;
        _metalLayer.drawableSize = new(width, height);

        SetSyncToVerticalBlank(_syncToVerticalBlank);

        GetNextDrawable();

        _framebuffer = new(gd, this, width, height, description.DepthFormat, format);
    }

    public void GetNextDrawable()
    {
        if (!_drawable.IsNull)
        {
            ObjectiveCRuntime.release(_drawable.NativePtr);
        }

        using (NSAutoreleasePool.Begin())
        {
            _drawable = _metalLayer.nextDrawable();
            ObjectiveCRuntime.retain(_drawable.NativePtr);
        }
    }

    public override void Resize(uint width, uint height)
    {
        if (_uiView.NativePtr != IntPtr.Zero)
        {
            UIScreen mainScreen = UIScreen.mainScreen;
            CGFloat nativeScale = mainScreen.nativeScale;
            width = (uint)(width * nativeScale);
            height = (uint)(height * nativeScale);

            _metalLayer.frame = _uiView.frame;
        }

        _framebuffer.Resize(width, height);
        _metalLayer.drawableSize = new(width, height);
        if (_uiView.NativePtr != IntPtr.Zero)
        {
            _metalLayer.frame = _uiView.frame;
        }
        GetNextDrawable();
    }

    void SetSyncToVerticalBlank(bool value)
    {
        _syncToVerticalBlank = value;

        if (
            _gd.MetalFeatures.MaxFeatureSet == MTLFeatureSet.macOS_GPUFamily1_v3
            || _gd.MetalFeatures.MaxFeatureSet == MTLFeatureSet.macOS_GPUFamily1_v4
            || _gd.MetalFeatures.MaxFeatureSet == MTLFeatureSet.macOS_GPUFamily2_v1
        )
        {
            _metalLayer.displaySyncEnabled = value;
        }
    }

    public override void Dispose()
    {
        if (_drawable.NativePtr != IntPtr.Zero)
        {
            ObjectiveCRuntime.objc_msgSend(_drawable.NativePtr, "release"u8);
        }
        _framebuffer.Dispose();
        ObjectiveCRuntime.release(_metalLayer.NativePtr);

        _disposed = true;
    }
}
