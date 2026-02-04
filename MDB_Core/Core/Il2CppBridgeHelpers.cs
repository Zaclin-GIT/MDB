// ==============================
// Il2CppBridge - Helper Methods
// ==============================
// This file contains helper methods and utility functions for the IL2CPP bridge.

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace GameSDK
{
    public static partial class Il2CppBridge
    {
        // ==============================
        // Error Handling Helpers
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

        /// <summary>
        /// Convert a managed string to an IL2CPP string.
        /// </summary>
        public static IntPtr ManagedStringToIl2Cpp(string managedString)
        {
            if (managedString == null)
                return IntPtr.Zero;

            return mdb_string_new(managedString);
        }


        // ==============================
        // OnGUI Hook Helpers
        // ==============================

        /// Get the name of the method that was hooked for OnGUI (managed wrapper).
        /// </summary>
        public static string GetHookedMethod()
        {
            IntPtr ptr = mdb_get_hooked_method();
            if (ptr == IntPtr.Zero)
                return string.Empty;
            return System.Runtime.InteropServices.Marshal.PtrToStringAnsi(ptr) ?? string.Empty;
        }


        // ==============================
        // Class Information Helpers
        // ==============================

        /// </summary>
        /// <param name="klass">Pointer to Il2CppClass</param>
        /// <returns>Class name, or null if invalid</returns>
        public static string GetClassName(IntPtr klass)
        {
            if (klass == IntPtr.Zero)
                return null;
            IntPtr namePtr = mdb_class_get_name(klass);
            if (namePtr == IntPtr.Zero)
                return null;
            return Marshal.PtrToStringAnsi(namePtr);
        }

        /// <summary>
        /// Get the full name (namespace.classname) of a class.
        /// </summary>
        /// <param name="klass">Pointer to Il2CppClass</param>
        /// <returns>Full class name, or null if invalid</returns>
        public static string GetClassFullName(IntPtr klass)
        {
            if (klass == IntPtr.Zero)
                return null;
            
            string ns = null;
            IntPtr nsPtr = mdb_class_get_namespace(klass);
            if (nsPtr != IntPtr.Zero)
                ns = Marshal.PtrToStringAnsi(nsPtr);
            
            string name = GetClassName(klass);
            if (name == null)
                return null;
            
            return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
        }


        // ==============================
        // Reflection Helpers
        // ==============================

        /// <summary>
        /// Get the name of a method (managed wrapper).
        /// </summary>
        public static string GetMethodName(IntPtr method)
        {
            IntPtr ptr = mdb_method_get_name(method);
            if (ptr == IntPtr.Zero)
                return null;
            return Marshal.PtrToStringAnsi(ptr);
        }

        /// <summary>Get field name (managed wrapper).</summary>
        public static string GetFieldName(IntPtr field)
        {
            IntPtr ptr = mdb_field_get_name(field);
            if (ptr == IntPtr.Zero) return null;
            return Marshal.PtrToStringAnsi(ptr);
        }

        /// <summary>Get type name (managed wrapper).</summary>
        public static string GetTypeName(IntPtr type)
        {
            if (type == IntPtr.Zero) return null;
            try
            {
                IntPtr ptr = mdb_type_get_name(type);
                if (ptr == IntPtr.Zero) return null;
                return Marshal.PtrToStringAnsi(ptr);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Get property name (managed wrapper).</summary>
        public static string GetPropertyName(IntPtr prop)
        {
            IntPtr ptr = mdb_property_get_name(prop);
            if (ptr == IntPtr.Zero) return null;
            return Marshal.PtrToStringAnsi(ptr);
        }

        /// <summary>Get method name string (managed wrapper).</summary>
        public static string GetMethodNameStr(IntPtr method)
        {
            IntPtr ptr = mdb_method_get_name_str(method);
            if (ptr == IntPtr.Zero) return null;
            return Marshal.PtrToStringAnsi(ptr);
        }


        // ==============================
        // Scene Helpers
        // ==============================

        /// <summary>
        /// Helper to get scene name as a managed string.
        /// </summary>
        public static string GetSceneName(int sceneIndex)
        {
            byte[] buffer = new byte[256];
            int length = mdb_scenemanager_get_scene_name(sceneIndex, buffer, buffer.Length);
            if (length <= 0) return null;
            return System.Text.Encoding.UTF8.GetString(buffer, 0, length);
        }

    }
}
