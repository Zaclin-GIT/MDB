#pragma once
#include <Windows.h>
#include <vector>
#include <string>
#include <sstream>
#include "Il2CppClass.hpp"
#include "SignatureScanner.hpp"
#include "Il2CppSignatures.hpp"

typedef struct Il2CppAssembly Il2CppAssembly;
typedef struct Il2CppDomain Il2CppDomain;
typedef struct Il2CppImage Il2CppImage;
typedef struct Il2CppClass Il2CppClass;

typedef Il2CppDomain* (*il2cpp_domain_get_func)();
typedef Il2CppAssembly** (*il2cpp_domain_get_assemblies_func)(const Il2CppDomain* domain, size_t* size);
typedef Il2CppImage* (*il2cpp_assembly_get_image_func)(const Il2CppAssembly* assembly);
typedef char* (*il2cpp_image_get_name_func)(const Il2CppImage* image);
typedef size_t (*il2cpp_image_get_class_count_func)(const Il2CppImage* image);
typedef Il2CppClass* (*il2cpp_image_get_class_func)(const Il2CppImage* image, size_t index);
typedef Il2CppType* (*il2cpp_class_get_type_func)(Il2CppClass* klass);
typedef Il2CppClass* (*il2cpp_class_from_type_func)(const Il2CppType* type);
typedef const char* (*il2cpp_class_get_namespace_func)(Il2CppClass* klass);
typedef int (*il2cpp_class_get_flags_func)(const Il2CppClass* klass);
typedef bool (*il2cpp_class_is_valuetype_func)(const Il2CppClass* klass);
typedef bool (*il2cpp_class_is_enum_func)(const Il2CppClass* klass);
typedef const char* (*il2cpp_class_get_name_func) (Il2CppClass* klass);
typedef Il2CppClass* (*il2cpp_class_get_parent_func)(Il2CppClass* klass);
typedef Il2CppClass* (*il2cpp_class_get_interfaces_func)(Il2CppClass* klass, void** iter);
typedef FieldInfo* (*il2cpp_class_get_fields_func)(Il2CppClass* klass, void** iter);
typedef int (*il2cpp_field_get_flags_func)(FieldInfo* field);
typedef const Il2CppType* (*il2cpp_field_get_type_func)(FieldInfo* field);
typedef void (*il2cpp_field_static_get_value_func)(FieldInfo* field, void* value);
typedef const char* (*il2cpp_field_get_name_func)(FieldInfo* field);
typedef size_t(*il2cpp_field_get_offset_func)(FieldInfo* field);
typedef const PropertyInfo* (*il2cpp_class_get_properties_func)(Il2CppClass* klass, void** iter);
typedef const MethodInfo* (*il2cpp_property_get_get_method_func)(PropertyInfo* prop);
typedef const MethodInfo* (*il2cpp_property_get_set_method_func)(PropertyInfo* prop);
typedef const char* (*il2cpp_property_get_name_func)(PropertyInfo* prop);
typedef uint32_t(*il2cpp_method_get_flags_func)(const MethodInfo* method, uint32_t* iflags);
typedef const Il2CppType* (*il2cpp_method_get_return_type_func)(const MethodInfo* method);
typedef const Il2CppType* (*il2cpp_method_get_param_func)(const MethodInfo* method, uint32_t index);
typedef const MethodInfo* (*il2cpp_class_get_methods_func)(Il2CppClass* klass, void** iter);
typedef bool (*il2cpp_type_is_byref_func)(const Il2CppType* type);
typedef const char* (*il2cpp_method_get_name_func)(const MethodInfo* method);
typedef uint32_t(*il2cpp_method_get_param_count_func)(const MethodInfo* method);
typedef const char* (*il2cpp_method_get_param_name_func)(const MethodInfo* method, uint32_t index);

// Macro for resolving exports with fallback chain
#define RESOLVE(func) this->func = reinterpret_cast<func##_func>(ResolveExport(#func))

