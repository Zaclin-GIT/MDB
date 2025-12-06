// ==============================
// Il2CppBase - Core IL2CPP Runtime Classes
// ==============================
// Base types and runtime invocation for IL2CPP wrapper classes

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GameSDK
{
    /// <summary>
    /// Base class for all IL2CPP objects. Contains a pointer to the native object.
    /// </summary>
    public class Il2CppObject
    {
        /// <summary>
        /// Pointer to the native IL2CPP object.
        /// </summary>
        public IntPtr NativePtr { get; protected set; }

        public Il2CppObject() { }

        public Il2CppObject(IntPtr nativePtr)
        {
            NativePtr = nativePtr;
        }

        /// <summary>
        /// Returns true if this object has a valid native pointer.
        /// </summary>
        public bool IsValid => NativePtr != IntPtr.Zero;

        /// <summary>
        /// Implicit conversion to IntPtr for P/Invoke calls.
        /// </summary>
        public static implicit operator IntPtr(Il2CppObject obj) => obj?.NativePtr ?? IntPtr.Zero;
    }

    /// <summary>
    /// Exception thrown when an IL2CPP operation fails.
    /// </summary>
    /// <summary>
    /// Exception thrown when an IL2CPP operation fails.
    /// </summary>
    public class Il2CppException : Exception
    {
        /// <summary>
        /// The native IL2CPP exception pointer (if available).
        /// </summary>
        public IntPtr NativeException { get; }
        
        /// <summary>
        /// The error code from the bridge (if available).
        /// </summary>
        public MdbErrorCode ErrorCode { get; }
        
        /// <summary>
        /// The native error message from the bridge.
        /// </summary>
        public string NativeMessage { get; }

        public Il2CppException(string message) : base(message)
        {
            ErrorCode = Il2CppBridge.GetLastErrorCode();
            NativeMessage = Il2CppBridge.GetLastError();
        }

        public Il2CppException(IntPtr nativeException) 
            : base($"IL2CPP exception at 0x{nativeException.ToInt64():X}")
        {
            NativeException = nativeException;
            ErrorCode = MdbErrorCode.ExceptionThrown;
            NativeMessage = Il2CppBridge.GetLastError();
        }
        
        public Il2CppException(MdbErrorCode errorCode, string message) 
            : base(message)
        {
            ErrorCode = errorCode;
            NativeMessage = Il2CppBridge.GetLastError();
        }
        
        public override string ToString()
        {
            return $"{base.ToString()}\nError Code: {ErrorCode}\nNative Message: {NativeMessage}";
        }
    }

    /// <summary>
    /// Static class for making IL2CPP runtime calls.
    /// Provides methods to invoke IL2CPP methods from managed code.
    /// </summary>
    public static class Il2CppRuntime
    {
        // Cache for class lookups (assembly:ns:name -> IntPtr)
        private static readonly Dictionary<string, IntPtr> _classCache = new Dictionary<string, IntPtr>();
        
        // Cache for method lookups (classPtr:methodName:paramCount -> IntPtr)
        private static readonly Dictionary<string, IntPtr> _methodCache = new Dictionary<string, IntPtr>();
        
        // Default assembly name for game classes
        private const string DefaultAssembly = "Assembly-CSharp";
        
        private static bool _initialized = false;
        private static readonly object _initLock = new object();

        // ==============================
        // Debug Logging - Only compiled in DEBUG builds
        // ==============================
        
        /// <summary>
        /// Log debug message (only in DEBUG builds).
        /// </summary>
        [Conditional("DEBUG")]
        private static void LogDebug(string message)
        {
            GameSDK.ModHost.ModLogger.LogInternal("Il2CppRuntime", $"[DEBUG] {message}");
        }
        
        /// <summary>
        /// Log trace message (only in DEBUG builds).
        /// </summary>
        [Conditional("DEBUG")]
        private static void LogTrace(string message)
        {
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

        /// <summary>
        /// Initialize the IL2CPP runtime. Called automatically on first use.
        /// </summary>
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

        /// <summary>
        /// Call an instance method and return the result.
        /// </summary>
        /// <typeparam name="T">Return type</typeparam>
        /// <param name="instance">The object instance (must be Il2CppObject or have NativePtr)</param>
        /// <param name="methodName">Name of the method to call</param>
        /// <param name="paramTypes">Parameter types (used for overload resolution)</param>
        /// <param name="args">Arguments to pass to the method</param>
        /// <returns>The return value, or default(T) on failure</returns>
        public static T Call<T>(object instance, string methodName, Type[] paramTypes, params object[] args)
        {
            EnsureInitialized();

            try
            {
                // Get native pointer from instance
                IntPtr nativeInstance = GetNativePointer(instance);
                if (nativeInstance == IntPtr.Zero)
                {
                    LogError($"Instance is null or invalid for method {methodName}");
                    return default(T);
                }

                // Get the class from the instance
                IntPtr klass = Il2CppBridge.mdb_object_get_class(nativeInstance);
                if (klass == IntPtr.Zero)
                {
                    LogError($"Could not get class for instance when calling {methodName}");
                    return default(T);
                }

                // Get the method
                IntPtr method = GetOrCacheMethod(klass, methodName, paramTypes?.Length ?? 0);
                if (method == IntPtr.Zero)
                {
                    LogError($"Method not found: {methodName}");
                    return default(T);
                }

                // Get method pointer for direct call
                IntPtr methodPtr = Il2CppBridge.mdb_get_method_pointer(method);
                if (methodPtr == IntPtr.Zero)
                {
                    LogError($"Method pointer is null for {methodName}");
                    return default(T);
                }

                // Marshal arguments
                IntPtr[] nativeArgs = Il2CppMarshaler.MarshalArguments(args);

                // Invoke the method
                IntPtr exception;
                IntPtr result = Il2CppBridge.mdb_invoke_method(method, nativeInstance, nativeArgs, out exception);

                // Check for exception
                if (exception != IntPtr.Zero)
                {
                    throw new Il2CppException(exception);
                }

                // Marshal return value
                return Il2CppMarshaler.MarshalReturn<T>(result);
            }
            catch (Exception ex)
            {
                LogError($"Call<{typeof(T).Name}>({methodName}): {ex.Message}");
                return default(T);
            }
        }

        /// <summary>
        /// Call an instance method that returns void.
        /// </summary>
        public static void InvokeVoid(object instance, string methodName, Type[] paramTypes, params object[] args)
        {
            EnsureInitialized();

            try
            {
                IntPtr nativeInstance = GetNativePointer(instance);
                if (nativeInstance == IntPtr.Zero)
                {
                    LogError($"Instance is null or invalid for method {methodName}");
                    return;
                }

                IntPtr klass = Il2CppBridge.mdb_object_get_class(nativeInstance);
                if (klass == IntPtr.Zero)
                {
                    LogError($"Could not get class for instance when calling {methodName}");
                    return;
                }

                IntPtr method = GetOrCacheMethod(klass, methodName, paramTypes?.Length ?? 0);
                if (method == IntPtr.Zero)
                {
                    LogError($"Method not found: {methodName}");
                    return;
                }

                IntPtr[] nativeArgs = Il2CppMarshaler.MarshalArguments(args);

                IntPtr exception;
                Il2CppBridge.mdb_invoke_method(method, nativeInstance, nativeArgs, out exception);

                if (exception != IntPtr.Zero)
                {
                    throw new Il2CppException(exception);
                }
            }
            catch (Exception ex)
            {
                LogError($"InvokeVoid({methodName}): {ex.Message}");
            }
        }

        /// <summary>
        /// Call a static method and return the result.
        /// </summary>
        public static T CallStatic<T>(string ns, string typeName, string methodName, Type[] paramTypes, params object[] args)
        {
            EnsureInitialized();
            
            LogDebug($"CallStatic: {ns}.{typeName}.{methodName} with {args?.Length ?? 0} args");

            try
            {
                IntPtr klass = GetOrCacheClass(DefaultAssembly, ns, typeName);
                if (klass == IntPtr.Zero)
                {
                    LogError($"Class not found: {ns}.{typeName}");
                    return default(T);
                }
                LogTrace($"  Class found: 0x{klass.ToInt64():X}");

                int paramCount = paramTypes?.Length ?? 0;
                LogTrace($"  Looking for method {methodName} with {paramCount} params...");
                IntPtr method = GetOrCacheMethod(klass, methodName, paramCount);
                
                // If not found with exact param count, try searching all (-1)
                if (method == IntPtr.Zero)
                {
                    LogTrace($"  Method not found with {paramCount} params, trying search all...");
                    method = Il2CppBridge.mdb_get_method(klass, methodName, -1);
                }
                
                if (method == IntPtr.Zero)
                {
                    LogError($"Static method not found: {ns}.{typeName}.{methodName}");
                    return default(T);
                }
                LogTrace($"  Method found: 0x{method.ToInt64():X}");

                LogTrace($"  Marshaling {args?.Length ?? 0} arguments...");
                IntPtr[] nativeArgs = Il2CppMarshaler.MarshalArguments(args);
                for (int i = 0; i < nativeArgs.Length; i++)
                {
                    LogTrace($"    Arg[{i}] = 0x{nativeArgs[i].ToInt64():X}");
                }

                LogTrace($"  Invoking method...");
                IntPtr exception;
                IntPtr result = Il2CppBridge.mdb_invoke_method(method, IntPtr.Zero, nativeArgs, out exception);
                LogTrace($"  Result: 0x{result.ToInt64():X}, Exception: 0x{exception.ToInt64():X}");

                if (exception != IntPtr.Zero)
                {
                    throw new Il2CppException(exception);
                }

                return Il2CppMarshaler.MarshalReturn<T>(result);
            }
            catch (Exception ex)
            {
                LogError($"CallStatic<{typeof(T).Name}>({ns}.{typeName}.{methodName}): {ex.Message}");
                LogTrace($"  Stack: {ex.StackTrace}");
                return default(T);
            }
        }

        /// <summary>
        /// Call a static method that returns void.
        /// </summary>
        public static void InvokeStaticVoid(string ns, string typeName, string methodName, Type[] paramTypes, params object[] args)
        {
            EnsureInitialized();

            try
            {
                IntPtr klass = GetOrCacheClass(DefaultAssembly, ns, typeName);
                if (klass == IntPtr.Zero)
                {
                    LogError($"Class not found: {ns}.{typeName}");
                    return;
                }

                IntPtr method = GetOrCacheMethod(klass, methodName, paramTypes?.Length ?? 0);
                if (method == IntPtr.Zero)
                {
                    LogError($"Static method not found: {ns}.{typeName}.{methodName}");
                    return;
                }

                IntPtr[] nativeArgs = Il2CppMarshaler.MarshalArguments(args);

                IntPtr exception;
                Il2CppBridge.mdb_invoke_method(method, IntPtr.Zero, nativeArgs, out exception);

                if (exception != IntPtr.Zero)
                {
                    throw new Il2CppException(exception);
                }
            }
            catch (Exception ex)
            {
                LogError($"InvokeStaticVoid({ns}.{typeName}.{methodName}): {ex.Message}");
            }
        }

        // ==============================
        // Helper Methods
        // ==============================

        /// <summary>
        /// Get native pointer from an object.
        /// </summary>
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
        /// Get or cache a class pointer.
        /// </summary>
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
        /// Get or cache a method pointer.
        /// </summary>
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

        // ==============================
        // RVA-based Method Invocation
        // ==============================
        // These methods use RVA (Relative Virtual Address) to call methods directly
        // by their offset in GameAssembly.dll. This is useful for obfuscated methods
        // with Unicode names that cannot be looked up by name.

        // Cache for RVA-based function pointers
        private static readonly Dictionary<ulong, IntPtr> _rvaCache = new Dictionary<ulong, IntPtr>();

        /// <summary>
        /// Get or cache a function pointer from RVA.
        /// </summary>
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

        /// <summary>
        /// Call an instance method by RVA and return the result.
        /// Used for obfuscated methods with Unicode names.
        /// </summary>
        /// <param name="instance">The object instance</param>
        /// <param name="rva">The RVA offset of the method</param>
        /// <param name="paramTypes">Parameter types for the call</param>
        /// <param name="args">Arguments to pass</param>
        public static T CallByRva<T>(Il2CppObject instance, ulong rva, Type[] paramTypes, params object[] args)
        {
            EnsureInitialized();
            
            LogDebug($"CallByRva: RVA=0x{rva:X} with {args?.Length ?? 0} args");

            try
            {
                IntPtr methodPtr = GetOrCacheRvaPointer(rva);
                if (methodPtr == IntPtr.Zero)
                {
                    LogError($"Failed to get method pointer for RVA 0x{rva:X}");
                    return default(T);
                }
                LogTrace($"  Method pointer: 0x{methodPtr.ToInt64():X}");

                // For RVA-based calls, we call the function pointer directly
                // This requires proper ABI handling based on calling convention
                // For now, we log a warning - full implementation requires delegate marshaling
                LogError($"RVA-based calls require native delegate invocation (not yet implemented)");
                return default(T);
            }
            catch (Exception ex)
            {
                LogError($"CallByRva(0x{rva:X}): {ex.Message}");
                return default(T);
            }
        }

        /// <summary>
        /// Call a static method by RVA and return the result.
        /// Used for obfuscated methods with Unicode names.
        /// </summary>
        public static T CallStaticByRva<T>(ulong rva, Type[] paramTypes, params object[] args)
        {
            EnsureInitialized();
            
            LogDebug($"CallStaticByRva: RVA=0x{rva:X} with {args?.Length ?? 0} args");

            try
            {
                IntPtr methodPtr = GetOrCacheRvaPointer(rva);
                if (methodPtr == IntPtr.Zero)
                {
                    LogError($"Failed to get method pointer for RVA 0x{rva:X}");
                    return default(T);
                }
                LogTrace($"  Method pointer: 0x{methodPtr.ToInt64():X}");

                // For RVA-based calls, we call the function pointer directly
                LogError($"RVA-based calls require native delegate invocation (not yet implemented)");
                return default(T);
            }
            catch (Exception ex)
            {
                LogError($"CallStaticByRva(0x{rva:X}): {ex.Message}");
                return default(T);
            }
        }

        /// <summary>
        /// Invoke a void instance method by RVA.
        /// Used for obfuscated methods with Unicode names.
        /// </summary>
        public static void InvokeVoidByRva(Il2CppObject instance, ulong rva, Type[] paramTypes, params object[] args)
        {
            EnsureInitialized();
            
            LogDebug($"InvokeVoidByRva: RVA=0x{rva:X} with {args?.Length ?? 0} args");

            try
            {
                IntPtr methodPtr = GetOrCacheRvaPointer(rva);
                if (methodPtr == IntPtr.Zero)
                {
                    LogError($"Failed to get method pointer for RVA 0x{rva:X}");
                    return;
                }
                LogTrace($"  Method pointer: 0x{methodPtr.ToInt64():X}");

                // For RVA-based calls, we call the function pointer directly
                LogError($"RVA-based calls require native delegate invocation (not yet implemented)");
            }
            catch (Exception ex)
            {
                LogError($"InvokeVoidByRva(0x{rva:X}): {ex.Message}");
            }
        }

        /// <summary>
        /// Invoke a void static method by RVA.
        /// Used for obfuscated methods with Unicode names.
        /// </summary>
        public static void InvokeStaticVoidByRva(ulong rva, Type[] paramTypes, params object[] args)
        {
            EnsureInitialized();
            
            LogDebug($"InvokeStaticVoidByRva: RVA=0x{rva:X} with {args?.Length ?? 0} args");

            try
            {
                IntPtr methodPtr = GetOrCacheRvaPointer(rva);
                if (methodPtr == IntPtr.Zero)
                {
                    LogError($"Failed to get method pointer for RVA 0x{rva:X}");
                    return;
                }
                LogTrace($"  Method pointer: 0x{methodPtr.ToInt64():X}");

                // For RVA-based calls, we call the function pointer directly
                LogError($"RVA-based calls require native delegate invocation (not yet implemented)");
            }
            catch (Exception ex)
            {
                LogError($"InvokeStaticVoidByRva(0x{rva:X}): {ex.Message}");
            }
        }
    }
}
