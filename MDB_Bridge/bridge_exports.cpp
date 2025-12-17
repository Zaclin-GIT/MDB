// ==============================
// MDB Bridge - Export Implementations
// ==============================
// Implements all P/Invoke exported functions using il2cpp_resolver.hpp

#include "bridge_exports.h"
#include "il2cpp_resolver.hpp"

#include <string>
#include <mutex>
#include <cstdio>

// MinHook for function hooking
// Include from local minhook folder
#if __has_include("minhook/include/MinHook.h")
#include "minhook/include/MinHook.h"
#define MDB_HAS_MINHOOK 1
#elif __has_include(<MinHook.h>)
#include <MinHook.h>
#define MDB_HAS_MINHOOK 1
#else
#define MDB_HAS_MINHOOK 0
#endif

// Debug logging - writes to both debugger output and a log file
static FILE* g_log_file = nullptr;
static std::mutex g_log_mutex;

static void mdb_debug_log(const char* fmt, ...) {
    std::lock_guard<std::mutex> lock(g_log_mutex);
    
    char buffer[1024];
    va_list args;
    va_start(args, fmt);
    vsnprintf(buffer, sizeof(buffer), fmt, args);
    va_end(args);
    
    // Output to debugger
    OutputDebugStringA("[MDB_Bridge] ");
    OutputDebugStringA(buffer);
    OutputDebugStringA("\n");
    
    // // Also write to log file
    // if (!g_log_file) {
    //     g_log_file = fopen("mdb_bridge_debug.log", "w");
    // }
    // if (g_log_file) {
    //     fprintf(g_log_file, "[MDB_Bridge] %s\n", buffer);
    //     fflush(g_log_file);
    // }
}

// Thread-local error storage
static thread_local std::string g_last_error;
static thread_local MdbErrorCode g_last_error_code = MdbErrorCode::Success;

static void set_error(MdbErrorCode code, const char* msg) {
    g_last_error_code = code;
    g_last_error = msg ? msg : "Unknown error";
}

static void set_error(MdbErrorCode code, Il2CppStatus status) {
    g_last_error_code = code;
    g_last_error = to_string(status);
}

static void clear_error() {
    g_last_error_code = MdbErrorCode::Success;
    g_last_error.clear();
}

// ==============================
// Initialization
// ==============================

MDB_API int mdb_init() {
    clear_error();
    auto result = il2cpp::init();
    if (result != Il2CppStatus::OK) {
        set_error(MdbErrorCode::InitFailed, result);
        return static_cast<int>(MdbErrorCode::InitFailed);
    }
    return 0;
}

MDB_API void* mdb_domain_get() {
    clear_error();
    auto status = il2cpp::_internal::ensure_exports();
    if (status != Il2CppStatus::OK) {
        set_error(MdbErrorCode::NotInitialized, status);
        return nullptr;
    }
    
    if (!il2cpp::_internal::il2cpp_domain_get) {
        set_error(MdbErrorCode::ExportNotFound, "il2cpp_domain_get not available");
        return nullptr;
    }
    
    return il2cpp::_internal::il2cpp_domain_get();
}

MDB_API void* mdb_thread_attach(void* domain) {
    clear_error();
    auto status = il2cpp::_internal::ensure_exports();
    if (status != Il2CppStatus::OK) {
        set_error(MdbErrorCode::NotInitialized, status);
        return nullptr;
    }
    
    if (!il2cpp::_internal::il2cpp_thread_attach) {
        set_error(MdbErrorCode::ExportNotFound, "il2cpp_thread_attach not available");
        return nullptr;
    }
    
    return il2cpp::_internal::il2cpp_thread_attach(domain);
}

// ==============================
// Class Resolution
// ==============================

MDB_API void* mdb_find_class(const char* assembly, const char* ns, const char* name) {
    clear_error();
    if (!assembly || !ns || !name) {
        set_error(MdbErrorCode::InvalidArgument, "Invalid arguments: assembly, ns, and name are required");
        return nullptr;
    }
    
    auto result = il2cpp::find_class(ns, name, assembly);
    if (!result) {
        set_error(MdbErrorCode::ClassNotFound, result.status);
        return nullptr;
    }
    
    return result.value;
}

MDB_API int mdb_get_class_size(void* klass) {
    clear_error();
    if (!klass) {
        set_error(MdbErrorCode::NullPointer, "Invalid argument: klass is null");
        return -1;
    }
    
    auto result = il2cpp::get_class_size(
        reinterpret_cast<il2cpp::_internal::unity_structs::il2cppClass*>(klass)
    );
    
    if (!result) {
        set_error(MdbErrorCode::InvalidClass, result.status);
        return -1;
    }
    
    return static_cast<int>(result.value);
}

// ==============================
// Method Resolution & Invocation
// ==============================

MDB_API void* mdb_get_method(void* klass, const char* name, int param_count) {
    clear_error();
    if (!klass || !name) {
        set_error(MdbErrorCode::InvalidArgument, "Invalid arguments: klass and name are required");
        return nullptr;
    }
    
    auto status = il2cpp::_internal::ensure_exports();
    if (status != Il2CppStatus::OK) {
        set_error(MdbErrorCode::NotInitialized, status);
        return nullptr;
    }
    
    auto* il2cpp_klass = reinterpret_cast<il2cpp::_internal::unity_structs::il2cppClass*>(klass);
    
    il2cpp::_internal::unity_structs::il2cppMethodInfo* method = nullptr;
    
    if (param_count >= 0) {
        method = il2cpp::_internal::il2cpp_class_get_method_from_name(il2cpp_klass, name, param_count);
    } else {
        // Search all parameter counts (0-16)
        for (int i = 0; i <= 16 && !method; ++i) {
            method = il2cpp::_internal::il2cpp_class_get_method_from_name(il2cpp_klass, name, i);
        }
    }
    
    if (!method) {
        set_error(MdbErrorCode::MethodNotFound, "Method not found");
        return nullptr;
    }
    
    return method;
}

MDB_API void* mdb_get_method_pointer(void* method) {
    clear_error();
    if (!method) {
        set_error(MdbErrorCode::NullPointer, "Invalid argument: method is null");
        return nullptr;
    }
    
    auto* mi = reinterpret_cast<il2cpp::_internal::unity_structs::il2cppMethodInfo*>(method);
    return mi->m_pMethodPointer;
}

MDB_API void* mdb_invoke_method(void* method, void* instance, void** args, void** exception) {
    clear_error();
    if (!method) {
        set_error(MdbErrorCode::NullPointer, "Invalid argument: method is null");
        return nullptr;
    }
    
    auto status = il2cpp::ensure_thread_attached();
    if (status != Il2CppStatus::OK) {
        set_error(MdbErrorCode::ThreadNotAttached, status);
        return nullptr;
    }
    
    // Clear exception output
    if (exception) {
        *exception = nullptr;
    }
    
    // Use il2cpp_runtime_invoke to properly call the method
    // This handles all the IL2CPP calling conventions correctly
    static auto il2cpp_runtime_invoke_fn = reinterpret_cast<void*(*)(void*, void*, void**, void**)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_runtime_invoke")
    );
    
    if (!il2cpp_runtime_invoke_fn) {
        set_error(MdbErrorCode::ExportNotFound, "il2cpp_runtime_invoke not available");
        return nullptr;
    }
    
    void* exc = nullptr;
    void* result = il2cpp_runtime_invoke_fn(method, instance, args, &exc);
    
    if (exc) {
        set_error(MdbErrorCode::ExceptionThrown, "IL2CPP exception thrown during invocation");
        if (exception) {
            *exception = exc;
        }
    }
    
    return result;
}

// ==============================
// Field Access
// ==============================

MDB_API void* mdb_get_field(void* klass, const char* name) {
    clear_error();
    if (!klass || !name) {
        set_error(MdbErrorCode::InvalidArgument, "Invalid arguments: klass and name are required");
        return nullptr;
    }
    
    auto status = il2cpp::_internal::ensure_exports();
    if (status != Il2CppStatus::OK) {
        set_error(MdbErrorCode::NotInitialized, status);
        return nullptr;
    }
    
    auto* il2cpp_klass = reinterpret_cast<il2cpp::_internal::unity_structs::il2cppClass*>(klass);
    auto* field = il2cpp::_internal::il2cpp_class_get_field_from_name(il2cpp_klass, name);
    
    if (!field) {
        set_error(MdbErrorCode::FieldNotFound, "Field not found");
        return nullptr;
    }
    
    return field;
}

MDB_API int mdb_get_field_offset(void* field) {
    clear_error();
    if (!field) {
        set_error(MdbErrorCode::NullPointer, "Invalid argument: field is null");
        return -1;
    }
    
    auto* fi = reinterpret_cast<il2cpp::_internal::unity_structs::il2cppFieldInfo*>(field);
    return fi->m_iOffset;
}

MDB_API void mdb_field_get_value(void* instance, void* field, void* out_value) {
    clear_error();
    if (!instance || !field || !out_value) {
        set_error(MdbErrorCode::InvalidArgument, "Invalid arguments");
        return;
    }
    
    auto status = il2cpp::_internal::ensure_exports();
    if (status != Il2CppStatus::OK) {
        set_error(MdbErrorCode::NotInitialized, status);
        return;
    }
    
    auto* fi = reinterpret_cast<il2cpp::_internal::unity_structs::il2cppFieldInfo*>(field);
    il2cpp::_internal::il2cpp_field_get_value(instance, fi, out_value);
}

MDB_API void mdb_field_set_value(void* instance, void* field, void* value) {
    clear_error();
    if (!instance || !field || !value) {
        set_error(MdbErrorCode::InvalidArgument, "Invalid arguments");
        return;
    }
    
    auto status = il2cpp::_internal::ensure_exports();
    if (status != Il2CppStatus::OK) {
        set_error(MdbErrorCode::NotInitialized, status);
        return;
    }
    
    auto* fi = reinterpret_cast<il2cpp::_internal::unity_structs::il2cppFieldInfo*>(field);
    il2cpp::_internal::il2cpp_field_set_value(instance, fi, value);
}

MDB_API void mdb_field_static_get_value(void* field, void* out_value) {
    clear_error();
    if (!field || !out_value) {
        set_error(MdbErrorCode::InvalidArgument, "Invalid arguments");
        return;
    }
    
    auto status = il2cpp::_internal::ensure_exports();
    if (status != Il2CppStatus::OK) {
        set_error(MdbErrorCode::NotInitialized, status);
        return;
    }
    
    auto* fi = reinterpret_cast<il2cpp::_internal::unity_structs::il2cppFieldInfo*>(field);
    il2cpp::_internal::il2cpp_field_static_get_value(fi, out_value);
}