class Il2CppApi {
public:
    static Il2CppApi& Instance() {
        static Il2CppApi instance;
        return instance;
    }

    Il2CppApi(const Il2CppApi&) = delete;
    Il2CppApi& operator=(const Il2CppApi&) = delete;

    il2cpp_domain_get_func il2cpp_domain_get = nullptr;
    il2cpp_domain_get_assemblies_func il2cpp_domain_get_assemblies = nullptr;
    il2cpp_assembly_get_image_func il2cpp_assembly_get_image = nullptr;
    il2cpp_image_get_name_func il2cpp_image_get_name = nullptr;
    il2cpp_image_get_class_count_func il2cpp_image_get_class_count = nullptr;
    il2cpp_image_get_class_func il2cpp_image_get_class = nullptr;
    il2cpp_class_get_type_func il2cpp_class_get_type = nullptr;
    il2cpp_class_from_type_func il2cpp_class_from_type = nullptr;
    il2cpp_class_get_namespace_func il2cpp_class_get_namespace = nullptr;
    il2cpp_class_get_flags_func il2cpp_class_get_flags = nullptr;
    il2cpp_class_is_valuetype_func il2cpp_class_is_valuetype = nullptr;
    il2cpp_class_is_enum_func il2cpp_class_is_enum = nullptr;
    il2cpp_class_get_name_func il2cpp_class_get_name = nullptr;
    il2cpp_class_get_parent_func il2cpp_class_get_parent = nullptr;
    il2cpp_class_get_interfaces_func il2cpp_class_get_interfaces = nullptr;
    il2cpp_class_get_fields_func il2cpp_class_get_fields = nullptr;
    il2cpp_field_get_flags_func il2cpp_field_get_flags = nullptr;
    il2cpp_field_get_type_func il2cpp_field_get_type = nullptr;
    il2cpp_field_static_get_value_func il2cpp_field_static_get_value = nullptr;
    il2cpp_field_get_name_func il2cpp_field_get_name = nullptr;
    il2cpp_field_get_offset_func il2cpp_field_get_offset = nullptr;
    il2cpp_class_get_properties_func il2cpp_class_get_properties = nullptr;
    il2cpp_property_get_get_method_func il2cpp_property_get_get_method = nullptr;
    il2cpp_property_get_set_method_func il2cpp_property_get_set_method = nullptr;
    il2cpp_property_get_name_func il2cpp_property_get_name = nullptr;
    il2cpp_method_get_flags_func il2cpp_method_get_flags = nullptr;
    il2cpp_method_get_return_type_func il2cpp_method_get_return_type = nullptr;
    il2cpp_method_get_param_func il2cpp_method_get_param = nullptr;
    il2cpp_class_get_methods_func il2cpp_class_get_methods = nullptr;
    il2cpp_type_is_byref_func il2cpp_type_is_byref = nullptr;
    il2cpp_method_get_name_func il2cpp_method_get_name = nullptr;
    il2cpp_method_get_param_count_func il2cpp_method_get_param_count = nullptr;
    il2cpp_method_get_param_name_func il2cpp_method_get_param_name = nullptr;

