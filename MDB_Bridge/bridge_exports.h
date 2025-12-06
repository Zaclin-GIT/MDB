#pragma once
// ==============================
// MDB Bridge - Export Declarations
// ==============================
// This header defines all exported functions that can be called via P/Invoke from C#

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
}