MDB_API void mdb_field_static_set_value(void* field, void* value) {
    clear_error();
    if (!field || !value) {
        set_error(MdbErrorCode::InvalidArgument, "Invalid arguments");
        return;
    }
    
    auto status = il2cpp::_internal::ensure_exports();
    if (status != Il2CppStatus::OK) {
        set_error(MdbErrorCode::NotInitialized, status);
        return;
    }
    
    auto* fi = reinterpret_cast<il2cpp::_internal::unity_structs::il2cppFieldInfo*>(field);
    il2cpp::_internal::il2cpp_field_static_set_value(fi, value);
}

// ==============================
// Object Creation
// ==============================

MDB_API void* mdb_object_new(void* klass) {
    clear_error();
    if (!klass) {
        set_error(MdbErrorCode::NullPointer, "Invalid argument: klass is null");
        return nullptr;
    }
    
    auto status = il2cpp::_internal::ensure_exports();
    if (status != Il2CppStatus::OK) {
        set_error(MdbErrorCode::NotInitialized, status);
        return nullptr;
    }
    
    status = il2cpp::ensure_thread_attached();
    if (status != Il2CppStatus::OK) {
        set_error(MdbErrorCode::ThreadNotAttached, status);
        return nullptr;
    }
    
    auto* il2cpp_klass = reinterpret_cast<il2cpp::_internal::unity_structs::il2cppClass*>(klass);
    
    if (!il2cpp::_internal::il2cpp_object_new) {
        set_error(MdbErrorCode::ExportNotFound, "il2cpp_object_new not available");
        return nullptr;
    }
    
    void* obj = il2cpp::_internal::il2cpp_object_new(il2cpp_klass);
    if (!obj) {
        set_error(MdbErrorCode::AllocationFailed, "Failed to allocate object");
        return nullptr;
    }
    
    return obj;
}

MDB_API void* mdb_string_new(const char* str) {
    clear_error();
    if (!str) {
        set_error(MdbErrorCode::NullPointer, "Invalid argument: str is null");
        return nullptr;
    }
    
    auto result = il2cpp::String::CreateNewString(str);
    if (!result) {
        set_error(MdbErrorCode::AllocationFailed, result.status);
        return nullptr;
    }
    
    return result.value;
}

MDB_API int mdb_string_to_utf8(void* str, char* buffer, int buffer_size) {
    clear_error();
    if (!str || !buffer || buffer_size <= 0) {
        set_error(MdbErrorCode::InvalidArgument, "Invalid arguments");
        return -1;
    }
    
    std::string utf8 = il2cpp::String::convert_to_std_string(str);
    if (utf8.empty()) {
        buffer[0] = '\0';
        return 0;
    }
    
    int copy_size = static_cast<int>(utf8.size());
    if (copy_size >= buffer_size) {
        set_error(MdbErrorCode::BufferTooSmall, "Buffer too small for string");
        copy_size = buffer_size - 1;
    }
    
    memcpy(buffer, utf8.c_str(), copy_size);
    buffer[copy_size] = '\0';
    
    return copy_size;
}

// ==============================
// Utilities
// ==============================

MDB_API void* mdb_object_get_class(void* instance) {
    clear_error();
    if (!instance) {
        set_error(MdbErrorCode::NullPointer, "Invalid argument: instance is null");
        return nullptr;
    }
    
    auto* obj = reinterpret_cast<il2cpp::_internal::unity_structs::il2cppObject*>(instance);
    return obj->m_pClass;
}

MDB_API const char* mdb_class_get_name(void* klass) {
    clear_error();
    if (!klass) {
        set_error(MdbErrorCode::NullPointer, "Invalid argument: klass is null");
        return nullptr;
    }
    
    auto* il2cpp_klass = reinterpret_cast<il2cpp::_internal::unity_structs::il2cppClass*>(klass);
    return il2cpp_klass->m_pName;
}

MDB_API const char* mdb_class_get_namespace(void* klass) {
    clear_error();
    if (!klass) {
        set_error(MdbErrorCode::NullPointer, "Invalid argument: klass is null");
        return nullptr;
    }
    
    auto* il2cpp_klass = reinterpret_cast<il2cpp::_internal::unity_structs::il2cppClass*>(klass);
    return il2cpp_klass->m_pNamespace;
}

MDB_API void* mdb_class_get_type(void* klass) {
    clear_error();
    if (!klass) {
        set_error(MdbErrorCode::NullPointer, "Invalid argument: klass is null");
        return nullptr;
    }
    
    // Use il2cpp_class_get_type API
    static auto il2cpp_class_get_type_fn = reinterpret_cast<void*(*)(void*)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_class_get_type")
    );
    
    if (!il2cpp_class_get_type_fn) {
        set_error(MdbErrorCode::ExportNotFound, "il2cpp_class_get_type not available");
        return nullptr;
    }
    
    return il2cpp_class_get_type_fn(klass);
}

MDB_API void* mdb_type_get_object(void* il2cpp_type) {
    clear_error();
    if (!il2cpp_type) {
        set_error(MdbErrorCode::NullPointer, "Invalid argument: il2cpp_type is null");
        return nullptr;
    }
    
    // il2cpp_type_get_object returns a System.Type (Il2CppReflectionType*)
    // We need to find and call this IL2CPP API function
    static auto il2cpp_type_get_object_fn = reinterpret_cast<void*(*)(void*)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_type_get_object")
    );
    
    if (!il2cpp_type_get_object_fn) {
        set_error(MdbErrorCode::ExportNotFound, "il2cpp_type_get_object not available");
        return nullptr;
    }
    
    return il2cpp_type_get_object_fn(il2cpp_type);
}

// ==============================
// Array Helpers
// ==============================

// IL2CPP array structure (simplified)
struct Il2CppArraySize {
    void* klass;
    void* monitor;
    void* bounds;
    size_t max_length;
    void* vector[1];  // Flexible array member
};

MDB_API int mdb_array_length(void* array) {
    clear_error();
    if (!array) {
        set_error(MdbErrorCode::NullPointer, "Invalid argument: array is null");
        return 0;
    }
    
    auto* arr = reinterpret_cast<Il2CppArraySize*>(array);
    return static_cast<int>(arr->max_length);
}

MDB_API void* mdb_array_get_element(void* array, int index) {
    clear_error();
    if (!array) {
        set_error(MdbErrorCode::NullPointer, "Invalid argument: array is null");
        return nullptr;
    }
    
    auto* arr = reinterpret_cast<Il2CppArraySize*>(array);
    if (index < 0 || index >= static_cast<int>(arr->max_length)) {
        set_error(MdbErrorCode::InvalidArgument, "Index out of bounds");
        return nullptr;
    }
    
    return arr->vector[index];
}

MDB_API void* mdb_array_get_element_class(void* array) {
    clear_error();
    if (!array) {
        set_error(MdbErrorCode::NullPointer, "Invalid argument: array is null");
        return nullptr;
    }
    
    // Get the class of the array object
    auto* obj = reinterpret_cast<il2cpp::_internal::unity_structs::il2cppObject*>(array);
    if (!obj->m_pClass) {
        set_error(MdbErrorCode::InvalidClass, "Array has no class");
        return nullptr;
    }
    
    // Use the official IL2CPP API to get element class - struct offsets vary between versions
    static auto il2cpp_class_get_element_class_fn = reinterpret_cast<void*(*)(void*)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_class_get_element_class")
    );
    
    if (il2cpp_class_get_element_class_fn) {
        return il2cpp_class_get_element_class_fn(obj->m_pClass);
    }
    
    // Fallback to struct access if API not available (may not work on all IL2CPP versions)
    set_error(MdbErrorCode::ExportNotFound, "il2cpp_class_get_element_class API not found");
    return obj->m_pClass->m_pElementClass;
}

MDB_API int mdb_class_is_valuetype(void* klass) {
    clear_error();
    if (!klass) {
        set_error(MdbErrorCode::NullPointer, "Invalid argument: klass is null");
        return -1;
    }
    
    auto* il2cpp_klass = reinterpret_cast<il2cpp::_internal::unity_structs::il2cppClass*>(klass);
    
    // Check the valuetype flag - it's in the bitfield section
    // We need to use the IL2CPP API for this
    static auto il2cpp_class_is_valuetype_fn = reinterpret_cast<bool(*)(void*)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_class_is_valuetype")
    );
    
    if (il2cpp_class_is_valuetype_fn) {
        return il2cpp_class_is_valuetype_fn(klass) ? 1 : 0;
    }
    
    // Fallback: Check if parent is System.ValueType
    if (il2cpp_klass->m_pParentClass) {
        const char* parentName = il2cpp_klass->m_pParentClass->m_pName;
        if (parentName && strcmp(parentName, "ValueType") == 0) {
            return 1;
        }
    }
    
    return 0;
}

MDB_API void* mdb_class_get_element_class(void* klass) {
    clear_error();
    if (!klass) {
        set_error(MdbErrorCode::NullPointer, "Invalid argument: klass is null");
        return nullptr;
    }
    
    auto* il2cpp_klass = reinterpret_cast<il2cpp::_internal::unity_structs::il2cppClass*>(klass);
    return il2cpp_klass->m_pElementClass;
}

// ==============================
// RVA-based Method Access
// ==============================

MDB_API void* mdb_get_gameassembly_base() {
    clear_error();
    auto status = il2cpp::_internal::ensure_exports();
    if (status != Il2CppStatus::OK) {
        set_error(MdbErrorCode::NotInitialized, status);
        return nullptr;
    }
    
    return reinterpret_cast<void*>(il2cpp::_internal::p_game_assembly);
}

MDB_API void* mdb_get_method_pointer_from_rva(uint64_t rva) {
    clear_error();
    auto status = il2cpp::_internal::ensure_exports();
    if (status != Il2CppStatus::OK) {
        set_error(MdbErrorCode::NotInitialized, status);
        return nullptr;
    }
    
    if (!il2cpp::_internal::p_game_assembly) {
        set_error(MdbErrorCode::GameAssemblyNotFound, "GameAssembly.dll not loaded");
        return nullptr;
    }
    
    // Calculate absolute address: base + RVA
    auto base = reinterpret_cast<uintptr_t>(il2cpp::_internal::p_game_assembly);
    auto method_ptr = reinterpret_cast<void*>(base + rva);
    
    return method_ptr;
}

MDB_API const char* mdb_get_last_error() {
    return g_last_error.c_str();
}

MDB_API int mdb_get_last_error_code() {
    return static_cast<int>(g_last_error_code);
}

// ==============================
// GameObject Component Helpers
// ==============================

// Cached method info for GetComponents
static void* g_cached_getComponents_method = nullptr;
static void* g_cached_component_type_object = nullptr;
static bool g_getComponents_init_attempted = false;