    void Initialize(HMODULE hModule) {
        m_hModule = hModule;
        m_missingExports.clear();
        
        // Initialize the signature scanner for fallback resolution
        SignatureScanner::Initialize(hModule);
        
        // Resolve all exports using fallback chain:
        // 1. GetProcAddress (standard)
        // 2. Suffix matching (obfuscated exports)
        // 3. Pattern scanning (fully obfuscated)
        RESOLVE(il2cpp_domain_get);
        RESOLVE(il2cpp_domain_get_assemblies);
        RESOLVE(il2cpp_assembly_get_image);
        RESOLVE(il2cpp_image_get_name);
        RESOLVE(il2cpp_image_get_class_count);
        RESOLVE(il2cpp_image_get_class);
        RESOLVE(il2cpp_class_get_type);
        RESOLVE(il2cpp_class_from_type);
        RESOLVE(il2cpp_class_get_namespace);
        RESOLVE(il2cpp_class_get_flags);
        RESOLVE(il2cpp_class_is_valuetype);
        RESOLVE(il2cpp_class_is_enum);
        RESOLVE(il2cpp_class_get_name);
        RESOLVE(il2cpp_class_get_parent);
        RESOLVE(il2cpp_class_get_interfaces);
        RESOLVE(il2cpp_class_get_fields);
        RESOLVE(il2cpp_field_get_flags);
        RESOLVE(il2cpp_field_get_type);
        RESOLVE(il2cpp_field_static_get_value);
        RESOLVE(il2cpp_field_get_name);
        RESOLVE(il2cpp_field_get_offset);
        RESOLVE(il2cpp_class_get_properties);
        RESOLVE(il2cpp_property_get_get_method);
        RESOLVE(il2cpp_property_get_set_method);
        RESOLVE(il2cpp_property_get_name);
        RESOLVE(il2cpp_method_get_flags);
        RESOLVE(il2cpp_method_get_return_type);
        RESOLVE(il2cpp_method_get_param);
        RESOLVE(il2cpp_class_get_methods);
        RESOLVE(il2cpp_type_is_byref);
        RESOLVE(il2cpp_method_get_name);
        RESOLVE(il2cpp_method_get_param_count);
        RESOLVE(il2cpp_method_get_param_name);
    }

    // Check if all required exports were resolved
    bool IsValid() const {
        return il2cpp_domain_get != nullptr &&
               il2cpp_domain_get_assemblies != nullptr &&
               il2cpp_assembly_get_image != nullptr &&
               il2cpp_image_get_name != nullptr &&
               il2cpp_image_get_class_count != nullptr &&
               il2cpp_image_get_class != nullptr &&
               il2cpp_class_get_type != nullptr &&
               il2cpp_class_from_type != nullptr &&
               il2cpp_class_get_namespace != nullptr &&
               il2cpp_class_get_flags != nullptr &&
               il2cpp_class_is_valuetype != nullptr &&
               il2cpp_class_is_enum != nullptr &&
               il2cpp_class_get_name != nullptr &&
               il2cpp_class_get_parent != nullptr &&
               il2cpp_class_get_interfaces != nullptr &&
               il2cpp_class_get_fields != nullptr &&
               il2cpp_field_get_flags != nullptr &&
               il2cpp_field_get_type != nullptr &&
               il2cpp_field_static_get_value != nullptr &&
               il2cpp_field_get_name != nullptr &&
               il2cpp_field_get_offset != nullptr &&
               il2cpp_class_get_properties != nullptr &&
               il2cpp_property_get_get_method != nullptr &&
               il2cpp_property_get_set_method != nullptr &&
               il2cpp_property_get_name != nullptr &&
               il2cpp_method_get_flags != nullptr &&
               il2cpp_method_get_return_type != nullptr &&
               il2cpp_method_get_param != nullptr &&
               il2cpp_class_get_methods != nullptr &&
               il2cpp_type_is_byref != nullptr &&
               il2cpp_method_get_name != nullptr &&
               il2cpp_method_get_param_count != nullptr &&
               il2cpp_method_get_param_name != nullptr;
    }

