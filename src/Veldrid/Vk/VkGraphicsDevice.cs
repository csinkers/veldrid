using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TerraFX.Interop.Vulkan;
using VulkanFence = TerraFX.Interop.Vulkan.VkFence;
using static TerraFX.Interop.Vulkan.VkStructureType;
using static TerraFX.Interop.Vulkan.Vulkan;
using static Veldrid.Vulkan.VulkanUtil;

namespace Veldrid.Vulkan
{
    internal sealed unsafe class VkGraphicsDevice : GraphicsDevice
    {
        private static readonly FixedUtf8String s_name = "Veldrid-VkGraphicsDevice";
        private static readonly Lazy<bool> s_isSupported = new(CheckIsSupported, isThreadSafe: true);

        private VkInstance _instance;
        private VkPhysicalDevice _physicalDevice;
        private string? _driverName;
        private string? _driverInfo;
        private VkDeviceMemoryManager _memoryManager;
        private VkPhysicalDeviceProperties _physicalDeviceProperties;
        private VkPhysicalDeviceFeatures _physicalDeviceFeatures;
        private VkPhysicalDeviceMemoryProperties _physicalDeviceMemProperties;
        private VkDevice _device;
        private uint _graphicsQueueIndex;
        private uint _presentQueueIndex;
        private VkCommandPool _graphicsCommandPool;
        private VkQueue _graphicsQueue;
        private readonly object _graphicsQueueLock = new();
        private VkDebugReportCallbackEXT _debugCallbackHandle;
        private bool _debugMarkerEnabled;
        private vkDebugMarkerSetObjectNameEXT_t? _setObjectNameDelegate;
        private vkCmdDebugMarkerBeginEXT_t? _markerBegin;
        private vkCmdDebugMarkerEndEXT_t? _markerEnd;
        private vkCmdDebugMarkerInsertEXT_t? _markerInsert;
        private readonly ConcurrentDictionary<VkFormat, VkFilter> _filters = new();
        private readonly BackendInfoVulkan _vulkanInfo;

        private const int MaxSharedCommandLists = 4;
        private Stack<VkCommandList> _sharedCommandLists = new(MaxSharedCommandLists);
        private VkDescriptorPoolManager _descriptorPoolManager;
        private bool _standardValidationSupported;
        private bool _khronosValidationSupported;
        private vkGetBufferMemoryRequirements2_t? _getBufferMemoryRequirements2;
        private vkGetImageMemoryRequirements2_t? _getImageMemoryRequirements2;
        private vkGetPhysicalDeviceProperties2_t? _getPhysicalDeviceProperties2;

        // Staging Resources
        private const uint MinStagingBufferSize = 64;
        private const uint MaxStagingBufferSize = 512;

        private readonly List<VkTexture> _availableStagingTextures = new();
        private readonly List<VkBuffer> _availableStagingBuffers = new();

        public override bool GetVulkanInfo(out BackendInfoVulkan info)
        {
            info = _vulkanInfo;
            return true;
        }

        public VkInstance Instance => _instance;
        public VkDevice Device => _device;
        public VkPhysicalDevice PhysicalDevice => _physicalDevice;
        public VkPhysicalDeviceMemoryProperties PhysicalDeviceMemProperties => _physicalDeviceMemProperties;
        public VkQueue GraphicsQueue => _graphicsQueue;
        public uint GraphicsQueueIndex => _graphicsQueueIndex;
        public uint PresentQueueIndex => _presentQueueIndex;
        public bool DebugMarkerEnabled => _debugMarkerEnabled;
        public string? DriverName => _driverName;
        public string? DriverInfo => _driverInfo;
        public VkDeviceMemoryManager MemoryManager => _memoryManager;
        public VkDescriptorPoolManager DescriptorPoolManager => _descriptorPoolManager;
        public vkCmdDebugMarkerBeginEXT_t? MarkerBegin => _markerBegin;
        public vkCmdDebugMarkerEndEXT_t? MarkerEnd => _markerEnd;
        public vkCmdDebugMarkerInsertEXT_t? MarkerInsert => _markerInsert;
        public vkGetBufferMemoryRequirements2_t? GetBufferMemoryRequirements2 => _getBufferMemoryRequirements2;
        public vkGetImageMemoryRequirements2_t? GetImageMemoryRequirements2 => _getImageMemoryRequirements2;

        private readonly object _submittedFencesLock = new();
        private readonly ConcurrentQueue<VulkanFence> _availableSubmissionFences = new();
        private readonly List<FenceSubmissionInfo> _submittedFences = new();

        private readonly List<FixedUtf8String> _surfaceExtensions = new();

        public VkGraphicsDevice(GraphicsDeviceOptions options, SwapchainDescription? scDesc)
            : this(options, scDesc, new VulkanDeviceOptions())
        {
        }

        public VkGraphicsDevice(GraphicsDeviceOptions options, SwapchainDescription? scDesc, VulkanDeviceOptions vkOptions)
        {
            BackendType = GraphicsBackend.Vulkan;
            IsUvOriginTopLeft = true;
            IsDepthRangeZeroToOne = true;
            IsClipSpaceYInverted = true;

            CreateInstance(options.Debug, vkOptions);
            IsDebug = options.Debug;

            VkSurfaceKHR surface = VkSurfaceKHR.NULL;
            if (scDesc != null)
            {
                surface = VkSurfaceUtil.CreateSurface(_instance, scDesc.Value.Source);
            }

            CreatePhysicalDevice();
            CreateLogicalDevice(surface, options.PreferStandardClipSpaceYDirection, vkOptions);

            _memoryManager = new VkDeviceMemoryManager(
                _device,
                _physicalDevice,
                _physicalDeviceProperties.limits.bufferImageGranularity,
                1024);

            Features = new GraphicsDeviceFeatures(
                computeShader: true,
                geometryShader: (VkBool32)_physicalDeviceFeatures.geometryShader,
                tessellationShaders: (VkBool32)_physicalDeviceFeatures.tessellationShader,
                multipleViewports: (VkBool32)_physicalDeviceFeatures.multiViewport,
                samplerLodBias: true,
                drawBaseVertex: true,
                drawBaseInstance: true,
                drawIndirect: true,
                drawIndirectBaseInstance: (VkBool32)_physicalDeviceFeatures.drawIndirectFirstInstance,
                fillModeWireframe: (VkBool32)_physicalDeviceFeatures.fillModeNonSolid,
                samplerAnisotropy: (VkBool32)_physicalDeviceFeatures.samplerAnisotropy,
                depthClipDisable: (VkBool32)_physicalDeviceFeatures.depthClamp,
                texture1D: true,
                independentBlend: (VkBool32)_physicalDeviceFeatures.independentBlend,
                structuredBuffer: true,
                subsetTextureView: true,
                commandListDebugMarkers: _debugMarkerEnabled,
                bufferRangeBinding: true,
                shaderFloat64: (VkBool32)_physicalDeviceFeatures.shaderFloat64);

            ResourceFactory = new VkResourceFactory(this);

            if (scDesc != null)
            {
                SwapchainDescription desc = scDesc.Value;
                MainSwapchain = new VkSwapchain(this, desc, surface);
            }

            _descriptorPoolManager = new VkDescriptorPoolManager(this);

            CreateGraphicsCommandPool();

            _vulkanInfo = new BackendInfoVulkan(this);

            PostDeviceCreated();
        }

        private protected override void SubmitCommandsCore(CommandList cl, Fence? fence)
        {
            VkCommandList vkCL = Util.AssertSubtype<CommandList, VkCommandList>(cl);
            SubmitCommandList(vkCL, 0, null, 0, null, fence, false);
        }

        internal void EndAndSubmitCommands(VkCommandList cl)
        {
            cl.End();
            SubmitCommandList(cl, 0, null, 0, null, null, true);
        }

