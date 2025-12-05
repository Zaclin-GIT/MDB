#pragma once
#include <Windows.h>
#include "Il2CppClass.hpp"

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
        this->il2cpp_domain_get = reinterpret_cast<il2cpp_domain_get_func>(GetProcAddress(hModule, "il2cpp_domain_get"));
        this->il2cpp_domain_get_assemblies = reinterpret_cast<il2cpp_domain_get_assemblies_func>(GetProcAddress(hModule, "il2cpp_domain_get_assemblies"));
        if (!this->il2cpp_domain_get_assemblies) {
            // Fallback for specific games with obfuscated exports
            this->il2cpp_domain_get_assemblies = reinterpret_cast<il2cpp_domain_get_assemblies_func>(GetProcAddress(hModule, "a44f8f_wasting_your_life"));
        }
        this->il2cpp_assembly_get_image = reinterpret_cast<il2cpp_assembly_get_image_func>(GetProcAddress(hModule, "il2cpp_assembly_get_image"));
        this->il2cpp_image_get_name = reinterpret_cast<il2cpp_image_get_name_func>(GetProcAddress(hModule, "il2cpp_image_get_name"));
        this->il2cpp_image_get_class_count = reinterpret_cast<il2cpp_image_get_class_count_func>(GetProcAddress(hModule, "il2cpp_image_get_class_count"));
        this->il2cpp_image_get_class = reinterpret_cast<il2cpp_image_get_class_func>(GetProcAddress(hModule, "il2cpp_image_get_class"));
        this->il2cpp_class_get_type = reinterpret_cast<il2cpp_class_get_type_func>(GetProcAddress(hModule, "il2cpp_class_get_type"));
        this->il2cpp_class_from_type = reinterpret_cast<il2cpp_class_from_type_func>(GetProcAddress(hModule, "il2cpp_class_from_type"));
        this->il2cpp_class_get_namespace = reinterpret_cast<il2cpp_class_get_namespace_func>(GetProcAddress(hModule, "il2cpp_class_get_namespace"));
        this->il2cpp_class_get_flags = reinterpret_cast<il2cpp_class_get_flags_func>(GetProcAddress(hModule, "il2cpp_class_get_flags"));
        this->il2cpp_class_is_valuetype = reinterpret_cast<il2cpp_class_is_valuetype_func>(GetProcAddress(hModule, "il2cpp_class_is_valuetype"));
        this->il2cpp_class_is_enum = reinterpret_cast<il2cpp_class_is_enum_func>(GetProcAddress(hModule, "il2cpp_class_is_enum"));
        this->il2cpp_class_get_name = reinterpret_cast<il2cpp_class_get_name_func>(GetProcAddress(hModule, "il2cpp_class_get_name"));
        this->il2cpp_class_get_parent = reinterpret_cast<il2cpp_class_get_parent_func>(GetProcAddress(hModule, "il2cpp_class_get_parent"));
        this->il2cpp_class_get_interfaces = reinterpret_cast<il2cpp_class_get_interfaces_func>(GetProcAddress(hModule, "il2cpp_class_get_interfaces"));
        this->il2cpp_class_get_fields = reinterpret_cast<il2cpp_class_get_fields_func>(GetProcAddress(hModule, "il2cpp_class_get_fields"));
        this->il2cpp_field_get_flags = reinterpret_cast<il2cpp_field_get_flags_func>(GetProcAddress(hModule, "il2cpp_field_get_flags"));
        this->il2cpp_field_get_type = reinterpret_cast<il2cpp_field_get_type_func>(GetProcAddress(hModule, "il2cpp_field_get_type"));
        this->il2cpp_field_static_get_value = reinterpret_cast<il2cpp_field_static_get_value_func>(GetProcAddress(hModule, "il2cpp_field_static_get_value"));
        this->il2cpp_field_get_name = reinterpret_cast<il2cpp_field_get_name_func>(GetProcAddress(hModule, "il2cpp_field_get_name"));
        this->il2cpp_field_get_offset = reinterpret_cast<il2cpp_field_get_offset_func>(GetProcAddress(hModule, "il2cpp_field_get_offset"));
        this->il2cpp_class_get_properties = reinterpret_cast<il2cpp_class_get_properties_func>(GetProcAddress(hModule, "il2cpp_class_get_properties"));
        this->il2cpp_property_get_get_method = reinterpret_cast<il2cpp_property_get_get_method_func>(GetProcAddress(hModule, "il2cpp_property_get_get_method"));
        this->il2cpp_property_get_set_method = reinterpret_cast<il2cpp_property_get_set_method_func>(GetProcAddress(hModule, "il2cpp_property_get_set_method"));
        this->il2cpp_property_get_name = reinterpret_cast<il2cpp_property_get_name_func>(GetProcAddress(hModule, "il2cpp_property_get_name"));
        this->il2cpp_method_get_flags = reinterpret_cast<il2cpp_method_get_flags_func>(GetProcAddress(hModule, "il2cpp_method_get_flags"));
        this->il2cpp_method_get_return_type = reinterpret_cast<il2cpp_method_get_return_type_func>(GetProcAddress(hModule, "il2cpp_method_get_return_type"));
        this->il2cpp_method_get_param = reinterpret_cast<il2cpp_method_get_param_func>(GetProcAddress(hModule, "il2cpp_method_get_param"));
        this->il2cpp_class_get_methods = reinterpret_cast<il2cpp_class_get_methods_func>(GetProcAddress(hModule, "il2cpp_class_get_methods"));
        this->il2cpp_type_is_byref = reinterpret_cast<il2cpp_type_is_byref_func>(GetProcAddress(hModule, "il2cpp_type_is_byref"));
        this->il2cpp_method_get_name = reinterpret_cast<il2cpp_method_get_name_func>(GetProcAddress(hModule, "il2cpp_method_get_name"));
        this->il2cpp_method_get_param_count = reinterpret_cast<il2cpp_method_get_param_count_func>(GetProcAddress(hModule, "il2cpp_method_get_param_count"));
        this->il2cpp_method_get_param_name = reinterpret_cast<il2cpp_method_get_param_name_func>(GetProcAddress(hModule, "il2cpp_method_get_param_name"));
    }

private:
    Il2CppApi() { }
};
