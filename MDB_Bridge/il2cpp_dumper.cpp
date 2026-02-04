#include "il2cpp_dumper.hpp"
#include "../MDB_Dumper/Il2cppApi.hpp"
#include "../MDB_Dumper/Il2CppTableDefine.hpp"
#include "../MDB_Dumper/Il2CppClass.hpp"

#include <Windows.h>
#include <fstream>
#include <sstream>
#include <vector>
#include <chrono>
#include <iomanip>
#include <ctime>
#include <filesystem>

namespace MDB {
namespace Dumper {

static uintptr_t GetGameAssemblyBaseAddress() {
    return (uintptr_t)GetModuleHandleW(L"GameAssembly.dll");
}

static bool _il2cpp_type_is_byref(const Il2CppType* type) {
    auto byref = type->byref;
    if (Il2CppApi::Instance().il2cpp_type_is_byref) {
        byref = Il2CppApi::Instance().il2cpp_type_is_byref(type);
    }
    return byref;
}

static std::string dump_field(Il2CppClass* klass) {
    std::stringstream outPut;
    outPut << "\n\t// Fields\n";
    auto is_enum = Il2CppApi::Instance().il2cpp_class_is_enum(klass);
    void* iter = nullptr;
    while (auto field = Il2CppApi::Instance().il2cpp_class_get_fields(klass, &iter)) {
        outPut << "\t";
        auto attrs = Il2CppApi::Instance().il2cpp_field_get_flags(field);
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
        auto field_type = Il2CppApi::Instance().il2cpp_field_get_type(field);
        auto field_class = Il2CppApi::Instance().il2cpp_class_from_type(field_type);
        outPut << Il2CppApi::Instance().il2cpp_class_get_name(field_class) << " " << Il2CppApi::Instance().il2cpp_field_get_name(field);
        if (attrs & FIELD_ATTRIBUTE_LITERAL && is_enum) {
            uint64_t val = 0;
            Il2CppApi::Instance().il2cpp_field_static_get_value(field, &val);
            outPut << " = " << std::dec << val;
        }
        outPut << "; // 0x" << std::hex << Il2CppApi::Instance().il2cpp_field_get_offset(field) << "\n";
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

static std::string dump_property(Il2CppClass* klass) {
    std::stringstream outPut;
    outPut << "\n\t// Properties\n";
    void* iter = nullptr;
    while (auto prop_const = Il2CppApi::Instance().il2cpp_class_get_properties(klass, &iter)) {
        auto prop = const_cast<PropertyInfo*>(prop_const);
        auto get = Il2CppApi::Instance().il2cpp_property_get_get_method(prop);
        auto set = Il2CppApi::Instance().il2cpp_property_get_set_method(prop);
        auto prop_name = Il2CppApi::Instance().il2cpp_property_get_name(prop);
        outPut << "\t";
        Il2CppClass* prop_class = nullptr;
        uint32_t iflags = 0;
        if (get) {
            outPut << get_method_modifier(Il2CppApi::Instance().il2cpp_method_get_flags(get, &iflags));
            prop_class = Il2CppApi::Instance().il2cpp_class_from_type(Il2CppApi::Instance().il2cpp_method_get_return_type(get));
        }
        else if (set) {
            outPut << get_method_modifier(Il2CppApi::Instance().il2cpp_method_get_flags(set, &iflags));
            auto param = Il2CppApi::Instance().il2cpp_method_get_param(set, 0);
            prop_class = Il2CppApi::Instance().il2cpp_class_from_type(param);
        }
        if (prop_class) {
            outPut << Il2CppApi::Instance().il2cpp_class_get_name(prop_class) << " " << prop_name << " { ";
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

static std::string dump_method(Il2CppClass* klass, uintptr_t GameAssemblyBaseAddress) {
    std::stringstream outPut;
    outPut << "\n\t// Methods\n";
    void* iter = nullptr;
    while (auto method = Il2CppApi::Instance().il2cpp_class_get_methods(klass, &iter)) {
        if (method->methodPointer) {
            outPut << "\t// RVA: 0x";
            outPut << std::hex << (uint64_t)method->methodPointer - GameAssemblyBaseAddress;
            outPut << " VA: 0x";
            outPut << std::hex << (uint64_t)method->methodPointer;
        }
        else {
            outPut << "\t// RVA: 0x VA: 0x0";
        }
        outPut << "\n\t";
        uint32_t iflags = 0;
        auto flags = Il2CppApi::Instance().il2cpp_method_get_flags(method, &iflags);
        outPut << get_method_modifier(flags);
        auto return_type = Il2CppApi::Instance().il2cpp_method_get_return_type(method);
        if (_il2cpp_type_is_byref(return_type)) {
            outPut << "ref ";
        }
        auto return_class = Il2CppApi::Instance().il2cpp_class_from_type(return_type);
        outPut << Il2CppApi::Instance().il2cpp_class_get_name(return_class) << " " << Il2CppApi::Instance().il2cpp_method_get_name(method)
            << "(";
        auto param_count = Il2CppApi::Instance().il2cpp_method_get_param_count(method);
        for (auto i = 0u; i < param_count; ++i) {
            auto param = Il2CppApi::Instance().il2cpp_method_get_param(method, i);
            auto attrs = param->attrs;
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
            auto parameter_class = Il2CppApi::Instance().il2cpp_class_from_type(param);
            outPut << Il2CppApi::Instance().il2cpp_class_get_name(parameter_class) << " "
                << Il2CppApi::Instance().il2cpp_method_get_param_name(method, i);
            outPut << ", ";
        }
        if (param_count > 0) {
            outPut.seekp(-2, outPut.cur);
        }
        outPut << ") { }\n";
    }
    return outPut.str();
}

static std::string dump_type(const Il2CppType* type, uintptr_t GameAssemblyBaseAddress) {
    std::stringstream outPut;
    auto* klass = Il2CppApi::Instance().il2cpp_class_from_type(type);
    outPut << "\n// Namespace: " << Il2CppApi::Instance().il2cpp_class_get_namespace(klass) << "\n";
    auto flags = Il2CppApi::Instance().il2cpp_class_get_flags(klass);
    if (flags & TYPE_ATTRIBUTE_SERIALIZABLE) {
        outPut << "[Serializable]\n";
    }
    auto is_valuetype = Il2CppApi::Instance().il2cpp_class_is_valuetype(klass);
    auto is_enum = Il2CppApi::Instance().il2cpp_class_is_enum(klass);
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
    outPut << Il2CppApi::Instance().il2cpp_class_get_name(klass);
    std::vector<std::string> extends;
    auto parent = Il2CppApi::Instance().il2cpp_class_get_parent(klass);
    if (!is_valuetype && !is_enum && parent) {
        auto parent_type = Il2CppApi::Instance().il2cpp_class_get_type(parent);
        if (parent_type->type != IL2CPP_TYPE_OBJECT) {
            extends.emplace_back(Il2CppApi::Instance().il2cpp_class_get_name(parent));
        }
    }
    void* iter = nullptr;
    while (auto itf = Il2CppApi::Instance().il2cpp_class_get_interfaces(klass, &iter)) {
        extends.emplace_back(Il2CppApi::Instance().il2cpp_class_get_name(itf));
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
    
    auto hModule = reinterpret_cast<HMODULE>(GameAssemblyBaseAddress);
    
    // Initialize IL2CPP API
    Il2CppApi::Instance().Initialize(hModule);
    
    // Validate all IL2CPP exports were resolved
    if (!Il2CppApi::Instance().IsValid()) {
        result.error_message = "Failed to resolve IL2CPP exports: " + Il2CppApi::Instance().GetMissingExports();
        return result;
    }
    
    // Get domain and assemblies
    size_t size;
    auto domain = Il2CppApi::Instance().il2cpp_domain_get();
    if (!domain) {
        result.error_message = "Failed to get IL2CPP domain";
        return result;
    }
    
    auto assemblies = Il2CppApi::Instance().il2cpp_domain_get_assemblies(domain, &size);
    if (!assemblies) {
        result.error_message = "Failed to get assemblies";
        return result;
    }
    
    result.total_assemblies = size;
    
    // Collect image information
    std::stringstream imageOutput;
    for (int i = 0; i < size; ++i) {
        auto image = Il2CppApi::Instance().il2cpp_assembly_get_image(assemblies[i]);
        imageOutput << "// Image " << i << ": " << Il2CppApi::Instance().il2cpp_image_get_name(image) << "\n";
    }
    
    // Dump types from assemblies
    std::vector<std::string> outPuts;
    size_t totalClasses = 0;
    
    for (int i = 0; i < size; ++i) {
        auto image = Il2CppApi::Instance().il2cpp_assembly_get_image(assemblies[i]);
        std::string imageName = Il2CppApi::Instance().il2cpp_image_get_name(image);
        
        std::stringstream imageStr;
        imageStr << "\n// Dll : " << imageName;
        auto classCount = Il2CppApi::Instance().il2cpp_image_get_class_count(image);
        totalClasses += classCount;
        
        for (int j = 0; j < classCount; ++j) {
            auto klass = Il2CppApi::Instance().il2cpp_image_get_class(image, j);
            auto type = Il2CppApi::Instance().il2cpp_class_get_type(const_cast<Il2CppClass*>(klass));
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
    for (int i = 0; i < outPuts.size(); ++i) {
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