        private void SubmitCommandList(
            VkCommandList vkCL,
            uint waitSemaphoreCount,
            VkSemaphore* waitSemaphoresPtr,
            uint signalSemaphoreCount,
            VkSemaphore* signalSemaphoresPtr,
            Fence? fence,
            bool isPooled)
        {
            // A fence may complete before Veldrid gets notified of the
            // corresponding VkCommandBuffer completion, so check fences here
            CheckSubmittedFences();

            VkCommandBuffer cb = vkCL.CommandBufferSubmitted();

            SubmitCommandBuffer(
                vkCL, cb, waitSemaphoreCount, waitSemaphoresPtr, signalSemaphoreCount, signalSemaphoresPtr, fence, isPooled);
        }

        private void SubmitCommandBuffer(
            VkCommandList vkCL,
            VkCommandBuffer vkCB,
            uint waitSemaphoreCount,
            VkSemaphore* waitSemaphoresPtr,
            uint signalSemaphoreCount,
            VkSemaphore* signalSemaphoresPtr,
            Fence? fence,
            bool isPooled)
        {
            VkPipelineStageFlags waitDstStageMask = VkPipelineStageFlags.VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT;

            VkSubmitInfo si = new()
            {
                sType = VK_STRUCTURE_TYPE_SUBMIT_INFO,
                commandBufferCount = 1,
                pCommandBuffers = &vkCB,
                pWaitDstStageMask = &waitDstStageMask,

                pWaitSemaphores = waitSemaphoresPtr,
                waitSemaphoreCount = waitSemaphoreCount,
                pSignalSemaphores = signalSemaphoresPtr,
                signalSemaphoreCount = signalSemaphoreCount
            };

            VulkanFence vkFence;
            VulkanFence submissionFence;
            if (fence != null)
            {
                vkFence = Util.AssertSubtype<Fence, VkFence>(fence).DeviceFence;
                submissionFence = GetFreeSubmissionFence();
            }
            else
            {
                vkFence = GetFreeSubmissionFence();
                submissionFence = vkFence;
            }

            lock (_graphicsQueueLock)
            {
                VkResult result = vkQueueSubmit(_graphicsQueue, 1, &si, vkFence);
                CheckResult(result);
                if (fence != null)
                {
                    result = vkQueueSubmit(_graphicsQueue, 0, null, submissionFence);
                    CheckResult(result);
                }
            }

            lock (_submittedFencesLock)
            {
                _submittedFences.Add(new FenceSubmissionInfo(submissionFence, vkCL, vkCB, isPooled));
            }
        }

        private void CheckSubmittedFences()
        {
            lock (_submittedFencesLock)
            {
                List<FenceSubmissionInfo> submittedFences = _submittedFences;
                for (int i = 0; i < submittedFences.Count; i++)
                {
                    FenceSubmissionInfo fsi = submittedFences[i];
                    if (vkGetFenceStatus(_device, fsi.Fence) == VkResult.VK_SUCCESS)
                    {
                        CompleteFenceSubmission(fsi);
                        submittedFences.RemoveAt(i);
                        i -= 1;
                    }
                }
            }
        }

        private void CompleteFenceSubmission(in FenceSubmissionInfo fsi)
        {
            VkCommandList cl = fsi.CommandList;
            VulkanFence fence = fsi.Fence;
            VkCommandBuffer completedCB = fsi.CommandBuffer;
            VkCommandList.StagingResourceInfo stagingInfo = cl.CommandBufferCompleted(completedCB);
            try
            {
                VkResult resetResult = vkResetFences(_device, 1, &fence);
                CheckResult(resetResult);
                ReturnSubmissionFence(fence);

                if (fsi.IsPooled)
                {
                    lock (_sharedCommandLists)
                    {
                        if (_sharedCommandLists.Count < MaxSharedCommandLists)
                        {
                            _sharedCommandLists.Push(cl);
                            return;
                        }
                    }
                    cl.Dispose();
                }
            }
            finally
            {
                cl.RecycleStagingInfo(stagingInfo);
            }
        }

        private void ReturnSubmissionFence(VulkanFence fence)
        {
            _availableSubmissionFences.Enqueue(fence);
        }

        private VulkanFence GetFreeSubmissionFence()
        {
            if (_availableSubmissionFences.TryDequeue(out VulkanFence availableFence))
            {
                return availableFence;
            }
            else
            {
                VkFenceCreateInfo fenceCI = new()
                {
                    sType = VK_STRUCTURE_TYPE_FENCE_CREATE_INFO
                };
                VulkanFence newFence;
                VkResult result = vkCreateFence(_device, &fenceCI, null, &newFence);
                CheckResult(result);
                return newFence;
            }
        }

        private protected override void SwapBuffersCore(Swapchain swapchain)
        {
            VkSwapchain vkSC = Util.AssertSubtype<Swapchain, VkSwapchain>(swapchain);
            VkSwapchainKHR deviceSwapchain = vkSC.DeviceSwapchain;
            uint imageIndex = vkSC.ImageIndex;
            VkPresentInfoKHR presentInfo = new()
            {
                sType = VK_STRUCTURE_TYPE_PRESENT_INFO_KHR,
                swapchainCount = 1,
                pSwapchains = &deviceSwapchain,
                pImageIndices = &imageIndex
            };

            object presentLock = vkSC.PresentQueueIndex == _graphicsQueueIndex ? _graphicsQueueLock : vkSC.PresentLock;
            lock (presentLock)
            {
                VkResult presentResult = vkQueuePresentKHR(vkSC.PresentQueue, &presentInfo);
                if (presentResult != VkResult.VK_SUCCESS &&
                    presentResult != VkResult.VK_SUBOPTIMAL_KHR &&
                    presentResult != VkResult.VK_ERROR_OUT_OF_DATE_KHR)
                {
                    ThrowResult(presentResult);
                }

                VulkanFence fence = vkSC.ImageAvailableFence;
                if (vkSC.AcquireNextImage(_device, VkSemaphore.NULL, fence))
                {
                    VkResult waitResult = vkWaitForFences(_device, 1, &fence, (VkBool32)true, ulong.MaxValue);
                    CheckResult(waitResult);

                    VkResult resetResult = vkResetFences(_device, 1, &fence);
                    CheckResult(resetResult);
                }
            }
        }

        internal void SetResourceName(DeviceResource resource, ReadOnlySpan<char> name)
        {
            if (_debugMarkerEnabled)
            {
                SetResourceNameCore(resource, name);
            }
        }