MDB_API void* mdb_gameobject_get_components(void* gameObject) {
    mdb_debug_log("=== mdb_gameobject_get_components called ===");
    mdb_debug_log("gameObject ptr: %p", gameObject);
    clear_error();
    
    if (!gameObject) {
        mdb_debug_log("ERROR: gameObject is null");
        set_error(MdbErrorCode::NullPointer, "Invalid argument: gameObject is null");
        return nullptr;
    }
    
    // Validate the pointer looks reasonable (basic sanity check)
    // IL2CPP objects typically have a valid vtable pointer as their first field
    __try {
        void* vtable = *reinterpret_cast<void**>(gameObject);
        if (!vtable) {
            mdb_debug_log("ERROR: gameObject has null vtable - likely destroyed");
            set_error(MdbErrorCode::NullPointer, "GameObject appears to be destroyed (null vtable)");
            return nullptr;
        }
    } __except(EXCEPTION_EXECUTE_HANDLER) {
        mdb_debug_log("ERROR: Exception reading gameObject vtable - invalid pointer");
        set_error(MdbErrorCode::NullPointer, "GameObject pointer is invalid");
        return nullptr;
    }
    
    auto status = il2cpp::ensure_thread_attached();
    if (status != Il2CppStatus::OK) {
        mdb_debug_log("ERROR: Thread not attached, status=%d", (int)status);
        set_error(MdbErrorCode::ThreadNotAttached, status);
        return nullptr;
    }
    mdb_debug_log("Thread attached OK");
    
    // Get required il2cpp functions (cached as static)
    static auto il2cpp_class_get_method_from_name_fn = reinterpret_cast<void*(*)(void*, const char*, int)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_class_get_method_from_name")
    );
    static auto il2cpp_runtime_invoke_fn = reinterpret_cast<void*(*)(void*, void*, void**, void**)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_runtime_invoke")
    );
    static auto il2cpp_class_get_type_fn = reinterpret_cast<void*(*)(void*)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_class_get_type")
    );
    static auto il2cpp_type_get_object_fn = reinterpret_cast<void*(*)(void*)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_type_get_object")
    );
    
    mdb_debug_log("il2cpp functions: get_method=%p, invoke=%p, get_type=%p, type_get_object=%p",
        il2cpp_class_get_method_from_name_fn, il2cpp_runtime_invoke_fn, 
        il2cpp_class_get_type_fn, il2cpp_type_get_object_fn);
    
    if (!il2cpp_class_get_method_from_name_fn || !il2cpp_runtime_invoke_fn || 
        !il2cpp_class_get_type_fn || !il2cpp_type_get_object_fn) {
        mdb_debug_log("ERROR: Required IL2CPP exports not found");
        set_error(MdbErrorCode::ExportNotFound, "Required IL2CPP exports not found");
        return nullptr;
    }
    
    // Cache the Component type object and GetComponents method (only try once)
    if (!g_getComponents_init_attempted) {
        mdb_debug_log("First call - initializing cache...");
        g_getComponents_init_attempted = true;
        
        // Use mdb_find_class which is already proven to work
        mdb_debug_log("Looking for GameObject class...");
        void* gameObjectClass = mdb_find_class("UnityEngine.CoreModule", "UnityEngine", "GameObject");
        mdb_debug_log("gameObjectClass = %p", gameObjectClass);
        if (!gameObjectClass) {
            mdb_debug_log("ERROR: GameObject class not found");
            set_error(MdbErrorCode::ClassNotFound, "GameObject class not found via mdb_find_class");
            return nullptr;
        }
        
        mdb_debug_log("Looking for Component class...");
        void* componentClass = mdb_find_class("UnityEngine.CoreModule", "UnityEngine", "Component");
        mdb_debug_log("componentClass = %p", componentClass);
        if (!componentClass) {
            mdb_debug_log("ERROR: Component class not found");
            set_error(MdbErrorCode::ClassNotFound, "Component class not found via mdb_find_class");
            return nullptr;
        }
        
        // Get GetComponentsInternal method - the non-generic internal method
        // Signature: GetComponentsInternal(Type type, bool useSearchTypeAsArrayReturnType, 
        //                                  bool recursive, bool includeInactive, bool reverse, object resultList)
        mdb_debug_log("Looking for GetComponentsInternal method...");
        
        static auto il2cpp_class_get_methods_fn = reinterpret_cast<void*(*)(void*, void**)>(
            GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_class_get_methods")
        );
        static auto il2cpp_method_get_name_fn = reinterpret_cast<const char*(*)(void*)>(
            GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_method_get_name")
        );
        static auto il2cpp_method_get_param_count_fn = reinterpret_cast<uint32_t(*)(void*)>(
            GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_method_get_param_count")
        );
        static auto il2cpp_method_is_generic_fn = reinterpret_cast<bool(*)(void*)>(
            GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_method_is_generic")
        );
        
        // Find GetComponentsInternal (6 params, non-generic)
        if (il2cpp_class_get_methods_fn && il2cpp_method_get_name_fn && il2cpp_method_get_param_count_fn) {
            void* iter = nullptr;
            void* method;
            while ((method = il2cpp_class_get_methods_fn(gameObjectClass, &iter)) != nullptr) {
                const char* methodName = il2cpp_method_get_name_fn(method);
                if (methodName && strcmp(methodName, "GetComponentsInternal") == 0) {
                    uint32_t paramCount = il2cpp_method_get_param_count_fn(method);
                    bool isGeneric = il2cpp_method_is_generic_fn ? il2cpp_method_is_generic_fn(method) : false;
                    mdb_debug_log("    Found: %s (params=%u, generic=%d) @ %p", methodName, paramCount, isGeneric, method);
                    
                    // GetComponentsInternal has 6 params and is not generic
                    if (paramCount == 6 && !isGeneric) {
                        g_cached_getComponents_method = method;
                        mdb_debug_log("    >>> SELECTED THIS METHOD <<<");
                        break;
                    }
                }
            }
        }
        
        if (!g_cached_getComponents_method) {
            mdb_debug_log("ERROR: GetComponentsInternal method not found");
            set_error(MdbErrorCode::MethodNotFound, "GetComponentsInternal method not found");
            return nullptr;
        }
        
        mdb_debug_log("Final selected method: %p", g_cached_getComponents_method);
        
        // Create typeof(Component)
        mdb_debug_log("Creating typeof(Component)...");
        void* componentType = il2cpp_class_get_type_fn(componentClass);
        mdb_debug_log("  componentType (Il2CppType*) = %p", componentType);
        if (!componentType) {
            mdb_debug_log("ERROR: Failed to get Component Il2CppType");
            set_error(MdbErrorCode::InvalidClass, "Failed to get Component Il2CppType");
            return nullptr;
        }
        
        g_cached_component_type_object = il2cpp_type_get_object_fn(componentType);
        mdb_debug_log("  typeObject (System.Type) = %p", g_cached_component_type_object);
        if (!g_cached_component_type_object) {
            mdb_debug_log("ERROR: Failed to create Component type object");
            set_error(MdbErrorCode::InvalidClass, "Failed to create Component type object");
            return nullptr;
        }
        
        mdb_debug_log("Cache initialized successfully!");
    }
    
    // Check if initialization succeeded
    if (!g_cached_getComponents_method || !g_cached_component_type_object) {
        mdb_debug_log("ERROR: Cache not initialized (method=%p, typeObj=%p)", 
            g_cached_getComponents_method, g_cached_component_type_object);
        set_error(MdbErrorCode::NotInitialized, "GetComponents cache initialization failed");
        return nullptr;
    }
    
    // Call GetComponentsInternal(Type type, bool useSearchTypeAsArrayReturnType, 
    //                            bool recursive, bool includeInactive, bool reverse, object resultList)
    // Args: typeof(Component), true, false, true, false, null
    //   - useSearchTypeAsArrayReturnType = true (return Component[])
    //   - recursive = false (just this object, not children)
    //   - includeInactive = true (include inactive components)
    //   - reverse = false
    //   - resultList = null (return new array)
    mdb_debug_log("Calling GetComponentsInternal...");
    mdb_debug_log("  method=%p, instance=%p, typeArg=%p", 
        g_cached_getComponents_method, gameObject, g_cached_component_type_object);
    
    // Create boxed boolean values for the bool parameters
    static auto il2cpp_value_box_fn = reinterpret_cast<void*(*)(void*, void*)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_value_box")
    );
    
    // Get Boolean class for boxing
    void* booleanClass = mdb_find_class("mscorlib", "System", "Boolean");
    mdb_debug_log("  booleanClass = %p", booleanClass);
    
    // For value types, we pass pointers to the values
    // For booleans in il2cpp_runtime_invoke, pass pointers to the bool values
    bool trueVal = true;
    bool falseVal = false;
    
    // GetComponentsInternal params: (Type, bool, bool, bool, bool, object)
    // The bool values are passed by pointer for il2cpp_runtime_invoke
    void* args[6] = { 
        g_cached_component_type_object,  // Type type
        &trueVal,                         // bool useSearchTypeAsArrayReturnType
        &falseVal,                        // bool recursive
        &trueVal,                         // bool includeInactive  
        &falseVal,                        // bool reverse
        nullptr                           // object resultList (null = return new array)
    };
    mdb_debug_log("  args: type=%p, useSearchType=&true, recursive=&false, includeInactive=&true, reverse=&false, resultList=null", args[0]);
    
    void* exc = nullptr;
    void* result = nullptr;
    
    // Wrap invoke in SEH to catch crashes from destroyed objects
    __try {
        result = il2cpp_runtime_invoke_fn(g_cached_getComponents_method, gameObject, args, &exc);
    } __except(EXCEPTION_EXECUTE_HANDLER) {
        mdb_debug_log("  SEH Exception during invoke - object likely destroyed");
        set_error(MdbErrorCode::ExceptionThrown, "Native exception during GetComponentsInternal - object may be destroyed");
        return nullptr;
    }
    
    mdb_debug_log("  result=%p, exception=%p", result, exc);
    
    if (exc) {
        // Try to get exception details
        static auto il2cpp_object_get_class_fn = reinterpret_cast<void*(*)(void*)>(
            GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_object_get_class")
        );
        static auto il2cpp_class_get_name_fn = reinterpret_cast<const char*(*)(void*)>(
            GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_class_get_name")
        );
        if (il2cpp_object_get_class_fn && il2cpp_class_get_name_fn) {
            void* excClass = il2cpp_object_get_class_fn(exc);
            const char* excName = excClass ? il2cpp_class_get_name_fn(excClass) : "unknown";
            mdb_debug_log("  Exception type: %s", excName);
        }
        mdb_debug_log("ERROR: Exception thrown during GetComponents call");
        set_error(MdbErrorCode::ExceptionThrown, "Exception during GetComponents call");
        return nullptr;
    }
    
    if (result) {
        int len = mdb_array_length(result);
        mdb_debug_log("SUCCESS! Returned array with %d components", len);
    } else {
        mdb_debug_log("WARNING: GetComponents returned null (no exception)");
    }
    
    return result;
}

MDB_API int mdb_components_array_length(void* componentsArray) {
    return mdb_array_length(componentsArray);
}

