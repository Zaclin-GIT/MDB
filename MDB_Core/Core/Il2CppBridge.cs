// ==============================
// Il2CppBridge - P/Invoke Declarations
// ==============================
// This file contains all P/Invoke declarations for the native MDB_Bridge.dll

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace GameSDK
{
    /// <summary>
    /// Error codes returned by bridge functions.
    /// These must match the MdbErrorCode enum in bridge_exports.h.
    /// </summary>
    public enum MdbErrorCode : int
    {
        Success = 0,
        
        // Initialization errors (1-99)
        NotInitialized = 1,
        InitFailed = 2,
        GameAssemblyNotFound = 3,
        ExportNotFound = 4,
        
        // Argument errors (100-199)
        InvalidArgument = 100,
        NullPointer = 101,
        InvalidClass = 102,
        InvalidMethod = 103,
        InvalidField = 104,
        
        // Resolution errors (200-299)
        ClassNotFound = 200,
        MethodNotFound = 201,
        FieldNotFound = 202,
        AssemblyNotFound = 203,
        
        // Invocation errors (300-399)
        InvocationFailed = 300,
        ExceptionThrown = 301,
        ThreadNotAttached = 302,
        
        // Memory errors (400-499)
        AllocationFailed = 400,
        BufferTooSmall = 401,
        
        // Unknown error
        Unknown = -1
    }

    /// <summary>
    /// P/Invoke declarations for the native IL2CPP bridge.
    /// These functions communicate with the MDB_Bridge.dll which wraps IL2CPP APIs.
    /// </summary>
    public static class Il2CppBridge
    {
        private const string DllName = "MDB_Bridge.dll";

        // ==============================
        // Initialization
        // ==============================

        /// <summary>
        /// Initialize the IL2CPP bridge. Must be called before any other bridge functions.
        /// </summary>
        /// <returns>0 on success, non-zero error code on failure</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int mdb_init();

        /// <summary>
        /// Get the IL2CPP domain.
        /// </summary>
        /// <returns>Pointer to the IL2CPP domain, or IntPtr.Zero on failure</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr mdb_domain_get();

        /// <summary>
        /// Attach the current thread to the IL2CPP domain.
        /// </summary>
        /// <param name="domain">The IL2CPP domain pointer</param>
        /// <returns>Thread handle, or IntPtr.Zero on failure</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr mdb_thread_attach(IntPtr domain);

        // ==============================
        // Class Resolution
        // ==============================

        /// <summary>
        /// Find an IL2CPP class by assembly, namespace, and name.
        /// </summary>
        /// <param name="assembly">Assembly name (e.g., "Assembly-CSharp")</param>
        /// <param name="ns">Namespace (e.g., "UnityEngine")</param>
        /// <param name="name">Class name (e.g., "GameObject")</param>
        /// <returns>Pointer to Il2CppClass, or IntPtr.Zero if not found</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern IntPtr mdb_find_class(string assembly, string ns, string name);

        /// <summary>
        /// Get the instance size of a class.
        /// </summary>
        /// <param name="klass">Pointer to Il2CppClass</param>
        /// <returns>Instance size in bytes, or -1 on error</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int mdb_get_class_size(IntPtr klass);

        // ==============================
        // Method Resolution & Invocation
        // ==============================

        /// <summary>
        /// Get a method from a class by name and parameter count.
        /// </summary>
        /// <param name="klass">Pointer to Il2CppClass</param>
        /// <param name="name">Method name</param>
        /// <param name="paramCount">Number of parameters (-1 to search all)</param>
        /// <returns>Pointer to MethodInfo, or IntPtr.Zero if not found</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern IntPtr mdb_get_method(IntPtr klass, string name, int paramCount);

        /// <summary>
        /// Get the raw function pointer for a method.
        /// </summary>
        /// <param name="method">Pointer to MethodInfo</param>
        /// <returns>Function pointer, or IntPtr.Zero if not available</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr mdb_get_method_pointer(IntPtr method);

        /// <summary>
        /// Invoke a method on an instance.
        /// </summary>
        /// <param name="method">Pointer to MethodInfo</param>
        /// <param name="instance">Pointer to object instance (IntPtr.Zero for static)</param>
        /// <param name="args">Array of argument pointers</param>
        /// <param name="exception">Output: exception if thrown</param>
        /// <returns>Return value pointer</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr mdb_invoke_method(IntPtr method, IntPtr instance, IntPtr[] args, out IntPtr exception);

        // ==============================
        // Field Access
        // ==============================

        /// <summary>
        /// Get a field from a class by name.
        /// </summary>
        /// <param name="klass">Pointer to Il2CppClass</param>
        /// <param name="name">Field name</param>
        /// <returns>Pointer to FieldInfo, or IntPtr.Zero if not found</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern IntPtr mdb_get_field(IntPtr klass, string name);

        /// <summary>
        /// Get the byte offset of a field.
        /// </summary>
        /// <param name="field">Pointer to FieldInfo</param>
        /// <returns>Field offset in bytes, or -1 on error</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int mdb_get_field_offset(IntPtr field);

        /// <summary>
        /// Get the value of an instance field.
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void mdb_field_get_value(IntPtr instance, IntPtr field, IntPtr outValue);

        /// <summary>
        /// Set the value of an instance field.
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void mdb_field_set_value(IntPtr instance, IntPtr field, IntPtr value);

        /// <summary>
        /// Get the value of a static field.
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void mdb_field_static_get_value(IntPtr field, IntPtr outValue);

        /// <summary>
        /// Set the value of a static field.
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void mdb_field_static_set_value(IntPtr field, IntPtr value);

        // ==============================
        // Object Creation
        // ==============================

        /// <summary>
        /// Allocate a new IL2CPP object.
        /// </summary>
        /// <param name="klass">Pointer to Il2CppClass</param>
        /// <returns>Pointer to new object, or IntPtr.Zero on failure</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr mdb_object_new(IntPtr klass);

        /// <summary>
        /// Create a new IL2CPP string from a UTF-8 string.
        /// </summary>
        /// <param name="str">UTF-8 string</param>
        /// <returns>Pointer to Il2CppString, or IntPtr.Zero on failure</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern IntPtr mdb_string_new(string str);

        /// <summary>
        /// Convert an IL2CPP string to UTF-8.
        /// </summary>
        /// <param name="str">Pointer to Il2CppString</param>
        /// <param name="buffer">Output buffer</param>
        /// <param name="bufferSize">Buffer size</param>
        /// <returns>Number of bytes written, or -1 on error</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int mdb_string_to_utf8(IntPtr str, StringBuilder buffer, int bufferSize);

        // ==============================
        // Utilities
        // ==============================

        /// <summary>
        /// Get the class of an object instance.
        /// </summary>
        /// <param name="instance">Pointer to object instance</param>
        /// <returns>Pointer to Il2CppClass</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr mdb_object_get_class(IntPtr instance);

        /// <summary>
        /// Get the Il2CppType* from a class.
        /// </summary>
        /// <param name="klass">Pointer to Il2CppClass</param>
        /// <returns>Pointer to Il2CppType</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr mdb_class_get_type(IntPtr klass);

        /// <summary>
        /// Get a System.Type object (Il2CppReflectionType*) from an Il2CppType*.
        /// </summary>
        /// <param name="il2cppType">Pointer to Il2CppType</param>
        /// <returns>Pointer to System.Type object</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr mdb_type_get_object(IntPtr il2cppType);

        /// <summary>
        /// Get the last error message from the bridge.
        /// </summary>
        /// <returns>Error message string</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr mdb_get_last_error();

        /// <summary>
        /// Get the last error code from the bridge.
        /// </summary>
        /// <returns>MdbErrorCode value</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int mdb_get_last_error_code();

        // ==============================
        // Helper Methods
        // ==============================

        /// <summary>
        /// Get the last error as a managed string.
        /// </summary>
        public static string GetLastError()
        {
            IntPtr errorPtr = mdb_get_last_error();
            return errorPtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(errorPtr) : "Unknown error";
        }

        /// <summary>
        /// Get the last error code.
        /// </summary>
        public static MdbErrorCode GetLastErrorCode()
        {
            int code = mdb_get_last_error_code();
            return Enum.IsDefined(typeof(MdbErrorCode), code) ? (MdbErrorCode)code : MdbErrorCode.Unknown;
        }

        /// <summary>
        /// Convert an IL2CPP string to a managed string.
        /// </summary>
        public static string Il2CppStringToManaged(IntPtr il2cppString)
        {
            if (il2cppString == IntPtr.Zero)
                return null;

            StringBuilder buffer = new StringBuilder(4096);
            int length = mdb_string_to_utf8(il2cppString, buffer, buffer.Capacity);
            
            if (length < 0)
                return null;

            return buffer.ToString(0, length);
        }
    }
}