        private void SetResourceNameCore(DeviceResource resource, ReadOnlySpan<char> name)
        {
            switch (resource)
            {
                case VkBuffer buffer:
                    SetDebugMarkerName(VkDebugReportObjectTypeEXT.VK_DEBUG_REPORT_OBJECT_TYPE_BUFFER_EXT, buffer.DeviceBuffer.Value, name);
                    break;

                case VkFramebuffer framebuffer:
                    SetDebugMarkerName(
                        VkDebugReportObjectTypeEXT.VK_DEBUG_REPORT_OBJECT_TYPE_FRAMEBUFFER_EXT,
                        framebuffer.CurrentFramebuffer.Value,
                        name);
                    break;

                case VkPipeline pipeline:
                    SetDebugMarkerName(VkDebugReportObjectTypeEXT.VK_DEBUG_REPORT_OBJECT_TYPE_PIPELINE_EXT, pipeline.DevicePipeline.Value, name);
                    SetDebugMarkerName(VkDebugReportObjectTypeEXT.VK_DEBUG_REPORT_OBJECT_TYPE_PIPELINE_LAYOUT_EXT, pipeline.PipelineLayout.Value, name);
                    break;

                case VkResourceLayout resourceLayout:
                    SetDebugMarkerName(
                        VkDebugReportObjectTypeEXT.VK_DEBUG_REPORT_OBJECT_TYPE_DESCRIPTOR_SET_LAYOUT_EXT,
                        resourceLayout.DescriptorSetLayout.Value,
                        name);
                    break;

                case VkResourceSet resourceSet:
                    SetDebugMarkerName(VkDebugReportObjectTypeEXT.VK_DEBUG_REPORT_OBJECT_TYPE_DESCRIPTOR_SET_EXT, resourceSet.DescriptorSet.Value, name);
                    break;

                case VkSampler sampler:
                    SetDebugMarkerName(VkDebugReportObjectTypeEXT.VK_DEBUG_REPORT_OBJECT_TYPE_SAMPLER_EXT, sampler.DeviceSampler.Value, name);
                    break;

                case VkShader shader:
                    SetDebugMarkerName(VkDebugReportObjectTypeEXT.VK_DEBUG_REPORT_OBJECT_TYPE_SHADER_MODULE_EXT, shader.ShaderModule.Value, name);
                    break;

                case VkTexture tex:
                    if (tex.OptimalDeviceImage.Value != 0)
                        SetDebugMarkerName(VkDebugReportObjectTypeEXT.VK_DEBUG_REPORT_OBJECT_TYPE_IMAGE_EXT, tex.OptimalDeviceImage.Value, name);
                    else
                        SetDebugMarkerName(VkDebugReportObjectTypeEXT.VK_DEBUG_REPORT_OBJECT_TYPE_BUFFER_EXT, tex.StagingBuffer.Value, name);
                    break;

                case VkTextureView texView:
                    SetDebugMarkerName(VkDebugReportObjectTypeEXT.VK_DEBUG_REPORT_OBJECT_TYPE_IMAGE_VIEW_EXT, texView.ImageView.Value, name);
                    break;

                case VkFence fence:
                    SetDebugMarkerName(VkDebugReportObjectTypeEXT.VK_DEBUG_REPORT_OBJECT_TYPE_FENCE_EXT, fence.DeviceFence.Value, name);
                    break;

                case VkSwapchain sc:
                    SetDebugMarkerName(VkDebugReportObjectTypeEXT.VK_DEBUG_REPORT_OBJECT_TYPE_SWAPCHAIN_KHR_EXT, sc.DeviceSwapchain.Value, name);
                    break;

                default:
                    break;
            }
        }

        [SkipLocalsInit]
        internal void SetDebugMarkerName(VkDebugReportObjectTypeEXT type, ulong target, ReadOnlySpan<char> name)
        {
            Span<byte> utf8Buffer = stackalloc byte[1024];
            Util.GetNullTerminatedUtf8(name, ref utf8Buffer);
            SetDebugMarkerName(type, target, utf8Buffer);
        }

        internal void SetDebugMarkerName(VkDebugReportObjectTypeEXT type, ulong target, ReadOnlySpan<byte> nameUtf8)
        {
            Debug.Assert(_setObjectNameDelegate != null);

            fixed (byte* utf8Ptr = nameUtf8)
            {
                VkDebugMarkerObjectNameInfoEXT nameInfo = new()
                {
                    sType = VK_STRUCTURE_TYPE_DEBUG_MARKER_OBJECT_NAME_INFO_EXT,
                    objectType = type,
                    @object = target,
                    pObjectName = (sbyte*)utf8Ptr
                };

                VkResult result = _setObjectNameDelegate(_device, &nameInfo);
                CheckResult(result);
            }
        }

        private void CreateInstance(bool debug, VulkanDeviceOptions options)
        {
            HashSet<string> availableInstanceLayers = new(EnumerateInstanceLayers());
            HashSet<string> availableInstanceExtensions = new(EnumerateInstanceExtensions());

            VkApplicationInfo applicationInfo = new()
            {
                sType = VK_STRUCTURE_TYPE_APPLICATION_INFO,
                apiVersion = new VkVersion(1, 0, 0),
                applicationVersion = new VkVersion(1, 0, 0),
                engineVersion = new VkVersion(1, 0, 0),
                pApplicationName = s_name,
                pEngineName = s_name
            };

            VkInstanceCreateInfo instanceCI = new()
            {
                sType = VK_STRUCTURE_TYPE_INSTANCE_CREATE_INFO,
                pApplicationInfo = &applicationInfo
            };

            List<IntPtr> instanceExtensions = new();
            List<IntPtr> instanceLayers = new();

            if (availableInstanceExtensions.Contains(CommonStrings.VK_KHR_portability_subset))
            {
                _surfaceExtensions.Add(CommonStrings.VK_KHR_portability_subset);
            }

            if (availableInstanceExtensions.Contains(CommonStrings.VK_KHR_SURFACE_EXTENSION_NAME))
            {
                _surfaceExtensions.Add(CommonStrings.VK_KHR_SURFACE_EXTENSION_NAME);
            }

            _surfaceExtensions.AddRange(GetSurfaceExtensions(availableInstanceExtensions));

            foreach (FixedUtf8String? ext in _surfaceExtensions)
            {
                instanceExtensions.Add(ext);
            }

            bool hasDeviceProperties2 = availableInstanceExtensions.Contains(CommonStrings.VK_KHR_get_physical_device_properties2);
            if (hasDeviceProperties2)
            {
                instanceExtensions.Add(CommonStrings.VK_KHR_get_physical_device_properties2);
            }

            string[] requestedInstanceExtensions = options.InstanceExtensions ?? Array.Empty<string>();
            List<FixedUtf8String> tempStrings = new();
            try
            {
                foreach (string requiredExt in requestedInstanceExtensions)
                {
                    if (!availableInstanceExtensions.Contains(requiredExt))
                    {
                        throw new VeldridException($"The required instance extension was not available: {requiredExt}");
                    }

                    FixedUtf8String utf8Str = new(requiredExt);
                    instanceExtensions.Add(utf8Str);
                    tempStrings.Add(utf8Str);
                }

                bool debugReportExtensionAvailable = false;
                if (debug)
                {
                    if (availableInstanceExtensions.Contains(CommonStrings.VK_EXT_DEBUG_REPORT_EXTENSION_NAME))
                    {
                        IsDriverDebug = true;
                        debugReportExtensionAvailable = true;
                        instanceExtensions.Add(CommonStrings.VK_EXT_DEBUG_REPORT_EXTENSION_NAME);
                    }
                    if (availableInstanceLayers.Contains(CommonStrings.StandardValidationLayerName))
                    {
                        _standardValidationSupported = true;
                        instanceLayers.Add(CommonStrings.StandardValidationLayerName);
                    }
                    if (availableInstanceLayers.Contains(CommonStrings.KhronosValidationLayerName))
                    {
                        _khronosValidationSupported = true;
                        instanceLayers.Add(CommonStrings.KhronosValidationLayerName);
                    }
                }

                fixed (IntPtr* ppInstanceExtensions = CollectionsMarshal.AsSpan(instanceExtensions))
                fixed (IntPtr* ppInstanceLayers = CollectionsMarshal.AsSpan(instanceLayers))
                {
                    instanceCI.enabledExtensionCount = (uint)instanceExtensions.Count;
                    instanceCI.ppEnabledExtensionNames = (sbyte**)ppInstanceExtensions;

                    instanceCI.enabledLayerCount = (uint)instanceLayers.Count;
                    if (instanceLayers.Count > 0)
                    {
                        instanceCI.ppEnabledLayerNames = (sbyte**)ppInstanceLayers;
                    }

                    VkInstance instance;
                    VkResult result = vkCreateInstance(&instanceCI, null, &instance);
                    CheckResult(result);
                    _instance = instance;

                    vkEnumerateInstanceVersion_t? instanceVersion =
                        GetInstanceProcAddr<vkEnumerateInstanceVersion_t>("vkEnumerateInstanceVersion");

                    if (instanceVersion != null)
                    {
                        VkVersion version;
                        instanceVersion(&version.value);

                        VkVersion currentVersion = new(instanceCI.pApplicationInfo->apiVersion);
                        if (version.Minor > currentVersion.Minor)
                        {
                            vkDestroyInstance(_instance, null);

                            instanceCI.pApplicationInfo->apiVersion = version;

                            result = vkCreateInstance(&instanceCI, null, &instance);
                            CheckResult(result);
                            _instance = instance;
                        }
                    }
                }

                if (debugReportExtensionAvailable)
                {
                    VkDebugReportFlagsEXT flags = VkDebugReportFlagsEXT.VK_DEBUG_REPORT_ERROR_BIT_EXT;
                    if (debug)
                    {
                        flags |=
                            VkDebugReportFlagsEXT.VK_DEBUG_REPORT_INFORMATION_BIT_EXT |
                            VkDebugReportFlagsEXT.VK_DEBUG_REPORT_WARNING_BIT_EXT |
                            VkDebugReportFlagsEXT.VK_DEBUG_REPORT_PERFORMANCE_WARNING_BIT_EXT |
                            VkDebugReportFlagsEXT.VK_DEBUG_REPORT_DEBUG_BIT_EXT;
                    }
                    EnableDebugCallback(flags);
                }

                if (hasDeviceProperties2)
                {
                    _getPhysicalDeviceProperties2 =
                        GetInstanceProcAddr<vkGetPhysicalDeviceProperties2_t>("vkGetPhysicalDeviceProperties2") ??
                        GetInstanceProcAddr<vkGetPhysicalDeviceProperties2_t>("vkGetPhysicalDeviceProperties2KHR");
                }
            }
            finally
            {
                foreach (FixedUtf8String tempStr in tempStrings)
                {
                    tempStr.Dispose();
                }
            }
        }

