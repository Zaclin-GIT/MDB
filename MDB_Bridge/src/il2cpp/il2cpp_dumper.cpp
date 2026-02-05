#include "il2cpp_dumper.hpp"
#include "il2cpp_resolver.hpp"

#include <Il2CppTableDefine.hpp>
#include <Il2CppTypes.hpp>

#include <Windows.h>
#include <fstream>
#include <sstream>
#include <vector>
#include <chrono>
#include <iomanip>
#include <ctime>
#include <filesystem>

// Convenience aliases into the resolver's internal namespace
namespace api = il2cpp::_internal;
using namespace il2cpp::_internal::unity_structs;

namespace MDB {
namespace Dumper {

static uintptr_t GetGameAssemblyBaseAddress() {
    return (uintptr_t)GetModuleHandleW(L"GameAssembly.dll");
}

static bool _il2cpp_type_is_byref(const il2cppType* type) {
    if (api::il2cpp_type_is_byref) {
        return api::il2cpp_type_is_byref(type);
    }
    return type->m_uByref != 0;
}

static std::string dump_field(il2cppClass* klass) {
    std::stringstream outPut;
    outPut << "\n\t// Fields\n";
    auto is_enum = api::il2cpp_class_is_enum(klass);
    void* iter = nullptr;
    while (auto field = api::il2cpp_class_get_fields(klass, &iter)) {
        outPut << "\t";
        auto attrs = api::il2cpp_field_get_flags(field);
        auto access = attrs & FIELD_ATTRIBUTE_FIELD_ACCESS_MASK;
        switch (access) {
        case FIELD_ATTRIBUTE_PRIVATE:
            outPut << "private ";
            break;
        case FIELD_ATTRIBUTE_PUBLIC:
            outPut << "public ";
            break;
        case FIELD_ATTRIBUTE_FAMILY:
            outPut << "protected ";
            break;
        case FIELD_ATTRIBUTE_ASSEMBLY:
        case FIELD_ATTRIBUTE_FAM_AND_ASSEM:
            outPut << "internal ";
            break;
        case FIELD_ATTRIBUTE_FAM_OR_ASSEM:
            outPut << "protected internal ";
            break;
        }
        if (attrs & FIELD_ATTRIBUTE_LITERAL) {
            outPut << "const ";
        }
        else {
            if (attrs & FIELD_ATTRIBUTE_STATIC) {
                outPut << "static ";
            }
            if (attrs & FIELD_ATTRIBUTE_INIT_ONLY) {
                outPut << "readonly ";
            }
        }
        auto field_type = api::il2cpp_field_get_type(field);
        auto field_class = api::il2cpp_class_from_type(field_type);
        outPut << api::il2cpp_class_get_name(field_class) << " " << api::il2cpp_field_get_name(field);
        if (attrs & FIELD_ATTRIBUTE_LITERAL && is_enum) {
            uint64_t val = 0;
            api::il2cpp_field_static_get_value(field, &val);
            outPut << " = " << std::dec << val;
        }
        outPut << "; // 0x" << std::hex << api::il2cpp_field_get_offset(field) << "\n";
    }
    return outPut.str();
}

static std::string get_method_modifier(uint32_t flags) {
    std::stringstream outPut;
    auto access = flags & METHOD_ATTRIBUTE_MEMBER_ACCESS_MASK;
    switch (access) {
    case METHOD_ATTRIBUTE_PRIVATE:
        outPut << "private ";
        break;
    case METHOD_ATTRIBUTE_PUBLIC:
        outPut << "public ";
        break;
    case METHOD_ATTRIBUTE_FAMILY:
        outPut << "protected ";
        break;
    case METHOD_ATTRIBUTE_ASSEM:
    case METHOD_ATTRIBUTE_FAM_AND_ASSEM:
        outPut << "internal ";
        break;
    case METHOD_ATTRIBUTE_FAM_OR_ASSEM:
        outPut << "protected internal ";
        break;
    }
    if (flags & METHOD_ATTRIBUTE_STATIC) {
        outPut << "static ";
    }
    if (flags & METHOD_ATTRIBUTE_ABSTRACT) {
        outPut << "abstract ";
        if ((flags & METHOD_ATTRIBUTE_VTABLE_LAYOUT_MASK) == METHOD_ATTRIBUTE_REUSE_SLOT) {
            outPut << "override ";
        }
    }
    else if (flags & METHOD_ATTRIBUTE_FINAL) {
        if ((flags & METHOD_ATTRIBUTE_VTABLE_LAYOUT_MASK) == METHOD_ATTRIBUTE_REUSE_SLOT) {
            outPut << "sealed override ";
        }
    }
    else if (flags & METHOD_ATTRIBUTE_VIRTUAL) {
        if ((flags & METHOD_ATTRIBUTE_VTABLE_LAYOUT_MASK) == METHOD_ATTRIBUTE_NEW_SLOT) {
            outPut << "virtual ";
        }
        else {
            outPut << "override ";
        }
    }
    if (flags & METHOD_ATTRIBUTE_PINVOKE_IMPL) {
        outPut << "extern ";
    }
    return outPut.str();
}

static std::string dump_property(il2cppClass* klass) {
    std::stringstream outPut;
    outPut << "\n\t// Properties\n";
    void* iter = nullptr;
    while (auto prop_const = api::il2cpp_class_get_properties(klass, &iter)) {
        // const_cast is safe — the IL2CPP API takes non-const in the getter helpers
        auto prop = const_cast<il2cppPropertyInfo*>(prop_const);
        auto get = api::il2cpp_property_get_get_method(prop);
        auto set = api::il2cpp_property_get_set_method(prop);
        auto prop_name = api::il2cpp_property_get_name(prop);
        outPut << "\t";
        il2cppClass* prop_class = nullptr;
        uint32_t iflags = 0;
        if (get) {
            outPut << get_method_modifier(api::il2cpp_method_get_flags(get, &iflags));
            prop_class = api::il2cpp_class_from_type(api::il2cpp_method_get_return_type(get));
        }
        else if (set) {
            outPut << get_method_modifier(api::il2cpp_method_get_flags(set, &iflags));
            auto param = api::il2cpp_method_get_param(set, 0);
            prop_class = api::il2cpp_class_from_type(param);
        }
        if (prop_class) {
            outPut << api::il2cpp_class_get_name(prop_class) << " " << prop_name << " { ";
            if (get) {
                outPut << "get; ";
            }
            if (set) {
                outPut << "set; ";
            }
            outPut << "}\n";
        }
        else {
            if (prop_name) {
                outPut << " // unknown property " << prop_name;
            }
        }
    }
    return outPut.str();
}

static std::string dump_method(il2cppClass* klass, uintptr_t GameAssemblyBaseAddress) {
    std::stringstream outPut;
    outPut << "\n\t// Methods\n";
    void* iter = nullptr;
    while (auto method = api::il2cpp_class_get_methods(klass, &iter)) {
        if (method->m_pMethodPointer) {
            outPut << "\t// RVA: 0x";
            outPut << std::hex << (uint64_t)method->m_pMethodPointer - GameAssemblyBaseAddress;
            outPut << " VA: 0x";
            outPut << std::hex << (uint64_t)method->m_pMethodPointer;
        }
        else {
            outPut << "\t// RVA: 0x VA: 0x0";
        }
        outPut << "\n\t";
        uint32_t iflags = 0;
        auto flags = api::il2cpp_method_get_flags(method, &iflags);
        outPut << get_method_modifier(flags);
        auto return_type = api::il2cpp_method_get_return_type(method);
        if (_il2cpp_type_is_byref(return_type)) {
            outPut << "ref ";
        }
        auto return_class = api::il2cpp_class_from_type(return_type);
        outPut << api::il2cpp_class_get_name(return_class) << " " << api::il2cpp_method_get_name(method)
            << "(";
        auto param_count = api::il2cpp_method_get_param_count(method);
        for (auto i = 0u; i < param_count; ++i) {
            auto param = api::il2cpp_method_get_param(method, i);
            auto attrs = param->m_uAttributes;
            if (_il2cpp_type_is_byref(param)) {
                if (attrs & PARAM_ATTRIBUTE_OUT && !(attrs & PARAM_ATTRIBUTE_IN)) {
                    outPut << "out ";
                }
                else if (attrs & PARAM_ATTRIBUTE_IN && !(attrs & PARAM_ATTRIBUTE_OUT)) {
                    outPut << "in ";
                }
                else {
                    outPut << "ref ";
                }
            }
            else {
                if (attrs & PARAM_ATTRIBUTE_IN) {
                    outPut << "[In] ";
                }
                if (attrs & PARAM_ATTRIBUTE_OUT) {
                    outPut << "[Out] ";
                }
            }
            auto parameter_class = api::il2cpp_class_from_type(param);
            outPut << api::il2cpp_class_get_name(parameter_class) << " "
                << api::il2cpp_method_get_param_name(method, i);
            outPut << ", ";
        }
        if (param_count > 0) {
            outPut.seekp(-2, outPut.cur);
        }
        outPut << ") { }\n";
    }
    return outPut.str();
}

static std::string dump_type(const il2cppType* type, uintptr_t GameAssemblyBaseAddress) {
    std::stringstream outPut;
    auto* klass = api::il2cpp_class_from_type(type);
    outPut << "\n// Namespace: " << api::il2cpp_class_get_namespace(klass) << "\n";
    auto flags = api::il2cpp_class_get_flags(klass);
    if (flags & TYPE_ATTRIBUTE_SERIALIZABLE) {
        outPut << "[Serializable]\n";
    }
    auto is_valuetype = api::il2cpp_class_is_valuetype(klass);
    auto is_enum = api::il2cpp_class_is_enum(klass);
    auto visibility = flags & TYPE_ATTRIBUTE_VISIBILITY_MASK;
    switch (visibility) {
    case TYPE_ATTRIBUTE_PUBLIC:
    case TYPE_ATTRIBUTE_NESTED_PUBLIC:
        outPut << "public ";
        break;
    case TYPE_ATTRIBUTE_NOT_PUBLIC:
    case TYPE_ATTRIBUTE_NESTED_FAM_AND_ASSEM:
    case TYPE_ATTRIBUTE_NESTED_ASSEMBLY:
        outPut << "internal ";
        break;
    case TYPE_ATTRIBUTE_NESTED_PRIVATE:
        outPut << "private ";
        break;
    case TYPE_ATTRIBUTE_NESTED_FAMILY:
        outPut << "protected ";
        break;
    case TYPE_ATTRIBUTE_NESTED_FAM_OR_ASSEM:
        outPut << "protected internal ";
        break;
    }
    if (flags & TYPE_ATTRIBUTE_ABSTRACT && flags & TYPE_ATTRIBUTE_SEALED) {
        outPut << "static ";
    }
    else if (!(flags & TYPE_ATTRIBUTE_INTERFACE) && flags & TYPE_ATTRIBUTE_ABSTRACT) {
        outPut << "abstract ";
    }
    else if (!is_valuetype && !is_enum && flags & TYPE_ATTRIBUTE_SEALED) {
        outPut << "sealed ";
    }
    if (flags & TYPE_ATTRIBUTE_INTERFACE) {
        outPut << "interface ";
    }
    else if (is_enum) {
        outPut << "enum ";
    }
    else if (is_valuetype) {
        outPut << "struct ";
    }
    else {
        outPut << "class ";
    }
    outPut << api::il2cpp_class_get_name(klass);
    std::vector<std::string> extends;
    auto parent = api::il2cpp_class_get_parent(klass);
    if (!is_valuetype && !is_enum && parent) {
        auto parent_type = api::il2cpp_class_get_type(parent);
        if (parent_type->m_uType != IL2CPP_TYPE_OBJECT) {
            extends.emplace_back(api::il2cpp_class_get_name(parent));
        }
    }
    void* iter = nullptr;
    while (auto itf = api::il2cpp_class_get_interfaces(klass, &iter)) {
        extends.emplace_back(api::il2cpp_class_get_name(itf));
    }
    if (!extends.empty()) {
        outPut << " : " << extends[0];
        for (int i = 1; i < extends.size(); ++i) {
            outPut << ", " << extends[i];
        }
    }
    outPut << "\n{";
    outPut << dump_field(klass);
    outPut << dump_property(klass);
    outPut << dump_method(klass, GameAssemblyBaseAddress);
    outPut << "}\n";
    return outPut.str();
}

DumpResult DumpIL2CppRuntime(const std::string& output_directory) {
    DumpResult result = { false, "", "", 0, 0 };
    
    // Wait for GameAssembly.dll to load
    uintptr_t GameAssemblyBaseAddress = GetGameAssemblyBaseAddress();
    if (GameAssemblyBaseAddress == 0) {
        result.error_message = "GameAssembly.dll not found";
        return result;
    }
    
    // Ensure the resolver has bound all IL2CPP exports
    auto status = api::ensure_exports();
    if (status != Il2CppStatus::OK) {
        result.error_message = std::string("Failed to resolve IL2CPP exports: ") + to_string(status);
        return result;
    }

    // Validate the dumper-specific APIs we need are available
    if (!api::il2cpp_assembly_get_image || !api::il2cpp_image_get_name ||
        !api::il2cpp_image_get_class_count || !api::il2cpp_image_get_class ||
        !api::il2cpp_class_get_type || !api::il2cpp_class_get_name) {
        result.error_message = "Required dumper APIs not resolved (assembly/image/class introspection)";
        return result;
    }
    
    // Get domain and assemblies
    size_t size;
    auto domain = api::il2cpp_domain_get();
    if (!domain) {
        result.error_message = "Failed to get IL2CPP domain";
        return result;
    }
    
    auto assemblies = api::il2cpp_domain_get_assemblies(domain, &size);
    if (!assemblies) {
        result.error_message = "Failed to get assemblies";
        return result;
    }
    
    result.total_assemblies = size;
    
    // Collect image information
    std::stringstream imageOutput;
    for (size_t i = 0; i < size; ++i) {
        auto image = api::il2cpp_assembly_get_image(assemblies[i]);
        imageOutput << "// Image " << i << ": " << api::il2cpp_image_get_name(image) << "\n";
    }
    
    // Dump types from assemblies
    std::vector<std::string> outPuts;
    size_t totalClasses = 0;
    
    for (size_t i = 0; i < size; ++i) {
        auto image = api::il2cpp_assembly_get_image(assemblies[i]);
        std::string imageName = api::il2cpp_image_get_name(image);
        
        std::stringstream imageStr;
        imageStr << "\n// Dll : " << imageName;
        auto classCount = api::il2cpp_image_get_class_count(image);
        totalClasses += classCount;
        
        for (size_t j = 0; j < classCount; ++j) {
            auto klass = api::il2cpp_image_get_class(image, j);
            auto type = api::il2cpp_class_get_type(klass);
            auto outPut = imageStr.str() + dump_type(type, GameAssemblyBaseAddress);
            outPuts.push_back(outPut);
        }
    }
    
    result.total_classes = totalClasses;
    
    // Create output directory and write dump file
    std::filesystem::create_directories(output_directory);
    std::string dumpPath = output_directory + "\\dump.cs";
    
    std::ofstream outStream(dumpPath);
    if (!outStream.is_open()) {
        result.error_message = "Failed to open dump file for writing: " + dumpPath;
        return result;
    }
    
    outStream << imageOutput.str();
    for (size_t i = 0; i < outPuts.size(); ++i) {
        outStream << outPuts[i];
    }
    outStream.close();
    
    result.success = true;
    result.dump_path = dumpPath;
    return result;
}

bool IsDumpFresh(const std::string& dump_path) {
    // Check if dump exists
    if (!std::filesystem::exists(dump_path)) {
        return false;
    }
    
    // Check if GameAssembly.dll exists and get its timestamp
    HMODULE hGameAssembly = GetModuleHandleW(L"GameAssembly.dll");
    if (!hGameAssembly) {
        return false;
    }
    
    wchar_t gameAssemblyPath[MAX_PATH];
    if (GetModuleFileNameW(hGameAssembly, gameAssemblyPath, MAX_PATH) == 0) {
        return false;
    }
    
    try {
        auto dump_time = std::filesystem::last_write_time(dump_path);
        auto game_time = std::filesystem::last_write_time(gameAssemblyPath);
        
        // If dump is newer than GameAssembly.dll, it's fresh
        return dump_time > game_time;
    } catch (...) {
        return false;
    }
}

} // namespace Dumper
} // namespace MDB
