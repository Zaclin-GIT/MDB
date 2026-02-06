// ==============================
// PatchDebugger - Debugging Utilities
// ==============================
// Hook debugging, diagnostics, and logging utilities

using System;

namespace GameSDK.ModHost.Patching
{
    /// <summary>
    /// Provides debugging utilities for patch operations.
    /// </summary>
    public static partial class PatchProcessor
    {
        /// <summary>
        /// Enable or disable verbose hook debugging.
        /// When enabled, hooks will log detailed information about signatures and trampolines.
        /// </summary>
        public static void SetDebugEnabled(bool enabled)
        {
            _debugEnabled = enabled;
            Il2CppBridge.mdb_hook_set_debug_enabled(enabled);
            _logger.Info($"Hook debugging {(enabled ? "ENABLED" : "DISABLED")}");
        }

        /// <summary>
        /// Check if hook debugging is enabled.
        /// </summary>
        public static bool IsDebugEnabled => _debugEnabled;

        /// <summary>
        /// Dump all active hooks to the debug log.
        /// Useful for diagnosing hook issues.
        /// </summary>
        public static void DumpAllHooks()
        {
            _logger.Info($"Dumping {Il2CppBridge.mdb_hook_get_count()} active hooks...");
            Il2CppBridge.mdb_hook_dump_all();
        }

        /// <summary>
        /// Enable a hook by its handle.
        /// </summary>
        public static bool EnableHook(long hookHandle)
        {
            if (!_hookToPatch.TryGetValue(hookHandle, out PatchInfo patch))
                return false;
                
            int result = Il2CppBridge.mdb_set_hook_enabled(hookHandle, true);
            if (result == 0)
            {
                string target = GetPatchTargetName(patch);
                _logger.Info($"+ {target}", ConsoleColor.Blue);
                return true;
            }
            return false;
        }
        
        /// <summary>
        /// Disable a hook by its handle.
        /// </summary>
        public static bool DisableHook(long hookHandle)
        {
            if (!_hookToPatch.TryGetValue(hookHandle, out PatchInfo patch))
                return false;
                
            int result = Il2CppBridge.mdb_set_hook_enabled(hookHandle, false);
            if (result == 0)
            {
                string target = GetPatchTargetName(patch);
                _logger.Info($"- {target}", ConsoleColor.DarkGray);
                return true;
            }
            return false;
        }
        
        /// <summary>
        /// Enable or disable a hook by its handle.
        /// </summary>
        public static bool SetHookEnabled(long hookHandle, bool enabled)
        {
            return enabled ? EnableHook(hookHandle) : DisableHook(hookHandle);
        }
        
        /// <summary>
        /// Enable or disable all hooks for a patch class type.
        /// </summary>
        public static bool SetHookEnabled(Type patchClass, bool enabled)
        {
            if (!_patchesByClass.TryGetValue(patchClass, out System.Collections.Generic.List<PatchInfo> patches))
                return false;
                
            bool allSuccess = true;
            foreach (var patch in patches)
            {
                if (patch.HookHandle > 0)
                {
                    if (!SetHookEnabled(patch.HookHandle, enabled))
                        allSuccess = false;
                }
            }
            return allSuccess;
        }
    }
}
