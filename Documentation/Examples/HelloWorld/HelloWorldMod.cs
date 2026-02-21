// ==============================
// HelloWorld - Simple MDB Example Mod
// ==============================
// Demonstrates: ModBase lifecycle, Logger, ImGuiManager, basic ImGui widgets

using System;
using GameSDK;
using GameSDK.ModHost;
using GameSDK.ModHost.Patching;
using UnityEngine;

// Alias System.Numerics vector types for ImGui (avoids ambiguity with UnityEngine structs)
using Vector2 = System.Numerics.Vector2;
using Vector4 = System.Numerics.Vector4;

namespace MDB.Examples.HelloWorld
{
    /// <summary>
    /// A simple "Hello World" mod that demonstrates the core MDB framework APIs.
    /// 
    /// Features demonstrated:
    ///   - [Mod] attribute for metadata
    ///   - ModBase lifecycle methods (OnLoad, OnUpdate)
    ///   - Logger (Info, Warning, Error, Debug)
    ///   - ImGuiManager callback registration
    ///   - Basic ImGui widgets (window, text, button, checkbox, slider)
    /// </summary>
    [Mod("Examples.HelloWorld", "Hello World", "1.0.0",
        Author = "MDB Framework",
        Description = "A simple example mod showing basic framework usage.")]
    public class HelloWorldMod : ModBase
    {
        // ── State ──
        private int _frameCount;
        private int _callbackId;
        private bool _windowOpen = true;
        private bool _enableOverlay = true;
        private float _overlayOpacity = 0.8f;
        private int _clickCount;
        private string _userName = "Modder";

        // ── Lifecycle ──

        /// <summary>
        /// Called once when the mod is loaded.
        /// Use for initialization: register ImGui callbacks, find IL2CPP classes, etc.
        /// </summary>
        public override void OnLoad()
        {
            Logger.Info("Hello World mod loaded!");
            Logger.Info($"Mod info: {Info}");

            // Register an ImGui draw callback so we can render UI each frame.
            // ImGuiManager.Initialize() is called automatically if needed.
            _callbackId = ImGuiManager.RegisterCallback(
                "HelloWorld",           // Name shown in logs / debug
                DrawImGui,              // Our draw method
                ImGuiPriority.Normal    // Priority (Normal = 100)
            );

            if (_callbackId > 0)
                Logger.Info($"ImGui callback registered (id={_callbackId})");
            else
                Logger.Error("Failed to register ImGui callback!");

            // Log some environment info
            Logger.Info($"DirectX version: {ImGuiManager.DirectXVersion}");
            Logger.Info($"ImGui initialized: {ImGuiManager.IsInitialized}");
        }

        /// <summary>
        /// Called every frame during Unity's Update loop.
        /// Use for game logic that runs each frame.
        /// </summary>
        public override void OnUpdate()
        {
            _frameCount++;

            // Log every 600 frames (~10 seconds at 60fps) as a heartbeat
            if (_frameCount % 600 == 0)
            {
                Logger.Debug($"Heartbeat: {_frameCount} frames processed");
            }
        }

        // ── ImGui Rendering ──

        /// <summary>
        /// Called each frame by ImGuiManager to draw our UI.
        /// All ImGui calls must happen inside this callback.
        /// </summary>
        private void DrawImGui()
        {
            // Show a simple overlay in the corner when enabled
            if (_enableOverlay)
            {
                DrawOverlay();
            }

            // Main window with close button
            if (!_windowOpen) return;

            ImGui.SetNextWindowSize(new Vector2(400, 320), ImGuiCond.FirstUseEver);

            if (ImGui.Begin("Hello World Mod", ref _windowOpen, ImGuiWindowFlags.None))
            {
                // ── Section: Welcome ──
                ImGui.TextColored(new Vector4(0.4f, 0.8f, 1.0f, 1.0f), "Welcome to MDB!");
                ImGui.Separator();
                ImGui.Spacing();

                // Text input
                ImGui.InputText("Your Name", ref _userName);
                ImGui.Text($"Hello, {_userName}!");
                ImGui.Spacing();

                // Button with click counter
                if (ImGui.Button("Click Me!"))
                {
                    _clickCount++;
                    Logger.Info($"{_userName} clicked the button! (count: {_clickCount})");
                }
                ImGui.SameLine();
                ImGui.Text($"Clicks: {_clickCount}");
                ImGui.Spacing();

                // ── Section: Settings ──
                if (ImGui.CollapsingHeader("Settings", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGui.Checkbox("Show Overlay", ref _enableOverlay);
                    ImGui.SliderFloat("Overlay Opacity", ref _overlayOpacity, 0.1f, 1.0f);
                }

                // ── Section: Framework Info ──
                if (ImGui.CollapsingHeader("Framework Info"))
                {
                    ImGui.BulletText($"Frames: {_frameCount}");
                    ImGui.BulletText($"DirectX: {ImGuiManager.DirectXVersion}");
                    ImGui.BulletText($"ImGui Callbacks: {ImGuiManager.CallbackCount}");
                    ImGui.BulletText($"Input Enabled: {ImGuiManager.IsInputEnabled}");
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.TextDisabled("Press F2 to toggle ImGui input capture");
            }
            ImGui.End();
        }

        /// <summary>
        /// Draws a small overlay in the top-left corner.
        /// Demonstrates: overlay window flags, colored text, DrawList API.
        /// </summary>
        private void DrawOverlay()
        {
            ImGui.SetNextWindowPos(new Vector2(10, 10), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new Vector2(200, 60), ImGuiCond.FirstUseEver);

            // Use PushStyleColor to set window background opacity
            ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0, 0, 0, _overlayOpacity));

            ImGuiWindowFlags flags =
                ImGuiWindowFlags.NoTitleBar |
                ImGuiWindowFlags.NoResize |
                ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.AlwaysAutoResize |
                ImGuiWindowFlags.NoFocusOnAppearing |
                ImGuiWindowFlags.NoNav;

            if (ImGui.Begin("##HelloOverlay", flags))
            {
                ImGui.TextColored(new Vector4(0.3f, 1.0f, 0.3f, 1.0f), "MDB Active");
                ImGui.Text($"FPS: ~{(int)(1.0f / Math.Max(Time.deltaTime, 0.001f))}");
            }
            ImGui.End();

            ImGui.PopStyleColor();
        }
    }
}
