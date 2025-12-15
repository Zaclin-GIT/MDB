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

        /// <summary>
        /// Invoke a method on an instance with raw pointer to args array.
        /// Use this when you need precise control over argument passing.
        /// For il2cpp_runtime_invoke, each arg must be a POINTER TO the value.
        /// </summary>
        /// <param name="method">Pointer to MethodInfo</param>
        /// <param name="instance">Pointer to object instance (IntPtr.Zero for static)</param>
        /// <param name="args">Pointer to array of argument pointers (void**)</param>
        /// <param name="exception">Output: exception if thrown</param>
        /// <returns>Return value pointer</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mdb_invoke_method")]
        public static extern IntPtr mdb_invoke_method_ptr(IntPtr method, IntPtr instance, IntPtr args, out IntPtr exception);

        // ==============================
        // RVA-based Method Access
        // ==============================

        /// <summary>
        /// Get the base address of GameAssembly.dll.
        /// </summary>
        /// <returns>Base address, or IntPtr.Zero if not loaded</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr mdb_get_gameassembly_base();

        /// <summary>
        /// Get a function pointer directly from an RVA offset.
        /// This allows calling methods by their RVA when the method name contains
        /// invalid characters (e.g., obfuscated Unicode names).
        /// </summary>
        /// <param name="rva">The RVA offset from the dump (e.g., 0x52f1e0)</param>
        /// <returns>Function pointer at base + RVA</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr mdb_get_method_pointer_from_rva(ulong rva);

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
        /// Get the name of a class.
        /// </summary>
        /// <param name="klass">Pointer to Il2CppClass</param>
        /// <returns>Class name string</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr mdb_class_get_name(IntPtr klass);

        /// <summary>
        /// Get the namespace of a class.
        /// </summary>
        /// <param name="klass">Pointer to Il2CppClass</param>
        /// <returns>Namespace string</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr mdb_class_get_namespace(IntPtr klass);

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
        // GameObject Component Helpers
        // ==============================

        /// <summary>
        /// Get all components on a GameObject.
        /// This is a specialized function that handles the tricky GetComponents call correctly.
        /// </summary>
        /// <param name="gameObject">Pointer to GameObject IL2CPP object</param>
        /// <returns>Pointer to Component[] array, or IntPtr.Zero on failure</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr mdb_gameobject_get_components(IntPtr gameObject);

        /// <summary>
        /// Get the number of components on a GameObject (from the returned array).
        /// </summary>
        /// <param name="componentsArray">Pointer to Component[] returned by mdb_gameobject_get_components</param>
        /// <returns>Number of components, or 0 on error</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int mdb_components_array_length(IntPtr componentsArray);

        /// <summary>
        /// Get a component from the array at the specified index.
        /// </summary>
        /// <param name="componentsArray">Pointer to Component[] returned by mdb_gameobject_get_components</param>
        /// <param name="index">Index into the array</param>
        /// <returns>Pointer to Component, or IntPtr.Zero on error</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr mdb_components_array_get(IntPtr componentsArray, int index);

        /// <summary>
        /// Set the active state of a GameObject.
        /// This properly handles the bool parameter boxing for IL2CPP.
        /// </summary>
        /// <param name="gameObject">Pointer to GameObject IL2CPP object</param>
        /// <param name="active">Whether to activate (true) or deactivate (false)</param>
        /// <returns>true on success, false on failure</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool mdb_gameobject_set_active(IntPtr gameObject, [MarshalAs(UnmanagedType.I1)] bool active);

        /// <summary>
        /// Get the scene handle that a GameObject belongs to.
        /// Can be used to identify DontDestroyOnLoad objects (their scene handle won't match any loaded scene).
        /// </summary>
        /// <param name="gameObject">Pointer to GameObject IL2CPP object</param>
        /// <returns>Scene handle, or 0 on error</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int mdb_gameobject_get_scene_handle(IntPtr gameObject);

        /// <summary>
        /// Get the activeSelf state of a GameObject.
        /// Properly unboxes the bool return value from IL2CPP.
        /// </summary>
        /// <param name="gameObject">Pointer to GameObject IL2CPP object</param>
        /// <returns>true if active, false if inactive or on error</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool mdb_gameobject_get_active_self(IntPtr gameObject);

        // ==============================
        // Transform Helpers
        // ==============================

        /// <summary>
        /// Get the child count of a Transform.
        /// Properly unboxes the int return value from IL2CPP.
        /// </summary>
        /// <param name="transform">Pointer to Transform IL2CPP object</param>
        /// <returns>Number of children, or 0 on error</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int mdb_transform_get_child_count(IntPtr transform);

        /// <summary>
        /// Get a child Transform at the specified index.
        /// </summary>
        /// <param name="transform">Pointer to Transform IL2CPP object</param>
        /// <param name="index">Index of the child</param>
        /// <returns>Pointer to child Transform, or IntPtr.Zero on error</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr mdb_transform_get_child(IntPtr transform, int index);

        /// <summary>
        /// Get the local position of a Transform.
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool mdb_transform_get_local_position(IntPtr transform, out float x, out float y, out float z);

        /// <summary>
        /// Set the local position of a Transform.
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool mdb_transform_set_local_position(IntPtr transform, float x, float y, float z);

        /// <summary>
        /// Get the local euler angles of a Transform.
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool mdb_transform_get_local_euler_angles(IntPtr transform, out float x, out float y, out float z);

        /// <summary>
        /// Set the local euler angles of a Transform.
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool mdb_transform_set_local_euler_angles(IntPtr transform, float x, float y, float z);

        /// <summary>
        /// Get the local scale of a Transform.
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool mdb_transform_get_local_scale(IntPtr transform, out float x, out float y, out float z);

        /// <summary>
        /// Set the local scale of a Transform.
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool mdb_transform_set_local_scale(IntPtr transform, float x, float y, float z);

        // ==============================
        // SceneManager Helpers
        // ==============================

        /// <summary>
        /// Get the number of loaded scenes.
        /// Properly unboxes the int return value from IL2CPP.
        /// </summary>
        /// <returns>Number of loaded scenes, or 0 on error</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int mdb_scenemanager_get_scene_count();

        /// <summary>
        /// Get the name of a scene at the specified index.
        /// </summary>
        /// <param name="sceneIndex">Index of the scene (0 to sceneCount-1)</param>
        /// <param name="buffer">Buffer to write the scene name to</param>
        /// <param name="bufferSize">Size of the buffer</param>
        /// <returns>Length of the name written, or 0 on error</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int mdb_scenemanager_get_scene_name(int sceneIndex, [Out] byte[] buffer, int bufferSize);

        /// <summary>
        /// Get the handle of a scene at the specified index.
        /// </summary>
        /// <param name="sceneIndex">Index of the scene (0 to sceneCount-1)</param>
        /// <returns>Scene handle, or -1 on error</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int mdb_scenemanager_get_scene_handle(int sceneIndex);

        /// <summary>
        /// Get the root GameObject count of a scene at the specified index.
        /// </summary>
        /// <param name="sceneIndex">Index of the scene (0 to sceneCount-1)</param>
        /// <returns>Number of root GameObjects in the scene, or 0 on error</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int mdb_scenemanager_get_scene_root_count(int sceneIndex);

        /// <summary>
        /// Get the DontDestroyOnLoad scene handle.
        /// </summary>
        /// <returns>Scene handle for DDOL scene, or -1 if not found</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int mdb_get_dontdestroyonload_scene_handle();

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

        // ==============================
        // Array Helpers
        // ==============================

        /// <summary>
        /// Get the length of an IL2CPP array.
        /// </summary>
        /// <param name="array">Pointer to the IL2CPP array</param>
        /// <returns>Length of the array, or -1 if null</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int mdb_array_length(IntPtr array);

        /// <summary>
        /// Get an element from an IL2CPP array.
        /// </summary>
        /// <param name="array">Pointer to the IL2CPP array</param>
        /// <param name="index">Index of the element</param>
        /// <returns>Pointer to the element, or IntPtr.Zero if out of bounds</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr mdb_array_get_element(IntPtr array, int index);

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

        // ==============================
        // OnGUI Hook Support
        // ==============================

        /// <summary>
        /// Delegate type for OnGUI callback from native code.
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void OnGUICallbackDelegate();

        /// <summary>
        /// Install the OnGUI hook by hooking a Unity GUI method.
        /// Requires MinHook to be compiled into MDB_Bridge.
        /// </summary>
        /// <returns>0 on success, negative error code on failure</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int mdb_install_ongui_hook();

        /// <summary>
        /// Register a callback function to be called during OnGUI.
        /// </summary>
        /// <param name="callback">The callback function pointer</param>
        /// <returns>0 on success</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int mdb_register_ongui_callback(OnGUICallbackDelegate callback);

        /// <summary>
        /// Manually trigger the OnGUI callback (for testing).
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void mdb_dispatch_ongui();
        
        /// <summary>
        /// Get the name of the method that was hooked for OnGUI.
        /// </summary>
        /// <returns>Method name string, or empty if not hooked</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr mdb_get_hooked_method();
        
        /// <summary>
        /// Get the name of the method that was hooked for OnGUI (managed wrapper).
        /// </summary>
        public static string GetHookedMethod()
        {
            IntPtr ptr = mdb_get_hooked_method();
            if (ptr == IntPtr.Zero)
                return string.Empty;
            return System.Runtime.InteropServices.Marshal.PtrToStringAnsi(ptr) ?? string.Empty;
        }

        /// <summary>
        /// Get the name of a class as a managed string.
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
        // Generic Method Hooking
        // ==============================

        /// <summary>
        /// Create a hook on an IL2CPP method.
        /// </summary>
        /// <param name="method">Pointer to MethodInfo</param>
        /// <param name="callback">Function pointer to the detour callback</param>
        /// <param name="original">Output: pointer to trampoline for calling original</param>
        /// <returns>Hook handle (>0 on success), or negative error code</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern long mdb_create_hook(IntPtr method, IntPtr callback, out IntPtr original);

        /// <summary>
        /// Create a hook on a method by RVA offset.
        /// </summary>
        /// <param name="rva">The RVA offset of the method</param>
        /// <param name="callback">Function pointer to the detour callback</param>
        /// <param name="original">Output: pointer to trampoline for calling original</param>
        /// <returns>Hook handle (>0 on success), or negative error code</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern long mdb_create_hook_rva(ulong rva, IntPtr callback, out IntPtr original);

        /// <summary>
        /// Create a hook on a direct function pointer.
        /// </summary>
        /// <param name="target">Target function pointer to hook</param>
        /// <param name="detour">Detour function pointer</param>
        /// <param name="original">Output: pointer to trampoline for calling original</param>
        /// <returns>Hook handle (>0 on success), or negative error code</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern long mdb_create_hook_ptr(IntPtr target, IntPtr detour, out IntPtr original);

        /// <summary>
        /// Remove a hook by handle.
        /// </summary>
        /// <param name="hookHandle">The hook handle returned by mdb_create_hook*</param>
        /// <returns>0 on success, non-zero on failure</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int mdb_remove_hook(long hookHandle);

        /// <summary>
        /// Enable or disable a hook.
        /// </summary>
        /// <param name="hookHandle">The hook handle</param>
        /// <param name="enabled">True to enable, false to disable</param>
        /// <returns>0 on success, non-zero on failure</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int mdb_set_hook_enabled(long hookHandle, [MarshalAs(UnmanagedType.I1)] bool enabled);

        /// <summary>
        /// Get information about an IL2CPP method.
        /// </summary>
        /// <param name="method">Pointer to MethodInfo</param>
        /// <param name="paramCount">Output: number of parameters</param>
        /// <param name="isStatic">Output: true if static method</param>
        /// <param name="hasReturn">Output: true if method has return value</param>
        /// <returns>0 on success, non-zero on failure</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int mdb_get_method_info(IntPtr method, out int paramCount, 
            [MarshalAs(UnmanagedType.I1)] out bool isStatic, 
            [MarshalAs(UnmanagedType.I1)] out bool hasReturn);

        /// <summary>
        /// Get the name of a method.
        /// </summary>
        /// <param name="method">Pointer to MethodInfo</param>
        /// <returns>Method name, or IntPtr.Zero on error</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr mdb_method_get_name(IntPtr method);

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

        // ==============================
        // Reflection Helpers for Component Inspector
        // ==============================

        /// <summary>Get the number of fields in a class.</summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int mdb_class_get_field_count(IntPtr klass);

        /// <summary>Get a field by index.</summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr mdb_class_get_field_by_index(IntPtr klass, int index);

        /// <summary>Get field name (native).</summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr mdb_field_get_name(IntPtr field);

        /// <summary>Get field name (managed wrapper).</summary>
        public static string GetFieldName(IntPtr field)
        {
            IntPtr ptr = mdb_field_get_name(field);
            if (ptr == IntPtr.Zero) return null;
            return Marshal.PtrToStringAnsi(ptr);
        }

        /// <summary>Get field type.</summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr mdb_field_get_type(IntPtr field);

        /// <summary>Check if field is static.</summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool mdb_field_is_static(IntPtr field);

        /// <summary>Get type name (native).</summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr mdb_type_get_name(IntPtr type);

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

        /// <summary>Get type enum (IL2CPP_TYPE_*).</summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int mdb_type_get_type_enum(IntPtr type);

        /// <summary>Get class from type.</summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr mdb_type_get_class(IntPtr type);

        /// <summary>Check if type is value type.</summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool mdb_type_is_valuetype(IntPtr type);

        /// <summary>Get the number of properties in a class.</summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int mdb_class_get_property_count(IntPtr klass);

        /// <summary>Get a property by index.</summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr mdb_class_get_property_by_index(IntPtr klass, int index);

        /// <summary>Get property name (native).</summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr mdb_property_get_name(IntPtr prop);

        /// <summary>Get property name (managed wrapper).</summary>
        public static string GetPropertyName(IntPtr prop)
        {
            IntPtr ptr = mdb_property_get_name(prop);
            if (ptr == IntPtr.Zero) return null;
            return Marshal.PtrToStringAnsi(ptr);
        }

        /// <summary>Get property getter method.</summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr mdb_property_get_get_method(IntPtr prop);

        /// <summary>Get property setter method.</summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr mdb_property_get_set_method(IntPtr prop);

        /// <summary>Get the number of methods in a class.</summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int mdb_class_get_method_count(IntPtr klass);

        /// <summary>Get a method by index.</summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr mdb_class_get_method_by_index(IntPtr klass, int index);

        /// <summary>Get method name (native).</summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr mdb_method_get_name_str(IntPtr method);

        /// <summary>Get method name string (managed wrapper).</summary>
        public static string GetMethodNameStr(IntPtr method)
        {
            IntPtr ptr = mdb_method_get_name_str(method);
            if (ptr == IntPtr.Zero) return null;
            return Marshal.PtrToStringAnsi(ptr);
        }

        /// <summary>Get method parameter count.</summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int mdb_method_get_param_count(IntPtr method);

        /// <summary>Get method return type.</summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr mdb_method_get_return_type(IntPtr method);

        /// <summary>Get method flags.</summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int mdb_method_get_flags(IntPtr method);

        /// <summary>Get parent class.</summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr mdb_class_get_parent(IntPtr klass);

        /// <summary>Get field value directly (for primitives).</summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool mdb_field_get_value_direct(IntPtr instance, IntPtr field, IntPtr outBuffer, int bufferSize);

        /// <summary>Set field value directly (for primitives).</summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool mdb_field_set_value_direct(IntPtr instance, IntPtr field, IntPtr value, int valueSize);

        // IL2CPP Type Enum Constants
        public static class Il2CppTypeEnum
        {
            public const int IL2CPP_TYPE_END = 0x00;
            public const int IL2CPP_TYPE_VOID = 0x01;
            public const int IL2CPP_TYPE_BOOLEAN = 0x02;
            public const int IL2CPP_TYPE_CHAR = 0x03;
            public const int IL2CPP_TYPE_I1 = 0x04;  // sbyte
            public const int IL2CPP_TYPE_U1 = 0x05;  // byte
            public const int IL2CPP_TYPE_I2 = 0x06;  // short
            public const int IL2CPP_TYPE_U2 = 0x07;  // ushort
            public const int IL2CPP_TYPE_I4 = 0x08;  // int
            public const int IL2CPP_TYPE_U4 = 0x09;  // uint
            public const int IL2CPP_TYPE_I8 = 0x0a;  // long
            public const int IL2CPP_TYPE_U8 = 0x0b;  // ulong
            public const int IL2CPP_TYPE_R4 = 0x0c;  // float
            public const int IL2CPP_TYPE_R8 = 0x0d;  // double
            public const int IL2CPP_TYPE_STRING = 0x0e;
            public const int IL2CPP_TYPE_PTR = 0x0f;
            public const int IL2CPP_TYPE_BYREF = 0x10;
            public const int IL2CPP_TYPE_VALUETYPE = 0x11;
            public const int IL2CPP_TYPE_CLASS = 0x12;
            public const int IL2CPP_TYPE_VAR = 0x13;
            public const int IL2CPP_TYPE_ARRAY = 0x14;
            public const int IL2CPP_TYPE_GENERICINST = 0x15;
            public const int IL2CPP_TYPE_TYPEDBYREF = 0x16;
            public const int IL2CPP_TYPE_I = 0x18;   // IntPtr
            public const int IL2CPP_TYPE_U = 0x19;   // UIntPtr
            public const int IL2CPP_TYPE_FNPTR = 0x1b;
            public const int IL2CPP_TYPE_OBJECT = 0x1c;
            public const int IL2CPP_TYPE_SZARRAY = 0x1d;  // Single-dimension array
            public const int IL2CPP_TYPE_MVAR = 0x1e;
            public const int IL2CPP_TYPE_ENUM = 0x55;
        }
    }
}