        private static IEnumerable<FixedUtf8String> GetSurfaceExtensions(HashSet<string> instanceExtensions)
        {
            if (instanceExtensions.Contains(CommonStrings.VK_KHR_WIN32_SURFACE_EXTENSION_NAME))
            {
                yield return CommonStrings.VK_KHR_WIN32_SURFACE_EXTENSION_NAME;
            }
            if (instanceExtensions.Contains(CommonStrings.VK_KHR_ANDROID_SURFACE_EXTENSION_NAME))
            {
                yield return (CommonStrings.VK_KHR_ANDROID_SURFACE_EXTENSION_NAME);
            }
            if (instanceExtensions.Contains(CommonStrings.VK_KHR_XLIB_SURFACE_EXTENSION_NAME))
            {
                yield return (CommonStrings.VK_KHR_XLIB_SURFACE_EXTENSION_NAME);
            }
            if (instanceExtensions.Contains(CommonStrings.VK_KHR_WAYLAND_SURFACE_EXTENSION_NAME))
            {
                yield return (CommonStrings.VK_KHR_WAYLAND_SURFACE_EXTENSION_NAME);
            }
            if (instanceExtensions.Contains(CommonStrings.VK_EXT_METAL_SURFACE_EXTENSION_NAME))
            {
                yield return (CommonStrings.VK_EXT_METAL_SURFACE_EXTENSION_NAME);
            }

            // Legacy MoltenVK extensions
            if (instanceExtensions.Contains(CommonStrings.VK_MVK_MACOS_SURFACE_EXTENSION_NAME))
            {
                yield return (CommonStrings.VK_MVK_MACOS_SURFACE_EXTENSION_NAME);
            }
            if (instanceExtensions.Contains(CommonStrings.VK_MVK_IOS_SURFACE_EXTENSION_NAME))
            {
                yield return (CommonStrings.VK_MVK_IOS_SURFACE_EXTENSION_NAME);
            }
        }

        public bool HasSurfaceExtension(FixedUtf8String extension)
        {
            return _surfaceExtensions.Contains(extension);
        }

        public void EnableDebugCallback(VkDebugReportFlagsEXT flags)
        {
            vkCreateDebugReportCallbackEXT_d? createFnPtr =
                GetInstanceProcAddr<vkCreateDebugReportCallbackEXT_d>("vkCreateDebugReportCallbackEXT");
            if (createFnPtr == null)
            {
                return;
            }

            VkDebugReportCallbackCreateInfoEXT debugCallbackCI = new()
            {
                sType = VK_STRUCTURE_TYPE_DEBUG_REPORT_CALLBACK_CREATE_INFO_EXT,
                flags = flags,
                pfnCallback = &DebugCallback
            };

            VkDebugReportCallbackEXT handle;
            VkResult result = createFnPtr(_instance, &debugCallbackCI, IntPtr.Zero, &handle);
            CheckResult(result);
            _debugCallbackHandle = handle;
        }

        [UnmanagedCallersOnly]
        private static uint DebugCallback(
            VkDebugReportFlagsEXT flags,
            VkDebugReportObjectTypeEXT objectType,
            ulong @object,
            nuint location,
            int messageCode,
            sbyte* pLayerPrefix,
            sbyte* pMessage,
            void* pUserData)
        {
            string message = Util.GetString(pMessage);
            VkDebugReportFlagsEXT debugReportFlags = flags;

#if DEBUG
            if ((flags & VkDebugReportFlagsEXT.VK_DEBUG_REPORT_ERROR_BIT_EXT) != 0)
            {
                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }
            }
#endif

            string fullMessage = $"[{debugReportFlags}] ({objectType}) {message}";

            if (debugReportFlags == VkDebugReportFlagsEXT.VK_DEBUG_REPORT_ERROR_BIT_EXT)
            {
                throw new VeldridException("A Vulkan validation error was encountered: " + fullMessage);
            }

            Debug.WriteLine(fullMessage);
            return 0;
        }

        private void CreatePhysicalDevice()
        {
            uint deviceCount = 0;
            vkEnumeratePhysicalDevices(_instance, &deviceCount, null);
            if (deviceCount == 0)
            {
                throw new InvalidOperationException("No physical devices exist.");
            }

            VkPhysicalDevice[] physicalDevices = new VkPhysicalDevice[deviceCount];
            fixed (VkPhysicalDevice* physicalDevicesPtr = physicalDevices)
            {
                vkEnumeratePhysicalDevices(_instance, &deviceCount, physicalDevicesPtr);
            }
            // Just use the first one.
            _physicalDevice = physicalDevices[0];

            VkPhysicalDeviceProperties physicalDeviceProperties;
            vkGetPhysicalDeviceProperties(_physicalDevice, &physicalDeviceProperties);
            _physicalDeviceProperties = physicalDeviceProperties;

            UniformBufferMinOffsetAlignment = (uint)_physicalDeviceProperties.limits.minUniformBufferOffsetAlignment;
            StructuredBufferMinOffsetAlignment = (uint)_physicalDeviceProperties.limits.minStorageBufferOffsetAlignment;

            ReadOnlySpan<sbyte> deviceName = physicalDeviceProperties.deviceName;
            DeviceName = Util.GetString(deviceName);

            VendorName = "id:" + _physicalDeviceProperties.vendorID.ToString("x8");
            ApiVersion = GraphicsApiVersion.Unknown;
            _driverInfo = "version:" + _physicalDeviceProperties.driverVersion.ToString("x8");

            VkPhysicalDeviceFeatures physicalDeviceFeatures;
            vkGetPhysicalDeviceFeatures(_physicalDevice, &physicalDeviceFeatures);
            _physicalDeviceFeatures = physicalDeviceFeatures;

            VkPhysicalDeviceMemoryProperties physicalDeviceMemProperties;
            vkGetPhysicalDeviceMemoryProperties(_physicalDevice, &physicalDeviceMemProperties);
            _physicalDeviceMemProperties = physicalDeviceMemProperties;
        }

