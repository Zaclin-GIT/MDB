// ==============================
// MDB Bridge - Export Implementations
// ==============================
// Implements all P/Invoke exported functions using il2cpp_resolver.hpp

#include "bridge_exports.h"
#include "il2cpp_resolver.hpp"

#include <string>
#include <mutex>

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

MDB_API const char* mdb_get_last_error() {
    return g_last_error.c_str();
}

MDB_API int mdb_get_last_error_code() {
    return static_cast<int>(g_last_error_code);
}
