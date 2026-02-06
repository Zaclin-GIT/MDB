// ==============================
// UnityDebugInterceptor - Captures Unity Debug.Log calls
// ==============================

using System;
using GameSDK.ModHost;
using GameSDK.ModHost.Patching;

namespace MDB.Mods.UnityDebugInterceptor
{
    /// <summary>
    /// Mod that intercepts Unity's Debug.Log, Debug.LogWarning, and Debug.LogError
    /// and redirects them to the MDB console with proper formatting.
    /// </summary>
    [Mod("MDB.UnityDebugInterceptor", "Unity Debug Interceptor", "1.0.0", Author = "MDB Team")]
    public class UnityDebugInterceptorMod : ModBase
    {
        public override void OnLoad()
        {
            Logger.Info("Unity debug output will be captured");
        }
    }

    /// <summary>
    /// Patches Debug.Log to redirect to MDB console.
    /// </summary>
    [Patch("UnityEngine", "Debug")]
    [PatchMethod("Log", 1)]
    public static class DebugLogPatch
    {
        [Prefix]
        public static bool Prefix(string __0)
        {
            ModLogger.LogInternal("Unity", __0 ?? "<null>", ConsoleColor.Gray);
            return true; // Continue to original
        }
    }

    /// <summary>
    /// Patches Debug.LogWarning to redirect to MDB console.
    /// </summary>
    [Patch("UnityEngine", "Debug")]
    [PatchMethod("LogWarning", 1)]
    public static class DebugLogWarningPatch
    {
        [Prefix]
        public static bool Prefix(string __0)
        {
            ModLogger.LogInternal("Unity.Warn", __0 ?? "<null>", ConsoleColor.Yellow);
            return true;
        }
    }

    /// <summary>
    /// Patches Debug.LogError to redirect to MDB console.
    /// </summary>
    [Patch("UnityEngine", "Debug")]
    [PatchMethod("LogError", 1)]
    public static class DebugLogErrorPatch
    {
        [Prefix]
        public static bool Prefix(string __0)
        {
            ModLogger.LogInternal("Unity.Error", __0 ?? "<null>", ConsoleColor.Red);
            return true;
        }
    }
}
