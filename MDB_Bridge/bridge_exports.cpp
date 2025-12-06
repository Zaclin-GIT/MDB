// ==============================
// MDB Bridge - Export Implementations
// ==============================
// Implements all P/Invoke exported functions using il2cpp_resolver.hpp

#include "bridge_exports.h"
#include "il2cpp_resolver.hpp"

#include <string>
#include <mutex>

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
