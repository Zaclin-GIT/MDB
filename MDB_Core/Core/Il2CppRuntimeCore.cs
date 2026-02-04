// ==============================
// Il2CppRuntimeCore - Initialization and Core Utilities
// ==============================
// Core initialization, caching, and helper methods for IL2CPP runtime

using System;
using System.Collections.Generic;

namespace GameSDK
{
    /// <summary>
    /// Core initialization and caching functionality for IL2CPP runtime.
    /// Handles runtime initialization, class/method caching, and logging.
    /// </summary>
    public static partial class Il2CppRuntime
    {
        // Cache for class lookups (assembly:ns:name -> IntPtr)
        private static readonly Dictionary<string, IntPtr> _classCache = new Dictionary<string, IntPtr>();
        
        // Cache for method lookups (classPtr:methodName:paramCount -> IntPtr)
        private static readonly Dictionary<string, IntPtr> _methodCache = new Dictionary<string, IntPtr>();
        
        // Cache for RVA-based function pointers
        private static readonly Dictionary<ulong, IntPtr> _rvaCache = new Dictionary<ulong, IntPtr>();
        
        // Default assembly name for game classes
        private const string DefaultAssembly = "Assembly-CSharp";
        
        private static bool _initialized = false;
        private static readonly object _initLock = new object();

        // ==============================
        // Debug Logging Configuration
        // ==============================
        
        /// <summary>
        /// Enable verbose debug/trace logging. Set to false for production.
        /// </summary>
        public static bool VerboseLogging = false;
        
        /// <summary>
        /// When true, null pointer access will throw exceptions instead of returning default values.
        /// Default is false for graceful handling.
        /// </summary>
        public static bool ThrowOnNullPointer = false;

        /// <summary>
        /// When true, suppresses error logging for null pointer access.
        /// Useful when you expect some objects might be null and are handling it.
        /// </summary>
        public static bool SuppressNullErrors = false;
        
        /// <summary>
        /// Log debug message (only when VerboseLogging is enabled).
        /// </summary>
        private static void LogDebug(string message)
        {
            if (VerboseLogging)
                GameSDK.ModHost.ModLogger.LogInternal("Il2CppRuntime", $"[DEBUG] {message}");
        }
        
        /// <summary>
        /// Log trace message (only when VerboseLogging is enabled).
        /// </summary>
        private static void LogTrace(string message)
        {
            if (VerboseLogging)
                GameSDK.ModHost.ModLogger.LogInternal("Il2CppRuntime", $"[TRACE] {message}");
        }
        
        /// <summary>
        /// Log error message (always logged).
        /// </summary>
        private static void LogError(string message)
        {
            GameSDK.ModHost.ModLogger.LogInternal("Il2CppRuntime", $"[ERROR] {message}");
        }
        
        /// <summary>
        /// Log info message (always logged).
        /// </summary>
        private static void LogInfo(string message)
        {
            GameSDK.ModHost.ModLogger.LogInternal("Il2CppRuntime", $"[INFO] {message}");
        }

        // ==============================
        // Initialization
        // ==============================

        /// <summary>
        /// Initialize the IL2CPP runtime. Called automatically on first use.
        /// </summary>
        /// <returns>True if initialization succeeded, false otherwise</returns>
        public static bool Initialize()
        {
            if (_initialized) return true;

            lock (_initLock)
            {
                if (_initialized) return true;

                int result = Il2CppBridge.mdb_init();
                if (result != 0)
                {
                    LogError($"mdb_init failed: {Il2CppBridge.GetLastError()}");
                    return false;
                }

                // Attach this thread
                IntPtr domain = Il2CppBridge.mdb_domain_get();
                if (domain != IntPtr.Zero)
                {
                    Il2CppBridge.mdb_thread_attach(domain);
                }

                _initialized = true;
                LogInfo("IL2CPP Runtime initialized");
                return true;
            }
        }

        /// <summary>
        /// Ensure the runtime is initialized before making calls.
        /// </summary>
        private static void EnsureInitialized()
        {
            if (!_initialized)
            {
                Initialize();
            }
        }

        // ==============================
        // Helper Methods
        // ==============================