MDB_API void* mdb_components_array_get(void* componentsArray, int index) {
    return mdb_array_get_element(componentsArray, index);
}

// ==============================
// GameObject SetActive helper
// ==============================

// Cached method for SetActive
static void* g_cached_setActive_method = nullptr;
static bool g_setActive_init_attempted = false;

MDB_API bool mdb_gameobject_set_active(void* gameObject, bool active) {
    mdb_debug_log("=== mdb_gameobject_set_active called ===");
    mdb_debug_log("gameObject ptr: %p, active: %s", gameObject, active ? "true" : "false");
    clear_error();
    
    if (!gameObject) {
        set_error(MdbErrorCode::NullPointer, "Invalid argument: gameObject is null");
        return false;
    }
    
    // Validate the pointer
    __try {
        void* vtable = *reinterpret_cast<void**>(gameObject);
        if (!vtable) {
            set_error(MdbErrorCode::NullPointer, "GameObject appears to be destroyed (null vtable)");
            return false;
        }
    } __except(EXCEPTION_EXECUTE_HANDLER) {
        set_error(MdbErrorCode::NullPointer, "GameObject pointer is invalid");
        return false;
    }
    
    auto status = il2cpp::ensure_thread_attached();
    if (status != Il2CppStatus::OK) {
        set_error(MdbErrorCode::ThreadNotAttached, status);
        return false;
    }
    
    static auto il2cpp_class_get_method_from_name_fn = reinterpret_cast<void*(*)(void*, const char*, int)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_class_get_method_from_name")
    );
    static auto il2cpp_runtime_invoke_fn = reinterpret_cast<void*(*)(void*, void*, void**, void**)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_runtime_invoke")
    );
    
    if (!il2cpp_class_get_method_from_name_fn || !il2cpp_runtime_invoke_fn) {
        set_error(MdbErrorCode::ExportNotFound, "Required IL2CPP exports not found");
        return false;
    }
    
    // Initialize SetActive method if needed
    if (!g_setActive_init_attempted) {
        g_setActive_init_attempted = true;
        
        void* gameObjectClass = mdb_find_class("UnityEngine.CoreModule", "UnityEngine", "GameObject");
        if (gameObjectClass) {
            g_cached_setActive_method = il2cpp_class_get_method_from_name_fn(gameObjectClass, "SetActive", 1);
            mdb_debug_log("SetActive method: %p", g_cached_setActive_method);
        }
    }
    
    if (!g_cached_setActive_method) {
        set_error(MdbErrorCode::MethodNotFound, "SetActive method not found on GameObject");
        return false;
    }
    
    // For bool parameters, pass pointer to the bool value
    bool activeVal = active;
    void* args[1] = { &activeVal };
    void* exc = nullptr;
    
    __try {
        il2cpp_runtime_invoke_fn(g_cached_setActive_method, gameObject, args, &exc);
    } __except(EXCEPTION_EXECUTE_HANDLER) {
        set_error(MdbErrorCode::ExceptionThrown, "Native exception during SetActive - object may be destroyed");
        return false;
    }
    
    if (exc) {
        set_error(MdbErrorCode::ExceptionThrown, "Exception during SetActive call");
        return false;
    }
    
    mdb_debug_log("SetActive succeeded");
    return true;
}

// ==============================
// GameObject.scene helper - get the scene handle of a GameObject
// ==============================

static void* g_cached_go_get_scene_method = nullptr;
static bool g_go_scene_init_attempted = false;

MDB_API int mdb_gameobject_get_scene_handle(void* gameObject) {
    if (!gameObject) return 0;
    
    auto status = il2cpp::ensure_thread_attached();
    if (status != Il2CppStatus::OK) return 0;
    
    static auto il2cpp_class_get_method_from_name_fn = reinterpret_cast<void*(*)(void*, const char*, int)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_class_get_method_from_name")
    );
    static auto il2cpp_runtime_invoke_fn = reinterpret_cast<void*(*)(void*, void*, void**, void**)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_runtime_invoke")
    );
    static auto il2cpp_object_unbox_fn = reinterpret_cast<void*(*)(void*)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_object_unbox")
    );
    
    if (!il2cpp_class_get_method_from_name_fn || !il2cpp_runtime_invoke_fn || !il2cpp_object_unbox_fn) {
        return 0;
    }
    
    // Initialize get_scene method if needed
    if (!g_go_scene_init_attempted) {
        g_go_scene_init_attempted = true;
        
        void* gameObjectClass = mdb_find_class("UnityEngine.CoreModule", "UnityEngine", "GameObject");
        if (gameObjectClass) {
            g_cached_go_get_scene_method = il2cpp_class_get_method_from_name_fn(gameObjectClass, "get_scene", 0);
        }
    }
    
    if (!g_cached_go_get_scene_method) return 0;
    
    void* exc = nullptr;
    void* sceneBoxed = nullptr;
    
    __try {
        sceneBoxed = il2cpp_runtime_invoke_fn(g_cached_go_get_scene_method, gameObject, nullptr, &exc);
    } __except(EXCEPTION_EXECUTE_HANDLER) {
        return 0;
    }
    
    if (exc || !sceneBoxed) return 0;
    
    // Scene is a struct with just an int handle
    struct UnityScene { int handle; };
    UnityScene* scene = (UnityScene*)il2cpp_object_unbox_fn(sceneBoxed);
    if (!scene) return 0;
    
    return scene->handle;
}

// ==============================
// GameObject.activeSelf helper - get the active state of a GameObject
// ==============================

static void* g_cached_go_get_activeSelf_method = nullptr;
static bool g_go_activeSelf_init_attempted = false;

MDB_API bool mdb_gameobject_get_active_self(void* gameObject) {
    if (!gameObject) return false;
    
    auto status = il2cpp::ensure_thread_attached();
    if (status != Il2CppStatus::OK) return false;
    
    static auto il2cpp_class_get_method_from_name_fn = reinterpret_cast<void*(*)(void*, const char*, int)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_class_get_method_from_name")
    );
    static auto il2cpp_runtime_invoke_fn = reinterpret_cast<void*(*)(void*, void*, void**, void**)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_runtime_invoke")
    );
    static auto il2cpp_object_unbox_fn = reinterpret_cast<void*(*)(void*)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_object_unbox")
    );
    
    if (!il2cpp_class_get_method_from_name_fn || !il2cpp_runtime_invoke_fn || !il2cpp_object_unbox_fn) {
        return false;
    }
    
    // Initialize get_activeSelf method if needed
    if (!g_go_activeSelf_init_attempted) {
        g_go_activeSelf_init_attempted = true;
        
        void* gameObjectClass = mdb_find_class("UnityEngine.CoreModule", "UnityEngine", "GameObject");
        if (gameObjectClass) {
            g_cached_go_get_activeSelf_method = il2cpp_class_get_method_from_name_fn(gameObjectClass, "get_activeSelf", 0);
        }
    }
    
    if (!g_cached_go_get_activeSelf_method) return false;
    
    void* exc = nullptr;
    void* resultBoxed = nullptr;
    
    __try {
        resultBoxed = il2cpp_runtime_invoke_fn(g_cached_go_get_activeSelf_method, gameObject, nullptr, &exc);
    } __except(EXCEPTION_EXECUTE_HANDLER) {
        return false;
    }
    
    if (exc || !resultBoxed) return false;
    
    // Unbox the bool result
    bool* unboxed = (bool*)il2cpp_object_unbox_fn(resultBoxed);
    if (!unboxed) return false;
    
    return *unboxed;
}

// ==============================
// Transform Helper Functions
// ==============================

// Cached pointers for Transform operations
static void* g_cached_transform_class = nullptr;
static void* g_cached_get_childCount_method = nullptr;
static void* g_cached_getChild_method = nullptr;
static bool g_transform_init_attempted = false;

static void init_transform_helpers() {
    if (g_transform_init_attempted) return;
    g_transform_init_attempted = true;
    
    g_cached_transform_class = mdb_find_class("UnityEngine.CoreModule", "UnityEngine", "Transform");
    if (!g_cached_transform_class) return;
    
    static auto il2cpp_class_get_method_from_name_fn = reinterpret_cast<void*(*)(void*, const char*, int)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_class_get_method_from_name")
    );
    if (!il2cpp_class_get_method_from_name_fn) return;
    
    g_cached_get_childCount_method = il2cpp_class_get_method_from_name_fn(g_cached_transform_class, "get_childCount", 0);
    g_cached_getChild_method = il2cpp_class_get_method_from_name_fn(g_cached_transform_class, "GetChild", 1);
    
    mdb_debug_log("Transform helpers initialized: childCount=%p, GetChild=%p", 
        g_cached_get_childCount_method, g_cached_getChild_method);
}

MDB_API int mdb_transform_get_child_count(void* transform) {
    if (!transform) return 0;
    
    auto status = il2cpp::ensure_thread_attached();
    if (status != Il2CppStatus::OK) return 0;
    
    init_transform_helpers();
    if (!g_cached_get_childCount_method) return 0;
    
    static auto il2cpp_runtime_invoke_fn = reinterpret_cast<void*(*)(void*, void*, void**, void**)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_runtime_invoke")
    );
    static auto il2cpp_object_unbox_fn = reinterpret_cast<void*(*)(void*)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_object_unbox")
    );
    
    if (!il2cpp_runtime_invoke_fn || !il2cpp_object_unbox_fn) return 0;
    
    void* exc = nullptr;
    void* result = nullptr;
    
    __try {
        result = il2cpp_runtime_invoke_fn(g_cached_get_childCount_method, transform, nullptr, &exc);
    } __except(EXCEPTION_EXECUTE_HANDLER) {
        return 0;
    }
    
    if (exc || !result) return 0;
    
    // Unbox the int result
    int* unboxed = (int*)il2cpp_object_unbox_fn(result);
    if (!unboxed) return 0;
    
    int count = *unboxed;
    
    // Sanity check
    if (count < 0 || count > 10000) return 0;
    
    return count;
}

MDB_API void* mdb_transform_get_child(void* transform, int index) {
    if (!transform || index < 0) return nullptr;
    
    auto status = il2cpp::ensure_thread_attached();
    if (status != Il2CppStatus::OK) return nullptr;
    
    init_transform_helpers();
    if (!g_cached_getChild_method) return nullptr;
    
    static auto il2cpp_runtime_invoke_fn = reinterpret_cast<void*(*)(void*, void*, void**, void**)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_runtime_invoke")
    );
    
    if (!il2cpp_runtime_invoke_fn) return nullptr;
    
    // For int parameter, pass pointer to the value
    void* args[1] = { &index };
    void* exc = nullptr;
    void* result = nullptr;
    
    __try {
        result = il2cpp_runtime_invoke_fn(g_cached_getChild_method, transform, args, &exc);
    } __except(EXCEPTION_EXECUTE_HANDLER) {
        return nullptr;
    }
    
    if (exc) return nullptr;
    
    return result;
}

