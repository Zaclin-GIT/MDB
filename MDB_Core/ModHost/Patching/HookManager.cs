// ==============================
// HookManager - Native Method Hooking
// ==============================
// Manages native function hooks via MinHook through the bridge

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace GameSDK.ModHost.Patching
{
    /// <summary>
    /// Information about an installed hook.
    /// </summary>
    public class HookInfo
    {
        /// <summary>
        /// Unique handle for this hook.
        /// </summary>
        public long Handle { get; internal set; }

        /// <summary>
        /// The target method pointer.
        /// </summary>
        public IntPtr Target { get; internal set; }

        /// <summary>
        /// Pointer to call the original method.
        /// </summary>
        public IntPtr Original { get; internal set; }

        /// <summary>
        /// Whether the hook is currently enabled.
        /// </summary>
        public bool Enabled { get; internal set; }

        /// <summary>
        /// Description of the hook for logging.
        /// </summary>
        public string Description { get; internal set; }
    }

    /// <summary>
    /// Delegate for hook detour functions.
    /// </summary>
    /// <param name="instance">The object instance (IntPtr.Zero for static)</param>
    /// <param name="args">Pointer to argument array</param>
    /// <param name="original">Pointer to original function trampoline</param>
    /// <returns>Return value pointer</returns>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate IntPtr HookCallback(IntPtr instance, IntPtr args, IntPtr original);

    /// <summary>
    /// Manages native method hooks using MinHook via the bridge.
    /// </summary>
    public static class HookManager
    {
        private static readonly Dictionary<long, HookInfo> _hooks = new Dictionary<long, HookInfo>();
        private static readonly Dictionary<long, GCHandle> _delegateHandles = new Dictionary<long, GCHandle>();
        private static readonly ModLogger _logger = new ModLogger("HookManager");
        private static readonly object _lock = new object();

        /// <summary>
        /// Create a hook on an IL2CPP method.
        /// </summary>
        /// <param name="methodPtr">Pointer to the MethodInfo</param>
        /// <param name="detour">The detour delegate</param>
        /// <param name="original">Output: pointer to trampoline for calling original</param>
        /// <param name="description">Description for logging</param>
        /// <returns>HookInfo on success, null on failure</returns>
        public static HookInfo CreateHook(IntPtr methodPtr, HookCallback detour, out IntPtr original, string description = null)
        {
            original = IntPtr.Zero;

            if (methodPtr == IntPtr.Zero)
            {
                _logger.Error("CreateHook: methodPtr is null");
                return null;
            }

            lock (_lock)
            {
                // Pin the delegate so it doesn't get garbage collected
                GCHandle handle = GCHandle.Alloc(detour);
                IntPtr detourPtr = Marshal.GetFunctionPointerForDelegate(detour);

                long result = Il2CppBridge.mdb_create_hook(methodPtr, detourPtr, out original);
                if (result <= 0)
                {
                    handle.Free();
                    _logger.Error($"CreateHook failed: {Il2CppBridge.GetLastError()}");
                    return null;
                }

                HookInfo info = new HookInfo
                {
                    Handle = result,
                    Target = methodPtr,
                    Original = original,
                    Enabled = true,
                    Description = description ?? $"Hook_{result}"
                };

                _hooks[result] = info;
                _delegateHandles[result] = handle;

                _logger.Info($"Created hook: {info.Description} (handle={result})");
                return info;
            }
        }

        /// <summary>
        /// Create a hook on a method by RVA.
        /// </summary>
        /// <param name="rva">The RVA offset of the method</param>
        /// <param name="detour">The detour delegate</param>
        /// <param name="original">Output: pointer to trampoline for calling original</param>
        /// <param name="description">Description for logging</param>
        /// <returns>HookInfo on success, null on failure</returns>
        public static HookInfo CreateHookByRva(ulong rva, HookCallback detour, out IntPtr original, string description = null)
        {
            original = IntPtr.Zero;

            lock (_lock)
            {
                GCHandle handle = GCHandle.Alloc(detour);
                IntPtr detourPtr = Marshal.GetFunctionPointerForDelegate(detour);

                long result = Il2CppBridge.mdb_create_hook_rva(rva, detourPtr, out original);
                if (result <= 0)
                {
                    handle.Free();
                    _logger.Error($"CreateHookByRva failed: {Il2CppBridge.GetLastError()}");
                    return null;
                }

                HookInfo info = new HookInfo
                {
                    Handle = result,
                    Target = Il2CppBridge.mdb_get_method_pointer_from_rva(rva),
                    Original = original,
                    Enabled = true,
                    Description = description ?? $"RvaHook_0x{rva:X}"
                };

                _hooks[result] = info;
                _delegateHandles[result] = handle;

                _logger.Info($"Created hook: {info.Description} (handle={result})");
                return info;
            }
        }

        /// <summary>
        /// Create a hook on a raw function pointer.
        /// </summary>
        /// <param name="target">The target function pointer</param>
        /// <param name="detour">The detour delegate</param>
        /// <param name="original">Output: pointer to trampoline for calling original</param>
        /// <param name="description">Description for logging</param>
        /// <returns>HookInfo on success, null on failure</returns>
        public static HookInfo CreateHookByPtr(IntPtr target, HookCallback detour, out IntPtr original, string description = null)
        {
            original = IntPtr.Zero;

            if (target == IntPtr.Zero)
            {
                _logger.Error("CreateHookByPtr: target is null");
                return null;
            }

            lock (_lock)
            {
                GCHandle handle = GCHandle.Alloc(detour);
                IntPtr detourPtr = Marshal.GetFunctionPointerForDelegate(detour);

                long result = Il2CppBridge.mdb_create_hook_ptr(target, detourPtr, out original);
                if (result <= 0)
                {
                    handle.Free();
                    _logger.Error($"CreateHookByPtr failed: {Il2CppBridge.GetLastError()}");
                    return null;
                }

                HookInfo info = new HookInfo
                {
                    Handle = result,
                    Target = target,
                    Original = original,
                    Enabled = true,
                    Description = description ?? $"PtrHook_0x{target.ToInt64():X}"
                };

                _hooks[result] = info;
                _delegateHandles[result] = handle;

                _logger.Info($"Created hook: {info.Description} (handle={result})");
                return info;
            }
        }

        /// <summary>
        /// Remove a hook.
        /// </summary>
        /// <param name="hookHandle">The hook handle</param>
        /// <returns>True on success</returns>
        public static bool RemoveHook(long hookHandle)
        {
            lock (_lock)
            {
                if (!_hooks.ContainsKey(hookHandle))
                {
                    _logger.Warning($"RemoveHook: hook {hookHandle} not found");
                    return false;
                }

                int result = Il2CppBridge.mdb_remove_hook(hookHandle);
                if (result != 0)
                {
                    _logger.Error($"RemoveHook failed: {Il2CppBridge.GetLastError()}");
                    return false;
                }

                // Free the delegate handle
                if (_delegateHandles.TryGetValue(hookHandle, out GCHandle handle))
                {
                    handle.Free();
                    _delegateHandles.Remove(hookHandle);
                }

                string desc = _hooks[hookHandle].Description;
                _hooks.Remove(hookHandle);
                _logger.Info($"Removed hook: {desc}");
                return true;
            }
        }

        /// <summary>
        /// Remove a hook by HookInfo.
        /// </summary>
        public static bool RemoveHook(HookInfo hook)
        {
            return hook != null && RemoveHook(hook.Handle);
        }

        /// <summary>
        /// Enable or disable a hook.
        /// </summary>
        /// <param name="hookHandle">The hook handle</param>
        /// <param name="enabled">True to enable, false to disable</param>
        /// <returns>True on success</returns>
        public static bool SetHookEnabled(long hookHandle, bool enabled)
        {
            lock (_lock)
            {
                if (!_hooks.TryGetValue(hookHandle, out HookInfo info))
                {
                    _logger.Warning($"SetHookEnabled: hook {hookHandle} not found");
                    return false;
                }

                int result = Il2CppBridge.mdb_set_hook_enabled(hookHandle, enabled);
                if (result != 0)
                {
                    _logger.Error($"SetHookEnabled failed: {Il2CppBridge.GetLastError()}");
                    return false;
                }

                info.Enabled = enabled;
                _logger.Info($"Hook {info.Description} {(enabled ? "enabled" : "disabled")}");
                return true;
            }
        }

        /// <summary>
        /// Get all active hooks.
        /// </summary>
        public static IEnumerable<HookInfo> GetAllHooks()
        {
            lock (_lock)
            {
                return new List<HookInfo>(_hooks.Values);
            }
        }

        /// <summary>
        /// Remove all hooks.
        /// </summary>
        public static void RemoveAllHooks()
        {
            lock (_lock)
            {
                foreach (long handle in new List<long>(_hooks.Keys))
                {
                    RemoveHook(handle);
                }
            }
        }
    }
}