    // Get comma-separated list of missing exports
    std::string GetMissingExports() const {
        std::stringstream ss;
        bool first = true;
        
        #define CHECK_MISSING(func) \
            if (func == nullptr) { \
                if (!first) ss << ", "; \
                ss << #func; \
                first = false; \
            }
        
        CHECK_MISSING(il2cpp_domain_get);
        CHECK_MISSING(il2cpp_domain_get_assemblies);
        CHECK_MISSING(il2cpp_assembly_get_image);
        CHECK_MISSING(il2cpp_image_get_name);
        CHECK_MISSING(il2cpp_image_get_class_count);
        CHECK_MISSING(il2cpp_image_get_class);
        CHECK_MISSING(il2cpp_class_get_type);
        CHECK_MISSING(il2cpp_class_from_type);
        CHECK_MISSING(il2cpp_class_get_namespace);
        CHECK_MISSING(il2cpp_class_get_flags);
        CHECK_MISSING(il2cpp_class_is_valuetype);
        CHECK_MISSING(il2cpp_class_is_enum);
        CHECK_MISSING(il2cpp_class_get_name);
        CHECK_MISSING(il2cpp_class_get_parent);
        CHECK_MISSING(il2cpp_class_get_interfaces);
        CHECK_MISSING(il2cpp_class_get_fields);
        CHECK_MISSING(il2cpp_field_get_flags);
        CHECK_MISSING(il2cpp_field_get_type);
        CHECK_MISSING(il2cpp_field_static_get_value);
        CHECK_MISSING(il2cpp_field_get_name);
        CHECK_MISSING(il2cpp_field_get_offset);
        CHECK_MISSING(il2cpp_class_get_properties);
        CHECK_MISSING(il2cpp_property_get_get_method);
        CHECK_MISSING(il2cpp_property_get_set_method);
        CHECK_MISSING(il2cpp_property_get_name);
        CHECK_MISSING(il2cpp_method_get_flags);
        CHECK_MISSING(il2cpp_method_get_return_type);
        CHECK_MISSING(il2cpp_method_get_param);
        CHECK_MISSING(il2cpp_class_get_methods);
        CHECK_MISSING(il2cpp_type_is_byref);
        CHECK_MISSING(il2cpp_method_get_name);
        CHECK_MISSING(il2cpp_method_get_param_count);
        CHECK_MISSING(il2cpp_method_get_param_name);
        
        #undef CHECK_MISSING
        
        return ss.str();
    }

private:
    Il2CppApi() { }
    
    HMODULE m_hModule = nullptr;
    std::vector<std::string> m_missingExports;

    // Resolve export with fallback chain:
    // 1. Standard GetProcAddress
    // 2. Suffix matching for obfuscated exports
    // 3. Pattern scanning for fully obfuscated exports
    void* ResolveExport(const char* name) {
        if (!m_hModule) return nullptr;
        
        // Strategy 1: Standard GetProcAddress
        void* addr = reinterpret_cast<void*>(GetProcAddress(m_hModule, name));
        if (addr) {
            // Follow any thunks to get real address
            return reinterpret_cast<void*>(SignatureScanner::FollowThunk(reinterpret_cast<uintptr_t>(addr)));
        }
        
        // Strategy 2: Check for obfuscated exports by suffix
        // Find the corresponding signature if we have one
        const FunctionSignature* sig = FindSignature(name);
        if (sig) {
            // Try each known obfuscation suffix
            for (const char* suffix : sig->suffixes) {
                uintptr_t found = SignatureScanner::FindExportBySuffix(suffix);
                if (found) {
                    return reinterpret_cast<void*>(found);
                }
            }
            
            // Strategy 3: Pattern scanning
            for (const auto& pattern : sig->patterns) {
                uintptr_t found = SignatureScanner::FindPattern(pattern.pattern, pattern.mask);
                if (found) {
                    return reinterpret_cast<void*>(found);
                }
            }
        }
        
        // All strategies failed
        m_missingExports.push_back(name);
        return nullptr;
    }
    
    // Find signature definition for a function name
    const FunctionSignature* FindSignature(const char* name) const {
        for (int i = 0; Il2CppSignatures::CRITICAL_SIGNATURES[i] != nullptr; i++) {
            if (strcmp(Il2CppSignatures::CRITICAL_SIGNATURES[i]->name, name) == 0) {
                return Il2CppSignatures::CRITICAL_SIGNATURES[i];
            }
        }
        return nullptr;
    }
};