// ==============================
// Transform Property Setters
// ==============================

// Unity Vector3 struct layout
struct UnityVector3 {
    float x, y, z;
};

// Cached methods for transform property setters
static void* g_cached_set_localPosition_method = nullptr;
static void* g_cached_set_localEulerAngles_method = nullptr;
static void* g_cached_set_localScale_method = nullptr;
static void* g_cached_get_localPosition_method = nullptr;
static void* g_cached_get_localEulerAngles_method = nullptr;
static void* g_cached_get_localScale_method = nullptr;
static bool g_transform_props_init_attempted = false;

static void init_transform_property_helpers() {
    if (g_transform_props_init_attempted) return;
    g_transform_props_init_attempted = true;
    
    init_transform_helpers(); // Ensure base transform class is resolved
    if (!g_cached_transform_class) return;
    
    static auto il2cpp_class_get_method_from_name_fn = reinterpret_cast<void*(*)(void*, const char*, int)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_class_get_method_from_name")
    );
    if (!il2cpp_class_get_method_from_name_fn) return;
    
    // Getters
    g_cached_get_localPosition_method = il2cpp_class_get_method_from_name_fn(g_cached_transform_class, "get_localPosition", 0);
    g_cached_get_localEulerAngles_method = il2cpp_class_get_method_from_name_fn(g_cached_transform_class, "get_localEulerAngles", 0);
    g_cached_get_localScale_method = il2cpp_class_get_method_from_name_fn(g_cached_transform_class, "get_localScale", 0);
    
    // Setters
    g_cached_set_localPosition_method = il2cpp_class_get_method_from_name_fn(g_cached_transform_class, "set_localPosition", 1);
    g_cached_set_localEulerAngles_method = il2cpp_class_get_method_from_name_fn(g_cached_transform_class, "set_localEulerAngles", 1);
    g_cached_set_localScale_method = il2cpp_class_get_method_from_name_fn(g_cached_transform_class, "set_localScale", 1);
    
    mdb_debug_log("Transform property helpers: getPos=%p, setPos=%p, getEuler=%p, setEuler=%p, getScale=%p, setScale=%p",
        g_cached_get_localPosition_method, g_cached_set_localPosition_method,
        g_cached_get_localEulerAngles_method, g_cached_set_localEulerAngles_method,
        g_cached_get_localScale_method, g_cached_set_localScale_method);
}

MDB_API bool mdb_transform_get_local_position(void* transform, float* outX, float* outY, float* outZ) {
    if (!transform || !outX || !outY || !outZ) return false;
    
    auto status = il2cpp::ensure_thread_attached();
    if (status != Il2CppStatus::OK) return false;
    
    init_transform_property_helpers();
    if (!g_cached_get_localPosition_method) return false;
    
    static auto il2cpp_runtime_invoke_fn = reinterpret_cast<void*(*)(void*, void*, void**, void**)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_runtime_invoke")
    );
    static auto il2cpp_object_unbox_fn = reinterpret_cast<void*(*)(void*)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_object_unbox")
    );
    
    if (!il2cpp_runtime_invoke_fn || !il2cpp_object_unbox_fn) return false;
    
    void* exc = nullptr;
    void* result = nullptr;
    
    __try {
        result = il2cpp_runtime_invoke_fn(g_cached_get_localPosition_method, transform, nullptr, &exc);
    } __except(EXCEPTION_EXECUTE_HANDLER) {
        return false;
    }
    
    if (exc || !result) return false;
    
    UnityVector3* vec = (UnityVector3*)il2cpp_object_unbox_fn(result);
    if (!vec) return false;
    
    *outX = vec->x;
    *outY = vec->y;
    *outZ = vec->z;
    return true;
}

MDB_API bool mdb_transform_set_local_position(void* transform, float x, float y, float z) {
    if (!transform) return false;
    
    auto status = il2cpp::ensure_thread_attached();
    if (status != Il2CppStatus::OK) return false;
    
    init_transform_property_helpers();
    if (!g_cached_set_localPosition_method) return false;
    
    static auto il2cpp_runtime_invoke_fn = reinterpret_cast<void*(*)(void*, void*, void**, void**)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_runtime_invoke")
    );
    
    if (!il2cpp_runtime_invoke_fn) return false;
    
    // For value type parameters, pass pointer to the struct
    UnityVector3 vec = { x, y, z };
    void* args[1] = { &vec };
    void* exc = nullptr;
    
    __try {
        il2cpp_runtime_invoke_fn(g_cached_set_localPosition_method, transform, args, &exc);
    } __except(EXCEPTION_EXECUTE_HANDLER) {
        return false;
    }
    
    return exc == nullptr;
}

MDB_API bool mdb_transform_get_local_euler_angles(void* transform, float* outX, float* outY, float* outZ) {
    if (!transform || !outX || !outY || !outZ) return false;
    
    auto status = il2cpp::ensure_thread_attached();
    if (status != Il2CppStatus::OK) return false;
    
    init_transform_property_helpers();
    if (!g_cached_get_localEulerAngles_method) return false;
    
    static auto il2cpp_runtime_invoke_fn = reinterpret_cast<void*(*)(void*, void*, void**, void**)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_runtime_invoke")
    );
    static auto il2cpp_object_unbox_fn = reinterpret_cast<void*(*)(void*)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_object_unbox")
    );
    
    if (!il2cpp_runtime_invoke_fn || !il2cpp_object_unbox_fn) return false;
    
    void* exc = nullptr;
    void* result = nullptr;
    
    __try {
        result = il2cpp_runtime_invoke_fn(g_cached_get_localEulerAngles_method, transform, nullptr, &exc);
    } __except(EXCEPTION_EXECUTE_HANDLER) {
        return false;
    }
    
    if (exc || !result) return false;
    
    UnityVector3* vec = (UnityVector3*)il2cpp_object_unbox_fn(result);
    if (!vec) return false;
    
    *outX = vec->x;
    *outY = vec->y;
    *outZ = vec->z;
    return true;
}

MDB_API bool mdb_transform_set_local_euler_angles(void* transform, float x, float y, float z) {
    if (!transform) return false;
    
    auto status = il2cpp::ensure_thread_attached();
    if (status != Il2CppStatus::OK) return false;
    
    init_transform_property_helpers();
    if (!g_cached_set_localEulerAngles_method) return false;
    
    static auto il2cpp_runtime_invoke_fn = reinterpret_cast<void*(*)(void*, void*, void**, void**)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_runtime_invoke")
    );
    
    if (!il2cpp_runtime_invoke_fn) return false;
    
    UnityVector3 vec = { x, y, z };
    void* args[1] = { &vec };
    void* exc = nullptr;
    
    __try {
        il2cpp_runtime_invoke_fn(g_cached_set_localEulerAngles_method, transform, args, &exc);
    } __except(EXCEPTION_EXECUTE_HANDLER) {
        return false;
    }
    
    return exc == nullptr;
}

MDB_API bool mdb_transform_get_local_scale(void* transform, float* outX, float* outY, float* outZ) {
    if (!transform || !outX || !outY || !outZ) return false;
    
    auto status = il2cpp::ensure_thread_attached();
    if (status != Il2CppStatus::OK) return false;
    
    init_transform_property_helpers();
    if (!g_cached_get_localScale_method) return false;
    
    static auto il2cpp_runtime_invoke_fn = reinterpret_cast<void*(*)(void*, void*, void**, void**)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_runtime_invoke")
    );
    static auto il2cpp_object_unbox_fn = reinterpret_cast<void*(*)(void*)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_object_unbox")
    );
    
    if (!il2cpp_runtime_invoke_fn || !il2cpp_object_unbox_fn) return false;
    
    void* exc = nullptr;
    void* result = nullptr;
    
    __try {
        result = il2cpp_runtime_invoke_fn(g_cached_get_localScale_method, transform, nullptr, &exc);
    } __except(EXCEPTION_EXECUTE_HANDLER) {
        return false;
    }
    
    if (exc || !result) return false;
    
    UnityVector3* vec = (UnityVector3*)il2cpp_object_unbox_fn(result);
    if (!vec) return false;
    
    *outX = vec->x;
    *outY = vec->y;
    *outZ = vec->z;
    return true;
}

MDB_API bool mdb_transform_set_local_scale(void* transform, float x, float y, float z) {
    if (!transform) return false;
    
    auto status = il2cpp::ensure_thread_attached();
    if (status != Il2CppStatus::OK) return false;
    
    init_transform_property_helpers();
    if (!g_cached_set_localScale_method) return false;
    
    static auto il2cpp_runtime_invoke_fn = reinterpret_cast<void*(*)(void*, void*, void**, void**)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_runtime_invoke")
    );
    
    if (!il2cpp_runtime_invoke_fn) return false;
    
    UnityVector3 vec = { x, y, z };
    void* args[1] = { &vec };
    void* exc = nullptr;
    
    __try {
        il2cpp_runtime_invoke_fn(g_cached_set_localScale_method, transform, args, &exc);
    } __except(EXCEPTION_EXECUTE_HANDLER) {
        return false;
    }
    
    return exc == nullptr;
}

// ==============================
// SceneManager Helper Functions
// ==============================

static void* g_cached_scenemanager_class = nullptr;
static void* g_cached_get_sceneCount_method = nullptr;
static void* g_cached_getSceneAt_method = nullptr;
static void* g_cached_scene_class = nullptr;
static void* g_cached_scene_get_name_method = nullptr;
static void* g_cached_scene_get_rootCount_method = nullptr;
static bool g_scenemanager_init_attempted = false;

static void init_scenemanager_helpers() {
    if (g_scenemanager_init_attempted) return;
    g_scenemanager_init_attempted = true;
    
    g_cached_scenemanager_class = mdb_find_class("UnityEngine.CoreModule", "UnityEngine.SceneManagement", "SceneManager");
    g_cached_scene_class = mdb_find_class("UnityEngine.CoreModule", "UnityEngine.SceneManagement", "Scene");
    
    if (!g_cached_scenemanager_class) {
        mdb_debug_log("SceneManager class not found");
        return;
    }
    
    static auto il2cpp_class_get_method_from_name_fn = reinterpret_cast<void*(*)(void*, const char*, int)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_class_get_method_from_name")
    );
    if (!il2cpp_class_get_method_from_name_fn) return;
    
    g_cached_get_sceneCount_method = il2cpp_class_get_method_from_name_fn(g_cached_scenemanager_class, "get_sceneCount", 0);
    g_cached_getSceneAt_method = il2cpp_class_get_method_from_name_fn(g_cached_scenemanager_class, "GetSceneAt", 1);
    
    if (g_cached_scene_class) {
        g_cached_scene_get_name_method = il2cpp_class_get_method_from_name_fn(g_cached_scene_class, "get_name", 0);
        g_cached_scene_get_rootCount_method = il2cpp_class_get_method_from_name_fn(g_cached_scene_class, "get_rootCount", 0);
    }
    
    mdb_debug_log("SceneManager helpers initialized: sceneCount=%p, GetSceneAt=%p", 
        g_cached_get_sceneCount_method, g_cached_getSceneAt_method);
}

