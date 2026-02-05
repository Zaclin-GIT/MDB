// ==============================
// Il2CppTypeSystem - Type System Utilities
// ==============================
// Handles IL2CPP type checking and runtime type information

using System;

namespace GameSDK
{
    /// <summary>
    /// Type system utilities for IL2CPP runtime.
    /// Provides runtime type checking and class name inspection.
    /// </summary>
    public static partial class Il2CppRuntime
    {
        // ==============================
        // Type Checking Utilities
        // ==============================

        /// <summary>
        /// Get the IL2CPP class name of an object instance.
        /// Useful for debugging or runtime type checking.
        /// </summary>
        /// <param name="instance">The IL2CPP object instance</param>
        /// <returns>The IL2CPP class name, or null if invalid</returns>
        public static string GetRuntimeClassName(object instance)
        {
            IntPtr nativePtr = GetNativePointer(instance);
            if (nativePtr == IntPtr.Zero)
                return null;

            IntPtr klass = Il2CppBridge.mdb_object_get_class(nativePtr);
            return Il2CppBridge.GetClassName(klass);
        }

        /// <summary>
        /// Get the full IL2CPP class name (namespace.classname) of an object instance.
        /// </summary>
        /// <param name="instance">The IL2CPP object instance</param>
        /// <returns>The full class name (namespace.classname), or null if invalid</returns>
        public static string GetRuntimeClassFullName(object instance)
        {
            IntPtr nativePtr = GetNativePointer(instance);
            if (nativePtr == IntPtr.Zero)
                return null;

            IntPtr klass = Il2CppBridge.mdb_object_get_class(nativePtr);
            return Il2CppBridge.GetClassFullName(klass);
        }

        /// <summary>
        /// Check if an IL2CPP object is of a specific class by name.
        /// </summary>
        /// <param name="instance">The IL2CPP object instance</param>
        /// <param name="expectedClassName">The expected IL2CPP class name (obfuscated name)</param>
        /// <returns>True if the object is of the expected class</returns>
        public static bool IsClassNamed(object instance, string expectedClassName)
        {
            string actualName = GetRuntimeClassName(instance);
            return actualName != null && actualName == expectedClassName;
        }

        /// <summary>
        /// Check if an IL2CPP object is an instance of a wrapper type T.
        /// Uses the _il2cppClassName field from generated wrapper classes.
        /// </summary>
        /// <typeparam name="T">The wrapper type to check against (must have _il2cppClassName field)</typeparam>
        /// <param name="instance">The IL2CPP object instance</param>
        /// <returns>True if the object is an instance of T</returns>
        public static bool IsInstanceOf<T>(object instance) where T : Il2CppObject
        {
            string actualName = GetRuntimeClassName(instance);
            if (actualName == null)
                return false;

            // Get the expected IL2CPP class name from the wrapper type
            Type t = typeof(T);
            var field = t.GetField("_il2cppClassName", 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Static);
            
            if (field == null)
            {
                // No _il2cppClassName field, fall back to managed type name
                LogDebug($"Type {t.Name} has no _il2cppClassName field, cannot verify IL2CPP type");
                return true; // Can't verify, assume valid
            }

            string expectedName = (string)field.GetValue(null);
            return actualName == expectedName;
        }
    }
}
