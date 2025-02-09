using System;
using TerraFX.Interop.Vulkan;

namespace Veldrid.Vk;

/// <summary>
/// An object which can be used to create a <see cref="VkSurfaceKHR"/>.
/// </summary>
public abstract class VkSurfaceSource
{
    internal VkSurfaceSource() { }

    /// <summary>
    /// Creates a new <see cref="VkSurfaceKHR"/> attached to this source.
    /// </summary>
    /// <param name="instance">The <see cref="VkInstance"/> to use.</param>
    /// <returns>A new <see cref="VkSurfaceKHR"/>.</returns>
    public abstract VkSurfaceKHR CreateSurface(VkInstance instance);

    /// <summary>
    /// Creates a new <see cref="VkSurfaceSource"/> from the given Win32 instance and window handle.
    /// </summary>
    /// <param name="hinstance">The Win32 instance handle.</param>
    /// <param name="hwnd">The Win32 window handle.</param>
    /// <returns>A new <see cref="VkSurfaceSource"/>.</returns>
    public static VkSurfaceSource CreateWin32(IntPtr hinstance, IntPtr hwnd) =>
        new Win32VkSurfaceInfo(hinstance, hwnd);

    /// <summary>
    /// Creates a new <see cref="VkSurfaceSource"/> from the given Xlib information.
    /// </summary>
    /// <param name="display">A pointer to the Xlib display.</param>
    /// <param name="window">An Xlib window.</param>
    /// <returns>A new <see cref="VkSurfaceSource"/>.</returns>
    public static VkSurfaceSource CreateXlib(IntPtr display, IntPtr window) =>
        new XlibVkSurfaceInfo(display, window);

    /// <summary>
    /// Creates a new <see cref="VkSurfaceSource"/> from the given Wayland information.
    /// </summary>
    /// <param name="display">A pointer to the Wayland display.</param>
    /// <param name="surface">A Wayland surface.</param>
    /// <returns>A new <see cref="VkSurfaceSource"/>.</returns>
    public static VkSurfaceSource CreateWayland(IntPtr display, IntPtr surface) =>
        new WaylandVkSurfaceInfo(display, surface);

    internal abstract SwapchainSource GetSurfaceSource();
}

internal sealed class Win32VkSurfaceInfo(IntPtr hinstance, IntPtr hwnd) : VkSurfaceSource
{
    public override VkSurfaceKHR CreateSurface(VkInstance instance) =>
        VkSurfaceUtil.CreateSurface(instance, GetSurfaceSource());

    internal override SwapchainSource GetSurfaceSource() =>
        new Win32SwapchainSource(hwnd, hinstance);
}

internal sealed class XlibVkSurfaceInfo(IntPtr display, IntPtr window) : VkSurfaceSource
{
    public override VkSurfaceKHR CreateSurface(VkInstance instance) =>
        VkSurfaceUtil.CreateSurface(instance, GetSurfaceSource());

    internal override SwapchainSource GetSurfaceSource() =>
        new XlibSwapchainSource(display, window);
}

internal sealed class WaylandVkSurfaceInfo(IntPtr display, IntPtr surface) : VkSurfaceSource
{
    public override VkSurfaceKHR CreateSurface(VkInstance instance) =>
        VkSurfaceUtil.CreateSurface(instance, GetSurfaceSource());

    internal override SwapchainSource GetSurfaceSource() =>
        new WaylandSwapchainSource(display, surface);
}