MDB_API int mdb_scenemanager_get_scene_count() {
    auto status = il2cpp::ensure_thread_attached();
    if (status != Il2CppStatus::OK) return 0;
    
    init_scenemanager_helpers();
    if (!g_cached_get_sceneCount_method) return 0;
    
    static auto il2cpp_runtime_invoke_fn = reinterpret_cast<void*(*)(void*, void*, void**, void**)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_runtime_invoke")
    );
    static auto il2cpp_object_unbox_fn = reinterpret_cast<void*(*)(void*)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_object_unbox")
    );
    
    if (!il2cpp_runtime_invoke_fn || !il2cpp_object_unbox_fn) return 0;
    
    void* exc = nullptr;
    void* result = nullptr;
    
    __try {
        result = il2cpp_runtime_invoke_fn(g_cached_get_sceneCount_method, nullptr, nullptr, &exc);
    } __except(EXCEPTION_EXECUTE_HANDLER) {
        mdb_debug_log("Exception getting scene count");
        return 0;
    }
    
    if (exc || !result) {
        mdb_debug_log("Failed to get scene count: exc=%p, result=%p", exc, result);
        return 0;
    }
    
    // Unbox the int result
    int* unboxed = (int*)il2cpp_object_unbox_fn(result);
    if (!unboxed) return 0;
    
    int count = *unboxed;
    mdb_debug_log("Scene count: %d", count);
    
    // Sanity check
    if (count < 0 || count > 100) return 0;
    
    return count;
}

// Scene struct layout in IL2CPP (Unity's Scene is a struct with just an int handle)
struct UnityScene {
    int handle;
};

MDB_API int mdb_scenemanager_get_scene_name(int sceneIndex, char* buffer, int bufferSize) {
    if (!buffer || bufferSize <= 0) return 0;
    buffer[0] = '\0';
    
    auto status = il2cpp::ensure_thread_attached();
    if (status != Il2CppStatus::OK) return 0;
    
    init_scenemanager_helpers();
    if (!g_cached_getSceneAt_method || !g_cached_scene_get_name_method) return 0;
    
    static auto il2cpp_runtime_invoke_fn = reinterpret_cast<void*(*)(void*, void*, void**, void**)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_runtime_invoke")
    );
    static auto il2cpp_object_unbox_fn = reinterpret_cast<void*(*)(void*)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_object_unbox")
    );
    static auto il2cpp_string_chars_fn = reinterpret_cast<const wchar_t*(*)(void*)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_string_chars")
    );
    
    if (!il2cpp_runtime_invoke_fn || !il2cpp_object_unbox_fn || !il2cpp_string_chars_fn) return 0;
    
    // Get scene at index
    void* args[1] = { &sceneIndex };
    void* exc = nullptr;
    void* sceneBoxed = nullptr;
    
    __try {
        sceneBoxed = il2cpp_runtime_invoke_fn(g_cached_getSceneAt_method, nullptr, args, &exc);
    } __except(EXCEPTION_EXECUTE_HANDLER) {
        return 0;
    }
    
    if (exc || !sceneBoxed) return 0;
    
    // Unbox to get Scene struct pointer
    UnityScene* scene = (UnityScene*)il2cpp_object_unbox_fn(sceneBoxed);
    if (!scene) return 0;
    
    // Call get_name on the scene (need to pass the unboxed struct)
    // For instance methods on value types, we pass the unboxed struct pointer
    void* nameResult = nullptr;
    
    __try {
        nameResult = il2cpp_runtime_invoke_fn(g_cached_scene_get_name_method, scene, nullptr, &exc);
    } __except(EXCEPTION_EXECUTE_HANDLER) {
        return 0;
    }
    
    if (exc || !nameResult) return 0;
    
    // Convert IL2CPP string to char*
    const wchar_t* wstr = il2cpp_string_chars_fn(nameResult);
    if (!wstr) return 0;
    
    // Convert wchar_t to char
    int written = 0;
    for (int i = 0; i < bufferSize - 1 && wstr[i]; i++) {
        buffer[i] = (char)wstr[i];
        written++;
    }
    buffer[written] = '\0';
    
    return written;
}

MDB_API int mdb_scenemanager_get_scene_handle(int sceneIndex) {
    auto status = il2cpp::ensure_thread_attached();
    if (status != Il2CppStatus::OK) return -1;
    
    init_scenemanager_helpers();
    if (!g_cached_getSceneAt_method) return -1;
    
    static auto il2cpp_runtime_invoke_fn = reinterpret_cast<void*(*)(void*, void*, void**, void**)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_runtime_invoke")
    );
    static auto il2cpp_object_unbox_fn = reinterpret_cast<void*(*)(void*)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_object_unbox")
    );
    
    if (!il2cpp_runtime_invoke_fn || !il2cpp_object_unbox_fn) return -1;
    
    void* args[1] = { &sceneIndex };
    void* exc = nullptr;
    void* sceneBoxed = nullptr;
    
    __try {
        sceneBoxed = il2cpp_runtime_invoke_fn(g_cached_getSceneAt_method, nullptr, args, &exc);
    } __except(EXCEPTION_EXECUTE_HANDLER) {
        return -1;
    }
    
    if (exc || !sceneBoxed) return -1;
    
    UnityScene* scene = (UnityScene*)il2cpp_object_unbox_fn(sceneBoxed);
    if (!scene) return -1;
    
    return scene->handle;
}

MDB_API int mdb_scenemanager_get_scene_root_count(int sceneIndex) {
    auto status = il2cpp::ensure_thread_attached();
    if (status != Il2CppStatus::OK) return 0;
    
    init_scenemanager_helpers();
    if (!g_cached_getSceneAt_method || !g_cached_scene_get_rootCount_method) return 0;
    
    static auto il2cpp_runtime_invoke_fn = reinterpret_cast<void*(*)(void*, void*, void**, void**)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_runtime_invoke")
    );
    static auto il2cpp_object_unbox_fn = reinterpret_cast<void*(*)(void*)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_object_unbox")
    );
    
    if (!il2cpp_runtime_invoke_fn || !il2cpp_object_unbox_fn) return 0;
    
    // Get scene at index
    void* args[1] = { &sceneIndex };
    void* exc = nullptr;
    void* sceneBoxed = nullptr;
    
    __try {
        sceneBoxed = il2cpp_runtime_invoke_fn(g_cached_getSceneAt_method, nullptr, args, &exc);
    } __except(EXCEPTION_EXECUTE_HANDLER) {
        return 0;
    }
    
    if (exc || !sceneBoxed) return 0;
    
    UnityScene* scene = (UnityScene*)il2cpp_object_unbox_fn(sceneBoxed);
    if (!scene) return 0;
    
    // Call get_rootCount on the scene
    void* rootCountResult = nullptr;
    
    __try {
        rootCountResult = il2cpp_runtime_invoke_fn(g_cached_scene_get_rootCount_method, scene, nullptr, &exc);
    } __except(EXCEPTION_EXECUTE_HANDLER) {
        return 0;
    }
    
    if (exc || !rootCountResult) return 0;
    
    int* unboxed = (int*)il2cpp_object_unbox_fn(rootCountResult);
    if (!unboxed) return 0;
    
    int count = *unboxed;
    if (count < 0 || count > 100000) return 0;
    
    return count;
}

// Get DontDestroyOnLoad scene by finding a GameObject marked with DontDestroyOnLoad
// Returns the scene handle, or -1 if not found
// This works by getting a special scene that Unity uses for DontDestroyOnLoad objects
MDB_API int mdb_get_dontdestroyonload_scene_handle() {
    auto status = il2cpp::ensure_thread_attached();
    if (status != Il2CppStatus::OK) return -1;
    
    // The DontDestroyOnLoad scene has a specific name: "DontDestroyOnLoad"
    // We can find it by checking all root objects and finding one in that scene
    // However, a simpler approach: Unity's SceneManager.sceneCount doesn't include it
    // The DDOL scene handle is typically a high number or specific value
    
    // Alternative: Use Scene.GetSceneByName which doesn't work for DDOL
    // Best approach: Find any DDOL object and get its scene
    
    // For now, return -1 to indicate we'll handle this differently
    // The C# code will detect DDOL objects by checking if their scene handle
    // doesn't match any loaded scene
    return -1;
}

// ==============================
// OnGUI Hook Support
// ==============================

// Global callback for OnGUI dispatch
static OnGUICallbackFn g_ongui_callback = nullptr;
static bool g_minhook_initialized = false;
static std::string g_hooked_method_name;  // Track which method was hooked

MDB_API const char* mdb_get_hooked_method() {
    return g_hooked_method_name.c_str();
}

#if MDB_HAS_MINHOOK

// Original function pointer for GUIUtility.BeginGUI
typedef void (*GUIUtility_BeginGUI_t)();
static GUIUtility_BeginGUI_t g_original_BeginGUI = nullptr;

// Hook function that intercepts GUIUtility.BeginGUI
static void Hooked_GUIUtility_BeginGUI() {
    // Call original first to set up GUI state
    if (g_original_BeginGUI) {
        g_original_BeginGUI();
    }
    
    // Now call our managed OnGUI callback
    if (g_ongui_callback) {
        g_ongui_callback();
    }
}

// Alternative: Hook GUIUtility.EndGUI for drawing after Unity GUI
typedef void (*GUIUtility_EndGUI_t)(int32_t layoutType);
static GUIUtility_EndGUI_t g_original_EndGUI = nullptr;

static void Hooked_GUIUtility_EndGUI(int32_t layoutType) {
    // Call our managed OnGUI callback before ending
    if (g_ongui_callback) {
        g_ongui_callback();
    }
    
    // Then call original to finalize
    if (g_original_EndGUI) {
        g_original_EndGUI(layoutType);
    }
}

#endif // MDB_HAS_MINHOOK

MDB_API int mdb_register_ongui_callback(OnGUICallbackFn callback) {
    g_ongui_callback = callback;
    return 0;
}

MDB_API void mdb_dispatch_ongui() {
    // Manually trigger OnGUI callback (for testing)
    if (g_ongui_callback) {
        g_ongui_callback();
    }
}

// ==============================
// Generic Method Hooking System
// ==============================

#include <unordered_map>

// Hook tracking structure
struct HookInfo {
    int64_t handle;
    void* target;
    void* detour;
    void* original;
    bool enabled;
};

static std::unordered_map<int64_t, HookInfo> g_hooks;
static int64_t g_next_hook_handle = 1;
static std::mutex g_hooks_mutex;

static bool ensure_minhook_initialized() {
#if MDB_HAS_MINHOOK
    if (!g_minhook_initialized) {
        if (MH_Initialize() != MH_OK) {
            return false;
        }
        g_minhook_initialized = true;
    }
    return true;
#else
    return false;
#endif
}

