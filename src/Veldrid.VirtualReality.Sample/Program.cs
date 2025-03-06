using System;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using ImGuiNET;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Veldrid.NeoDemo;
using Veldrid.SDL2;
using Veldrid.StartupUtilities;

namespace Veldrid.VirtualReality.Sample;

internal static class Program
{
    const double MotionSpeed = 2.0;
    static Vector3 _userPosition;
    static bool _useOculus;
    static bool _switchVrContext;

    static void Main(string[] args)
    {
        GraphicsBackend backend = GraphicsBackend.Direct3D11;
        foreach (var arg in args)
        {
            switch (arg.ToUpperInvariant())
            {
                case "-VK":
                    backend = GraphicsBackend.Vulkan;
                    break;

                case "-GL":
                    backend = GraphicsBackend.OpenGL;
                    break;
            }
        }

        _useOculus = true;
        if (!VRContext.IsOculusSupported())
        {
            _useOculus = false;
            if (!VRContext.IsOpenVRSupported())
            {
                Console.WriteLine("This sample requires an Oculus or OpenVR-capable headset.");
                return;
            }
        }

        Sdl2Window window = VeldridStartup.CreateWindow(
            new(
                Sdl2Native.SDL_WINDOWPOS_CENTERED,
                Sdl2Native.SDL_WINDOWPOS_CENTERED,
                1280,
                720,
                WindowState.Normal,
                "Veldrid.VirtualReality Sample"
            )
        );

        VRContextOptions options = new() { EyeFramebufferSampleCount = TextureSampleCount.Count4 };
        VRContext vrContext = _useOculus
            ? VRContext.CreateOculus(options)
            : VRContext.CreateOpenVR(options);

        bool debug =
#if DEBUG
            backend != GraphicsBackend.Vulkan; // Oculus runtime causes validation errors.
#else
            false;
#endif

        GraphicsDeviceOptions gdo = new(
            debug,
            null,
            false,
            ResourceBindingModel.Improved,
            true,
            true,
            true
        );

        (GraphicsDevice gd, Swapchain sc) = CreateDeviceAndSwapchain(
            window,
            vrContext,
            backend,
            gdo
        );
        window.Resized += () => sc.Resize((uint)window.Width, (uint)window.Height);

        vrContext.Initialize(gd);

        ImGuiRenderer igr = new(
            gd,
            sc.Framebuffer.OutputDescription,
            window.Width,
            window.Height,
            ColorSpaceHandling.Linear
        );
        window.Resized += () => igr.WindowResized(window.Width, window.Height);

        AssimpMesh mesh = new(
            gd,
            vrContext.LeftEyeFramebuffer.OutputDescription,
            Path.Combine(AppContext.BaseDirectory, "cat", "cat.obj"),
            Path.Combine(AppContext.BaseDirectory, "cat", "cat_diff.png")
        );

        Skybox skybox = new(
            Image.Load<Rgba32>(Path.Combine(AppContext.BaseDirectory, "skybox", "miramar_ft.png")),
            Image.Load<Rgba32>(Path.Combine(AppContext.BaseDirectory, "skybox", "miramar_bk.png")),
            Image.Load<Rgba32>(Path.Combine(AppContext.BaseDirectory, "skybox", "miramar_lf.png")),
            Image.Load<Rgba32>(Path.Combine(AppContext.BaseDirectory, "skybox", "miramar_rt.png")),
            Image.Load<Rgba32>(Path.Combine(AppContext.BaseDirectory, "skybox", "miramar_up.png")),
            Image.Load<Rgba32>(Path.Combine(AppContext.BaseDirectory, "skybox", "miramar_dn.png"))
        );
        skybox.CreateDeviceObjects(gd, vrContext.LeftEyeFramebuffer.OutputDescription);

        CommandList windowCL = gd.ResourceFactory.CreateCommandList();
        CommandList eyesCL = gd.ResourceFactory.CreateCommandList();

        MirrorTextureEyeSource eyeSource = MirrorTextureEyeSource.BothEyes;

        Stopwatch sw = Stopwatch.StartNew();
        double lastFrameTime = sw.Elapsed.TotalSeconds;

        while (window.Exists)
        {
            double newFrameTime = sw.Elapsed.TotalSeconds;
            double deltaSeconds = newFrameTime - lastFrameTime;
            lastFrameTime = newFrameTime;

            InputSnapshot snapshot = window.PumpEvents();
            if (!window.Exists)
            {
                break;
            }
            InputTracker.UpdateFrameInput(snapshot, window);
            HandleInputs(deltaSeconds);

            igr.Update(1f / 60f, snapshot);

            if (ImGui.BeginMainMenuBar())
            {
                if (ImGui.BeginMenu("Settings"))
                {
                    if (ImGui.BeginMenu("Mirror Texture"))
                    {
                        if (
                            ImGui.MenuItem(
                                "Both Eyes",
                                null,
                                eyeSource == MirrorTextureEyeSource.BothEyes
                            )
                        )
                        {
                            eyeSource = MirrorTextureEyeSource.BothEyes;
                        }
                        if (
                            ImGui.MenuItem(
                                "Left Eye",
                                null,
                                eyeSource == MirrorTextureEyeSource.LeftEye
                            )
                        )
                        {
                            eyeSource = MirrorTextureEyeSource.LeftEye;
                        }
                        if (
                            ImGui.MenuItem(
                                "Right Eye",
                                null,
                                eyeSource == MirrorTextureEyeSource.RightEye
                            )
                        )
                        {
                            eyeSource = MirrorTextureEyeSource.RightEye;
                        }

                        ImGui.EndMenu();
                    }

                    if (ImGui.BeginMenu("VR API"))
                    {
                        if (ImGui.MenuItem("Oculus", null, _useOculus) && !_useOculus)
                        {
                            _useOculus = true;
                            _switchVrContext = true;
                        }
                        if (ImGui.MenuItem("OpenVR", null, !_useOculus) && _useOculus)
                        {
                            _useOculus = false;
                            _switchVrContext = true;
                        }

                        ImGui.EndMenu();
                    }

                    ImGui.EndMenu();
                }

                ImGui.EndMainMenuBar();
            }

            windowCL.Begin();
            windowCL.SetFramebuffer(sc.Framebuffer);
            windowCL.ClearColorTarget(0, new(0f, 0f, 0.2f, 1f));
            vrContext.RenderMirrorTexture(windowCL, sc.Framebuffer, eyeSource);
            igr.Render(gd, windowCL);
            windowCL.End();
            gd.SubmitCommands(windowCL);
            gd.SwapBuffers(sc);

            HmdPoseState poses = vrContext.WaitForPoses();

            // Render Eyes
            eyesCL.Begin();

            eyesCL.PushDebugGroup("Left Eye");
            Matrix4x4 leftView = poses.CreateView(
                VREye.Left,
                _userPosition,
                -Vector3.UnitZ,
                Vector3.UnitY
            );
            RenderEye(
                eyesCL,
                vrContext.LeftEyeFramebuffer,
                mesh,
                skybox,
                poses.LeftEyeProjection,
                leftView
            );
            eyesCL.PopDebugGroup();

            eyesCL.PushDebugGroup("Right Eye");
            Matrix4x4 rightView = poses.CreateView(
                VREye.Right,
                _userPosition,
                -Vector3.UnitZ,
                Vector3.UnitY
            );
            RenderEye(
                eyesCL,
                vrContext.RightEyeFramebuffer,
                mesh,
                skybox,
                poses.RightEyeProjection,
                rightView
            );
            eyesCL.PopDebugGroup();

            eyesCL.End();
            gd.SubmitCommands(eyesCL);

            vrContext.SubmitFrame();

            if (_switchVrContext)
            {
                _switchVrContext = false;
                vrContext.Dispose();
                vrContext = _useOculus ? VRContext.CreateOculus() : VRContext.CreateOpenVR();
                vrContext.Initialize(gd);
            }
        }

        vrContext.Dispose();
        gd.Dispose();
    }