        public VkExtensionProperties[] GetDeviceExtensionProperties()
        {
            uint propertyCount = 0;
            VkResult result = vkEnumerateDeviceExtensionProperties(_physicalDevice, (sbyte*)null, &propertyCount, null);
            CheckResult(result);
            VkExtensionProperties[] props = new VkExtensionProperties[(int)propertyCount];
            fixed (VkExtensionProperties* properties = props)
            {
                result = vkEnumerateDeviceExtensionProperties(_physicalDevice, (sbyte*)null, &propertyCount, properties);
                CheckResult(result);
            }
            return props;
        }

        private void CreateLogicalDevice(VkSurfaceKHR surface, bool preferStandardClipY, VulkanDeviceOptions options)
        {
            GetQueueFamilyIndices(surface);

            HashSet<uint> familyIndices = new() { _graphicsQueueIndex, _presentQueueIndex };
            VkDeviceQueueCreateInfo* queueCreateInfos = stackalloc VkDeviceQueueCreateInfo[familyIndices.Count];
            uint queueCreateInfosCount = (uint)familyIndices.Count;

            int i = 0;
            foreach (uint index in familyIndices)
            {
                float priority = 1f;
                VkDeviceQueueCreateInfo queueCreateInfo = new()
                {
                    sType = VK_STRUCTURE_TYPE_DEVICE_QUEUE_CREATE_INFO,
                    queueFamilyIndex = _graphicsQueueIndex,
                    queueCount = 1,
                    pQueuePriorities = &priority
                };
                queueCreateInfos[i] = queueCreateInfo;
                i += 1;
            }

            VkPhysicalDeviceFeatures deviceFeatures = _physicalDeviceFeatures;

            VkExtensionProperties[] props = GetDeviceExtensionProperties();

            HashSet<string> requiredDeviceExtensions = new(options.DeviceExtensions ?? Array.Empty<string>());

            bool hasMemReqs2 = false;
            bool hasDedicatedAllocation = false;
            bool hasDriverProperties = false;
            IntPtr[] activeExtensions = new IntPtr[props.Length];
            uint activeExtensionCount = 0;

            fixed (VkExtensionProperties* properties = props)
            {
                for (int property = 0; property < props.Length; property++)
                {
                    string extensionName = Util.GetString(properties[property].extensionName);
                    if (extensionName == "VK_EXT_debug_marker")
                    {
                        requiredDeviceExtensions.Remove(extensionName);
                        _debugMarkerEnabled = true;
                    }
                    else if (extensionName == "VK_EXT_debug_utils")
                    {
                        // TODO: debug_utils are obsolete on AMD, modern replacement required

                        requiredDeviceExtensions.Remove(extensionName);
                        _debugMarkerEnabled = true;
                    }
                    else if (extensionName == "VK_KHR_swapchain")
                    {
                        requiredDeviceExtensions.Remove(extensionName);
                    }
                    else if (preferStandardClipY && extensionName == "VK_KHR_maintenance1")
                    {
                        requiredDeviceExtensions.Remove(extensionName);
                        IsClipSpaceYInverted = false;
                    }
                    else if (extensionName == "VK_KHR_get_memory_requirements2")
                    {
                        requiredDeviceExtensions.Remove(extensionName);
                        hasMemReqs2 = true;
                    }
                    else if (extensionName == "VK_KHR_dedicated_allocation")
                    {
                        requiredDeviceExtensions.Remove(extensionName);
                        hasDedicatedAllocation = true;
                    }
                    else if (extensionName == "VK_KHR_driver_properties")
                    {
                        requiredDeviceExtensions.Remove(extensionName);
                        hasDriverProperties = true;
                    }
                    else if (extensionName == "VK_KHR_portability_subset")
                    {
                        requiredDeviceExtensions.Remove(extensionName);
                    }
                    else if (requiredDeviceExtensions.Remove(extensionName))
                    {
                    }
                    else
                    {
                        continue;
                    }
                    activeExtensions[activeExtensionCount++] = (IntPtr)(&properties[property].extensionName);
                }
            }

            if (requiredDeviceExtensions.Count != 0)
            {
                string missingList = string.Join(", ", requiredDeviceExtensions);
                throw new VeldridException(
                    $"The following Vulkan device extensions were not available: {missingList}");
            }


            StackList<IntPtr> layerNames = new();
            if (_standardValidationSupported)
            {
                layerNames.Add(CommonStrings.StandardValidationLayerName);
            }
            if (_khronosValidationSupported)
            {
                layerNames.Add(CommonStrings.KhronosValidationLayerName);
            }

            fixed (IntPtr* activeExtensionsPtr = activeExtensions)
            {
                VkDeviceCreateInfo deviceCreateInfo = new()
                {
                    sType = VK_STRUCTURE_TYPE_DEVICE_CREATE_INFO,
                    queueCreateInfoCount = queueCreateInfosCount,
                    pQueueCreateInfos = queueCreateInfos,

                    pEnabledFeatures = &deviceFeatures,

                    enabledLayerCount = layerNames.Count,
                    ppEnabledLayerNames = (sbyte**)layerNames.Data,

                    enabledExtensionCount = activeExtensionCount,
                    ppEnabledExtensionNames = (sbyte**)activeExtensionsPtr
                };

                VkDevice device;
                VkResult result = vkCreateDevice(_physicalDevice, &deviceCreateInfo, null, &device);
                CheckResult(result);
                _device = device;
            }

            VkQueue graphicsQueue;
            vkGetDeviceQueue(_device, _graphicsQueueIndex, 0, &graphicsQueue);
            _graphicsQueue = graphicsQueue;

            if (_debugMarkerEnabled)
            {
                _setObjectNameDelegate = Marshal.GetDelegateForFunctionPointer<vkDebugMarkerSetObjectNameEXT_t>(
                    GetInstanceProcAddr("vkDebugMarkerSetObjectNameEXT"));
                _markerBegin = Marshal.GetDelegateForFunctionPointer<vkCmdDebugMarkerBeginEXT_t>(
                    GetInstanceProcAddr("vkCmdDebugMarkerBeginEXT"));
                _markerEnd = Marshal.GetDelegateForFunctionPointer<vkCmdDebugMarkerEndEXT_t>(
                    GetInstanceProcAddr("vkCmdDebugMarkerEndEXT"));
                _markerInsert = Marshal.GetDelegateForFunctionPointer<vkCmdDebugMarkerInsertEXT_t>(
                    GetInstanceProcAddr("vkCmdDebugMarkerInsertEXT"));
            }
            if (hasDedicatedAllocation && hasMemReqs2)
            {
                _getBufferMemoryRequirements2 = GetDeviceProcAddr<vkGetBufferMemoryRequirements2_t>("vkGetBufferMemoryRequirements2")
                    ?? GetDeviceProcAddr<vkGetBufferMemoryRequirements2_t>("vkGetBufferMemoryRequirements2KHR");
                _getImageMemoryRequirements2 = GetDeviceProcAddr<vkGetImageMemoryRequirements2_t>("vkGetImageMemoryRequirements2")
                    ?? GetDeviceProcAddr<vkGetImageMemoryRequirements2_t>("vkGetImageMemoryRequirements2KHR");
            }
            if (_getPhysicalDeviceProperties2 != null && hasDriverProperties)
            {
                VkPhysicalDeviceDriverProperties driverProps = new()
                {
                    sType = VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_DRIVER_PROPERTIES
                };

                VkPhysicalDeviceProperties2 deviceProps = new()
                {
                    sType = VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_PROPERTIES_2,
                    pNext = &driverProps
                };
                _getPhysicalDeviceProperties2(_physicalDevice, &deviceProps);

                string driverName = Util.GetString(driverProps.driverName);
                string driverInfo = Util.GetString(driverProps.driverInfo);

                VkConformanceVersion conforming = driverProps.conformanceVersion;
                ApiVersion = new GraphicsApiVersion(conforming.major, conforming.minor, conforming.subminor, conforming.patch);
                _driverName = driverName;
                _driverInfo = driverInfo;
            }
        }

