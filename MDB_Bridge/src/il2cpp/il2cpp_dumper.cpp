#include "il2cpp_dumper.hpp"
#include "il2cpp_resolver.hpp"

#include <Il2CppTableDefine.hpp>
#include <Il2CppTypes.hpp>

#define NOMINMAX
#include <Windows.h>
#include <fstream>
#include <sstream>
#include <vector>
#include <chrono>
#include <iomanip>
#include <ctime>
#include <filesystem>
#include <map>
#include <set>
#include <algorithm>
#include <functional>

// Convenience aliases into the resolver's internal namespace
namespace api = il2cpp::_internal;
using namespace il2cpp::_internal::unity_structs;

namespace MDB {
namespace Dumper {

// ============================================================================
// Constants & Configuration
// ============================================================================

// Namespaces whose types should NOT be wrapped (BCL / Unity internals)
static const std::set<std::string> SKIP_NAMESPACES = {
    "System", "System.Collections", "System.Collections.Generic", "System.IO", "System.Text",
    "System.Threading", "System.Threading.Tasks", "System.Linq", "System.Reflection",
    "System.Runtime", "System.Runtime.CompilerServices", "System.Runtime.InteropServices",
    "System.Diagnostics", "System.Globalization", "System.Security", "System.ComponentModel",
    "System.Net", "System.Xml", "Mono", "mscorlib", "Internal", "Microsoft",
    "UnityEngine.Internal", "UnityEngineInternal"
};

static bool ShouldSkipNamespace(const std::string& ns) {
    if (SKIP_NAMESPACES.find(ns) != SKIP_NAMESPACES.end()) return true;
    if (ns.rfind("System.", 0) == 0) return true;
    if (ns.rfind("Mono.", 0) == 0) return true;
    if (ns.rfind("Internal.", 0) == 0) return true;
    if (ns.rfind("Microsoft.", 0) == 0) return true;
    return false;
}

// ============================================================================
// IL2CPP Type-Name Helpers
// ============================================================================

static uintptr_t GetGameAssemblyBaseAddress() {
    return (uintptr_t)GetModuleHandleW(L"GameAssembly.dll");
}

static bool _il2cpp_type_is_byref(const il2cppType* type) {
    if (api::il2cpp_type_is_byref) {
        return api::il2cpp_type_is_byref(type);
    }
    return type->m_uByref != 0;
}

// Map IL2CPP primitive type enum to C# keyword
static std::string PrimitiveTypeName(unsigned int typeEnum) {
    switch (typeEnum) {
    case IL2CPP_TYPE_VOID:      return "void";
    case IL2CPP_TYPE_BOOLEAN:   return "bool";
    case IL2CPP_TYPE_CHAR:      return "char";
    case IL2CPP_TYPE_I1:        return "sbyte";
    case IL2CPP_TYPE_U1:        return "byte";
    case IL2CPP_TYPE_I2:        return "short";
    case IL2CPP_TYPE_U2:        return "ushort";
    case IL2CPP_TYPE_I4:        return "int";
    case IL2CPP_TYPE_U4:        return "uint";
    case IL2CPP_TYPE_I8:        return "long";
    case IL2CPP_TYPE_U8:        return "ulong";
    case IL2CPP_TYPE_R4:        return "float";
    case IL2CPP_TYPE_R8:        return "double";
    case IL2CPP_TYPE_STRING:    return "string";
    case IL2CPP_TYPE_OBJECT:    return "object";
    case IL2CPP_TYPE_I:         return "IntPtr";
    case IL2CPP_TYPE_U:         return "UIntPtr";
    default:                    return "";
    }
}

// Forward declarations for mutual recursion
static std::string GetFullyQualifiedTypeName(const il2cppType* type, const std::string& currentNamespace);
static std::string GetFullyQualifiedClassName(il2cppClass* klass, const std::string& currentNamespace);

/// Get the fully-qualified C# type name from an il2cppClass.
/// If the type lives in `currentNamespace`, returns the short name.
/// Otherwise, returns `global::Full.Namespace.TypeName`.
static std::string GetFullyQualifiedClassName(il2cppClass* klass, const std::string& currentNamespace) {
    if (!klass) return "object";

    const char* name = api::il2cpp_class_get_name(klass);
    const char* ns   = api::il2cpp_class_get_namespace(klass);
    if (!name) return "object";

    std::string nameStr(name);
    std::string nsStr(ns ? ns : "");

    // Check for primitives by well-known System class names
    if (nsStr == "System") {
        if (nameStr == "Void")      return "void";
        if (nameStr == "Boolean")   return "bool";
        if (nameStr == "Char")      return "char";
        if (nameStr == "SByte")     return "sbyte";
        if (nameStr == "Byte")      return "byte";
        if (nameStr == "Int16")     return "short";
        if (nameStr == "UInt16")    return "ushort";
        if (nameStr == "Int32")     return "int";
        if (nameStr == "UInt32")    return "uint";
        if (nameStr == "Int64")     return "long";
        if (nameStr == "UInt64")    return "ulong";
        if (nameStr == "Single")    return "float";
        if (nameStr == "Double")    return "double";
        if (nameStr == "String")    return "string";
        if (nameStr == "Object")    return "object";
        if (nameStr == "IntPtr")    return "IntPtr";
        if (nameStr == "UIntPtr")   return "UIntPtr";
    }

    // For types whose namespace matches the file we're generating, use short name
    // Handle the "Global" bucket: empty namespace -> "Global"
    std::string effectiveNs = nsStr.empty() ? "Global" : nsStr;
    if (effectiveNs == currentNamespace) {
        return nameStr;
    }

    // For empty namespace types referenced from elsewhere
    if (nsStr.empty()) {
        return "global::Global." + nameStr;
    }

    // Fully qualify with global:: prefix
    return "global::" + nsStr + "." + nameStr;
}

/// Get the fully-qualified C# type name from an il2cppType.
static std::string GetFullyQualifiedTypeName(const il2cppType* type, const std::string& currentNamespace) {
    if (!type) return "object";

    // Check for primitive types by IL2CPP type enum
    std::string prim = PrimitiveTypeName(type->m_uType);
    if (!prim.empty()) return prim;

    // For SZARRAY (T[])
    if (type->m_uType == IL2CPP_TYPE_SZARRAY) {
        auto elemType = type->m_pType;
        if (elemType) {
            return GetFullyQualifiedTypeName(elemType, currentNamespace) + "[]";
        }
    }

    // For GENERICINST (e.g., List<T>, Dictionary<K,V>)
    // IL2CPP doesn't expose a clean way to get generic type arguments,
    // so we fall back to getting the class name which includes the arity
    // and map known generic types to their C# equivalents.

    // Fall back to class-based resolution
    auto* klass = api::il2cpp_class_from_type(type);
    if (!klass) return "object";

    const char* name = api::il2cpp_class_get_name(klass);
    const char* ns   = api::il2cpp_class_get_namespace(klass);
    std::string nameStr(name ? name : "");
    std::string nsStr(ns ? ns : "");

    // Handle generic collections by mapping known generic type names
    // IL2CPP class names for generics look like "List`1", "Dictionary`2", etc.
    // For wrapper generation, we use the object-erased forms.
    if (type->m_uType == IL2CPP_TYPE_GENERICINST) {
        // Check if this is a well-known generic collection
        if (nsStr == "System.Collections.Generic") {
            if (nameStr.rfind("List`1", 0) == 0) return "List<object>";
            if (nameStr.rfind("Dictionary`2", 0) == 0) return "Dictionary<object, object>";
            if (nameStr.rfind("IList`1", 0) == 0) return "IList<object>";
            if (nameStr.rfind("IEnumerable`1", 0) == 0) return "IEnumerable<object>";
            if (nameStr.rfind("ICollection`1", 0) == 0) return "ICollection<object>";
            if (nameStr.rfind("IDictionary`2", 0) == 0) return "IDictionary<object, object>";
            if (nameStr.rfind("IReadOnlyList`1", 0) == 0) return "IReadOnlyList<object>";
            if (nameStr.rfind("IReadOnlyCollection`1", 0) == 0) return "IReadOnlyCollection<object>";
            if (nameStr.rfind("IEnumerator`1", 0) == 0) return "IEnumerator<object>";
            if (nameStr.rfind("KeyValuePair`2", 0) == 0) return "KeyValuePair<object, object>";
            if (nameStr.rfind("HashSet`1", 0) == 0) return "HashSet<object>";
            if (nameStr.rfind("Queue`1", 0) == 0) return "Queue<object>";
            if (nameStr.rfind("Stack`1", 0) == 0) return "Stack<object>";
            if (nameStr.rfind("LinkedList`1", 0) == 0) return "LinkedList<object>";
        }
        if (nsStr == "System") {
            if (nameStr.rfind("Nullable`1", 0) == 0) return "object";
            if (nameStr.rfind("Action`", 0) == 0) return "Action";
            if (nameStr.rfind("Func`", 0) == 0) return "object";
            if (nameStr.rfind("Tuple`", 0) == 0) return "object";
            if (nameStr.rfind("ValueTuple`", 0) == 0) return "object";
        }
        if (nsStr == "System.Threading.Tasks") {
            if (nameStr.rfind("Task`1", 0) == 0) return "Task<object>";
        }
        if (nsStr == "Cysharp.Threading.Tasks") {
            if (nameStr.rfind("UniTask`1", 0) == 0) return "object";
        }

        // Generic type not specifically handled - strip the arity suffix
        // and return object-erased form
        auto backtickPos = nameStr.find('`');
        if (backtickPos != std::string::npos) {
            // Unknown generic → just use object
            return "object";
        }
    }

    return GetFullyQualifiedClassName(klass, currentNamespace);
}

// ============================================================================
// Type Classification Helpers
// ============================================================================

enum class TypeKind { Delegate, Enum, Interface, Struct, Class };

struct ClassInfo {
    il2cppClass* klass;
    std::string name;
    std::string ns;            // effective namespace (empty→"Global")
    std::string rawNs;         // raw IL2CPP namespace (may be empty)
    std::string dll;
    int flags;
    bool is_valuetype;
    bool is_enum;
    bool is_interface;
    TypeKind kind;
    std::string visibility;
    bool is_abstract;
    bool is_sealed;
    bool is_static;
    std::string base_class;    // fully qualified base class for wrappers
};

static std::string GetVisibility(int flags) {
    auto visibility = flags & TYPE_ATTRIBUTE_VISIBILITY_MASK;
    switch (visibility) {
    case TYPE_ATTRIBUTE_PUBLIC:
    case TYPE_ATTRIBUTE_NESTED_PUBLIC:
        return "public";
    case TYPE_ATTRIBUTE_NOT_PUBLIC:
    case TYPE_ATTRIBUTE_NESTED_FAM_AND_ASSEM:
    case TYPE_ATTRIBUTE_NESTED_ASSEMBLY:
        return "internal";
    case TYPE_ATTRIBUTE_NESTED_PRIVATE:
        return "private";
    case TYPE_ATTRIBUTE_NESTED_FAMILY:
        return "protected";
    case TYPE_ATTRIBUTE_NESTED_FAM_OR_ASSEM:
        return "protected internal";
    default:
        return "internal";
    }
}

static std::string GetMethodVisibility(uint32_t flags) {
    auto access = flags & METHOD_ATTRIBUTE_MEMBER_ACCESS_MASK;
    switch (access) {
    case METHOD_ATTRIBUTE_PRIVATE:        return "private";
    case METHOD_ATTRIBUTE_PUBLIC:         return "public";
    case METHOD_ATTRIBUTE_FAMILY:         return "protected";
    case METHOD_ATTRIBUTE_ASSEM:
    case METHOD_ATTRIBUTE_FAM_AND_ASSEM:  return "internal";
    case METHOD_ATTRIBUTE_FAM_OR_ASSEM:   return "protected internal";
    default:                              return "private";
    }
}

static std::string GetFieldVisibility(uint32_t attrs) {
    auto access = attrs & FIELD_ATTRIBUTE_FIELD_ACCESS_MASK;
    switch (access) {
    case FIELD_ATTRIBUTE_PRIVATE:        return "private";
    case FIELD_ATTRIBUTE_PUBLIC:         return "public";
    case FIELD_ATTRIBUTE_FAMILY:         return "protected";
    case FIELD_ATTRIBUTE_ASSEMBLY:
    case FIELD_ATTRIBUTE_FAM_AND_ASSEM:  return "internal";
    case FIELD_ATTRIBUTE_FAM_OR_ASSEM:   return "protected internal";
    default:                              return "private";
    }
}

/// Check if a class is a delegate (parent is System.MulticastDelegate)
static bool IsDelegate(il2cppClass* klass) {
    auto* parent = api::il2cpp_class_get_parent(klass);
    if (!parent) return false;
    const char* parentName = api::il2cpp_class_get_name(parent);
    const char* parentNs   = api::il2cpp_class_get_namespace(parent);
    if (!parentName || !parentNs) return false;
    return (std::string(parentNs) == "System" &&
            (std::string(parentName) == "MulticastDelegate" || std::string(parentName) == "Delegate"));
}

/// Classify a type and fill in ClassInfo
static ClassInfo ClassifyType(il2cppClass* klass, const std::string& dllName, const std::string& effectiveNamespace) {
    ClassInfo info{};
    info.klass = klass;
    info.name = api::il2cpp_class_get_name(klass) ? api::il2cpp_class_get_name(klass) : "";
    const char* rawNs = api::il2cpp_class_get_namespace(klass);
    info.rawNs = rawNs ? rawNs : "";
    info.ns = effectiveNamespace;
    info.dll = dllName;
    info.flags = api::il2cpp_class_get_flags(klass);
    info.is_valuetype = api::il2cpp_class_is_valuetype(klass);
    info.is_enum = api::il2cpp_class_is_enum(klass);
    info.is_interface = (info.flags & TYPE_ATTRIBUTE_INTERFACE) != 0;
    info.is_abstract = (info.flags & TYPE_ATTRIBUTE_ABSTRACT) != 0;
    info.is_sealed = (info.flags & TYPE_ATTRIBUTE_SEALED) != 0;
    info.is_static = info.is_abstract && info.is_sealed;
    info.visibility = GetVisibility(info.flags);

    // Determine kind
    if (IsDelegate(klass)) {
        info.kind = TypeKind::Delegate;
    } else if (info.is_enum) {
        info.kind = TypeKind::Enum;
    } else if (info.is_interface) {
        info.kind = TypeKind::Interface;
    } else if (info.is_valuetype) {
        info.kind = TypeKind::Struct;
    } else {
        info.kind = TypeKind::Class;
    }

    // Determine base class for class wrappers
    if (info.kind == TypeKind::Class) {
        auto* parent = api::il2cpp_class_get_parent(klass);
        if (parent) {
            auto parentType = api::il2cpp_class_get_type(parent);
            if (parentType && parentType->m_uType != IL2CPP_TYPE_OBJECT) {
                const char* parentName = api::il2cpp_class_get_name(parent);
                const char* parentNs = api::il2cpp_class_get_namespace(parent);
                std::string pName(parentName ? parentName : "");
                std::string pNs(parentNs ? parentNs : "");

                // Skip synthetic base types
                if (pNs == "System" && (pName == "ValueType" || pName == "Enum" ||
                    pName == "MulticastDelegate" || pName == "Delegate")) {
                    info.base_class = "";
                } else {
                    info.base_class = GetFullyQualifiedClassName(parent, effectiveNamespace);
                }
            }
        }

        // Default base: Il2CppObject
        if (info.base_class.empty()) {
            info.base_class = "Il2CppObject";
        }
    }

    return info;
}

// ============================================================================
// Delegate Generation
// ============================================================================

static std::string GenerateDelegate(il2cppClass* klass, const std::string& currentNamespace) {
    std::stringstream ss;

    // Find the "Invoke" method for the delegate signature
    void* iter = nullptr;
    const il2cppMethodInfo* invokeMethod = nullptr;
    while (auto method = api::il2cpp_class_get_methods(klass, &iter)) {
        if (std::string(api::il2cpp_method_get_name(method)) == "Invoke") {
            invokeMethod = method;
            break;
        }
    }

    std::string vis = GetVisibility(api::il2cpp_class_get_flags(klass));
    const char* delegateName = api::il2cpp_class_get_name(klass);

    if (!invokeMethod) {
        // Fallback
        ss << "    " << vis << " delegate void " << delegateName << "();\n";
        return ss.str();
    }

    auto returnType = api::il2cpp_method_get_return_type(invokeMethod);
    std::string returnTypeName = GetFullyQualifiedTypeName(returnType, currentNamespace);

    ss << "    " << vis << " delegate " << returnTypeName << " " << delegateName << "(";

    auto paramCount = api::il2cpp_method_get_param_count(invokeMethod);
    for (uint32_t i = 0; i < paramCount; ++i) {
        if (i > 0) ss << ", ";
        auto param = api::il2cpp_method_get_param(invokeMethod, i);
        std::string paramTypeName = GetFullyQualifiedTypeName(param, currentNamespace);
        const char* paramName = api::il2cpp_method_get_param_name(invokeMethod, i);
        ss << paramTypeName << " " << (paramName ? paramName : ("arg" + std::to_string(i)));
    }

    ss << ");\n";
    return ss.str();
}

// ============================================================================
// Enum Generation
// ============================================================================

static std::string GenerateEnum(il2cppClass* klass) {
    std::stringstream ss;

    std::string vis = GetVisibility(api::il2cpp_class_get_flags(klass));
    ss << "    " << vis << " enum " << api::il2cpp_class_get_name(klass) << "\n";
    ss << "    {\n";

    void* iter = nullptr;
    bool first = true;
    while (auto field = api::il2cpp_class_get_fields(klass, &iter)) {
        auto attrs = api::il2cpp_field_get_flags(field);
        if (!(attrs & FIELD_ATTRIBUTE_LITERAL)) continue;

        if (!first) ss << ",\n";
        first = false;

        uint64_t val = 0;
        api::il2cpp_field_static_get_value(field, &val);
        ss << "        " << api::il2cpp_field_get_name(field) << " = " << std::dec << (int64_t)val;
    }

    if (!first) ss << "\n";
    ss << "    }\n";
    return ss.str();
}

// ============================================================================
// Interface Generation (Stub)
// ============================================================================

static std::string GenerateInterface(il2cppClass* klass) {
    std::stringstream ss;
    std::string vis = GetVisibility(api::il2cpp_class_get_flags(klass));
    ss << "    " << vis << " interface " << api::il2cpp_class_get_name(klass) << "\n";
    ss << "    {\n";
    ss << "        // Stub interface\n";
    ss << "    }\n";
    return ss.str();
}

// ============================================================================
// Struct Generation
// ============================================================================

static std::string GenerateStruct(il2cppClass* klass, const std::string& currentNamespace) {
    std::stringstream ss;
    std::string vis = GetVisibility(api::il2cpp_class_get_flags(klass));
    std::string name = api::il2cpp_class_get_name(klass);

    ss << "    " << vis << " struct " << name << "\n";
    ss << "    {\n";

    bool hasFields = false;
    void* iter = nullptr;
    while (auto field = api::il2cpp_class_get_fields(klass, &iter)) {
        auto attrs = api::il2cpp_field_get_flags(field);

        // Skip static/const/literal fields for struct layout
        if (attrs & FIELD_ATTRIBUTE_STATIC) continue;
        if (attrs & FIELD_ATTRIBUTE_LITERAL) continue;

        auto fieldType = api::il2cpp_field_get_type(field);
        std::string fieldTypeName = GetFullyQualifiedTypeName(fieldType, currentNamespace);
        const char* fieldName = api::il2cpp_field_get_name(field);
        if (!fieldName) continue;
        // Skip compiler-generated backing fields
        if (fieldName[0] == '<') continue;

        ss << "        public " << fieldTypeName << " " << fieldName << ";\n";
        hasFields = true;
    }

    if (!hasFields) {
        ss << "        // Stub struct\n";
    }

    ss << "    }\n";
    return ss.str();
}

// ============================================================================
// Class Generation (Fields, Properties, Methods)
// ============================================================================

/// Generate the field-as-property wrappers for a class
static std::string GenerateClassFields(il2cppClass* klass, const std::string& currentNamespace) {
    std::stringstream ss;
    bool hasFields = false;

    void* iter = nullptr;
    while (auto field = api::il2cpp_class_get_fields(klass, &iter)) {
        auto attrs = api::il2cpp_field_get_flags(field);

        // Skip static, const, literal, and compiler-controlled fields
        if (attrs & FIELD_ATTRIBUTE_LITERAL) continue;
        if (attrs & FIELD_ATTRIBUTE_STATIC) continue;
        auto access = attrs & FIELD_ATTRIBUTE_FIELD_ACCESS_MASK;
        if (access == FIELD_ATTRIBUTE_COMPILER_CONTROLLED) continue;

        const char* fieldName = api::il2cpp_field_get_name(field);
        if (!fieldName) continue;
        // Skip backing fields
        if (fieldName[0] == '<') continue;

        std::string vis = GetFieldVisibility(attrs);
        auto fieldType = api::il2cpp_field_get_type(field);
        std::string typeName = GetFullyQualifiedTypeName(fieldType, currentNamespace);

        if (!hasFields) {
            ss << "\n        // Fields\n";
            hasFields = true;
        }

        ss << "        " << vis << " " << typeName << " " << fieldName << "\n";
        ss << "        {\n";
        ss << "            get => Il2CppRuntime.GetField<" << typeName << ">(this, \"" << fieldName << "\");\n";
        ss << "            set => Il2CppRuntime.SetField<" << typeName << ">(this, \"" << fieldName << "\", value);\n";
        ss << "        }\n\n";
    }

    return ss.str();
}

/// Generate property wrappers (get_/set_ methods exposed as C# properties)
static std::string GenerateClassProperties(il2cppClass* klass, const std::string& currentNamespace,
                                            bool classIsStatic) {
    std::stringstream ss;
    bool hasProperties = false;

    const char* classNsRaw = api::il2cpp_class_get_namespace(klass);
    const char* classNameRaw = api::il2cpp_class_get_name(klass);
    std::string classNs(classNsRaw ? classNsRaw : "");
    std::string className(classNameRaw ? classNameRaw : "");
    std::string staticNs = classNs.empty() ? "Global" : classNs;

    void* iter = nullptr;
    while (auto prop_const = api::il2cpp_class_get_properties(klass, &iter)) {
        auto prop = const_cast<il2cppPropertyInfo*>(prop_const);
        auto get = api::il2cpp_property_get_get_method(prop);
        auto set = api::il2cpp_property_get_set_method(prop);
        auto propName = api::il2cpp_property_get_name(prop);
        if (!propName) continue;

        std::string propTypeName;
        std::string vis;
        bool isStatic = false;
        uint32_t iflags = 0;

        if (get) {
            auto flags = api::il2cpp_method_get_flags(get, &iflags);
            vis = GetMethodVisibility(flags);
            isStatic = (flags & METHOD_ATTRIBUTE_STATIC) != 0;
            auto retType = api::il2cpp_method_get_return_type(get);
            propTypeName = GetFullyQualifiedTypeName(retType, currentNamespace);
        } else if (set) {
            auto flags = api::il2cpp_method_get_flags(set, &iflags);
            vis = GetMethodVisibility(flags);
            isStatic = (flags & METHOD_ATTRIBUTE_STATIC) != 0;
            auto param = api::il2cpp_method_get_param(set, 0);
            propTypeName = GetFullyQualifiedTypeName(param, currentNamespace);
        }

        if (propTypeName.empty()) continue;

        if (!hasProperties) {
            ss << "\n        // Properties\n";
            hasProperties = true;
        }

        ss << "        " << vis;
        if (isStatic) ss << " static";
        ss << " " << propTypeName << " " << propName << "\n";
        ss << "        {\n";

        if (get) {
            if (isStatic) {
                ss << "            get => Il2CppRuntime.CallStatic<" << propTypeName << ">(\""
                   << staticNs << "\", \"" << className << "\", \"get_" << propName
                   << "\", global::System.Type.EmptyTypes);\n";
            } else {
                ss << "            get => Il2CppRuntime.Call<" << propTypeName << ">(this, \"get_"
                   << propName << "\", global::System.Type.EmptyTypes);\n";
            }
        }

        if (set) {
            if (isStatic) {
                ss << "            set => Il2CppRuntime.InvokeStaticVoid(\"" << staticNs << "\", \""
                   << className << "\", \"set_" << propName << "\", new[] { typeof("
                   << propTypeName << ") }, value);\n";
            } else {
                ss << "            set => Il2CppRuntime.InvokeVoid(this, \"set_" << propName
                   << "\", new[] { typeof(" << propTypeName << ") }, value);\n";
            }
        }

        ss << "        }\n\n";
    }

    return ss.str();
}

/// Generate method wrappers
static std::string GenerateClassMethods(il2cppClass* klass, const std::string& currentNamespace,
                                         bool classIsStatic) {
    std::stringstream ss;
    bool hasMethods = false;

    // Collect property accessor names to skip
    std::set<std::string> propertyMethods;
    {
        void* piter = nullptr;
        while (auto prop_const = api::il2cpp_class_get_properties(klass, &piter)) {
            auto prop = const_cast<il2cppPropertyInfo*>(prop_const);
            auto propName = api::il2cpp_property_get_name(prop);
            if (propName) {
                auto get = api::il2cpp_property_get_get_method(prop);
                auto set = api::il2cpp_property_get_set_method(prop);
                if (get) propertyMethods.insert("get_" + std::string(propName));
                if (set) propertyMethods.insert("set_" + std::string(propName));
            }
        }
    }

    const char* classNsRaw = api::il2cpp_class_get_namespace(klass);
    const char* classNameRaw = api::il2cpp_class_get_name(klass);
    std::string classNs(classNsRaw ? classNsRaw : "");
    std::string className(classNameRaw ? classNameRaw : "");
    std::string staticNs = classNs.empty() ? "Global" : classNs;

    void* iter = nullptr;
    while (auto method = api::il2cpp_class_get_methods(klass, &iter)) {
        const char* methodName = api::il2cpp_method_get_name(method);
        if (!methodName) continue;
        std::string methodNameStr(methodName);

        // Skip constructors, finalizers, and property accessors
        if (methodNameStr == ".ctor" || methodNameStr == ".cctor" || methodNameStr == "Finalize") continue;
        if (propertyMethods.count(methodNameStr)) continue;

        uint32_t iflags = 0;
        auto flags = api::il2cpp_method_get_flags(method, &iflags);

        // Skip event accessors and operator overloads with special names
        if ((flags & METHOD_ATTRIBUTE_SPECIAL_NAME) &&
            (methodNameStr.rfind("add_", 0) == 0 || methodNameStr.rfind("remove_", 0) == 0 ||
             methodNameStr.rfind("op_", 0) == 0)) {
            continue;
        }

        // Skip abstract methods
        if (flags & METHOD_ATTRIBUTE_ABSTRACT) continue;

        std::string vis = GetMethodVisibility(flags);
        bool isStatic = (flags & METHOD_ATTRIBUTE_STATIC) != 0;

        // Return type
        auto returnType = api::il2cpp_method_get_return_type(method);
        std::string returnTypeName = GetFullyQualifiedTypeName(returnType, currentNamespace);
        bool isVoid = (returnTypeName == "void");

        // Parameters
        auto paramCount = api::il2cpp_method_get_param_count(method);

        if (!hasMethods) {
            ss << "\n        // Methods\n";
            hasMethods = true;
        }

        // Signature
        ss << "        " << vis;
        if (isStatic) ss << " static";
        ss << " " << returnTypeName << " " << methodNameStr << "(";

        std::vector<std::string> paramNames;
        std::vector<std::string> paramTypeNames;
        for (uint32_t i = 0; i < paramCount; ++i) {
            if (i > 0) ss << ", ";
            auto param = api::il2cpp_method_get_param(method, i);
            std::string pTypeName = GetFullyQualifiedTypeName(param, currentNamespace);
            const char* pName = api::il2cpp_method_get_param_name(method, i);
            std::string pNameStr = pName ? pName : ("arg" + std::to_string(i));
            paramNames.push_back(pNameStr);
            paramTypeNames.push_back(pTypeName);

            // Handle ref/out/in
            if (_il2cpp_type_is_byref(param)) {
                auto pAttrs = param->m_uAttributes;
                if (pAttrs & PARAM_ATTRIBUTE_OUT && !(pAttrs & PARAM_ATTRIBUTE_IN)) {
                    ss << "out ";
                } else if (pAttrs & PARAM_ATTRIBUTE_IN && !(pAttrs & PARAM_ATTRIBUTE_OUT)) {
                    ss << "in ";
                } else {
                    ss << "ref ";
                }
            }
            ss << pTypeName << " " << pNameStr;
        }
        ss << ")\n";
        ss << "        {\n";

        // Build Type[] expression
        std::string typeArrayExpr;
        if (paramCount == 0) {
            typeArrayExpr = "global::System.Type.EmptyTypes";
        } else {
            typeArrayExpr = "new Type[] { ";
            for (uint32_t i = 0; i < paramCount; ++i) {
                if (i > 0) typeArrayExpr += ", ";
                typeArrayExpr += "typeof(" + paramTypeNames[i] + ")";
            }
            typeArrayExpr += " }";
        }

        // Method body
        if (isStatic) {
            if (isVoid) {
                ss << "            Il2CppRuntime.InvokeStaticVoid(\"" << staticNs << "\", \""
                   << className << "\", \"" << methodNameStr << "\", " << typeArrayExpr;
            } else {
                ss << "            return Il2CppRuntime.CallStatic<" << returnTypeName << ">(\""
                   << staticNs << "\", \"" << className << "\", \"" << methodNameStr << "\", " << typeArrayExpr;
            }
        } else {
            if (isVoid) {
                ss << "            Il2CppRuntime.InvokeVoid(this, \"" << methodNameStr << "\", " << typeArrayExpr;
            } else {
                ss << "            return Il2CppRuntime.Call<" << returnTypeName << ">(this, \""
                   << methodNameStr << "\", " << typeArrayExpr;
            }
        }

        // Append arguments
        for (uint32_t i = 0; i < paramCount; ++i) {
            ss << ", " << paramNames[i];
        }
        ss << ");\n";
        ss << "        }\n\n";
    }

    return ss.str();
}

/// Generate a full class wrapper
static std::string GenerateClass(const ClassInfo& info, const std::string& currentNamespace) {
    std::stringstream ss;

    ss << "    " << info.visibility << " partial class " << info.name << " : " << info.base_class << "\n";
    ss << "    {\n";
    ss << "        public " << info.name << "(IntPtr nativePtr) : base(nativePtr) { }\n";

    // Fields as properties (skip for static classes)
    if (!info.is_static) {
        ss << GenerateClassFields(info.klass, currentNamespace);
    }

    // Properties
    ss << GenerateClassProperties(info.klass, currentNamespace, info.is_static);

    // Methods
    ss << GenerateClassMethods(info.klass, currentNamespace, info.is_static);

    ss << "    }\n";
    return ss.str();
}

// ============================================================================
// File-Level Wrapper Generation
// ============================================================================

/// Build the using-statements header, excluding the file's own namespace
static std::string BuildUsingStatements(const std::string& fileNamespace) {
    std::stringstream ss;

    ss << "using System;\n";
    ss << "using System.Collections;\n";
    ss << "using System.Collections.Generic;\n";
    ss << "using GameSDK;\n";
    ss << "\n";
    ss << "// Core Unity namespace references\n";
    ss << "using TMPro;\n";
    ss << "using Unity.Mathematics;\n";

    auto maybeUsing = [&](const std::string& ns) {
        if (ns != fileNamespace) {
            ss << "using " << ns << ";\n";
        }
    };

    maybeUsing("UnityEngine");
    maybeUsing("UnityEngine.AI");
    maybeUsing("UnityEngine.Animations");
    maybeUsing("UnityEngine.Audio");
    maybeUsing("UnityEngine.EventSystems");
    maybeUsing("UnityEngine.Events");
    maybeUsing("UnityEngine.Rendering");
    maybeUsing("UnityEngine.SceneManagement");
    maybeUsing("UnityEngine.UI");

    ss << "\n// System namespaces for common types\n";
    ss << "using System.Text;\n";
    ss << "using System.IO;\n";
    ss << "using System.Xml;\n";
    ss << "using System.Reflection;\n";
    ss << "using System.Globalization;\n";
    ss << "using System.Runtime.Serialization;\n";
    ss << "using System.Threading;\n";
    ss << "using System.Threading.Tasks;\n";

    return ss.str();
}

/// Produce a safe filename from a namespace: dots → underscores
static std::string SafeFileName(const std::string& ns) {
    std::string safe = ns;
    std::replace(safe.begin(), safe.end(), '.', '_');
    if (safe.empty()) safe = "Global";
    return safe;
}

// ============================================================================
// Main Dump & Generate Function
// ============================================================================

DumpResult DumpIL2CppRuntime(const std::string& output_directory) {
    DumpResult result = { false, "", "", 0, 0, {}, 0 };

    // ---- Wait for GameAssembly.dll ----
    uintptr_t gaBase = GetGameAssemblyBaseAddress();
    if (gaBase == 0) {
        result.error_message = "GameAssembly.dll not found";
        return result;
    }

    // ---- Resolve IL2CPP exports ----
    auto status = api::ensure_exports();
    if (status != Il2CppStatus::OK) {
        result.error_message = std::string("Failed to resolve IL2CPP exports: ") + to_string(status);
        return result;
    }

    if (!api::il2cpp_assembly_get_image || !api::il2cpp_image_get_name ||
        !api::il2cpp_image_get_class_count || !api::il2cpp_image_get_class ||
        !api::il2cpp_class_get_type || !api::il2cpp_class_get_name) {
        result.error_message = "Required dumper APIs not resolved";
        return result;
    }

    // ---- Get domain & assemblies ----
    size_t size;
    auto domain = api::il2cpp_domain_get();
    if (!domain) { result.error_message = "Failed to get IL2CPP domain"; return result; }

    auto assemblies = api::il2cpp_domain_get_assemblies(domain, &size);
    if (!assemblies) { result.error_message = "Failed to get assemblies"; return result; }
    result.total_assemblies = size;

    // ---- Phase 1: Collect all types grouped by effective namespace ----
    std::map<std::string, std::vector<ClassInfo>> typesByNamespace;
    size_t totalClasses = 0;

    // Also build the image list for the raw diagnostic dump
    std::stringstream rawDump;
    for (size_t i = 0; i < size; ++i) {
        auto image = api::il2cpp_assembly_get_image(assemblies[i]);
        rawDump << "// Image " << i << ": " << api::il2cpp_image_get_name(image) << "\n";
    }

    for (size_t i = 0; i < size; ++i) {
        auto image = api::il2cpp_assembly_get_image(assemblies[i]);
        std::string dllName = api::il2cpp_image_get_name(image);
        auto classCount = api::il2cpp_image_get_class_count(image);
        totalClasses += classCount;

        for (size_t j = 0; j < classCount; ++j) {
            auto klass = api::il2cpp_image_get_class(image, j);
            if (!klass) continue;

            const char* ns = api::il2cpp_class_get_namespace(klass);
            const char* name = api::il2cpp_class_get_name(klass);
            if (!name) continue;

            std::string nsStr(ns ? ns : "");
            std::string nameStr(name);

            // Skip compiler-generated types
            if (nameStr.find('<') != std::string::npos) continue;
            if (nameStr.find('>') != std::string::npos) continue;
            if (nameStr.find('/') != std::string::npos) continue;

            // Skip system/internal namespaces
            if (ShouldSkipNamespace(nsStr)) continue;

            // Skip non-public types
            int flags = api::il2cpp_class_get_flags(klass);
            auto vis = flags & TYPE_ATTRIBUTE_VISIBILITY_MASK;
            if (vis != TYPE_ATTRIBUTE_PUBLIC && vis != TYPE_ATTRIBUTE_NESTED_PUBLIC) continue;

            // Use "Global" bucket for empty namespace
            std::string bucketNs = nsStr.empty() ? "Global" : nsStr;

            ClassInfo info = ClassifyType(klass, dllName, bucketNs);
            typesByNamespace[bucketNs].push_back(info);
        }
    }
    result.total_classes = totalClasses;

    // ---- Phase 2: Generate .cs files per namespace ----
    std::filesystem::create_directories(output_directory);

    for (auto& [ns, types] : typesByNamespace) {
        if (types.empty()) continue;

        std::stringstream file;

        // File header
        file << "// Auto-generated Il2Cpp wrapper classes\n";
        file << "// Namespace: " << ns << "\n";
        file << "// Do not edit manually\n\n";

        // Using statements
        file << BuildUsingStatements(ns) << "\n";

        // Namespace declaration
        file << "namespace " << ns << "\n";
        file << "{\n";

        // Sort types: delegates → enums → interfaces → structs → classes
        std::stable_sort(types.begin(), types.end(), [](const ClassInfo& a, const ClassInfo& b) {
            return static_cast<int>(a.kind) < static_cast<int>(b.kind);
        });

        for (const auto& info : types) {
            switch (info.kind) {
            case TypeKind::Delegate:
                file << GenerateDelegate(info.klass, ns) << "\n";
                break;
            case TypeKind::Enum:
                file << GenerateEnum(info.klass) << "\n";
                break;
            case TypeKind::Interface:
                file << GenerateInterface(info.klass) << "\n";
                break;
            case TypeKind::Struct:
                file << GenerateStruct(info.klass, ns) << "\n";
                break;
            case TypeKind::Class:
                file << GenerateClass(info, ns) << "\n";
                result.total_wrappers_generated++;
                break;
            }
        }

        file << "}\n";

        // Write file: GameSDK.<SafeNamespace>.cs
        std::string safeName = SafeFileName(ns);
        std::string filename = "GameSDK." + safeName + ".cs";
        std::filesystem::path filePath = std::filesystem::path(output_directory) / filename;

        std::ofstream outFile(filePath);
        if (!outFile.is_open()) {
            result.error_message = "Failed to write: " + filePath.string();
            return result;
        }
        outFile << file.str();
        outFile.close();

        result.generated_files.push_back(filePath.string());
    }

    // ---- Phase 3: Write raw dump.cs for diagnostics ----
    std::string dumpPath = output_directory + "\\dump.cs";
    std::ofstream rawOut(dumpPath);
    if (rawOut.is_open()) {
        rawOut << rawDump.str();
        rawOut.close();
    }
    result.dump_path = dumpPath;

    result.success = true;
    return result;
}

// ============================================================================
// Freshness Checks
// ============================================================================

bool IsDumpFresh(const std::string& dump_path) {
    if (!std::filesystem::exists(dump_path)) return false;

    HMODULE hGA = GetModuleHandleW(L"GameAssembly.dll");
    if (!hGA) return false;

    wchar_t gaPath[MAX_PATH];
    if (GetModuleFileNameW(hGA, gaPath, MAX_PATH) == 0) return false;

    try {
        auto dumpTime = std::filesystem::last_write_time(dump_path);
        auto gaTime   = std::filesystem::last_write_time(gaPath);
        return dumpTime > gaTime;
    } catch (...) {
        return false;
    }
}

bool AreWrappersFresh(const std::string& output_directory) {
    if (!std::filesystem::exists(output_directory)) return false;

    bool hasFiles = false;
    for (const auto& entry : std::filesystem::directory_iterator(output_directory)) {
        if (entry.path().extension() == ".cs") { hasFiles = true; break; }
    }
    if (!hasFiles) return false;

    HMODULE hGA = GetModuleHandleW(L"GameAssembly.dll");
    if (!hGA) return false;

    wchar_t gaPath[MAX_PATH];
    if (GetModuleFileNameW(hGA, gaPath, MAX_PATH) == 0) return false;

    try {
        auto gaTime = std::filesystem::last_write_time(gaPath);
        std::filesystem::file_time_type oldestWrapper = (std::filesystem::file_time_type::max)();

        for (const auto& entry : std::filesystem::directory_iterator(output_directory)) {
            if (entry.path().extension() == ".cs") {
                auto wTime = std::filesystem::last_write_time(entry.path());
                if (wTime < oldestWrapper) oldestWrapper = wTime;
            }
        }
        return oldestWrapper > gaTime;
    } catch (...) {
        return false;
    }
}

} // namespace Dumper
} // namespace MDB