MDB_API int64_t mdb_create_hook(void* method, HookCallbackFn callback, void** out_original) {
    clear_error();
    
    if (!method) {
        set_error(MdbErrorCode::NullPointer, "Invalid argument: method is null");
        return -1;
    }
    
    // Get method pointer
    void* methodPtr = mdb_get_method_pointer(method);
    if (!methodPtr) {
        set_error(MdbErrorCode::InvalidMethod, "Method has no function pointer");
        return -2;
    }
    
    return mdb_create_hook_ptr(methodPtr, (void*)callback, out_original);
}

MDB_API int64_t mdb_create_hook_rva(uint64_t rva, HookCallbackFn callback, void** out_original) {
    clear_error();
    
    void* methodPtr = mdb_get_method_pointer_from_rva(rva);
    if (!methodPtr) {
        set_error(MdbErrorCode::InvalidMethod, "Could not resolve RVA to method pointer");
        return -1;
    }
    
    return mdb_create_hook_ptr(methodPtr, (void*)callback, out_original);
}

MDB_API int64_t mdb_create_hook_ptr(void* target, void* detour, void** out_original) {
    clear_error();
    
#if MDB_HAS_MINHOOK
    if (!target || !detour) {
        set_error(MdbErrorCode::InvalidArgument, "Invalid arguments: target and detour are required");
        return -1;
    }
    
    if (!ensure_minhook_initialized()) {
        set_error(MdbErrorCode::InitFailed, "MinHook initialization failed");
        return -2;
    }
    
    std::lock_guard<std::mutex> lock(g_hooks_mutex);
    
    void* original = nullptr;
    MH_STATUS status = MH_CreateHook(target, detour, &original);
    if (status != MH_OK) {
        mdb_debug_log("MH_CreateHook failed: %d", status);
        set_error(MdbErrorCode::InvocationFailed, "MH_CreateHook failed");
        return -3;
    }
    
    status = MH_EnableHook(target);
    if (status != MH_OK) {
        MH_RemoveHook(target);
        set_error(MdbErrorCode::InvocationFailed, "MH_EnableHook failed");
        return -4;
    }
    
    // Store hook info
    int64_t handle = g_next_hook_handle++;
    g_hooks[handle] = HookInfo{handle, target, detour, original, true};
    
    if (out_original) {
        *out_original = original;
    }
    
    mdb_debug_log("Created hook %lld: target=%p, detour=%p, original=%p", handle, target, detour, original);
    return handle;
    
#else
    set_error(MdbErrorCode::NotInitialized, "MinHook not available");
    return -100;
#endif
}

MDB_API int mdb_remove_hook(int64_t hook_handle) {
    clear_error();
    
#if MDB_HAS_MINHOOK
    std::lock_guard<std::mutex> lock(g_hooks_mutex);
    
    auto it = g_hooks.find(hook_handle);
    if (it == g_hooks.end()) {
        set_error(MdbErrorCode::InvalidArgument, "Invalid hook handle");
        return -1;
    }
    
    HookInfo& info = it->second;
    
    MH_STATUS status = MH_DisableHook(info.target);
    if (status != MH_OK && status != MH_ERROR_DISABLED) {
        set_error(MdbErrorCode::InvocationFailed, "MH_DisableHook failed");
        return -2;
    }
    
    status = MH_RemoveHook(info.target);
    if (status != MH_OK) {
        set_error(MdbErrorCode::InvocationFailed, "MH_RemoveHook failed");
        return -3;
    }
    
    g_hooks.erase(it);
    mdb_debug_log("Removed hook %lld", hook_handle);
    return 0;
    
#else
    set_error(MdbErrorCode::NotInitialized, "MinHook not available");
    return -100;
#endif
}

MDB_API int mdb_set_hook_enabled(int64_t hook_handle, bool enabled) {
    clear_error();
    
#if MDB_HAS_MINHOOK
    std::lock_guard<std::mutex> lock(g_hooks_mutex);
    
    auto it = g_hooks.find(hook_handle);
    if (it == g_hooks.end()) {
        set_error(MdbErrorCode::InvalidArgument, "Invalid hook handle");
        return -1;
    }
    
    HookInfo& info = it->second;
    
    MH_STATUS status;
    if (enabled) {
        status = MH_EnableHook(info.target);
    } else {
        status = MH_DisableHook(info.target);
    }
    
    if (status != MH_OK) {
        set_error(MdbErrorCode::InvocationFailed, enabled ? "MH_EnableHook failed" : "MH_DisableHook failed");
        return -2;
    }
    
    info.enabled = enabled;
    return 0;
    
#else
    set_error(MdbErrorCode::NotInitialized, "MinHook not available");
    return -100;
#endif
}

MDB_API int mdb_get_method_info(void* method, int* out_param_count, bool* out_is_static, bool* out_has_return) {
    clear_error();
    
    if (!method) {
        set_error(MdbErrorCode::NullPointer, "Invalid argument: method is null");
        return -1;
    }
    
    auto status = il2cpp::_internal::ensure_exports();
    if (status != Il2CppStatus::OK) {
        set_error(MdbErrorCode::NotInitialized, status);
        return -2;
    }
    
    static auto il2cpp_method_get_param_count_fn = reinterpret_cast<uint32_t(*)(void*)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_method_get_param_count")
    );
    static auto il2cpp_method_get_flags_fn = reinterpret_cast<uint32_t(*)(void*, uint32_t*)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_method_get_flags")
    );
    static auto il2cpp_method_get_return_type_fn = reinterpret_cast<void*(*)(void*)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_method_get_return_type")
    );
    static auto il2cpp_type_get_type_fn = reinterpret_cast<int(*)(void*)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_type_get_type")
    );
    
    if (out_param_count && il2cpp_method_get_param_count_fn) {
        *out_param_count = il2cpp_method_get_param_count_fn(method);
    }
    
    if (out_is_static && il2cpp_method_get_flags_fn) {
        uint32_t iflags = 0;
        uint32_t flags = il2cpp_method_get_flags_fn(method, &iflags);
        // STATIC = 0x0010 in method flags
        *out_is_static = (flags & 0x0010) != 0;
    }
    
    if (out_has_return && il2cpp_method_get_return_type_fn && il2cpp_type_get_type_fn) {
        void* returnType = il2cpp_method_get_return_type_fn(method);
        if (returnType) {
            int typeEnum = il2cpp_type_get_type_fn(returnType);
            // IL2CPP_TYPE_VOID = 1
            *out_has_return = (typeEnum != 1);
        } else {
            *out_has_return = false;
        }
    }
    
    return 0;
}

MDB_API const char* mdb_method_get_name(void* method) {
    clear_error();
    
    if (!method) {
        set_error(MdbErrorCode::NullPointer, "Invalid argument: method is null");
        return nullptr;
    }
    
    static auto il2cpp_method_get_name_fn = reinterpret_cast<const char*(*)(void*)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_method_get_name")
    );
    
    if (!il2cpp_method_get_name_fn) {
        set_error(MdbErrorCode::ExportNotFound, "il2cpp_method_get_name not available");
        return nullptr;
    }
    
    return il2cpp_method_get_name_fn(method);
}

MDB_API int mdb_install_ongui_hook() {
    clear_error();
    
#if MDB_HAS_MINHOOK
    // Initialize MinHook if not done
    if (!g_minhook_initialized) {
        if (MH_Initialize() != MH_OK) {
            set_error(MdbErrorCode::InitFailed, "MinHook initialization failed");
            return -1;
        }
        g_minhook_initialized = true;
    }
    
    // Ensure IL2CPP is initialized
    auto status = il2cpp::_internal::ensure_exports();
    if (status != Il2CppStatus::OK) {
        set_error(MdbErrorCode::NotInitialized, status);
        return -1;
    }
    
    il2cpp::_internal::unity_structs::il2cppMethodInfo* targetMethod = nullptr;
    std::string methodDescription;
    
    // Strategy 1: Try to hook GUIBrowserUI.OnGUI if it exists (we know this is called)
    auto browserResult = il2cpp::find_class("ZenFulcrum.EmbeddedBrowser", "GUIBrowserUI", "Assembly-CSharp");
    if (browserResult.status == Il2CppStatus::OK && browserResult.value) {
        auto mi = il2cpp::_internal::il2cpp_class_get_method_from_name(browserResult.value, "OnGUI", 0);
        if (mi && mi->m_pMethodPointer) {
            targetMethod = mi;
            methodDescription = "GUIBrowserUI.OnGUI";
        }
    }
    
    // Strategy 2: Try GUIUtility internal methods
    if (!targetMethod) {
        const char* assemblies[] = { "UnityEngine.IMGUIModule", "UnityEngine.CoreModule", "UnityEngine" };
        il2cpp::_internal::unity_structs::il2cppClass* guiUtilityClass = nullptr;
        
        for (const char* assembly : assemblies) {
            auto result = il2cpp::find_class("UnityEngine", "GUIUtility", assembly);
            if (result.status == Il2CppStatus::OK && result.value) {
                guiUtilityClass = result.value;
                break;
            }
        }
        
        if (guiUtilityClass) {
            // Try various hook points
            const char* methodNames[] = { "BeginGUI", "CheckOnGUI", "ProcessEvent", "DoGUIEvent" };
            for (const char* methodName : methodNames) {
                auto mi = il2cpp::_internal::il2cpp_class_get_method_from_name(guiUtilityClass, methodName, -1);
                if (mi && mi->m_pMethodPointer) {
                    targetMethod = mi;
                    methodDescription = std::string("GUIUtility.") + methodName;
                    break;
                }
                mi = il2cpp::_internal::il2cpp_class_get_method_from_name(guiUtilityClass, methodName, 0);
                if (mi && mi->m_pMethodPointer) {
                    targetMethod = mi;
                    methodDescription = std::string("GUIUtility.") + methodName;
                    break;
                }
            }
        }
    }
    
    // Strategy 3: Try GUI.DoWindow or GUI.Window (commonly called during GUI)
    if (!targetMethod) {
        const char* assemblies[] = { "UnityEngine.IMGUIModule", "UnityEngine.CoreModule", "UnityEngine" };
        for (const char* assembly : assemblies) {
            auto result = il2cpp::find_class("UnityEngine", "GUI", assembly);
            if (result.status == Il2CppStatus::OK && result.value) {
                auto mi = il2cpp::_internal::il2cpp_class_get_method_from_name(result.value, "Label", -1);
                if (mi && mi->m_pMethodPointer) {
                    targetMethod = mi;
                    methodDescription = "GUI.Label";
                    break;
                }
            }
        }
    }
    
    if (!targetMethod || !targetMethod->m_pMethodPointer) {
        set_error(MdbErrorCode::MethodNotFound, "No suitable OnGUI hook point found");
        return -3;
    }
    
    // Store the method name for diagnostics
    g_hooked_method_name = methodDescription;
    
    // Create the hook
    MH_STATUS mhStatus = MH_CreateHook(
        targetMethod->m_pMethodPointer,
        (void*)&Hooked_GUIUtility_BeginGUI,
        (void**)&g_original_BeginGUI
    );
    
    if (mhStatus != MH_OK) {
        set_error(MdbErrorCode::InvocationFailed, "MH_CreateHook failed");
        return -5;
    }
    
    // Enable the hook
    mhStatus = MH_EnableHook(targetMethod->m_pMethodPointer);
    if (mhStatus != MH_OK) {
        set_error(MdbErrorCode::InvocationFailed, "MH_EnableHook failed");
        return -6;
    }
    
    return 0; // Success
    
#else
    // MinHook not available - OnGUI hook not supported
    set_error(MdbErrorCode::NotInitialized, "MinHook not available - compile with MinHook for OnGUI support");
    return -100;
#endif
}