        private IntPtr GetInstanceProcAddr(string name)
        {
            return VulkanUtil.GetInstanceProcAddr(_instance, name);
        }

        private T? GetInstanceProcAddr<T>(string name)
        {
            IntPtr funcPtr = GetInstanceProcAddr(name);
            if (funcPtr != IntPtr.Zero)
            {
                return Marshal.GetDelegateForFunctionPointer<T>(funcPtr);
            }
            return default;
        }

        [SkipLocalsInit]
        private IntPtr GetDeviceProcAddr(ReadOnlySpan<char> name)
        {
            Span<byte> byteBuffer = stackalloc byte[1024];

            Util.GetNullTerminatedUtf8(name, ref byteBuffer);
            fixed (byte* utf8Ptr = byteBuffer)
            {
                return (IntPtr)vkGetDeviceProcAddr(_device, (sbyte*)utf8Ptr);
            }
        }

        private T GetDeviceProcAddr<T>(string name)
        {
            IntPtr funcPtr = GetDeviceProcAddr(name);
            if (funcPtr != IntPtr.Zero)
            {
                return Marshal.GetDelegateForFunctionPointer<T>(funcPtr);
            }
            throw new EntryPointNotFoundException(name);
        }

        private void GetQueueFamilyIndices(VkSurfaceKHR surface)
        {
            uint queueFamilyCount = 0;
            vkGetPhysicalDeviceQueueFamilyProperties(_physicalDevice, &queueFamilyCount, null);
            VkQueueFamilyProperties[] qfp = new VkQueueFamilyProperties[queueFamilyCount];
            fixed (VkQueueFamilyProperties* qfpPtr = qfp)
            {
                vkGetPhysicalDeviceQueueFamilyProperties(_physicalDevice, &queueFamilyCount, qfpPtr);
            }

            bool foundGraphics = false;
            bool foundPresent = surface == VkSurfaceKHR.NULL;

            for (uint i = 0; i < qfp.Length; i++)
            {
                if ((qfp[i].queueFlags & VkQueueFlags.VK_QUEUE_GRAPHICS_BIT) != 0)
                {
                    _graphicsQueueIndex = i;
                    foundGraphics = true;
                }

                if (!foundPresent)
                {
                    uint presentSupported;
                    vkGetPhysicalDeviceSurfaceSupportKHR(_physicalDevice, i, surface, &presentSupported);
                    if ((VkBool32)presentSupported)
                    {
                        _presentQueueIndex = i;
                        foundPresent = true;
                    }
                }

                if (foundGraphics && foundPresent)
                {
                    return;
                }
            }
        }

        private void CreateGraphicsCommandPool()
        {
            VkCommandPoolCreateInfo commandPoolCI = new()
            {
                sType = VK_STRUCTURE_TYPE_COMMAND_POOL_CREATE_INFO,
                flags = VkCommandPoolCreateFlags.VK_COMMAND_POOL_CREATE_RESET_COMMAND_BUFFER_BIT,
                queueFamilyIndex = _graphicsQueueIndex
            };
            VkCommandPool graphicsCommandPool;
            VkResult result = vkCreateCommandPool(_device, &commandPoolCI, null, &graphicsCommandPool);
            CheckResult(result);
            _graphicsCommandPool = graphicsCommandPool;
        }

        private protected override MappedResource MapCore(
            MappableResource resource, uint offsetInBytes, uint sizeInBytes, MapMode mode, uint subresource)
        {
            VkMemoryBlock memoryBlock;
            IntPtr mappedPtr = IntPtr.Zero;
            uint rowPitch = 0;
            uint depthPitch = 0;

            if (resource is VkBuffer buffer)
            {
                memoryBlock = buffer.Memory;
            }
            else
            {
                VkTexture texture = Util.AssertSubtype<MappableResource, VkTexture>(resource);
                Util.GetMipLevelAndArrayLayer(texture, subresource, out uint mipLevel, out uint arrayLayer);
                VkSubresourceLayout layout = texture.GetSubresourceLayout(mipLevel, arrayLayer);
                memoryBlock = texture.Memory;
                offsetInBytes += (uint)layout.offset;
                rowPitch = (uint)layout.rowPitch;
                depthPitch = (uint)layout.depthPitch;
            }

            if (memoryBlock.DeviceMemory != VkDeviceMemory.NULL)
            {
                ulong atomSize = _physicalDeviceProperties.limits.nonCoherentAtomSize;
                ulong mapOffset = memoryBlock.Offset + offsetInBytes;
                ulong bindOffset = ((mapOffset / atomSize) * atomSize);
                ulong bindSize = ((sizeInBytes + atomSize - 1) / atomSize) * atomSize;

                if (memoryBlock.IsPersistentMapped)
                {
                    mappedPtr = (IntPtr)((byte*)memoryBlock.BaseMappedPointer + mapOffset);
                }
                else
                {
                    void* ret;
                    VkResult result = vkMapMemory(
                        _device, memoryBlock.DeviceMemory, bindOffset, bindSize, 0, &ret);
                    if (result != VkResult.VK_ERROR_MEMORY_MAP_FAILED)
                    {
                        CheckResult(result);
                    }
                    else
                    {
                        ThrowMapFailedException(resource, subresource);
                    }
                    mappedPtr = (IntPtr)((byte*)ret + (mapOffset - bindOffset));
                }
            }

            return new MappedResource(
                resource,
                mode,
                mappedPtr,
                offsetInBytes,
                sizeInBytes,
                subresource,
                rowPitch,
                depthPitch);
        }