    static void RenderEye(
        CommandList cl,
        Framebuffer fb,
        AssimpMesh mesh,
        Skybox skybox,
        Matrix4x4 proj,
        Matrix4x4 view
    )
    {
        cl.SetFramebuffer(fb);
        cl.ClearDepthStencil(1f);
        cl.ClearColorTarget(0, RgbaFloat.CornflowerBlue);

        mesh.Render(
            cl,
            new(proj, view, Matrix4x4.CreateScale(1f) * Matrix4x4.CreateTranslation(0f, -1, -2f))
        );

        mesh.Render(
            cl,
            new(
                proj,
                view,
                Matrix4x4.CreateScale(0.66f) * Matrix4x4.CreateTranslation(-0.5f, -1, -2f)
            )
        );

        mesh.Render(
            cl,
            new(
                proj,
                view,
                Matrix4x4.CreateScale(1.5f) * Matrix4x4.CreateTranslation(0.5f, -1, -2f)
            )
        );

        skybox.Render(cl, fb, proj, view);
    }

    static void HandleInputs(double deltaSeconds)
    {
        Vector3 motionDir = Vector3.Zero;

        if (InputTracker.GetKey(Key.W))
            motionDir += -Vector3.UnitZ;

        if (InputTracker.GetKey(Key.A))
            motionDir += -Vector3.UnitX;

        if (InputTracker.GetKey(Key.S))
            motionDir += Vector3.UnitZ;

        if (InputTracker.GetKey(Key.D))
            motionDir += Vector3.UnitX;

        if (InputTracker.GetKey(Key.Q))
            motionDir += -Vector3.UnitY;

        if (InputTracker.GetKey(Key.E))
            motionDir += Vector3.UnitY;

        if (motionDir != Vector3.Zero)
        {
            motionDir = Vector3.Normalize(motionDir);
            _userPosition += motionDir * (float)(deltaSeconds * MotionSpeed);
        }
    }

    static (GraphicsDevice gd, Swapchain sc) CreateDeviceAndSwapchain(
        Sdl2Window window,
        VRContext vrc,
        GraphicsBackend backend,
        GraphicsDeviceOptions gdo
    )
    {
        if (backend == GraphicsBackend.Vulkan)
        {
            (string[] instance, string[] device) = vrc.GetRequiredVulkanExtensions();
            VulkanDeviceOptions vdo = new(instance, device);
            GraphicsDevice gd = GraphicsDevice.CreateVulkan(gdo, vdo);
            Swapchain sc = gd.ResourceFactory.CreateSwapchain(
                new(
                    VeldridStartup.GetSwapchainSource(window),
                    (uint)window.Width,
                    (uint)window.Height,
                    gdo.SwapchainDepthFormat,
                    gdo.SyncToVerticalBlank,
                    true
                )
            );
            return (gd, sc);
        }
        else
        {
            GraphicsDevice gd = VeldridStartup.CreateGraphicsDevice(window, gdo, backend);
            Swapchain sc = gd.MainSwapchain!;
            return (gd, sc);
        }
    }
}
