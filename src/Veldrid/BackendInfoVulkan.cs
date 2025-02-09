#if !EXCLUDE_VULKAN_BACKEND
using System;
using System.Collections.ObjectModel;
using TerraFX.Interop.Vulkan;
using Veldrid.Vk;

namespace Veldrid;

/// <summary>
/// Exposes Vulkan-specific functionality,
/// useful for interoperating with native components which interface directly with Vulkan.
/// Can only be used on <see cref="GraphicsBackend.Vulkan"/>.
/// </summary>
public unsafe class BackendInfoVulkan
{
    readonly VkGraphicsDevice _gd;
    readonly Lazy<ReadOnlyCollection<ExtensionProperties>> _deviceExtensions;

    internal BackendInfoVulkan(VkGraphicsDevice gd)
    {
        _gd = gd;
        AvailableInstanceLayers = new(VulkanUtil.EnumerateInstanceLayers());
        AvailableInstanceExtensions = new(VulkanUtil.EnumerateInstanceExtensions());
        _deviceExtensions = new(EnumerateDeviceExtensions);
    }

    /// <summary>
    /// Gets the underlying VkInstance used by the GraphicsDevice.
    /// </summary>
    public IntPtr Instance => (IntPtr)_gd.Instance.Value;

    /// <summary>
    /// Gets the underlying VkDevice used by the GraphicsDevice.
    /// </summary>
    public IntPtr Device => (IntPtr)_gd.Device.Value;

    /// <summary>
    /// Gets the underlying VkPhysicalDevice used by the GraphicsDevice.
    /// </summary>
    public IntPtr PhysicalDevice => (IntPtr)_gd.PhysicalDevice.Value;

    /// <summary>
    /// Gets the VkQueue which is used by the GraphicsDevice to submit graphics work.
    /// </summary>
    public IntPtr GraphicsQueue => _gd.GraphicsQueue;

    /// <summary>
    /// Gets the queue family index of the graphics VkQueue.
    /// </summary>
    public uint GraphicsQueueFamilyIndex => _gd.GraphicsQueueIndex;

    /// <summary>
    /// Gets the driver name of the device. May be null.
    /// </summary>
    public string? DriverName => _gd.DriverName;

    /// <summary>
    /// Gets the driver information of the device. May be null.
    /// </summary>
    public string? DriverInfo => _gd.DriverInfo;

    /// <summary>
    /// Instance layers which are available on the current system.
    /// </summary>
    public ReadOnlyCollection<string> AvailableInstanceLayers { get; }

    /// <summary>
    /// Instance extensions which are available on the current system.
    /// </summary>
    public ReadOnlyCollection<string> AvailableInstanceExtensions { get; }

    /// <summary>
    /// Device extensions which are available on the current system.
    /// </summary>
    public ReadOnlyCollection<ExtensionProperties> AvailableDeviceExtensions =>
        _deviceExtensions.Value;

    /// <summary>
    /// Overrides the current VkImageLayout tracked by the given Texture. This should be used when a VkImage is created by
    /// an external library to inform Veldrid about its initial layout.
    /// </summary>
    /// <param name="texture">The Texture whose currently-tracked VkImageLayout will be overridden.</param>
    /// <param name="layout">The new VkImageLayout value.</param>
    public void OverrideImageLayout(Texture texture, uint layout)
    {
        VkTexture vkTex = Util.AssertSubtype<Texture, VkTexture>(texture);
        for (uint layer = 0; layer < vkTex.ArrayLayers; layer++)
        {
            for (uint level = 0; level < vkTex.MipLevels; level++)
            {
                vkTex.SetImageLayout(level, layer, (VkImageLayout)layout);
            }
        }
    }

    /// <summary>
    /// Gets the underlying VkImage wrapped by the given Veldrid Texture. This method can not be used on Textures with
    /// TextureUsage.Staging.
    /// </summary>
    /// <param name="texture">The Texture whose underlying VkImage will be returned.</param>
    /// <returns>The underlying VkImage for the given Texture.</returns>
    public ulong GetVkImage(Texture texture)
    {
        VkTexture vkTexture = Util.AssertSubtype<Texture, VkTexture>(texture);
        if ((vkTexture.Usage & TextureUsage.Staging) != 0)
        {
            throw new VeldridException(
                $"{nameof(GetVkImage)} cannot be used if the {nameof(Texture)} "
                    + $"has {nameof(TextureUsage)}.{nameof(TextureUsage.Staging)}."
            );
        }

        return vkTexture.OptimalDeviceImage.Value;
    }

    /// <summary>
    /// Transitions the given Texture's underlying VkImage into a new layout.
    /// </summary>
    /// <param name="commandList">The command list to record the image transition into.</param>
    /// <param name="texture">The Texture whose underlying VkImage will be transitioned.</param>
    /// <param name="layout">The new VkImageLayout value.</param>
    public void TransitionImageLayout(CommandList commandList, Texture texture, uint layout)
    {
        VkCommandList vkCL = Util.AssertSubtype<CommandList, VkCommandList>(commandList);
        vkCL.TransitionImageLayout(
            Util.AssertSubtype<Texture, VkTexture>(texture),
            (VkImageLayout)layout
        );
    }

    /// <inheritdoc cref="TransitionImageLayout(CommandList, Texture, uint)"/>
    [Obsolete("Prefer using the overload taking a CommandList for proper synchronization.")]
    public void TransitionImageLayout(Texture texture, uint layout)
    {
        VkCommandList cl = _gd.GetAndBeginCommandList();
        cl.TransitionImageLayout(
            Util.AssertSubtype<Texture, VkTexture>(texture),
            (VkImageLayout)layout
        );
        _gd.EndAndSubmitCommands(cl);
    }

    /// <summary>
    /// Gets the pixel format used by the underlying Vulkan texture.
    /// </summary>
    /// <param name="texture"></param>
    /// <returns></returns>
    public VkFormat GetVkFormat(Texture texture)
    {
        VkTexture vkTexture = Util.AssertSubtype<Texture, VkTexture>(texture);
        return vkTexture.VkFormat;
    }

    ReadOnlyCollection<ExtensionProperties> EnumerateDeviceExtensions()
    {
        VkExtensionProperties[] vkProps = _gd.GetDeviceExtensionProperties();
        ExtensionProperties[] veldridProps = new ExtensionProperties[vkProps.Length];

        for (int i = 0; i < vkProps.Length; i++)
        {
            VkExtensionProperties prop = vkProps[i];
            veldridProps[i] = new(Util.GetString(prop.extensionName), prop.specVersion);
        }

        return new(veldridProps);
    }

    /// <summary>
    /// Represents the properties of a Vulkan extension.
    /// </summary>
    /// <param name="name">The name of the extension.</param>
    /// <param name="specVersion">The specification version of the extension.</param>
    public readonly struct ExtensionProperties(string name, uint specVersion)
    {
        /// <summary>
        /// The name of the extension.
        /// </summary>
        public readonly string Name = name ?? throw new ArgumentNullException(nameof(name));

        /// <summary>
        /// The specification version of the extension.
        /// </summary>
        public readonly uint SpecVersion = specVersion;

        /// <summary>
        /// Returns the name of the extension.
        /// </summary>
        /// <returns>The name of the extension.</returns>
        public override string ToString() => Name;
    }
}
#endif