// ==============================
// Reflection Helpers for Component Inspector
// ==============================

// Get field count for a class
MDB_API int mdb_class_get_field_count(void* klass) {
    if (!klass) return 0;
    
    static auto il2cpp_class_get_fields_fn = reinterpret_cast<void*(*)(void*, void**)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_class_get_fields")
    );
    
    if (!il2cpp_class_get_fields_fn) return 0;
    
    int count = 0;
    void* iter = nullptr;
    while (il2cpp_class_get_fields_fn(klass, &iter) != nullptr) {
        count++;
    }
    return count;
}

// Get field by index (returns FieldInfo pointer)
MDB_API void* mdb_class_get_field_by_index(void* klass, int index) {
    if (!klass || index < 0) return nullptr;
    
    static auto il2cpp_class_get_fields_fn = reinterpret_cast<void*(*)(void*, void**)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_class_get_fields")
    );
    
    if (!il2cpp_class_get_fields_fn) return nullptr;
    
    void* iter = nullptr;
    void* field = nullptr;
    int i = 0;
    while ((field = il2cpp_class_get_fields_fn(klass, &iter)) != nullptr) {
        if (i == index) return field;
        i++;
    }
    return nullptr;
}

// Get field name
MDB_API const char* mdb_field_get_name(void* field) {
    if (!field) return nullptr;
    auto* fi = reinterpret_cast<il2cpp::_internal::unity_structs::il2cppFieldInfo*>(field);
    return fi->m_pName;
}

// Get field type
MDB_API void* mdb_field_get_type(void* field) {
    if (!field) return nullptr;
    auto* fi = reinterpret_cast<il2cpp::_internal::unity_structs::il2cppFieldInfo*>(field);
    return fi->m_pType;
}

// Get type name from Il2CppType
MDB_API const char* mdb_type_get_name(void* type) {
    if (!type) return nullptr;
    
    static auto il2cpp_type_get_name_fn = reinterpret_cast<char*(*)(void*)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_type_get_name")
    );
    
    if (!il2cpp_type_get_name_fn) return nullptr;
    return il2cpp_type_get_name_fn(type);
}

// Get type kind (int, float, string, object, etc.)
MDB_API int mdb_type_get_type_enum(void* type) {
    if (!type) return -1;
    auto* t = reinterpret_cast<il2cpp::_internal::unity_structs::il2cppType*>(type);
    return t->m_uType;
}

// Get property count for a class
MDB_API int mdb_class_get_property_count(void* klass) {
    if (!klass) return 0;
    
    static auto il2cpp_class_get_properties_fn = reinterpret_cast<void*(*)(void*, void**)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_class_get_properties")
    );
    
    if (!il2cpp_class_get_properties_fn) return 0;
    
    int count = 0;
    void* iter = nullptr;
    while (il2cpp_class_get_properties_fn(klass, &iter) != nullptr) {
        count++;
    }
    return count;
}

// Get property by index
MDB_API void* mdb_class_get_property_by_index(void* klass, int index) {
    if (!klass || index < 0) return nullptr;
    
    static auto il2cpp_class_get_properties_fn = reinterpret_cast<void*(*)(void*, void**)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_class_get_properties")
    );
    
    if (!il2cpp_class_get_properties_fn) return nullptr;
    
    void* iter = nullptr;
    void* prop = nullptr;
    int i = 0;
    while ((prop = il2cpp_class_get_properties_fn(klass, &iter)) != nullptr) {
        if (i == index) return prop;
        i++;
    }
    return nullptr;
}

// Get property name
MDB_API const char* mdb_property_get_name(void* prop) {
    if (!prop) return nullptr;
    auto* pi = reinterpret_cast<il2cpp::_internal::unity_structs::il2cppPropertyInfo*>(prop);
    return pi->m_pName;
}

// Get property getter method
MDB_API void* mdb_property_get_get_method(void* prop) {
    if (!prop) return nullptr;
    auto* pi = reinterpret_cast<il2cpp::_internal::unity_structs::il2cppPropertyInfo*>(prop);
    return pi->m_pGet;
}

// Get property setter method
MDB_API void* mdb_property_get_set_method(void* prop) {
    if (!prop) return nullptr;
    auto* pi = reinterpret_cast<il2cpp::_internal::unity_structs::il2cppPropertyInfo*>(prop);
    return pi->m_pSet;
}

// Get method count for a class
MDB_API int mdb_class_get_method_count(void* klass) {
    if (!klass) return 0;
    
    static auto il2cpp_class_get_methods_fn = reinterpret_cast<void*(*)(void*, void**)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_class_get_methods")
    );
    
    if (!il2cpp_class_get_methods_fn) return 0;
    
    int count = 0;
    void* iter = nullptr;
    while (il2cpp_class_get_methods_fn(klass, &iter) != nullptr) {
        count++;
    }
    return count;
}

// Get method by index
MDB_API void* mdb_class_get_method_by_index(void* klass, int index) {
    if (!klass || index < 0) return nullptr;
    
    static auto il2cpp_class_get_methods_fn = reinterpret_cast<void*(*)(void*, void**)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_class_get_methods")
    );
    
    if (!il2cpp_class_get_methods_fn) return nullptr;
    
    void* iter = nullptr;
    void* method = nullptr;
    int i = 0;
    while ((method = il2cpp_class_get_methods_fn(klass, &iter)) != nullptr) {
        if (i == index) return method;
        i++;
    }
    return nullptr;
}

// Get method name (already exists as mdb_method_get_name, but adding for completeness)
MDB_API const char* mdb_method_get_name_str(void* method) {
    if (!method) return nullptr;
    auto* mi = reinterpret_cast<il2cpp::_internal::unity_structs::il2cppMethodInfo*>(method);
    return mi->m_pName;
}

// Get method parameter count
MDB_API int mdb_method_get_param_count(void* method) {
    if (!method) return 0;
    auto* mi = reinterpret_cast<il2cpp::_internal::unity_structs::il2cppMethodInfo*>(method);
    return mi->m_uArgsCount;
}

// Get method return type
MDB_API void* mdb_method_get_return_type(void* method) {
    if (!method) return nullptr;
    auto* mi = reinterpret_cast<il2cpp::_internal::unity_structs::il2cppMethodInfo*>(method);
    return mi->m_pReturnType;
}

// Get method flags (to check if static, public, etc.)
MDB_API int mdb_method_get_flags(void* method) {
    if (!method) return 0;
    auto* mi = reinterpret_cast<il2cpp::_internal::unity_structs::il2cppMethodInfo*>(method);
    return mi->m_uFlags;
}

// Check if field is static using proper IL2CPP API
MDB_API bool mdb_field_is_static(void* field) {
    if (!field) return false;
    
    // Use the proper il2cpp_field_get_flags API
    static auto il2cpp_field_get_flags_fn = reinterpret_cast<int(*)(void*)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_field_get_flags")
    );
    
    if (!il2cpp_field_get_flags_fn) {
        // Fallback: check offset (static fields typically have offset -1 or 0)
        auto* fi = reinterpret_cast<il2cpp::_internal::unity_structs::il2cppFieldInfo*>(field);
        return fi->m_iOffset == -1;
    }
    
    // FIELD_ATTRIBUTE_STATIC = 0x0010
    int flags = il2cpp_field_get_flags_fn(field);
    return (flags & 0x0010) != 0;
}

// Get class from type
MDB_API void* mdb_type_get_class(void* type) {
    if (!type) return nullptr;
    
    static auto il2cpp_class_from_type_fn = reinterpret_cast<void*(*)(void*)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_class_from_type")
    );
    
    if (!il2cpp_class_from_type_fn) return nullptr;
    return il2cpp_class_from_type_fn(type);
}

// Check if type is value type
MDB_API bool mdb_type_is_valuetype(void* type) {
    if (!type) return false;
    
    static auto il2cpp_class_is_valuetype_fn = reinterpret_cast<bool(*)(void*)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_class_is_valuetype")
    );
    
    if (!il2cpp_class_is_valuetype_fn) return false;
    
    void* klass = mdb_type_get_class(type);
    if (!klass) return false;
    
    return il2cpp_class_is_valuetype_fn(klass);
}

// Get parent class
MDB_API void* mdb_class_get_parent(void* klass) {
    if (!klass) return nullptr;
    
    static auto il2cpp_class_get_parent_fn = reinterpret_cast<void*(*)(void*)>(
        GetProcAddress(il2cpp::_internal::p_game_assembly, "il2cpp_class_get_parent")
    );
    
    if (!il2cpp_class_get_parent_fn) return nullptr;
    return il2cpp_class_get_parent_fn(klass);
}

// Get value type data directly from field (for primitive types)
MDB_API bool mdb_field_get_value_direct(void* instance, void* field, void* outBuffer, int bufferSize) {
    if (!instance || !field || !outBuffer || bufferSize <= 0) return false;
    
    auto status = il2cpp::_internal::ensure_exports();
    if (status != Il2CppStatus::OK) return false;
    
    auto* fi = reinterpret_cast<il2cpp::_internal::unity_structs::il2cppFieldInfo*>(field);
    
    // For instance fields, offset is positive
    if (fi->m_iOffset >= 0) {
        char* ptr = reinterpret_cast<char*>(instance) + fi->m_iOffset;
        memcpy(outBuffer, ptr, bufferSize);
        return true;
    }
    
    return false;
}

// Set value type data directly to field (for primitive types)
MDB_API bool mdb_field_set_value_direct(void* instance, void* field, void* value, int valueSize) {
    if (!instance || !field || !value || valueSize <= 0) return false;
    
    auto* fi = reinterpret_cast<il2cpp::_internal::unity_structs::il2cppFieldInfo*>(field);
    
    // For instance fields, offset is positive
    if (fi->m_iOffset >= 0) {
        char* ptr = reinterpret_cast<char*>(instance) + fi->m_iOffset;
        memcpy(ptr, value, valueSize);
        return true;
    }
    
    return false;
}

