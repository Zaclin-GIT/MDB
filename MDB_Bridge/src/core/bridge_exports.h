#pragma once
// ==============================
// MDB Bridge - Export Declarations
// ==============================
// This header defines all exported functions that can be called via P/Invoke from C#

#include <cstdint>

#ifdef MDB_BRIDGE_EXPORTS
#define MDB_API __declspec(dllexport)
#else
#define MDB_API __declspec(dllimport)
#endif

// ==============================
// Error Codes
// ==============================

/// <summary>
/// Error codes returned by bridge functions.
/// These match the MdbErrorCode enum in C#.
/// </summary>
enum class MdbErrorCode : int {
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
};

extern "C" {
    // ==============================
    // Initialization
    // ==============================
    
    /// <summary>
    /// Initialize the IL2CPP bridge. Must be called before any other bridge functions.
    /// </summary>
    /// <returns>0 on success, non-zero error code on failure</returns>
    MDB_API int mdb_init();
    
    /// <summary>
    /// Get the IL2CPP domain.
    /// </summary>
    /// <returns>Pointer to the IL2CPP domain, or nullptr on failure</returns>
    MDB_API void* mdb_domain_get();
    
    /// <summary>
    /// Attach the current thread to the IL2CPP domain.
    /// Must be called from any thread that will make IL2CPP calls.
    /// </summary>
    /// <param name="domain">The IL2CPP domain pointer</param>
    /// <returns>Thread handle, or nullptr on failure</returns>
    MDB_API void* mdb_thread_attach(void* domain);
    
    // ==============================
    // Class Resolution
    // ==============================
    
    /// <summary>
    /// Find an IL2CPP class by assembly, namespace, and name.
    /// </summary>
    /// <param name="assembly">Assembly name (e.g., "Assembly-CSharp")</param>
    /// <param name="ns">Namespace (e.g., "UnityEngine")</param>
    /// <param name="name">Class name (e.g., "GameObject")</param>
    /// <returns>Pointer to Il2CppClass, or nullptr if not found</returns>
    MDB_API void* mdb_find_class(const char* assembly, const char* ns, const char* name);
    
    /// <summary>
    /// Get the instance size of a class.
    /// </summary>
    /// <param name="klass">Pointer to Il2CppClass</param>
    /// <returns>Instance size in bytes, or -1 on error</returns>
    MDB_API int mdb_get_class_size(void* klass);
    
    // ==============================
    // Method Resolution & Invocation
    // ==============================
    
    /// <summary>
    /// Get a method from a class by name and parameter count.
    /// </summary>
    /// <param name="klass">Pointer to Il2CppClass</param>
    /// <param name="name">Method name</param>
    /// <param name="param_count">Number of parameters (-1 to search all)</param>
    /// <returns>Pointer to MethodInfo, or nullptr if not found</returns>
    MDB_API void* mdb_get_method(void* klass, const char* name, int param_count);
    
    /// <summary>
    /// Get the raw function pointer for a method (for direct calls).
    /// </summary>
    /// <param name="method">Pointer to MethodInfo</param>
    /// <returns>Function pointer, or nullptr if not available</returns>
    MDB_API void* mdb_get_method_pointer(void* method);
    
    /// <summary>
    /// Invoke a method on an instance (or static if instance is nullptr).
    /// </summary>
    /// <param name="method">Pointer to MethodInfo</param>
    /// <param name="instance">Pointer to object instance (nullptr for static methods)</param>
    /// <param name="args">Array of argument pointers</param>
    /// <param name="exception">Output: exception object if thrown (can be nullptr)</param>
    /// <returns>Return value pointer (nullptr for void methods)</returns>
    MDB_API void* mdb_invoke_method(void* method, void* instance, void** args, void** exception);
    
    /// <summary>
    /// Get the parameter type for a method at a specific index.
    /// </summary>
    /// <param name="method">Pointer to MethodInfo</param>
    /// <param name="index">Parameter index (0-based)</param>
    /// <returns>Pointer to Il2CppType, or nullptr if invalid</returns>
    MDB_API void* mdb_method_get_param_type(void* method, int index);
    
    /// <summary>
    /// Get the return type for a method.
    /// </summary>
    /// <param name="method">Pointer to MethodInfo</param>
    /// <returns>Pointer to Il2CppType, or nullptr if invalid</returns>
    MDB_API void* mdb_method_get_return_type(void* method);
    
    /// <summary>
    /// Get the type enum value from an Il2CppType.
    /// Common values: 0x0c = float (R4), 0x0d = double (R8), 0x01 = void
    /// </summary>
    /// <param name="type">Pointer to Il2CppType</param>
    /// <returns>Type enum value, or -1 on error</returns>
    MDB_API int mdb_type_get_type_enum(void* type);
    
    // ==============================
    // RVA-based Method Access
    // ==============================
    
    /// <summary>
    /// Get the base address of GameAssembly.dll.
    /// </summary>
    /// <returns>Base address, or nullptr if not loaded</returns>
    MDB_API void* mdb_get_gameassembly_base();
    
    /// <summary>
    /// Get a function pointer directly from an RVA offset.
    /// This allows calling methods by their RVA when the method name contains
    /// invalid characters (e.g., obfuscated Unicode names).
    /// </summary>
    /// <param name="rva">The RVA offset from the dump (e.g., 0x52f1e0)</param>
    /// <returns>Function pointer at base + RVA</returns>
    MDB_API void* mdb_get_method_pointer_from_rva(uint64_t rva);
    
    // ==============================
    // Field Access
    // ==============================
    
    /// <summary>
    /// Get a field from a class by name.
    /// </summary>
    /// <param name="klass">Pointer to Il2CppClass</param>
    /// <param name="name">Field name</param>
    /// <returns>Pointer to FieldInfo, or nullptr if not found</returns>
    MDB_API void* mdb_get_field(void* klass, const char* name);
    
    /// <summary>
    /// Get the byte offset of a field within an object.
    /// </summary>
    /// <param name="field">Pointer to FieldInfo</param>
    /// <returns>Field offset in bytes, or -1 on error</returns>
    MDB_API int mdb_get_field_offset(void* field);
    
    /// <summary>
    /// Get the value of an instance field.
    /// </summary>
    /// <param name="instance">Pointer to object instance</param>
    /// <param name="field">Pointer to FieldInfo</param>
    /// <param name="out_value">Output buffer for the value</param>
    MDB_API void mdb_field_get_value(void* instance, void* field, void* out_value);
    
    /// <summary>
    /// Set the value of an instance field.
    /// </summary>
    /// <param name="instance">Pointer to object instance</param>
    /// <param name="field">Pointer to FieldInfo</param>
    /// <param name="value">Pointer to the new value</param>
    MDB_API void mdb_field_set_value(void* instance, void* field, void* value);
    
    /// <summary>
    /// Get the value of a static field.
    /// </summary>
    /// <param name="field">Pointer to FieldInfo</param>
    /// <param name="out_value">Output buffer for the value</param>
    MDB_API void mdb_field_static_get_value(void* field, void* out_value);
    
    /// <summary>
    /// Set the value of a static field.
    /// </summary>
    /// <param name="field">Pointer to FieldInfo</param>
    /// <param name="value">Pointer to the new value</param>
    MDB_API void mdb_field_static_set_value(void* field, void* value);
    
    // ==============================
    // Object Creation
    // ==============================
    
    /// <summary>
    /// Allocate a new IL2CPP object of the given class.
    /// Note: This does NOT call the constructor.
    /// </summary>
    /// <param name="klass">Pointer to Il2CppClass</param>
    /// <returns>Pointer to new object, or nullptr on failure</returns>
    MDB_API void* mdb_object_new(void* klass);
    
    /// <summary>
    /// Create a new IL2CPP string from a UTF-8 C string.
    /// </summary>
    /// <param name="str">UTF-8 string</param>
    /// <returns>Pointer to Il2CppString, or nullptr on failure</returns>
    MDB_API void* mdb_string_new(const char* str);
    
    /// <summary>
    /// Convert an IL2CPP string to UTF-8.
    /// </summary>
    /// <param name="str">Pointer to Il2CppString</param>
    /// <param name="buffer">Output buffer for UTF-8 string</param>
    /// <param name="buffer_size">Size of buffer in bytes</param>
    /// <returns>Number of bytes written (excluding null terminator), or -1 on error</returns>
    MDB_API int mdb_string_to_utf8(void* str, char* buffer, int buffer_size);
    
    // ==============================
    // Utilities
    // ==============================
    
    /// <summary>
    /// Get the class of an object instance.
    /// </summary>
    /// <param name="instance">Pointer to object instance</param>
    /// <returns>Pointer to Il2CppClass, or nullptr on error</returns>
    MDB_API void* mdb_object_get_class(void* instance);
    
    /// <summary>
    /// Get the name of a class.
    /// </summary>
    /// <param name="klass">Pointer to Il2CppClass</param>
    /// <returns>Class name string, or nullptr on error</returns>
    MDB_API const char* mdb_class_get_name(void* klass);
    
    /// <summary>
    /// Get the namespace of a class.
    /// </summary>
    /// <param name="klass">Pointer to Il2CppClass</param>
    /// <returns>Namespace string, or nullptr on error</returns>
    MDB_API const char* mdb_class_get_namespace(void* klass);
    
    /// <summary>
    /// Get the Il2CppType* from a class.
    /// </summary>
    /// <param name="klass">Pointer to Il2CppClass</param>
    /// <returns>Pointer to Il2CppType, or nullptr on error</returns>
    MDB_API void* mdb_class_get_type(void* klass);
    
    /// <summary>
    /// Get a System.Type reflection object from an Il2CppType*.
    /// </summary>
    /// <param name="il2cpp_type">Pointer to Il2CppType</param>
    /// <returns>Pointer to Il2CppReflectionType (System.Type), or nullptr on error</returns>
    MDB_API void* mdb_type_get_object(void* il2cpp_type);
    
    // ==============================
    // Array Helpers
    // ==============================
    
    /// <summary>
    /// Get the length of an IL2CPP array.
    /// </summary>
    /// <param name="array">Pointer to IL2CPP array</param>
    /// <returns>Array length, or 0 on error</returns>
    MDB_API int mdb_array_length(void* array);
    
    /// <summary>
    /// Get an element from an IL2CPP array.
    /// </summary>
    /// <param name="array">Pointer to IL2CPP array</param>
    /// <param name="index">Element index</param>
    /// <returns>Pointer to element, or nullptr on error</returns>
    MDB_API void* mdb_array_get_element(void* array, int index);
    
    /// <summary>
    /// Get the element class of an array.
    /// </summary>
    /// <param name="array">Pointer to IL2CPP array</param>
    /// <returns>Pointer to the element's Il2CppClass, or nullptr on error</returns>
    MDB_API void* mdb_array_get_element_class(void* array);
    
    /// <summary>
    /// Check if a class is a value type (struct).
    /// </summary>
    /// <param name="klass">Pointer to Il2CppClass</param>
    /// <returns>1 if value type, 0 if reference type, -1 on error</returns>
    MDB_API int mdb_class_is_valuetype(void* klass);
    
    /// <summary>
    /// Get the element class of a class (for arrays, this is the element type).
    /// </summary>
    /// <param name="klass">Pointer to Il2CppClass</param>
    /// <returns>Pointer to the element's Il2CppClass, or nullptr</returns>
    MDB_API void* mdb_class_get_element_class(void* klass);
    
    /// <summary>
    /// Get the last error message from the bridge.
    /// </summary>
    /// <returns>Error message string (valid until next bridge call)</returns>
    MDB_API const char* mdb_get_last_error();
    
    /// <summary>
    /// Get the last error code from the bridge.
    /// </summary>
    /// <returns>MdbErrorCode value</returns>
    MDB_API int mdb_get_last_error_code();
    
    // ==============================
    // GameObject Component Helpers
    // ==============================
    
    /// <summary>
    /// Get all components on a GameObject.
    /// This is a specialized function that handles the tricky GetComponents call correctly.
    /// </summary>
    /// <param name="gameObject">Pointer to GameObject IL2CPP object</param>
    /// <returns>Pointer to Component[] array, or nullptr on failure</returns>
    MDB_API void* mdb_gameobject_get_components(void* gameObject);
    
    /// <summary>
    /// Get the number of components on a GameObject (from the returned array).
    /// </summary>
    /// <param name="componentsArray">Pointer to Component[] returned by mdb_gameobject_get_components</param>
    /// <returns>Number of components, or 0 on error</returns>
    MDB_API int mdb_components_array_length(void* componentsArray);
    
    /// <summary>
    /// Get a component from the array at the specified index.
    /// </summary>
    /// <param name="componentsArray">Pointer to Component[] returned by mdb_gameobject_get_components</param>
    /// <param name="index">Index into the array</param>
    /// <returns>Pointer to Component, or nullptr on error</returns>
    MDB_API void* mdb_components_array_get(void* componentsArray, int index);

    /// <summary>
    /// Set the active state of a GameObject.
    /// This properly handles the bool parameter boxing for IL2CPP.
    /// </summary>
    /// <param name="gameObject">Pointer to GameObject IL2CPP object</param>
    /// <param name="active">Whether to activate (true) or deactivate (false)</param>
    /// <returns>true on success, false on failure</returns>
    MDB_API bool mdb_gameobject_set_active(void* gameObject, bool active);

    /// <summary>
    /// Get the scene handle that a GameObject belongs to.
    /// Can be used to identify DontDestroyOnLoad objects (their scene handle won't match any loaded scene).
    /// </summary>
    /// <param name="gameObject">Pointer to GameObject IL2CPP object</param>
    /// <returns>Scene handle, or 0 on error</returns>
    MDB_API int mdb_gameobject_get_scene_handle(void* gameObject);

    /// <summary>
    /// Get the activeSelf state of a GameObject.
    /// Properly unboxes the bool return value from IL2CPP.
    /// </summary>
    /// <param name="gameObject">Pointer to GameObject IL2CPP object</param>
    /// <returns>true if active, false if inactive or on error</returns>
    MDB_API bool mdb_gameobject_get_active_self(void* gameObject);

    // ==============================
    // Transform Helpers
    // ==============================

    /// <summary>
    /// Get the child count of a Transform.
    /// Properly unboxes the int return value from IL2CPP.
    /// </summary>
    /// <param name="transform">Pointer to Transform IL2CPP object</param>
    /// <returns>Number of children, or 0 on error</returns>
    MDB_API int mdb_transform_get_child_count(void* transform);

    /// <summary>
    /// Get a child Transform at the specified index.
    /// </summary>
    /// <param name="transform">Pointer to Transform IL2CPP object</param>
    /// <param name="index">Index of the child</param>
    /// <returns>Pointer to child Transform, or nullptr on error</returns>
    MDB_API void* mdb_transform_get_child(void* transform, int index);

    // ==============================
    // Transform Property Accessors
    // ==============================

    /// <summary>
    /// Get the local position of a Transform.
    /// </summary>
    MDB_API bool mdb_transform_get_local_position(void* transform, float* outX, float* outY, float* outZ);

    /// <summary>
    /// Set the local position of a Transform.
    /// </summary>
    MDB_API bool mdb_transform_set_local_position(void* transform, float x, float y, float z);

    /// <summary>
    /// Get the local euler angles (rotation) of a Transform.
    /// </summary>
    MDB_API bool mdb_transform_get_local_euler_angles(void* transform, float* outX, float* outY, float* outZ);

    /// <summary>
    /// Set the local euler angles (rotation) of a Transform.
    /// </summary>
    MDB_API bool mdb_transform_set_local_euler_angles(void* transform, float x, float y, float z);

    /// <summary>
    /// Get the local scale of a Transform.
    /// </summary>
    MDB_API bool mdb_transform_get_local_scale(void* transform, float* outX, float* outY, float* outZ);

    /// <summary>
    /// Set the local scale of a Transform.
    /// </summary>
    MDB_API bool mdb_transform_set_local_scale(void* transform, float x, float y, float z);

    // ==============================
    // SceneManager Helpers
    // ==============================

    /// <summary>
    /// Get the number of loaded scenes.
    /// Properly unboxes the int return value from IL2CPP.
    /// </summary>
    /// <returns>Number of loaded scenes, or 0 on error</returns>
    MDB_API int mdb_scenemanager_get_scene_count();

    /// <summary>
    /// Get the name of a scene at the specified index.
    /// </summary>
    /// <param name="sceneIndex">Index of the scene (0 to sceneCount-1)</param>
    /// <param name="buffer">Buffer to write the scene name to</param>
    /// <param name="bufferSize">Size of the buffer</param>
    /// <returns>Length of the name written, or 0 on error</returns>
    MDB_API int mdb_scenemanager_get_scene_name(int sceneIndex, char* buffer, int bufferSize);

    /// <summary>
    /// Get the handle of a scene at the specified index.
    /// </summary>
    /// <param name="sceneIndex">Index of the scene (0 to sceneCount-1)</param>
    /// <returns>Scene handle, or -1 on error</returns>
    MDB_API int mdb_scenemanager_get_scene_handle(int sceneIndex);

    /// <summary>
    /// Get the root GameObject count of a scene at the specified index.
    /// </summary>
    /// <param name="sceneIndex">Index of the scene (0 to sceneCount-1)</param>
    /// <returns>Number of root GameObjects in the scene, or 0 on error</returns>
    MDB_API int mdb_scenemanager_get_scene_root_count(int sceneIndex);

    /// <summary>
    /// Get the DontDestroyOnLoad scene handle.
    /// </summary>
    /// <returns>Scene handle for DDOL scene, or -1 if not found</returns>
    MDB_API int mdb_get_dontdestroyonload_scene_handle();

    // ==============================
    // OnGUI Hooking
    // ==============================
    
    /// <summary>
    /// Callback type for OnGUI dispatch.
    /// </summary>
    typedef void (*OnGUICallbackFn)();
    
    /// <summary>
    /// Register a callback to be called during Unity's OnGUI phase.
    /// This hooks into Unity's GUI system and dispatches to the managed callback.
    /// </summary>
    /// <param name="callback">The callback function to call during OnGUI</param>
    /// <returns>0 on success, non-zero on failure</returns>
    MDB_API int mdb_register_ongui_callback(OnGUICallbackFn callback);
    
    /// <summary>
    /// Manually trigger OnGUI dispatch (for testing or manual hooking).
    /// Call this from a hooked Unity method during the GUI phase.
    /// </summary>
    MDB_API void mdb_dispatch_ongui();
    
    /// <summary>
    /// Install OnGUI hook by hooking GUIUtility.CheckOnGUI or similar.
    /// This attempts to automatically hook Unity's GUI phase.
    /// </summary>
    /// <returns>0 on success, non-zero on failure</returns>
    MDB_API int mdb_install_ongui_hook();
    
    /// <summary>
    /// Get the name of the method that was hooked for OnGUI.
    /// </summary>
    /// <returns>Method name string, or empty if not hooked</returns>
    MDB_API const char* mdb_get_hooked_method();

    // ==============================
    // Generic Method Hooking
    // ==============================
    
    /// <summary>
    /// Hook callback function signature.
    /// Called when a hooked method is invoked.
    /// </summary>
    /// <param name="instance">The object instance (nullptr for static methods)</param>
    /// <param name="args">Array of argument pointers</param>
    /// <param name="original">Pointer to call the original method</param>
    /// <returns>The return value, or nullptr for void methods</returns>
    typedef void* (*HookCallbackFn)(void* instance, void** args, void* original);
    
    /// <summary>
    /// Create a hook on an IL2CPP method.
    /// </summary>
    /// <param name="method">Pointer to MethodInfo to hook</param>
    /// <param name="callback">The callback function to invoke instead</param>
    /// <param name="out_original">Output: pointer to trampoline for calling original</param>
    /// <returns>Hook handle (>0 on success), or negative error code</returns>
    MDB_API int64_t mdb_create_hook(void* method, HookCallbackFn callback, void** out_original);
    
    /// <summary>
    /// Create a hook on a method by its RVA offset.
    /// </summary>
    /// <param name="rva">The RVA offset of the method</param>
    /// <param name="callback">The callback function to invoke instead</param>
    /// <param name="out_original">Output: pointer to trampoline for calling original</param>
    /// <returns>Hook handle (>0 on success), or negative error code</returns>
    MDB_API int64_t mdb_create_hook_rva(uint64_t rva, HookCallbackFn callback, void** out_original);
    
    /// <summary>
    /// Create a hook on a method by direct function pointer.
    /// </summary>
    /// <param name="target">The target function pointer to hook</param>
    /// <param name="detour">The detour function pointer</param>
    /// <param name="out_original">Output: pointer to trampoline for calling original</param>
    /// <returns>Hook handle (>0 on success), or negative error code</returns>
    MDB_API int64_t mdb_create_hook_ptr(void* target, void* detour, void** out_original);
    
    /// <summary>
    /// Remove a hook by its handle.
    /// </summary>
    /// <param name="hook_handle">The hook handle returned by mdb_create_hook*</param>
    /// <returns>0 on success, non-zero on failure</returns>
    MDB_API int mdb_remove_hook(int64_t hook_handle);
    
    /// <summary>
    /// Enable or disable a hook temporarily.
    /// </summary>
    /// <param name="hook_handle">The hook handle</param>
    /// <param name="enabled">true to enable, false to disable</param>
    /// <returns>0 on success, non-zero on failure</returns>
    MDB_API int mdb_set_hook_enabled(int64_t hook_handle, bool enabled);
    
    /// <summary>
    /// Get information about an IL2CPP method.
    /// </summary>
    /// <param name="method">Pointer to MethodInfo</param>
    /// <param name="out_param_count">Output: number of parameters</param>
    /// <param name="out_is_static">Output: true if static method</param>
    /// <param name="out_has_return">Output: true if method has return value</param>
    /// <returns>0 on success, non-zero on failure</returns>
    MDB_API int mdb_get_method_info(void* method, int* out_param_count, bool* out_is_static, bool* out_has_return);
    
    /// <summary>
    /// Get the method name.
    /// </summary>
    /// <param name="method">Pointer to MethodInfo</param>
    /// <returns>Method name string, or nullptr on error</returns>
    MDB_API const char* mdb_method_get_name(void* method);
    
    // ==============================
    // ImGui Integration
    // ==============================
    
    /// <summary>
    /// DirectX version enumeration.
    /// </summary>
    enum MdbDxVersion {
        MDB_DX_UNKNOWN = 0,
        MDB_DX_11 = 11,
        MDB_DX_12 = 12
    };
    
    /// <summary>
    /// Get the detected DirectX version.
    /// </summary>
    /// <returns>MDB_DX_11, MDB_DX_12, or MDB_DX_UNKNOWN</returns>
    MDB_API MdbDxVersion mdb_imgui_get_dx_version();
    
    /// <summary>
    /// Initialize ImGui with auto-detected DirectX version.
    /// Hooks the game's Present function and sets up rendering.
    /// </summary>
    /// <returns>true on success, false on failure</returns>
    MDB_API bool mdb_imgui_init();
    
    /// <summary>
    /// Shutdown ImGui and remove all hooks.
    /// </summary>
    MDB_API void mdb_imgui_shutdown();
    
    /// <summary>
    /// Check if ImGui is initialized and ready.
    /// </summary>
    /// <returns>true if initialized</returns>
    MDB_API bool mdb_imgui_is_initialized();
    
    /// <summary>
    /// Callback type for custom draw functions.
    /// Called each frame during Present hook, after ImGui::NewFrame().
    /// </summary>
    typedef void (*MdbImGuiDrawCallback)();
    
    /// <summary>
    /// Register a draw callback to be called each frame.
    /// </summary>
    /// <param name="callback">Function pointer to call each frame</param>
    MDB_API void mdb_imgui_register_draw_callback(MdbImGuiDrawCallback callback);
    
    /// <summary>
    /// Enable or disable ImGui input capture.
    /// When disabled, input passes through to the game.
    /// </summary>
    /// <param name="enabled">true to capture input, false to pass through</param>
    MDB_API void mdb_imgui_set_input_enabled(bool enabled);
    
    /// <summary>
    /// Check if ImGui input capture is enabled.
    /// </summary>
    /// <returns>true if input is being captured</returns>
    MDB_API bool mdb_imgui_is_input_enabled();
    
    /// <summary>
    /// Set the keyboard key used to toggle ImGui visibility/input.
    /// Default is VK_F2 (0x71).
    /// </summary>
    /// <param name="vkCode">Virtual key code</param>
    MDB_API void mdb_imgui_set_toggle_key(int vkCode);

    // ==============================
    // Reflection Helpers for Component Inspector
    // ==============================

    /// <summary>Get the number of fields in a class.</summary>
    MDB_API int mdb_class_get_field_count(void* klass);

    /// <summary>Get a field by index.</summary>
    MDB_API void* mdb_class_get_field_by_index(void* klass, int index);

    /// <summary>Get field name.</summary>
    MDB_API const char* mdb_field_get_name(void* field);

    /// <summary>Get field type.</summary>
    MDB_API void* mdb_field_get_type(void* field);

    /// <summary>Check if field is static.</summary>
    MDB_API bool mdb_field_is_static(void* field);

    /// <summary>Get type name from Il2CppType.</summary>
    MDB_API const char* mdb_type_get_name(void* type);

    /// <summary>Get type enum (IL2CPP_TYPE_*).</summary>
    MDB_API int mdb_type_get_type_enum(void* type);

    /// <summary>Get class from type.</summary>
    MDB_API void* mdb_type_get_class(void* type);

    /// <summary>Check if type is value type.</summary>
    MDB_API bool mdb_type_is_valuetype(void* type);

    /// <summary>Get the number of properties in a class.</summary>
    MDB_API int mdb_class_get_property_count(void* klass);

    /// <summary>Get a property by index.</summary>
    MDB_API void* mdb_class_get_property_by_index(void* klass, int index);

    /// <summary>Get property name.</summary>
    MDB_API const char* mdb_property_get_name(void* prop);

    /// <summary>Get property getter method.</summary>
    MDB_API void* mdb_property_get_get_method(void* prop);

    /// <summary>Get property setter method.</summary>
    MDB_API void* mdb_property_get_set_method(void* prop);

    /// <summary>Get the number of methods in a class.</summary>
    MDB_API int mdb_class_get_method_count(void* klass);

    /// <summary>Get a method by index.</summary>
    MDB_API void* mdb_class_get_method_by_index(void* klass, int index);

    /// <summary>Get method name.</summary>
    MDB_API const char* mdb_method_get_name_str(void* method);

    /// <summary>Get method parameter count.</summary>
    MDB_API int mdb_method_get_param_count(void* method);

    /// <summary>Get method return type.</summary>
    MDB_API void* mdb_method_get_return_type(void* method);

    /// <summary>Get method flags.</summary>
    MDB_API int mdb_method_get_flags(void* method);

    /// <summary>Get parent class.</summary>
    MDB_API void* mdb_class_get_parent(void* klass);

    /// <summary>Get field value directly (for primitives).</summary>
    MDB_API bool mdb_field_get_value_direct(void* instance, void* field, void* outBuffer, int bufferSize);

    /// <summary>Set field value directly (for primitives).</summary>
    MDB_API bool mdb_field_set_value_direct(void* instance, void* field, void* value, int valueSize);

    // ==============================
    // Hook Debugging
    // ==============================

    /// <summary>
    /// Debug info structure for a hook.
    /// </summary>
    struct MdbHookDebugInfo {
        int64_t handle;
        void* target;
        void* detour;
        void* trampoline;
        bool enabled;
        char description[256];
    };

    /// <summary>
    /// Enable or disable verbose hook debugging.
    /// When enabled, hooks will log detailed information about each call.
    /// </summary>
    /// <param name="enabled">true to enable debug logging</param>
    MDB_API void mdb_hook_set_debug_enabled(bool enabled);

    /// <summary>
    /// Check if hook debugging is enabled.
    /// </summary>
    MDB_API bool mdb_hook_is_debug_enabled();

    /// <summary>
    /// Get the number of active hooks.
    /// </summary>
    MDB_API int mdb_hook_get_count();

    /// <summary>
    /// Get debug info for a hook by index.
    /// </summary>
    /// <param name="index">Index of the hook (0 to count-1)</param>
    /// <param name="out_info">Output structure to fill</param>
    /// <returns>0 on success, non-zero on failure</returns>
    MDB_API int mdb_hook_get_debug_info(int index, MdbHookDebugInfo* out_info);

    /// <summary>
    /// Dump all hook info to the debug log.
    /// </summary>
    MDB_API void mdb_hook_dump_all();

    /// <summary>
    /// Create a debug hook with signature information for tracing.
    /// This creates a hook and logs detailed information about the calling convention.
    /// </summary>
    /// <param name="target">Target function pointer</param>
    /// <param name="detour">Detour function pointer</param>
    /// <param name="out_original">Output: trampoline pointer</param>
    /// <param name="signature">Parameter signature (P=ptr, F=float, D=double)</param>
    /// <param name="description">Description for logging</param>
    /// <returns>Hook handle (>0 on success), or negative error code</returns>
    MDB_API int64_t mdb_create_hook_debug(void* target, void* detour, void** out_original, 
                                           const char* signature, const char* description);

    /// <summary>
    /// Validate that a trampoline function works correctly by doing a test call.
    /// This helps diagnose issues with float parameter passing.
    /// </summary>
    /// <param name="trampoline">The trampoline function pointer</param>
    /// <param name="signature">Expected signature (P=ptr, F=float, D=double)</param>
    /// <returns>true if validation passes, false otherwise</returns>
    MDB_API bool mdb_hook_validate_trampoline(void* trampoline, const char* signature);

    /// <summary>
    /// Log a hook call for debugging purposes.
    /// Call this from your detour to trace execution.
    /// </summary>
    /// <param name="hook_handle">The hook handle</param>
    /// <param name="arg0">First argument (instance or first param)</param>
    /// <param name="arg1_float">Second argument as float (if applicable)</param>
    /// <param name="arg2_float">Third argument as float (if applicable)</param>
    MDB_API void mdb_hook_log_call(int64_t hook_handle, void* arg0, float arg1_float, float arg2_float);
}

