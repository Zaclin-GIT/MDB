// ==============================
// Il2CppMethodInvoker - Method Invocation
// ==============================
// Handles all IL2CPP method invocation (instance, static, and RVA-based)

using System;
using System.Collections.Generic;

namespace GameSDK
{
    /// <summary>
    /// Method invocation functionality for IL2CPP runtime.
    /// Supports instance methods, static methods, and RVA-based method calls.
    /// </summary>
    public static partial class Il2CppRuntime
    {
        // ==============================
        // Instance Method Invocation
        // ==============================

        /// <summary>
        /// Call an instance method and return the result.
        /// Returns default(T) if the instance is null/invalid - no exceptions thrown unless ThrowOnNullPointer is true.
        /// </summary>
        /// <typeparam name="T">Return type</typeparam>
        /// <param name="instance">The object instance (must be Il2CppObject or have NativePtr)</param>
        /// <param name="methodName">Name of the method to call</param>
        /// <param name="paramTypes">Parameter types (used for overload resolution)</param>
        /// <param name="args">Arguments to pass to the method</param>
        /// <returns>The return value, or default(T) if instance is null/invalid</returns>
        public static T Call<T>(object instance, string methodName, Type[] paramTypes, params object[] args)
        {
            EnsureInitialized();

            try
            {
                // Get native pointer from instance - handle null gracefully
                IntPtr nativeInstance = GetNativePointer(instance);
                if (nativeInstance == IntPtr.Zero)
                {
                    // Silently return default for null pointers (expected behavior)
                    if (!SuppressNullErrors)
                        LogDebug($"Instance is null for method {methodName} - returning default");
                    if (ThrowOnNullPointer)
                        throw new Il2CppException(MdbErrorCode.NullPointer, $"Instance is null for method {methodName}");
                    return default(T);
                }

                // Get the class from the instance
                IntPtr klass = Il2CppBridge.mdb_object_get_class(nativeInstance);
                if (klass == IntPtr.Zero)
                {
                    if (!SuppressNullErrors)
                        LogDebug($"Could not get class for instance when calling {methodName}");
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
        /// Does nothing if the instance is null/invalid - no exceptions thrown unless ThrowOnNullPointer is true.
        /// </summary>
        /// <param name="instance">The object instance</param>
        /// <param name="methodName">Name of the method to call</param>
        /// <param name="paramTypes">Parameter types (used for overload resolution)</param>
        /// <param name="args">Arguments to pass to the method</param>
        public static void InvokeVoid(object instance, string methodName, Type[] paramTypes, params object[] args)
        {
            EnsureInitialized();

            try
            {
                IntPtr nativeInstance = GetNativePointer(instance);
                if (nativeInstance == IntPtr.Zero)
                {
                    // Silently return for null pointers
                    if (!SuppressNullErrors)
                        LogDebug($"Instance is null for method {methodName} - skipping");
                    if (ThrowOnNullPointer)
                        throw new Il2CppException(MdbErrorCode.NullPointer, $"Instance is null for method {methodName}");
                    return;
                }

                IntPtr klass = Il2CppBridge.mdb_object_get_class(nativeInstance);
                if (klass == IntPtr.Zero)
                {
                    if (!SuppressNullErrors)
                        LogDebug($"Could not get class for instance when calling {methodName}");
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

        // ==============================
        // Generic Instance Method Invocation
        // ==============================

        /// <summary>
        /// Call a generic instance method and return the result.
        /// Inflates the generic method definition with concrete type arguments before invocation.
        /// </summary>
        /// <typeparam name="T">Return type</typeparam>
        /// <param name="instance">The object instance</param>
        /// <param name="methodName">Name of the method to call</param>
        /// <param name="genericArgs">Managed Types representing the generic type arguments</param>
        /// <param name="paramTypes">Parameter types (used for overload resolution)</param>
        /// <param name="args">Arguments to pass to the method</param>
        /// <returns>The return value, or default(T) on failure</returns>
        public static T CallGeneric<T>(object instance, string methodName, Type[] genericArgs, Type[] paramTypes, params object[] args)
        {
            EnsureInitialized();

            try
            {
                IntPtr nativeInstance = GetNativePointer(instance);
                if (nativeInstance == IntPtr.Zero)
                {
                    if (!SuppressNullErrors)
                        LogDebug($"Instance is null for generic method {methodName} - returning default");
                    if (ThrowOnNullPointer)
                        throw new Il2CppException(MdbErrorCode.NullPointer, $"Instance is null for method {methodName}");
                    return default(T);
                }

                IntPtr klass = Il2CppBridge.mdb_object_get_class(nativeInstance);
                if (klass == IntPtr.Zero)
                {
                    if (!SuppressNullErrors)
                        LogDebug($"Could not get class for instance when calling {methodName}");
                    return default(T);
                }

                // Get the generic method definition
                IntPtr method = GetOrCacheMethod(klass, methodName, paramTypes?.Length ?? 0);
                if (method == IntPtr.Zero)
                {
                    LogError($"Generic method definition not found: {methodName}");
                    return default(T);
                }

                // Inflate with concrete type arguments
                IntPtr inflatedMethod = InflateMethod(method, methodName, genericArgs);
                if (inflatedMethod == IntPtr.Zero)
                {
                    return default(T);
                }

                // Marshal arguments and invoke the inflated method
                IntPtr[] nativeArgs = Il2CppMarshaler.MarshalArguments(args);
                IntPtr exception;
                IntPtr result = Il2CppBridge.mdb_invoke_method(inflatedMethod, nativeInstance, nativeArgs, out exception);

                if (exception != IntPtr.Zero)
                {
                    throw new Il2CppException(exception);
                }

                return Il2CppMarshaler.MarshalReturn<T>(result);
            }
            catch (Exception ex)
            {
                LogError($"CallGeneric<{typeof(T).Name}>({methodName}): {ex.Message}");
                return default(T);
            }
        }

        /// <summary>
        /// Call a generic instance method that returns void.
        /// Inflates the generic method definition with concrete type arguments before invocation.
        /// </summary>
        public static void InvokeGenericVoid(object instance, string methodName, Type[] genericArgs, Type[] paramTypes, params object[] args)
        {
            EnsureInitialized();

            try
            {
                IntPtr nativeInstance = GetNativePointer(instance);
                if (nativeInstance == IntPtr.Zero)
                {
                    if (!SuppressNullErrors)
                        LogDebug($"Instance is null for generic method {methodName} - skipping");
                    if (ThrowOnNullPointer)
                        throw new Il2CppException(MdbErrorCode.NullPointer, $"Instance is null for method {methodName}");
                    return;
                }

                IntPtr klass = Il2CppBridge.mdb_object_get_class(nativeInstance);
                if (klass == IntPtr.Zero)
                {
                    if (!SuppressNullErrors)
                        LogDebug($"Could not get class for instance when calling {methodName}");
                    return;
                }

                IntPtr method = GetOrCacheMethod(klass, methodName, paramTypes?.Length ?? 0);
                if (method == IntPtr.Zero)
                {
                    LogError($"Generic method definition not found: {methodName}");
                    return;
                }

                IntPtr inflatedMethod = InflateMethod(method, methodName, genericArgs);
                if (inflatedMethod == IntPtr.Zero)
                {
                    return;
                }

                IntPtr[] nativeArgs = Il2CppMarshaler.MarshalArguments(args);
                IntPtr exception;
                Il2CppBridge.mdb_invoke_method(inflatedMethod, nativeInstance, nativeArgs, out exception);

                if (exception != IntPtr.Zero)
                {
                    throw new Il2CppException(exception);
                }
            }
            catch (Exception ex)
            {
                LogError($"InvokeGenericVoid({methodName}): {ex.Message}");
            }
        }

        /// <summary>
        /// Call a generic static method and return the result.
        /// </summary>
        public static T CallStaticGeneric<T>(string ns, string typeName, string methodName, Type[] genericArgs, Type[] paramTypes, params object[] args)
        {
            EnsureInitialized();

            try
            {
                IntPtr klass = GetOrCacheClass(DefaultAssembly, ns, typeName);
                if (klass == IntPtr.Zero)
                {
                    LogError($"Class not found: {ns}.{typeName}");
                    return default(T);
                }

                IntPtr method = GetOrCacheMethod(klass, methodName, paramTypes?.Length ?? 0);
                if (method == IntPtr.Zero)
                {
                    method = Il2CppBridge.mdb_get_method(klass, methodName, -1);
                }
                if (method == IntPtr.Zero)
                {
                    LogError($"Static generic method not found: {ns}.{typeName}.{methodName}");
                    return default(T);
                }

                IntPtr inflatedMethod = InflateMethod(method, methodName, genericArgs);
                if (inflatedMethod == IntPtr.Zero)
                {
                    return default(T);
                }

                IntPtr[] nativeArgs = Il2CppMarshaler.MarshalArguments(args);
                IntPtr exception;
                IntPtr result = Il2CppBridge.mdb_invoke_method(inflatedMethod, IntPtr.Zero, nativeArgs, out exception);

                if (exception != IntPtr.Zero)
                {
                    throw new Il2CppException(exception);
                }

                return Il2CppMarshaler.MarshalReturn<T>(result);
            }
            catch (Exception ex)
            {
                LogError($"CallStaticGeneric<{typeof(T).Name}>({ns}.{typeName}.{methodName}): {ex.Message}");
                return default(T);
            }
        }

        /// <summary>
        /// Call a generic static method that returns void.
        /// </summary>
        public static void InvokeStaticGenericVoid(string ns, string typeName, string methodName, Type[] genericArgs, Type[] paramTypes, params object[] args)
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
                    method = Il2CppBridge.mdb_get_method(klass, methodName, -1);
                }
                if (method == IntPtr.Zero)
                {
                    LogError($"Static generic method not found: {ns}.{typeName}.{methodName}");
                    return;
                }

                IntPtr inflatedMethod = InflateMethod(method, methodName, genericArgs);
                if (inflatedMethod == IntPtr.Zero)
                {
                    return;
                }

                IntPtr[] nativeArgs = Il2CppMarshaler.MarshalArguments(args);
                IntPtr exception;
                Il2CppBridge.mdb_invoke_method(inflatedMethod, IntPtr.Zero, nativeArgs, out exception);

                if (exception != IntPtr.Zero)
                {
                    throw new Il2CppException(exception);
                }
            }
            catch (Exception ex)
            {
                LogError($"InvokeStaticGenericVoid({ns}.{typeName}.{methodName}): {ex.Message}");
            }
        }

        // ==============================
        // Generic Inflation Helper
        // ==============================

        // Cache for inflated generic methods (generic_def_ptr:typeArg1:typeArg2... -> inflated_ptr)
        private static readonly Dictionary<string, IntPtr> _inflatedMethodCache = new Dictionary<string, IntPtr>();

        /// <summary>
        /// Inflate a generic method definition with concrete type arguments.
        /// Caches results for performance.
        /// </summary>
        private static IntPtr InflateMethod(IntPtr method, string methodName, Type[] genericArgs)
        {
            if (genericArgs == null || genericArgs.Length == 0)
            {
                LogError($"No generic type arguments provided for {methodName}");
                return IntPtr.Zero;
            }

            // Build cache key
            string cacheKey = method.ToInt64().ToString("X");
            for (int i = 0; i < genericArgs.Length; i++)
            {
                cacheKey += ":" + genericArgs[i].FullName;
            }

            lock (_inflatedMethodCache)
            {
                if (_inflatedMethodCache.TryGetValue(cacheKey, out IntPtr cached))
                {
                    return cached;
                }
            }

            // Resolve each Type to an Il2CppClass*
            IntPtr[] typeClasses = new IntPtr[genericArgs.Length];
            for (int i = 0; i < genericArgs.Length; i++)
            {
                string ns = genericArgs[i].Namespace ?? "";
                string name = genericArgs[i].Name;

                IntPtr typeClass = GetOrCacheClass(DefaultAssembly, ns, name);
                if (typeClass == IntPtr.Zero)
                {
                    LogError($"Could not resolve generic type argument '{ns}.{name}' for {methodName}");
                    return IntPtr.Zero;
                }
                typeClasses[i] = typeClass;
            }

            // Call native inflation
            IntPtr inflated = Il2CppBridge.mdb_inflate_generic_method(method, typeClasses, typeClasses.Length);
            if (inflated == IntPtr.Zero)
            {
                LogError($"Failed to inflate generic method {methodName}: {Il2CppBridge.GetLastError()}");
                return IntPtr.Zero;
            }

            lock (_inflatedMethodCache)
            {
                _inflatedMethodCache[cacheKey] = inflated;
            }

            return inflated;
        }

        // ==============================
        // Static Method Invocation
        // ==============================

        /// <summary>
        /// Call a static method and return the result.
        /// </summary>
        /// <typeparam name="T">Return type</typeparam>
        /// <param name="ns">Namespace of the class</param>
        /// <param name="typeName">Class name</param>
        /// <param name="methodName">Name of the static method to call</param>
        /// <param name="paramTypes">Parameter types (used for overload resolution)</param>
        /// <param name="args">Arguments to pass to the method</param>
        /// <returns>The return value, or default(T) on failure</returns>
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
        /// <param name="ns">Namespace of the class</param>
        /// <param name="typeName">Class name</param>
        /// <param name="methodName">Name of the static method to call</param>
        /// <param name="paramTypes">Parameter types (used for overload resolution)</param>
        /// <param name="args">Arguments to pass to the method</param>
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
        // RVA-based Method Invocation
        // ==============================
        // These methods use RVA (Relative Virtual Address) to call methods directly
        // by their offset in GameAssembly.dll. This is useful for obfuscated methods
        // with Unicode names that cannot be looked up by name.

        /// <summary>
        /// Call an instance method by RVA and return the result.
        /// Used for obfuscated methods with Unicode names.
        /// </summary>
        /// <typeparam name="T">Return type</typeparam>
        /// <param name="instance">The object instance</param>
        /// <param name="rva">The RVA offset of the method in GameAssembly.dll</param>
        /// <param name="paramTypes">Parameter types for the call</param>
        /// <param name="args">Arguments to pass</param>
        /// <returns>The return value, or default(T) on failure</returns>
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
        /// <typeparam name="T">Return type</typeparam>
        /// <param name="rva">The RVA offset of the method in GameAssembly.dll</param>
        /// <param name="paramTypes">Parameter types for the call</param>
        /// <param name="args">Arguments to pass</param>
        /// <returns>The return value, or default(T) on failure</returns>
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
        /// <param name="instance">The object instance</param>
        /// <param name="rva">The RVA offset of the method in GameAssembly.dll</param>
        /// <param name="paramTypes">Parameter types for the call</param>
        /// <param name="args">Arguments to pass</param>
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
        /// <param name="rva">The RVA offset of the method in GameAssembly.dll</param>
        /// <param name="paramTypes">Parameter types for the call</param>
        /// <param name="args">Arguments to pass</param>
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
