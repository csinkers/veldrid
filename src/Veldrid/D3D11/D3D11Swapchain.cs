﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SharpGen.Runtime;
using Vortice;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace Veldrid.D3D11;

internal sealed class D3D11Swapchain : Swapchain
{
    readonly D3D11GraphicsDevice _gd;
    readonly PixelFormat? _depthFormat;
    readonly IDXGISwapChain _dxgiSwapChain;
    bool _vsync;
    int _syncInterval;
    D3D11Framebuffer? _framebuffer;
    D3D11Texture? _depthTexture;
    readonly float _pixelScale = 1f;
    bool _disposed;

    readonly object _referencedCLsLock = new();
    readonly HashSet<D3D11CommandList> _referencedCLs = [];

    public override Framebuffer Framebuffer => _framebuffer!;

    public override string? Name
    {
        get
        {
            unsafe
            {
                byte* pname = stackalloc byte[1024];
                int size = 1024 - 1;
                _dxgiSwapChain.GetPrivateData(CommonGuid.DebugObjectName, ref size, new(pname));
                pname[size] = 0;
                return Marshal.PtrToStringAnsi(new(pname));
            }
        }
        set
        {
            if (string.IsNullOrEmpty(value))
            {
                _dxgiSwapChain.SetPrivateData(CommonGuid.DebugObjectName, 0, IntPtr.Zero);
            }
            else
            {
                IntPtr namePtr = Marshal.StringToHGlobalAnsi(value);
                _dxgiSwapChain.SetPrivateData(CommonGuid.DebugObjectName, value.Length, namePtr);
                Marshal.FreeHGlobal(namePtr);
            }
        }
    }

    public override bool SyncToVerticalBlank
    {
        get => _vsync;
        set
        {
            _vsync = value;
            _syncInterval = D3D11Util.GetSyncInterval(value);
        }
    }

    readonly Format _colorFormat;

    public IDXGISwapChain DxgiSwapChain => _dxgiSwapChain;

    public int SyncInterval => _syncInterval;

    public D3D11Swapchain(D3D11GraphicsDevice gd, in SwapchainDescription description)
    {
        _gd = gd;
        _depthFormat = description.DepthFormat;
        SyncToVerticalBlank = description.SyncToVerticalBlank;

        _colorFormat = description.ColorSrgb ? Format.B8G8R8A8_UNorm_SRgb : Format.B8G8R8A8_UNorm;

        if (description.Source is Win32SwapchainSource win32Source)
        {
            SwapChainDescription dxgiSCDesc = new()
            {
                BufferCount = 2,
                Windowed = true,
                BufferDescription = new(
                    (int)description.Width,
                    (int)description.Height,
                    _colorFormat
                ),
                OutputWindow = win32Source.Hwnd,
                SampleDescription = new(1, 0),
                SwapEffect = SwapEffect.Discard,
                BufferUsage = Usage.RenderTargetOutput,
            };

            using IDXGIFactory dxgiFactory = _gd.Adapter.GetParent<IDXGIFactory>();
            _dxgiSwapChain = dxgiFactory.CreateSwapChain(_gd.Device, dxgiSCDesc);
            dxgiFactory.MakeWindowAssociation(
                win32Source.Hwnd,
                WindowAssociationFlags.IgnoreAltEnter
            );
        }
        else if (description.Source is UwpSwapchainSource uwpSource)
        {
            _pixelScale = uwpSource.LogicalDpi / 96.0f;

            // Properties of the swap chain
            SwapChainDescription1 swapChainDescription = new()
            {
                AlphaMode = AlphaMode.Ignore,
                BufferCount = 2,
                Format = _colorFormat,
                Height = (int)(description.Height * _pixelScale),
                Width = (int)(description.Width * _pixelScale),
                SampleDescription = new(1, 0),
                SwapEffect = SwapEffect.FlipSequential,
                BufferUsage = Usage.RenderTargetOutput,
            };

            // Get the Vortice.DXGI factory automatically created when initializing the Direct3D device.
            using (IDXGIFactory2 dxgiFactory = _gd.Adapter.GetParent<IDXGIFactory2>())
            {
                // Create the swap chain and get the highest version available.
                using IDXGISwapChain1 swapChain1 = dxgiFactory.CreateSwapChainForComposition(
                    _gd.Device,
                    swapChainDescription
                )!;
                _dxgiSwapChain = swapChain1.QueryInterface<IDXGISwapChain2>();
            }

            ComObject co = new(uwpSource.SwapChainPanelNative);

            ISwapChainPanelNative? swapchainPanelNative =
                co.QueryInterfaceOrNull<ISwapChainPanelNative>();
            if (swapchainPanelNative != null)
            {
                swapchainPanelNative.SetSwapChain(_dxgiSwapChain);
            }
            else
            {
                ISwapChainBackgroundPanelNative? bgPanelNative =
                    co.QueryInterfaceOrNull<ISwapChainBackgroundPanelNative>();
                if (bgPanelNative != null)
                {
                    bgPanelNative.SetSwapChain(_dxgiSwapChain);
                }
            }
        }

        if (_dxgiSwapChain == null)
        {
            throw new VeldridException("Failed to create DXGI swapchain.");
        }

        Resize(description.Width, description.Height);
    }

    public override void Resize(uint width, uint height)
    {
        lock (_referencedCLsLock)
        {
            foreach (D3D11CommandList cl in _referencedCLs)
            {
                cl.Reset();
            }

            _referencedCLs.Clear();
        }

        bool resizeBuffers = false;

        if (_framebuffer != null)
        {
            resizeBuffers = true;
            _depthTexture?.Dispose();
            _framebuffer.Dispose();
        }

        uint actualWidth = (uint)(width * _pixelScale);
        uint actualHeight = (uint)(height * _pixelScale);
        if (resizeBuffers)
        {
            _dxgiSwapChain
                .ResizeBuffers(
                    2,
                    (int)actualWidth,
                    (int)actualHeight,
                    _colorFormat,
                    SwapChainFlags.None
                )
                .CheckError();
        }

        // Get the backbuffer from the swapchain
        using ID3D11Texture2D backBufferTexture = _dxgiSwapChain.GetBuffer<ID3D11Texture2D>(0);
        if (_depthFormat != null)
        {
            TextureDescription depthDesc = new(
                actualWidth,
                actualHeight,
                1,
                1,
                1,
                _depthFormat.Value,
                TextureUsage.DepthStencil,
                TextureType.Texture2D
            );
            _depthTexture = new(_gd.Device, depthDesc);
        }

        D3D11Texture backBufferVdTexture = new(
            backBufferTexture,
            TextureType.Texture2D,
            D3D11Formats.ToVdFormat(_colorFormat)
        );

        FramebufferDescription desc = new(_depthTexture, backBufferVdTexture);
        _framebuffer = new(_gd.Device, desc) { Swapchain = this };
    }

    public void AddCommandListReference(D3D11CommandList cl)
    {
        lock (_referencedCLsLock)
        {
            _referencedCLs.Add(cl);
        }
    }

    public void RemoveCommandListReference(D3D11CommandList cl)
    {
        lock (_referencedCLsLock)
        {
            _referencedCLs.Remove(cl);
        }
    }

    public override bool IsDisposed => _disposed;

    public override void Dispose()
    {
        if (_disposed)
            return;

        _depthTexture?.Dispose();
        _framebuffer?.Dispose();
        _dxgiSwapChain.Dispose();

        _disposed = true;
    }
}
