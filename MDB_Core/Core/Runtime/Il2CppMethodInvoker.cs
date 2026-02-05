// ==============================
// Il2CppMethodInvoker - Method Invocation
// ==============================
// Handles all IL2CPP method invocation (instance, static, and RVA-based)

using System;

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