        private protected override void UnmapCore(MappableResource resource, uint subresource)
        {
            VkMemoryBlock memoryBlock;
            if (resource is VkBuffer buffer)
            {
                memoryBlock = buffer.Memory;
            }
            else
            {
                VkTexture tex = Util.AssertSubtype<MappableResource, VkTexture>(resource);
                memoryBlock = tex.Memory;
            }

            if (memoryBlock.DeviceMemory != VkDeviceMemory.NULL && !memoryBlock.IsPersistentMapped)
            {
                vkUnmapMemory(_device, memoryBlock.DeviceMemory);
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            Debug.Assert(_submittedFences.Count == 0);
            foreach (VulkanFence fence in _availableSubmissionFences)
            {
                vkDestroyFence(_device, fence, null);
            }

            MainSwapchain?.Dispose();

            if (_debugCallbackHandle != VkDebugReportCallbackEXT.NULL)
            {
                IntPtr destroyCbFnPtr = GetInstanceProcAddr("vkDestroyDebugReportCallbackEXT");
                ((delegate* unmanaged<VkInstance, VkDebugReportCallbackEXT, VkAllocationCallbacks*, void>)destroyCbFnPtr)(
                    _instance, _debugCallbackHandle, null);

                _debugCallbackHandle = VkDebugReportCallbackEXT.NULL;
            }

            _descriptorPoolManager.DestroyAll();
            vkDestroyCommandPool(_device, _graphicsCommandPool, null);

            lock (_availableStagingTextures)
            {
                foreach (VkTexture tex in _availableStagingTextures)
                {
                    tex.Dispose();
                }
            }

            lock (_availableStagingBuffers)
            {
                foreach (VkBuffer buffer in _availableStagingBuffers)
                {
                    buffer.Dispose();
                }
            }

            lock (_sharedCommandLists)
            {
                while (_sharedCommandLists.TryPop(out VkCommandList? cl))
                {
                    cl.Dispose();
                }
            }

            _memoryManager.Dispose();

            VkResult result = vkDeviceWaitIdle(_device);
            CheckResult(result);
            vkDestroyDevice(_device, null);
            vkDestroyInstance(_instance, null);
        }

        private protected override void WaitForIdleCore()
        {
            lock (_graphicsQueueLock)
            {
                vkQueueWaitIdle(_graphicsQueue);
            }

            CheckSubmittedFences();
        }

        public override TextureSampleCount GetSampleCountLimit(PixelFormat format, bool depthFormat)
        {
            VkImageUsageFlags usageFlags = VkImageUsageFlags.VK_IMAGE_USAGE_SAMPLED_BIT;
            usageFlags |= depthFormat
                ? VkImageUsageFlags.VK_IMAGE_USAGE_DEPTH_STENCIL_ATTACHMENT_BIT
                : VkImageUsageFlags.VK_IMAGE_USAGE_COLOR_ATTACHMENT_BIT;

            VkImageFormatProperties formatProperties;
            vkGetPhysicalDeviceImageFormatProperties(
                _physicalDevice,
                VkFormats.VdToVkPixelFormat(format, depthFormat ? TextureUsage.DepthStencil : default),
                VkImageType.VK_IMAGE_TYPE_2D,
                VkImageTiling.VK_IMAGE_TILING_OPTIMAL,
                usageFlags,
                0,
                &formatProperties);

            VkSampleCountFlags vkSampleCounts = formatProperties.sampleCounts;
            if ((vkSampleCounts & VkSampleCountFlags.VK_SAMPLE_COUNT_64_BIT) == VkSampleCountFlags.VK_SAMPLE_COUNT_64_BIT)
            {
                return TextureSampleCount.Count64;
            }
            else if ((vkSampleCounts & VkSampleCountFlags.VK_SAMPLE_COUNT_32_BIT) == VkSampleCountFlags.VK_SAMPLE_COUNT_32_BIT)
            {
                return TextureSampleCount.Count32;
            }
            else if ((vkSampleCounts & VkSampleCountFlags.VK_SAMPLE_COUNT_16_BIT) == VkSampleCountFlags.VK_SAMPLE_COUNT_16_BIT)
            {
                return TextureSampleCount.Count16;
            }
            else if ((vkSampleCounts & VkSampleCountFlags.VK_SAMPLE_COUNT_8_BIT) == VkSampleCountFlags.VK_SAMPLE_COUNT_8_BIT)
            {
                return TextureSampleCount.Count8;
            }
            else if ((vkSampleCounts & VkSampleCountFlags.VK_SAMPLE_COUNT_4_BIT) == VkSampleCountFlags.VK_SAMPLE_COUNT_4_BIT)
            {
                return TextureSampleCount.Count4;
            }
            else if ((vkSampleCounts & VkSampleCountFlags.VK_SAMPLE_COUNT_2_BIT) == VkSampleCountFlags.VK_SAMPLE_COUNT_2_BIT)
            {
                return TextureSampleCount.Count2;
            }
            return TextureSampleCount.Count1;
        }

        private protected override bool GetPixelFormatSupportCore(
            PixelFormat format,
            TextureType type,
            TextureUsage usage,
            out PixelFormatProperties properties)
        {
            VkFormat vkFormat = VkFormats.VdToVkPixelFormat(format, usage);
            VkImageType vkType = VkFormats.VdToVkTextureType(type);
            VkImageTiling tiling = usage == TextureUsage.Staging
                ? VkImageTiling.VK_IMAGE_TILING_LINEAR
                : VkImageTiling.VK_IMAGE_TILING_OPTIMAL;
            VkImageUsageFlags vkUsage = VkFormats.VdToVkTextureUsage(usage);

            VkImageFormatProperties vkProps;
            VkResult result = vkGetPhysicalDeviceImageFormatProperties(
                _physicalDevice,
                vkFormat,
                vkType,
                tiling,
                vkUsage,
                0,
                &vkProps);

            if (result == VkResult.VK_ERROR_FORMAT_NOT_SUPPORTED)
            {
                properties = default;
                return false;
            }
            CheckResult(result);

            properties = new PixelFormatProperties(
               vkProps.maxExtent.width,
               vkProps.maxExtent.height,
               vkProps.maxExtent.depth,
               vkProps.maxMipLevels,
               vkProps.maxArrayLayers,
               (uint)vkProps.sampleCounts);
            return true;
        }

        internal VkFilter GetFormatFilter(VkFormat format)
        {
            if (!_filters.TryGetValue(format, out VkFilter filter))
            {
                VkFormatProperties vkFormatProps;
                vkGetPhysicalDeviceFormatProperties(_physicalDevice, format, &vkFormatProps);
                filter = (vkFormatProps.optimalTilingFeatures & VkFormatFeatureFlags.VK_FORMAT_FEATURE_SAMPLED_IMAGE_FILTER_LINEAR_BIT) != 0
                    ? VkFilter.VK_FILTER_LINEAR
                    : VkFilter.VK_FILTER_NEAREST;
                _filters.TryAdd(format, filter);
            }

            return filter;
        }

        private protected override void UpdateBufferCore(DeviceBuffer buffer, uint bufferOffsetInBytes, IntPtr source, uint sizeInBytes)
        {
            VkBuffer vkBuffer = Util.AssertSubtype<DeviceBuffer, VkBuffer>(buffer);
            VkBuffer? copySrcVkBuffer = null;
            IntPtr mappedPtr;
            byte* destPtr;
            bool isPersistentMapped = vkBuffer.Memory.IsPersistentMapped;
            if (isPersistentMapped)
            {
                mappedPtr = (IntPtr)vkBuffer.Memory.BlockMappedPointer;
                destPtr = (byte*)mappedPtr + bufferOffsetInBytes;
            }
            else
            {
                copySrcVkBuffer = GetPooledStagingBuffer(sizeInBytes);
                copySrcVkBuffer.Name = "Staging Buffer (GraphicsDevice)";
                mappedPtr = (IntPtr)copySrcVkBuffer.Memory.BlockMappedPointer;
                destPtr = (byte*)mappedPtr;
            }

            Unsafe.CopyBlock(destPtr, source.ToPointer(), sizeInBytes);

            if (copySrcVkBuffer != null)
            {
                VkCommandList cl = GetAndBeginCommandList();
                cl.AddStagingResource(copySrcVkBuffer);
                cl.CopyBuffer(copySrcVkBuffer, 0, vkBuffer, bufferOffsetInBytes, sizeInBytes);
                EndAndSubmitCommands(cl);
            }
        }

        internal VkCommandList GetAndBeginCommandList()
        {
            VkCommandList? sharedList;
            lock (_sharedCommandLists)
            {
                _sharedCommandLists.TryPop(out sharedList);
            }

            if (sharedList == null)
            {
                CommandListDescription desc = new()
                {
                    Transient = true
                };
                sharedList = (VkCommandList)ResourceFactory.CreateCommandList(desc);
            }

            sharedList.Begin();
            sharedList.Name = "Shared CommandList (GraphicsDevice)";
            return sharedList;
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
            uint arrayLayer)
        {
            VkTexture vkTex = Util.AssertSubtype<Texture, VkTexture>(texture);
            bool isStaging = (vkTex.Usage & TextureUsage.Staging) != 0;
            if (isStaging)
            {
                VkSubresourceLayout layout = vkTex.GetSubresourceLayout(mipLevel, arrayLayer);
                byte* imageBasePtr = (byte*)vkTex.Memory.BlockMappedPointer + layout.offset;

                uint srcRowPitch = FormatHelpers.GetRowPitch(width, texture.Format);
                uint srcDepthPitch = FormatHelpers.GetDepthPitch(srcRowPitch, height, texture.Format);
                Util.CopyTextureRegion(
                    source.ToPointer(),
                    0, 0, 0,
                    srcRowPitch, srcDepthPitch,
                    imageBasePtr,
                    x, y, z,
                    (uint)layout.rowPitch, (uint)layout.depthPitch,
                    width, height, depth,
                    texture.Format);
            }
            else
            {
                VkTexture stagingTex = GetPooledStagingTexture(width, height, depth, texture.Format);
                stagingTex.Name = "Staging Texture (GraphicsDevice)";
                UpdateTexture(stagingTex, source, sizeInBytes, 0, 0, 0, width, height, depth, 0, 0);

                VkCommandList cl = GetAndBeginCommandList();
                cl.AddStagingResource(stagingTex);
                cl.CopyTexture(
                    stagingTex, 0, 0, 0, 0, 0,
                    texture, x, y, z, mipLevel, arrayLayer,
                    width, height, depth, 1);
                EndAndSubmitCommands(cl);
            }
        }

        internal VkTexture GetPooledStagingTexture(uint width, uint height, uint depth, PixelFormat format)
        {
            uint totalSize = FormatHelpers.GetRegionSize(width, height, depth, format);
            lock (_availableStagingTextures)
            {
                for (int i = 0; i < _availableStagingTextures.Count; i++)
                {
                    VkTexture tex = _availableStagingTextures[i];
                    if (tex.Memory.Size >= totalSize)
                    {
                        _availableStagingTextures.RemoveAt(i);
                        tex.SetStagingDimensions(width, height, depth, format);
                        return tex;
                    }
                }
            }

            uint texWidth = Math.Max(256, width);
            uint texHeight = Math.Max(256, height);
            VkTexture newTex = (VkTexture)ResourceFactory.CreateTexture(TextureDescription.Texture3D(
                texWidth, texHeight, depth, 1, format, TextureUsage.Staging));
            newTex.SetStagingDimensions(width, height, depth, format);
            return newTex;
        }

        internal VkBuffer GetPooledStagingBuffer(uint size)
        {
            lock (_availableStagingBuffers)
            {
                for (int i = 0; i < _availableStagingBuffers.Count; i++)
                {
                    VkBuffer buffer = _availableStagingBuffers[i];
                    if (buffer.SizeInBytes >= size)
                    {
                        _availableStagingBuffers.RemoveAt(i);
                        return buffer;
                    }
                }
            }

            uint newBufferSize = Math.Max(MinStagingBufferSize, size);
            VkBuffer newBuffer = (VkBuffer)ResourceFactory.CreateBuffer(
                new BufferDescription(newBufferSize, BufferUsage.StagingWrite));
            return newBuffer;
        }

        internal void ReturnPooledStagingBuffers(ReadOnlySpan<VkBuffer> buffers)
        {
            lock (_availableStagingBuffers)
            {
                foreach (VkBuffer buffer in buffers)
                {
                    _availableStagingBuffers.Add(buffer);
                }
            }
        }

        internal void ReturnPooledStagingTextures(ReadOnlySpan<VkTexture> textures)
        {
            lock (_availableStagingTextures)
            {
                foreach (VkTexture texture in textures)
                {
                    _availableStagingTextures.Add(texture);
                }
            }
        }

        public override void ResetFence(Fence fence)
        {
            VulkanFence vkFence = Util.AssertSubtype<Fence, VkFence>(fence).DeviceFence;
            vkResetFences(_device, 1, &vkFence);
        }

        public override bool WaitForFence(Fence fence, ulong nanosecondTimeout)
        {
            VulkanFence vkFence = Util.AssertSubtype<Fence, VkFence>(fence).DeviceFence;
            VkResult result = vkWaitForFences(_device, 1, &vkFence, (VkBool32)true, nanosecondTimeout);
            return result == VkResult.VK_SUCCESS;
        }

        public override bool WaitForFences(Fence[] fences, bool waitAll, ulong nanosecondTimeout)
        {
            int fenceCount = fences.Length;
            VulkanFence* fencesPtr = stackalloc VulkanFence[fenceCount];
            for (int i = 0; i < fenceCount; i++)
            {
                fencesPtr[i] = Util.AssertSubtype<Fence, VkFence>(fences[i]).DeviceFence;
            }

            VkResult result = vkWaitForFences(_device, (uint)fenceCount, fencesPtr, (VkBool32)waitAll, nanosecondTimeout);
            return result == VkResult.VK_SUCCESS;
        }

        internal static bool IsSupported()
        {
            return s_isSupported.Value;
        }

        private static bool CheckIsSupported()
        {
            VkApplicationInfo applicationInfo = new()
            {
                sType = VK_STRUCTURE_TYPE_APPLICATION_INFO,
                apiVersion = new VkVersion(1, 0, 0),
                applicationVersion = new VkVersion(1, 0, 0),
                engineVersion = new VkVersion(1, 0, 0),
                pApplicationName = s_name,
                pEngineName = s_name
            };

            VkInstanceCreateInfo instanceCI = new()
            {
                sType = VK_STRUCTURE_TYPE_INSTANCE_CREATE_INFO,
                pApplicationInfo = &applicationInfo
            };

            VkInstance testInstance;
            VkResult result = vkCreateInstance(&instanceCI, null, &testInstance);
            if (result != VkResult.VK_SUCCESS)
            {
                return false;
            }

            uint physicalDeviceCount = 0;
            result = vkEnumeratePhysicalDevices(testInstance, &physicalDeviceCount, null);
            if (result != VkResult.VK_SUCCESS || physicalDeviceCount == 0)
            {
                vkDestroyInstance(testInstance, null);
                return false;
            }

            vkDestroyInstance(testInstance, null);

            HashSet<string> instanceExtensions = new(EnumerateInstanceExtensions());
            if (!instanceExtensions.Contains(CommonStrings.VK_KHR_SURFACE_EXTENSION_NAME))
            {
                return false;
            }

            foreach (FixedUtf8String surfaceExtension in GetSurfaceExtensions(instanceExtensions))
            {
                if (instanceExtensions.Contains(surfaceExtension))
                {
                    return true;
                }
            }

            return false;
        }

        private struct FenceSubmissionInfo
        {
            public VulkanFence Fence;
            public VkCommandList CommandList;
            public VkCommandBuffer CommandBuffer;
            public bool IsPooled;

            public FenceSubmissionInfo(VulkanFence fence, VkCommandList commandList, VkCommandBuffer commandBuffer, bool isPooled)
            {
                Fence = fence;
                CommandList = commandList;
                CommandBuffer = commandBuffer;
                IsPooled = isPooled;
            }
        }
    }

