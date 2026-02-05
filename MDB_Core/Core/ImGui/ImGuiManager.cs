// ==============================
// ImGuiManager - Simplified ImGui Integration for MDB
// ==============================
// Provides a centralized, simplified API for mods to use ImGui.
// Handles initialization, multi-mod support, and callback management automatically.

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace GameSDK
{
    /// <summary>
    /// DirectX version detected by the bridge.
    /// </summary>
    public enum DxVersion : int
    {
        Unknown = 0,
        DX11 = 11,
        DX12 = 12
    }

    /// <summary>
    /// Priority levels for ImGui draw callbacks.
    /// Higher priority callbacks are called first.
    /// </summary>
    public static class ImGuiPriority
    {
        public const int Background = -100;
        public const int Low = 0;
        public const int Normal = 100;
        public const int High = 200;
        public const int Overlay = 300;
    }

    /// <summary>
    /// Delegate for ImGui draw callbacks.
    /// </summary>
    public delegate void ImGuiDrawCallback();

    /// <summary>
    /// Centralized manager for ImGui integration.
    /// Handles initialization and multi-mod callback support.
    /// </summary>
    public static class ImGuiManager
    {
        private const string DllName = "MDB_Bridge.dll";

        #region Native P/Invoke

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern DxVersion mdb_imgui_get_dx_version();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool mdb_imgui_init();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void mdb_imgui_shutdown();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool mdb_imgui_is_initialized();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void mdb_imgui_set_input_enabled([MarshalAs(UnmanagedType.I1)] bool enabled);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool mdb_imgui_is_input_enabled();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void mdb_imgui_set_toggle_key(int vkCode);

        // Multi-callback API
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int mdb_imgui_add_callback(string name, IntPtr callback, int priority);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool mdb_imgui_remove_callback(int callbackId);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool mdb_imgui_set_callback_enabled(int callbackId, [MarshalAs(UnmanagedType.I1)] bool enabled);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int mdb_imgui_get_callback_count();

        // Native callback delegate (must match C++ signature)
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void NativeDrawCallback();

        #endregion

        #region Callback Management

        // Track registered callbacks to prevent GC
        private class CallbackInfo
        {
            public int NativeId;
            public string Name;
            public ImGuiDrawCallback ManagedCallback;
            public NativeDrawCallback NativeCallback;
            public GCHandle Handle;
            public int Priority;
            public bool Enabled;
        }

        private static readonly Dictionary<int, CallbackInfo> _callbacks = new Dictionary<int, CallbackInfo>();
        private static readonly object _lock = new object();
        private static int _nextManagedId = 1;
        private static bool _initialized = false;

        #endregion

        #region Properties

        /// <summary>
        /// Whether ImGui has been initialized.
        /// </summary>
        public static bool IsInitialized
        {
            get
            {
                try { return mdb_imgui_is_initialized(); }
                catch { return false; }
            }
        }

        /// <summary>
        /// The detected DirectX version.
        /// </summary>
        public static DxVersion DirectXVersion
        {
            get
            {
                try { return mdb_imgui_get_dx_version(); }
                catch { return DxVersion.Unknown; }
            }
        }

        /// <summary>
        /// Whether ImGui input capture is currently enabled.
        /// </summary>
        public static bool IsInputEnabled
        {
            get
            {
                try { return mdb_imgui_is_input_enabled(); }
                catch { return false; }
            }
            set
            {
                try { mdb_imgui_set_input_enabled(value); }
                catch { }
            }
        }

        /// <summary>
        /// Number of registered callbacks.
        /// </summary>
        public static int CallbackCount
        {
            get
            {
                try { return mdb_imgui_get_callback_count(); }
                catch { return 0; }
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Initialize ImGui if not already initialized.
        /// This is called automatically when registering a callback, but can be called manually.
        /// </summary>
        /// <returns>True if initialization succeeded or was already done</returns>
        public static bool Initialize()
        {
            if (_initialized && IsInitialized)
                return true;

            try
            {
                bool result = mdb_imgui_init();
                _initialized = result;
                return result;
            }
            catch (Exception ex)
            {
                ModHost.ModLogger.LogInternal("ImGuiManager", $"[ERROR] Initialization failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Register a draw callback. ImGui will be initialized automatically if needed.
        /// </summary>
        /// <param name="name">Name for this callback (shown in debug/logs)</param>
        /// <param name="callback">The draw callback to invoke each frame</param>
        /// <param name="priority">Priority (higher = called first). Use ImGuiPriority constants.</param>
        /// <returns>Callback ID that can be used to unregister, or 0 on failure</returns>
        public static int RegisterCallback(string name, ImGuiDrawCallback callback, int priority = ImGuiPriority.Normal)
        {
            if (callback == null)
                return 0;

            // Ensure ImGui is initialized
            if (!Initialize())
            {
                ModHost.ModLogger.LogInternal("ImGuiManager", "[ERROR] Cannot register callback - ImGui not initialized");
                return 0;
            }

            lock (_lock)
            {
                try
                {
                    int managedId = _nextManagedId++;

                    // Create native callback wrapper
                    var info = new CallbackInfo
                    {
                        Name = name ?? $"Callback_{managedId}",
                        ManagedCallback = callback,
                        Priority = priority,
                        Enabled = true
                    };

                    // Create the native delegate
                    info.NativeCallback = () =>
                    {
                        try
                        {
                            if (info.Enabled)
                                info.ManagedCallback?.Invoke();
                        }
                        catch (Exception ex)
                        {
                            ModHost.ModLogger.LogInternal("ImGuiManager", $"[ERROR] Callback '{info.Name}' threw: {ex.Message}");
                        }
                    };

                    // Pin the delegate to prevent GC
                    info.Handle = GCHandle.Alloc(info.NativeCallback);
                    IntPtr ptr = Marshal.GetFunctionPointerForDelegate(info.NativeCallback);

                    // Register with native side
                    info.NativeId = mdb_imgui_add_callback(info.Name, ptr, priority);
                    if (info.NativeId <= 0)
                    {
                        info.Handle.Free();
                        ModHost.ModLogger.LogInternal("ImGuiManager", $"[ERROR] Failed to register native callback for '{name}'");
                        return 0;
                    }

                    _callbacks[managedId] = info;

                    ModHost.ModLogger.LogInternal("ImGuiManager", $"[INFO] Registered callback '{info.Name}' (id={managedId}, priority={priority})");
                    return managedId;
                }
                catch (Exception ex)
                {
                    ModHost.ModLogger.LogInternal("ImGuiManager", $"[ERROR] Failed to register callback: {ex.Message}");
                    return 0;
                }
            }
        }

        /// <summary>
        /// Unregister a previously registered callback.
        /// </summary>
        /// <param name="callbackId">The callback ID returned by RegisterCallback</param>
        /// <returns>True if successfully unregistered</returns>
        public static bool UnregisterCallback(int callbackId)
        {
            lock (_lock)
            {
                if (!_callbacks.TryGetValue(callbackId, out var info))
                    return false;

                try
                {
                    // Remove from native side
                    mdb_imgui_remove_callback(info.NativeId);

                    // Free the pinned delegate
                    if (info.Handle.IsAllocated)
                        info.Handle.Free();

                    _callbacks.Remove(callbackId);

                    ModHost.ModLogger.LogInternal("ImGuiManager", $"[INFO] Unregistered callback '{info.Name}'");
                    return true;
                }
                catch (Exception ex)
                {
                    ModHost.ModLogger.LogInternal("ImGuiManager", $"[ERROR] Failed to unregister callback: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// Enable or disable a callback without unregistering it.
        /// </summary>
        public static bool SetCallbackEnabled(int callbackId, bool enabled)
        {
            lock (_lock)
            {
                if (!_callbacks.TryGetValue(callbackId, out var info))
                    return false;

                info.Enabled = enabled;
                mdb_imgui_set_callback_enabled(info.NativeId, enabled);
                return true;
            }
        }

        /// <summary>
        /// Set the key used to toggle ImGui input capture.
        /// Default is F2 (VK_F2 = 0x71 = 113).
        /// </summary>
        public static void SetToggleKey(int virtualKeyCode)
        {
            try { mdb_imgui_set_toggle_key(virtualKeyCode); }
            catch { }
        }

        /// <summary>
        /// Shutdown ImGui and cleanup all callbacks.
        /// </summary>
        public static void Shutdown()
        {
            lock (_lock)
            {
                // Free all pinned delegates
                foreach (var kvp in _callbacks)
                {
                    if (kvp.Value.Handle.IsAllocated)
                        kvp.Value.Handle.Free();
                }
                _callbacks.Clear();
            }

            try
            {
                mdb_imgui_shutdown();
            }
            catch { }

            _initialized = false;
        }

        #endregion
    }
}