        /// <summary>
        /// Get native pointer from an object (Il2CppObject or IntPtr).
        /// </summary>
        /// <param name="obj">The object to extract pointer from</param>
        /// <returns>The native pointer, or IntPtr.Zero if null or invalid</returns>
        private static IntPtr GetNativePointer(object obj)
        {
            if (obj == null) return IntPtr.Zero;

            if (obj is Il2CppObject il2cppObj)
            {
                return il2cppObj.NativePtr;
            }

            if (obj is IntPtr ptr)
            {
                return ptr;
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Get or cache a class pointer by assembly, namespace, and name.
        /// </summary>
        /// <param name="assembly">Assembly name (e.g., "Assembly-CSharp")</param>
        /// <param name="ns">Namespace</param>
        /// <param name="name">Class name</param>
        /// <returns>The class pointer, or IntPtr.Zero if not found</returns>
        private static IntPtr GetOrCacheClass(string assembly, string ns, string name)
        {
            string key = $"{ns}:{name}";

            lock (_classCache)
            {
                if (_classCache.TryGetValue(key, out IntPtr cached))
                {
                    return cached;
                }

                // Try the specified assembly first
                IntPtr klass = Il2CppBridge.mdb_find_class(assembly, ns, name);
                
                // If not found and namespace starts with UnityEngine, try Unity assemblies
                if (klass == IntPtr.Zero && ns.StartsWith("UnityEngine"))
                {
                    string[] unityAssemblies = new string[]
                    {
                        "UnityEngine.CoreModule",
                        "UnityEngine",
                        "UnityEngine.PhysicsModule",
                        "UnityEngine.UI",
                        "UnityEngine.UIModule",
                        "UnityEngine.InputLegacyModule",
                        ""  // Empty string to search all
                    };
                    
                    foreach (var asm in unityAssemblies)
                    {
                        klass = Il2CppBridge.mdb_find_class(asm, ns, name);
                        if (klass != IntPtr.Zero)
                        {
                            LogDebug($"Found {ns}.{name} in assembly: {(string.IsNullOrEmpty(asm) ? "(all)" : asm)}");
                            break;
                        }
                    }
                }
                
                // Also try empty assembly (search all)
                if (klass == IntPtr.Zero)
                {
                    klass = Il2CppBridge.mdb_find_class("", ns, name);
                }
                
                if (klass != IntPtr.Zero)
                {
                    _classCache[key] = klass;
                }

                return klass;
            }
        }

        /// <summary>
        /// Get or cache a method pointer by class pointer, method name, and parameter count.
        /// </summary>
        /// <param name="klass">The class pointer</param>
        /// <param name="methodName">Method name</param>
        /// <param name="paramCount">Number of parameters</param>
        /// <returns>The method pointer, or IntPtr.Zero if not found</returns>
        private static IntPtr GetOrCacheMethod(IntPtr klass, string methodName, int paramCount)
        {
            string key = $"{klass.ToInt64():X}:{methodName}:{paramCount}";

            lock (_methodCache)
            {
                if (_methodCache.TryGetValue(key, out IntPtr cached))
                {
                    return cached;
                }

                IntPtr method = Il2CppBridge.mdb_get_method(klass, methodName, paramCount);
                if (method != IntPtr.Zero)
                {
                    _methodCache[key] = method;
                }

                return method;
            }
        }

        /// <summary>
        /// Get or cache a function pointer from RVA (Relative Virtual Address).
        /// Used for calling obfuscated methods by their offset in GameAssembly.dll.
        /// </summary>
        /// <param name="rva">The RVA offset</param>
        /// <returns>The function pointer, or IntPtr.Zero if not found</returns>
        private static IntPtr GetOrCacheRvaPointer(ulong rva)
        {
            lock (_rvaCache)
            {
                if (_rvaCache.TryGetValue(rva, out IntPtr cached))
                {
                    return cached;
                }

                IntPtr ptr = Il2CppBridge.mdb_get_method_pointer_from_rva(rva);
                if (ptr != IntPtr.Zero)
                {
                    _rvaCache[rva] = ptr;
                }

                return ptr;
            }
        }
    }
}