    internal unsafe delegate VkResult vkCreateDebugReportCallbackEXT_d(
        VkInstance instance,
        VkDebugReportCallbackCreateInfoEXT* createInfo,
        IntPtr allocatorPtr,
        VkDebugReportCallbackEXT* ret);

    internal unsafe delegate VkResult vkEnumerateInstanceVersion_t(uint* pApiVersion);

    internal unsafe delegate VkResult vkDebugMarkerSetObjectNameEXT_t(VkDevice device, VkDebugMarkerObjectNameInfoEXT* pNameInfo);
    internal unsafe delegate void vkCmdDebugMarkerBeginEXT_t(VkCommandBuffer commandBuffer, VkDebugMarkerMarkerInfoEXT* pMarkerInfo);
    internal unsafe delegate void vkCmdDebugMarkerEndEXT_t(VkCommandBuffer commandBuffer);
    internal unsafe delegate void vkCmdDebugMarkerInsertEXT_t(VkCommandBuffer commandBuffer, VkDebugMarkerMarkerInfoEXT* pMarkerInfo);

    internal unsafe delegate void vkGetBufferMemoryRequirements2_t(VkDevice device, VkBufferMemoryRequirementsInfo2* pInfo, VkMemoryRequirements2* pMemoryRequirements);
    internal unsafe delegate void vkGetImageMemoryRequirements2_t(VkDevice device, VkImageMemoryRequirementsInfo2* pInfo, VkMemoryRequirements2* pMemoryRequirements);

    internal unsafe delegate void vkGetPhysicalDeviceProperties2_t(VkPhysicalDevice physicalDevice, void* properties);
}
