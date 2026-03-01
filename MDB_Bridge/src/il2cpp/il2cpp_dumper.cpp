#include "il2cpp_dumper.hpp"
#include "il2cpp_resolver.hpp"
#include "obfuscation_detector.hpp"
#include "mapping_loader.hpp"

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
    if (ns.rfind("MS.", 0) == 0) return true;
    return false;
}

// Set of fully-qualified type names that will be emitted (populated between Phase 1 and Phase 2)
static std::set<std::string> g_knownTypes;

// Obfuscation fake method detector (populated early in DumpIL2CppRuntime)
static MDB::Obfuscation::Detector* g_obfuscation_detector = nullptr;

// Deobfuscation mapping lookup (loaded from mappings.json during dump)
static MDB::Mappings::MappingLookup g_mappingLookup;

// Namespaces whose types are not available in .NET Framework 4.7.2
static bool IsBlockedNamespace(const std::string& ns) {
    if (ns == "Mono" || ns.rfind("Mono.", 0) == 0) return true;
    if (ns == "Internal" || ns.rfind("Internal.", 0) == 0) return true;
    if (ns == "UnityEngineInternal" || ns == "UnityEngine.Internal") return true;
    if (ns == "System.IO.Enumeration") return true;
    if (ns == "System.Net.Http") return true;
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

/// Strip backtick+arity suffix from IL2CPP generic type names for use as C# identifiers.
/// e.g. "List`1" -> "List", "Dictionary`2" -> "Dictionary"
static std::string SanitizeTypeName(const std::string& name) {
    auto pos = name.find('`');
    return (pos != std::string::npos) ? name.substr(0, pos) : name;
}

/// Walk the declaring type chain to find the effective namespace for nested types.
/// In IL2CPP metadata, nested types (e.g. InputField.ContentType) have an empty namespace;
/// the real namespace is on the outermost declaring type.
/// Returns the resolved namespace string (may still be empty for truly global types).
static std::string ResolveEffectiveNamespace(il2cppClass* klass) {
    if (!klass) return "";

    // First check this class's own namespace
    const char* ns = api::il2cpp_class_get_namespace(klass);
    std::string nsStr(ns ? ns : "");
    if (!nsStr.empty()) return nsStr;

    // Empty namespace — check if this is a nested type by walking declaring type chain
    il2cppClass* declaring = nullptr;

    // Prefer the API function if available
    if (api::il2cpp_class_get_declaring_type) {
        declaring = api::il2cpp_class_get_declaring_type(klass);
    } else {
        // Fallback: read directly from struct field
        declaring = klass->m_pDeclareClass;
    }

    if (declaring) {
        // Walk up the chain (handles multiply-nested types like A.B.C)
        constexpr int MAX_DEPTH = 16; // safety limit
        for (int depth = 0; depth < MAX_DEPTH && declaring; ++depth) {
            const char* declNs = api::il2cpp_class_get_namespace(declaring);
            std::string declNsStr(declNs ? declNs : "");
            if (!declNsStr.empty()) return declNsStr;

            // Keep walking up
            il2cppClass* next = nullptr;
            if (api::il2cpp_class_get_declaring_type) {
                next = api::il2cpp_class_get_declaring_type(declaring);
            } else {
                next = declaring->m_pDeclareClass;
            }
            declaring = next;
        }
    }

    // Truly namespace-less type (no declaring type chain had a namespace)
    return "";
}

// Forward declarations for mutual recursion
static std::string GetFullyQualifiedTypeName(const il2cppType* type, const std::string& currentNamespace,
                                              const std::vector<std::string>* methodGenericParams = nullptr,
                                              uint32_t mvarBaseIndex = 0);
static std::string GetFullyQualifiedClassName(il2cppClass* klass, const std::string& currentNamespace);

/// Get the fully-qualified C# type name from an il2cppClass.
/// If the type lives in `currentNamespace`, returns the short name.
/// Otherwise, returns `global::Full.Namespace.TypeName`.
/// Validates game types against g_knownTypes registry when populated.
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

    // Block types from namespaces not available in .NET Framework
    if (IsBlockedNamespace(nsStr)) return "object";

    // Sanitize generic backtick names for C# identifiers
    std::string safeName = SanitizeTypeName(nameStr);

    // Compiler-generated types (fixed buffers, display classes, state machines)
    // contain <> and are not valid C# identifiers - fall back to object
    if (safeName.find('<') != std::string::npos || safeName.find('>') != std::string::npos) {
        return "object";
    }

    // Determine if this is a system/framework type or a game type
    bool isSystemType = ShouldSkipNamespace(nsStr);

    // Block specific system types not available in .NET Framework 4.7.2
    if (isSystemType) {
        if (nsStr == "System.Threading.Tasks" && nameStr.rfind("ValueTask", 0) == 0) return "object";
        if (nsStr == "System.Buffers" || nsStr == "System.Memory") return "object";
    }

    // For system types with generic arity, add <object, ...> since the real .NET type is generic
    if (isSystemType) {
        auto backtickPos = nameStr.find('`');
        if (backtickPos != std::string::npos) {
            try {
                int arity = std::stoi(nameStr.substr(backtickPos + 1));
                if (arity > 0) {
                    safeName += "<";
                    for (int a = 0; a < arity; ++a) {
                        if (a > 0) safeName += ", ";
                        safeName += "object";
                    }
                    safeName += ">";
                }
            } catch (...) {}
        }
    }

    // Resolve the effective namespace (walks declaring type chain for nested types)
    std::string resolvedNs = nsStr.empty() ? ResolveEffectiveNamespace(klass) : nsStr;
    std::string effectiveNs = resolvedNs.empty() ? "Global" : resolvedNs;

    // For game types, validate against known types registry
    if (!isSystemType && !g_knownTypes.empty()) {
        std::string fqn = resolvedNs.empty() ? safeName : (resolvedNs + "." + safeName);
        if (g_knownTypes.find(fqn) == g_knownTypes.end()) {
            return "object";
        }
    }

    // Apply deobfuscation type name remapping (friendly display names)
    if (g_mappingLookup.HasMappings()) {
        std::string friendly = g_mappingLookup.ResolveType(safeName);
        if (!friendly.empty()) {
            safeName = friendly;
        }
    }

    // For types whose namespace matches the file we're generating, use short name
    if (effectiveNs == currentNamespace) {
        return safeName;
    }

    // Fully qualify with global:: prefix
    return "global::" + effectiveNs + "." + safeName;
}

/// Get the fully-qualified C# type name from an il2cppType.
/// When methodGenericParams is non-null, IL2CPP_TYPE_MVAR types are resolved to
/// the named type parameters (T, T0, T1, …) instead of being erased to "object".
/// mvarBaseIndex is the global generic-parameter index of the method's first type param.
static std::string GetFullyQualifiedTypeName(const il2cppType* type, const std::string& currentNamespace,
                                              const std::vector<std::string>* methodGenericParams,
                                              uint32_t mvarBaseIndex) {
    if (!type) return "object";

    // Check for primitive types by IL2CPP type enum
    std::string prim = PrimitiveTypeName(type->m_uType);
    if (!prim.empty()) return prim;

    // Method-level generic type parameters (MVAR) — resolve to T / T0 / T1 / … when available
    if (type->m_uType == IL2CPP_TYPE_MVAR) {
        if (methodGenericParams && !methodGenericParams->empty()) {
            uint32_t localIdx = type->m_uGenericParameterIndex - mvarBaseIndex;
            if (localIdx < methodGenericParams->size())
                return (*methodGenericParams)[localIdx];
        }
        return "object";
    }

    // Class-level generic type parameters (VAR) — erase to object (our class wrappers are non-generic)
    if (type->m_uType == IL2CPP_TYPE_VAR) {
        return "object";
    }

    // Pointer types (e.g., System.Int64*) → erase to IntPtr
    if (type->m_uType == IL2CPP_TYPE_PTR) {
        return "IntPtr";
    }

    // For SZARRAY (T[])
    if (type->m_uType == IL2CPP_TYPE_SZARRAY) {
        auto elemType = type->m_pType;
        if (elemType) {
            return GetFullyQualifiedTypeName(elemType, currentNamespace, methodGenericParams, mvarBaseIndex) + "[]";
        }
    }

    // For GENERICINST (e.g., List<T>, Dictionary<K,V>)
    // Walk the Il2CppGenericClass → Il2CppGenericInst → type_argv to get actual type args.
    if (type->m_uType == IL2CPP_TYPE_GENERICINST) {
        auto* genericClass = type->m_pGenericClass;
        
        // Fall back to class-based resolution to get the base type name
        auto* klass = api::il2cpp_class_from_type(type);
        if (!klass) return "object";

        const char* name = api::il2cpp_class_get_name(klass);
        const char* ns   = api::il2cpp_class_get_namespace(klass);
        std::string nameStr(name ? name : "");
        std::string nsStr(ns ? ns : "");

        // --- Special cases: types that should be erased entirely ---
        if (nsStr == "System") {
            if (nameStr.rfind("Nullable`1", 0) == 0) return "object";
            if (nameStr.rfind("Func`", 0) == 0) return "object";
            if (nameStr.rfind("Tuple`", 0) == 0) return "object";
            if (nameStr.rfind("ValueTuple`", 0) == 0) return "object";
            if (nameStr.rfind("Span`1", 0) == 0) return "object";
            if (nameStr.rfind("ReadOnlySpan`1", 0) == 0) return "object";
            if (nameStr.rfind("Memory`1", 0) == 0) return "object";
            if (nameStr.rfind("ReadOnlyMemory`1", 0) == 0) return "object";
        }
        if (nsStr == "System.Threading.Tasks") {
            if (nameStr.rfind("ValueTask`1", 0) == 0) return "object";
        }
        if (nsStr == "Cysharp.Threading.Tasks") {
            if (nameStr.rfind("UniTask`1", 0) == 0) return "object";
        }
        // Types from namespaces blocked or unavailable in .NET Framework 4.7.2
        if (IsBlockedNamespace(nsStr)) return "object";
        if (nsStr == "System.Runtime.CompilerServices") {
            if (nameStr.rfind("CallSite`", 0) == 0) return "object";
        }

        // --- Try to resolve actual generic type arguments ---
        std::vector<std::string> typeArgs;
        bool resolvedArgs = false;

        if (genericClass) {
            auto* classInst = genericClass->m_Context.m_pClassInst;
            if (classInst && classInst->m_uTypeArgc > 0 && classInst->m_pTypeArgv) {
                resolvedArgs = true;
                for (uint32_t i = 0; i < classInst->m_uTypeArgc; ++i) {
                    auto* argType = classInst->m_pTypeArgv[i];
                    if (argType) {
                        std::string resolved = GetFullyQualifiedTypeName(argType, currentNamespace, methodGenericParams, mvarBaseIndex);
                        // void is not a valid generic type argument — erase to object
                        if (resolved == "void") resolved = "object";
                        typeArgs.push_back(resolved);
                    } else {
                        typeArgs.push_back("object");
                    }
                }
            }
        }

        // If we couldn't resolve the args, fall back to object-erased form
        if (!resolvedArgs) {
            auto backtickPos = nameStr.find('`');
            if (backtickPos != std::string::npos) {
                try {
                    int arity = std::stoi(nameStr.substr(backtickPos + 1));
                    for (int a = 0; a < arity; ++a) typeArgs.push_back("object");
                } catch (...) {
                    typeArgs.push_back("object");
                }
            }
        }

        // Build the final type name
        if (typeArgs.empty()) {
            // Non-generic or failed to determine arity — use class resolution
            return GetFullyQualifiedClassName(klass, currentNamespace);
        }

        // For system types, produce a simple name (List<X>, Dictionary<X,Y>, etc.)
        bool isSystemType = ShouldSkipNamespace(nsStr);
        std::string baseName;
        if (isSystemType) {
            baseName = SanitizeTypeName(nameStr);
            // For Action`N with resolved args, use Action<...> form
            if (nsStr == "System" && nameStr.rfind("Action`", 0) == 0) {
                baseName = "Action";
            }
        } else {
            // Game types: our wrappers are emitted WITHOUT generic type parameters
            // (we erase T→object in class definitions), so we cannot reference them
            // with type args. Just return the plain class name.
            return GetFullyQualifiedClassName(klass, currentNamespace);
        }

        // If the base name resolved to "object" (blocked namespace, unknown type, etc.)
        // don't append generic type args — just return "object"
        if (baseName == "object") {
            return "object";
        }

        baseName += "<";
        for (size_t i = 0; i < typeArgs.size(); ++i) {
            if (i > 0) baseName += ", ";
            baseName += typeArgs[i];
        }
        baseName += ">";

        return baseName;
    }

    // Fall back to class-based resolution for all other types
    auto* klass = api::il2cpp_class_from_type(type);
    if (!klass) return "object";

    return GetFullyQualifiedClassName(klass, currentNamespace);
}

// ============================================================================
// Type Classification Helpers
// ============================================================================

enum class TypeKind { Delegate, Enum, Interface, Struct, Class };

struct ClassInfo {
    il2cppClass* klass;
    std::string name;          // display name (friendly if mapped, else sanitized)
    std::string rawName;       // raw IL2CPP name (unsanitized, always obfuscated)
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
    const char* rawClassName = api::il2cpp_class_get_name(klass);
    info.rawName = rawClassName ? rawClassName : "";
    info.name = SanitizeTypeName(info.rawName);
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
                } else if (ShouldSkipNamespace(pNs)) {
                    // System/framework types don't have IL2CPP wrapper IntPtr constructors
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

static std::string GenerateDelegate(il2cppClass* klass, const std::string& currentNamespace,
                                    const std::string& obfTypeName) {
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
    std::string delegateName = SanitizeTypeName(api::il2cpp_class_get_name(klass) ? api::il2cpp_class_get_name(klass) : "");

    // Resolve display name from mappings
    bool isDeobfuscated = false;
    if (g_mappingLookup.HasMappings()) {
        std::string friendly = g_mappingLookup.ResolveType(obfTypeName);
        if (!friendly.empty()) {
            delegateName = friendly;
            isDeobfuscated = true;
        }
    }

    if (isDeobfuscated) {
        ss << "    /// <summary>Deobfuscated delegate. IL2CPP name: '" << obfTypeName << "'</summary>\n";
    }

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
        ss << paramTypeName << " " << ((paramName && paramName[0] != '\0') ? paramName : ("arg" + std::to_string(i)));
    }

    ss << ");\n";
    return ss.str();
}

// ============================================================================
// Enum Generation
// ============================================================================

static std::string GenerateEnum(il2cppClass* klass, const std::string& obfTypeName) {
    std::stringstream ss;

    std::string vis = GetVisibility(api::il2cpp_class_get_flags(klass));

    // Resolve display name from mappings
    std::string displayName = SanitizeTypeName(api::il2cpp_class_get_name(klass));
    bool isDeobfuscated = false;
    if (g_mappingLookup.HasMappings()) {
        std::string friendly = g_mappingLookup.ResolveType(obfTypeName);
        if (!friendly.empty()) {
            displayName = friendly;
            isDeobfuscated = true;
        }
    }

    if (isDeobfuscated) {
        ss << "    /// <summary>Deobfuscated enum. IL2CPP name: '" << obfTypeName << "'</summary>\n";
    }
    ss << "    " << vis << " enum " << displayName;

    // Detect enum backing type from value__ field
    void* btIter = nullptr;
    unsigned int backingTypeEnum = IL2CPP_TYPE_I4; // default to int
    bool isUnsigned = false;
    while (auto btField = api::il2cpp_class_get_fields(klass, &btIter)) {
        const char* fn = api::il2cpp_field_get_name(btField);
        if (fn && std::string(fn) == "value__") {
            auto ftype = api::il2cpp_field_get_type(btField);
            if (ftype) {
                backingTypeEnum = ftype->m_uType;
                switch (backingTypeEnum) {
                case IL2CPP_TYPE_U4: ss << " : uint"; isUnsigned = true; break;
                case IL2CPP_TYPE_I8: ss << " : long"; break;
                case IL2CPP_TYPE_U8: ss << " : ulong"; isUnsigned = true; break;
                case IL2CPP_TYPE_I2: ss << " : short"; break;
                case IL2CPP_TYPE_U2: ss << " : ushort"; isUnsigned = true; break;
                case IL2CPP_TYPE_I1: ss << " : sbyte"; break;
                case IL2CPP_TYPE_U1: ss << " : byte"; isUnsigned = true; break;
                default: break; // int is default
                }
            }
            break;
        }
    }

    ss << "\n    {\n";

    void* iter = nullptr;
    bool first = true;
    while (auto field = api::il2cpp_class_get_fields(klass, &iter)) {
        auto attrs = api::il2cpp_field_get_flags(field);
        if (!(attrs & FIELD_ATTRIBUTE_LITERAL)) continue;

        if (!first) ss << ",\n";
        first = false;

        uint64_t val = 0;
        api::il2cpp_field_static_get_value(field, &val);
        ss << "        " << api::il2cpp_field_get_name(field) << " = ";
        if (isUnsigned) {
            ss << std::dec << val;
        } else {
            // Sign-extend based on backing type width
            int64_t signedVal;
            switch (backingTypeEnum) {
            case IL2CPP_TYPE_I1: signedVal = (int64_t)(int8_t)(val & 0xFF); break;
            case IL2CPP_TYPE_I2: signedVal = (int64_t)(int16_t)(val & 0xFFFF); break;
            case IL2CPP_TYPE_I8: signedVal = (int64_t)val; break;
            default: signedVal = (int64_t)(int32_t)(val & 0xFFFFFFFF); break;
            }
            ss << std::dec << signedVal;
        }
    }

    if (!first) ss << "\n";
    ss << "    }\n";
    return ss.str();
}

// ============================================================================
// Interface Generation (Stub)
// ============================================================================

static std::string GenerateInterface(il2cppClass* klass, const std::string& obfTypeName) {
    std::stringstream ss;
    std::string vis = GetVisibility(api::il2cpp_class_get_flags(klass));

    // Resolve display name from mappings
    std::string displayName = SanitizeTypeName(api::il2cpp_class_get_name(klass));
    bool isDeobfuscated = false;
    if (g_mappingLookup.HasMappings()) {
        std::string friendly = g_mappingLookup.ResolveType(obfTypeName);
        if (!friendly.empty()) {
            displayName = friendly;
            isDeobfuscated = true;
        }
    }

    if (isDeobfuscated) {
        ss << "    /// <summary>Deobfuscated interface. IL2CPP name: '" << obfTypeName << "'</summary>\n";
    }
    ss << "    " << vis << " interface " << displayName << "\n";
    ss << "    {\n";
    ss << "        // Stub interface\n";
    ss << "    }\n";
    return ss.str();
}

// ============================================================================
// Struct Generation
// ============================================================================

static std::string GenerateStruct(il2cppClass* klass, const std::string& currentNamespace,
                                  const std::string& obfTypeName) {
    std::stringstream ss;
    std::string vis = GetVisibility(api::il2cpp_class_get_flags(klass));

    // Resolve display name from mappings
    std::string displayName = SanitizeTypeName(api::il2cpp_class_get_name(klass));
    bool isDeobfuscated = false;
    if (g_mappingLookup.HasMappings()) {
        std::string friendly = g_mappingLookup.ResolveType(obfTypeName);
        if (!friendly.empty()) {
            displayName = friendly;
            isDeobfuscated = true;
        }
    }

    if (isDeobfuscated) {
        ss << "    /// <summary>Deobfuscated struct. IL2CPP name: '" << obfTypeName << "'</summary>\n";
    }
    ss << "    " << vis << " struct " << displayName << "\n";
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
        // Skip fields whose type is compiler-generated (e.g. <buffer>e__FixedBuffer)
        if (fieldTypeName.find('<') != std::string::npos || fieldTypeName.find('>') != std::string::npos) continue;

        // Resolve field display name from mappings
        std::string fieldNameStr(fieldName);
        std::string displayFieldName = fieldNameStr;
        if (g_mappingLookup.HasMappings()) {
            std::string ff = g_mappingLookup.ResolveMember(obfTypeName, fieldNameStr);
            if (!ff.empty()) {
                ss << "        /// <summary>Deobfuscated field. IL2CPP name: '" << fieldNameStr << "'</summary>\n";
                displayFieldName = ff;
            }
        }

        ss << "        public " << fieldTypeName << " " << displayFieldName << ";\n";
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
static std::string GenerateClassFields(il2cppClass* klass, const std::string& currentNamespace,
                                       const std::string& obfClassName) {
    std::stringstream ss;
    bool hasFields = false;
    std::set<std::string> emittedFieldNames;

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

        std::string fieldNameStr(fieldName);

        // Resolve field display name from mappings
        std::string displayFieldName = fieldNameStr;
        bool fieldIsDeobfuscated = false;
        if (g_mappingLookup.HasMappings()) {
            std::string friendly = g_mappingLookup.ResolveMember(obfClassName, fieldNameStr);
            if (!friendly.empty()) {
                displayFieldName = friendly;
                fieldIsDeobfuscated = true;
            }
        }

        // Skip duplicate field names (using display name for C# compilation)
        if (!emittedFieldNames.insert(displayFieldName).second) continue;

        std::string vis = GetFieldVisibility(attrs);
        auto fieldType = api::il2cpp_field_get_type(field);
        std::string typeName = GetFullyQualifiedTypeName(fieldType, currentNamespace);
        // Skip fields whose type is compiler-generated (e.g. <buffer>e__FixedBuffer)
        if (typeName.find('<') != std::string::npos || typeName.find('>') != std::string::npos) continue;

        // If the field type is an IL2CPP interface, use Il2CppObject instead.
        // C# can't instantiate interfaces, so GetField<InterfaceType> would return null.
        // Users can call .As<ConcreteType>() to re-wrap the result.
        {
            auto fieldClass = api::il2cpp_class_from_type(fieldType);
            if (fieldClass) {
                auto classFlags = api::il2cpp_class_get_flags(fieldClass);
                if (classFlags & TYPE_ATTRIBUTE_INTERFACE) {
                    typeName = "Il2CppObject";
                }
            }
        }

        if (!hasFields) {
            ss << "\n        // Fields\n";
            hasFields = true;
        }

        if (fieldIsDeobfuscated) {
            ss << "        /// <summary>Deobfuscated field. IL2CPP name: '" << fieldNameStr << "'</summary>\n";
        }
        ss << "        " << vis << " " << typeName << " " << displayFieldName << "\n";
        ss << "        {\n";
        ss << "            get => Il2CppRuntime.GetField<" << typeName << ">(this, \"" << fieldNameStr << "\");\n";
        ss << "            set => Il2CppRuntime.SetField<" << typeName << ">(this, \"" << fieldNameStr << "\", value);\n";
        ss << "        }\n\n";
    }

    return ss.str();
}

/// Generate property wrappers (get_/set_ methods exposed as C# properties)
static std::string GenerateClassProperties(il2cppClass* klass, const std::string& currentNamespace,
                                            bool classIsStatic, const std::string& obfClassName) {
    std::stringstream ss;
    bool hasProperties = false;

    const char* classNsRaw = api::il2cpp_class_get_namespace(klass);
    const char* classNameRaw = api::il2cpp_class_get_name(klass);
    std::string classNs(classNsRaw ? classNsRaw : "");
    std::string className(classNameRaw ? classNameRaw : "");
    std::string staticNs = classNs.empty() ? "Global" : classNs;

    // Track emitted property names to avoid CS0102 duplicates (e.g., multiple 'Item' indexers)
    std::set<std::string> emittedPropNames;

    void* iter = nullptr;
    while (auto prop_const = api::il2cpp_class_get_properties(klass, &iter)) {
        auto prop = const_cast<il2cppPropertyInfo*>(prop_const);
        auto get = api::il2cpp_property_get_get_method(prop);
        auto set = api::il2cpp_property_get_set_method(prop);
        auto propName = api::il2cpp_property_get_name(prop);
        if (!propName) continue;

        // Obfuscation filter: skip properties where ALL accessors are fake
        if (g_obfuscation_detector) {
            bool getIsFake = get ? g_obfuscation_detector->IsFakeMethod(get) : true;
            bool setIsFake = set ? g_obfuscation_detector->IsFakeMethod(set) : true;
            if (getIsFake && setIsFake) continue;
            // If only one accessor is fake, null it out so we don't emit it
            if (get && g_obfuscation_detector->IsFakeMethod(get)) get = nullptr;
            if (set && g_obfuscation_detector->IsFakeMethod(set)) set = nullptr;
        }

        // Skip explicit interface implementation properties
        std::string propNameStr(propName);
        if (propNameStr.find('.') != std::string::npos) continue;

        // Resolve property display name from mappings
        std::string displayPropName = propNameStr;
        bool propIsDeobfuscated = false;
        if (g_mappingLookup.HasMappings()) {
            std::string friendly = g_mappingLookup.ResolveMember(obfClassName, propNameStr);
            if (!friendly.empty()) {
                displayPropName = friendly;
                propIsDeobfuscated = true;
            }
        }

        // Skip duplicate property names using display name (e.g., multiple 'Item' indexers)
        if (!emittedPropNames.insert(displayPropName).second) continue;

        // Also skip if getter/setter method names indicate explicit interface impl
        if (get) {
            const char* getName = api::il2cpp_method_get_name(get);
            if (getName && std::string(getName).find('.') != std::string::npos) continue;
        }
        if (set) {
            const char* setName = api::il2cpp_method_get_name(set);
            if (setName && std::string(setName).find('.') != std::string::npos) continue;
        }

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
            // Interface return types can't be instantiated by GetField/Call — use Il2CppObject
            auto retClass = api::il2cpp_class_from_type(retType);
            if (retClass && (api::il2cpp_class_get_flags(retClass) & TYPE_ATTRIBUTE_INTERFACE))
                propTypeName = "Il2CppObject";
        } else if (set) {
            auto flags = api::il2cpp_method_get_flags(set, &iflags);
            vis = GetMethodVisibility(flags);
            isStatic = (flags & METHOD_ATTRIBUTE_STATIC) != 0;
            auto param = api::il2cpp_method_get_param(set, 0);
            propTypeName = GetFullyQualifiedTypeName(param, currentNamespace);
            // Interface param types — use Il2CppObject
            auto paramClass = api::il2cpp_class_from_type(param);
            if (paramClass && (api::il2cpp_class_get_flags(paramClass) & TYPE_ATTRIBUTE_INTERFACE))
                propTypeName = "Il2CppObject";
        }

        if (propTypeName.empty()) continue;

        if (!hasProperties) {
            ss << "\n        // Properties\n";
            hasProperties = true;
        }

        if (propIsDeobfuscated) {
            ss << "        /// <summary>Deobfuscated property. IL2CPP name: '" << propNameStr << "'</summary>\n";
        }
        ss << "        " << vis;
        if (isStatic) ss << " static";
        ss << " " << propTypeName << " " << displayPropName << "\n";
        ss << "        {\n";

        if (get) {
            if (isStatic) {
                ss << "            get => Il2CppRuntime.CallStatic<" << propTypeName << ">(\""
                   << staticNs << "\", \"" << className << "\", \"get_" << propNameStr
                   << "\", global::System.Type.EmptyTypes);\n";
            } else {
                ss << "            get => Il2CppRuntime.Call<" << propTypeName << ">(this, \"get_"
                   << propNameStr << "\", global::System.Type.EmptyTypes);\n";
            }
        }

        if (set) {
            if (isStatic) {
                ss << "            set => Il2CppRuntime.InvokeStaticVoid(\"" << staticNs << "\", \""
                   << className << "\", \"set_" << propNameStr << "\", new[] { typeof("
                   << propTypeName << ") }, value);\n";
            } else {
                ss << "            set => Il2CppRuntime.InvokeVoid(this, \"set_" << propNameStr
                   << "\", new[] { typeof(" << propTypeName << ") }, value);\n";
            }
        }

        ss << "        }\n\n";
    }

    return ss.str();
}

/// Generate method wrappers
static std::string GenerateClassMethods(il2cppClass* klass, const std::string& currentNamespace,
                                         bool classIsStatic, const std::string& obfClassName) {
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

    // Track emitted method signatures to avoid CS0111 duplicates after type erasure
    std::set<std::string> emittedMethodSigs;

    void* iter = nullptr;
    while (auto method = api::il2cpp_class_get_methods(klass, &iter)) {
        const char* methodName = api::il2cpp_method_get_name(method);
        if (!methodName) continue;
        std::string methodNameStr(methodName);

        // Obfuscation filter: skip fake methods
        if (g_obfuscation_detector && g_obfuscation_detector->IsFakeMethod(method)) continue;

        // Skip inflated (instantiated) generic methods — we only want definitions
        if (method->m_uInflated) continue;

        // Skip constructors, finalizers, and property accessors
        if (methodNameStr == ".ctor" || methodNameStr == ".cctor" || methodNameStr == "Finalize") continue;
        if (propertyMethods.count(methodNameStr)) continue;
        // Skip compiler-generated methods (local functions, state machines, etc.)
        if (methodNameStr.find('<') != std::string::npos || methodNameStr.find('>') != std::string::npos) continue;
        // Skip explicit interface implementations (e.g., IResolvedStyle.get_maxHeight)
        // These contain a '.' that isn't at position 0 (unlike .ctor/.cctor)
        if (methodNameStr.find('.') != std::string::npos) continue;

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

        // ── Generic method detection ──────────────────────────────────────
        bool isGenericMethod = method->m_uGeneric != 0;
        std::vector<std::string> genericParamNames;   // e.g. {"T"} or {"T0","T1"}
        uint32_t mvarBaseIndex = 0;

        if (isGenericMethod && method->m_pGenericContainer) {
            auto* container = reinterpret_cast<il2cppGenericContainer*>(method->m_pGenericContainer);
            int typeArgc = container->m_iTypeArgc;
            if (typeArgc <= 0) {
                isGenericMethod = false;    // defensive: treat as non-generic
            } else {
                if (typeArgc == 1) {
                    genericParamNames.push_back("T");
                } else {
                    for (int gi = 0; gi < typeArgc; ++gi)
                        genericParamNames.push_back("T" + std::to_string(gi));
                }

                // Discover the MVAR base index by scanning the method's return type and params.
                // IL2CPP stores a global parameter index; we subtract the minimum to get a local one.
                uint32_t minMvar = UINT32_MAX;
                auto scanMvar = [&](const il2cppType* t) {
                    if (t && t->m_uType == IL2CPP_TYPE_MVAR) {
                        if (t->m_uGenericParameterIndex < minMvar)
                            minMvar = t->m_uGenericParameterIndex;
                    }
                };
                scanMvar(api::il2cpp_method_get_return_type(method));
                for (uint32_t i = 0; i < api::il2cpp_method_get_param_count(method); ++i)
                    scanMvar(api::il2cpp_method_get_param(method, i));
                mvarBaseIndex = (minMvar == UINT32_MAX) ? 0 : minMvar;
            }
        } else {
            isGenericMethod = false;   // no container pointer
        }

        const std::vector<std::string>* gpPtr = isGenericMethod ? &genericParamNames : nullptr;

        // Return type
        auto returnType = api::il2cpp_method_get_return_type(method);
        std::string returnTypeName = GetFullyQualifiedTypeName(returnType, currentNamespace, gpPtr, mvarBaseIndex);
        bool isVoid = (returnTypeName == "void");

        // Interface return types can't be instantiated by Call<T> — use Il2CppObject
        if (!isVoid) {
            auto retClass = api::il2cpp_class_from_type(returnType);
            if (retClass && (api::il2cpp_class_get_flags(retClass) & TYPE_ATTRIBUTE_INTERFACE))
                returnTypeName = "Il2CppObject";
        }

        // Collect parameters first (before writing) for dedup check
        auto paramCount = api::il2cpp_method_get_param_count(method);
        std::vector<std::string> paramNames;
        std::vector<std::string> paramTypeNames;
        std::vector<bool> paramIsByRef;
        std::vector<std::string> paramRefKind;  // "", "out ", "in ", "ref "
        for (uint32_t i = 0; i < paramCount; ++i) {
            auto param = api::il2cpp_method_get_param(method, i);
            std::string pTypeName = GetFullyQualifiedTypeName(param, currentNamespace, gpPtr, mvarBaseIndex);
            const char* pName = api::il2cpp_method_get_param_name(method, i);
            std::string pNameStr = (pName && pName[0] != '\0') ? pName : ("arg" + std::to_string(i));
            paramNames.push_back(pNameStr);
            paramTypeNames.push_back(pTypeName);

            std::string refKind = "";
            if (_il2cpp_type_is_byref(param)) {
                auto pAttrs = param->m_uAttributes;
                if (pAttrs & PARAM_ATTRIBUTE_OUT && !(pAttrs & PARAM_ATTRIBUTE_IN)) {
                    refKind = "out ";
                } else if (pAttrs & PARAM_ATTRIBUTE_IN && !(pAttrs & PARAM_ATTRIBUTE_OUT)) {
                    refKind = "in ";
                } else {
                    refKind = "ref ";
                }
            }
            paramRefKind.push_back(refKind);
        }

        // Build signature key for dedup (methodName + generic arity + param types)
        // Resolve method display name from mappings
        std::string displayMethodName = methodNameStr;
        bool methodIsDeobfuscated = false;
        if (g_mappingLookup.HasMappings()) {
            std::string friendly = g_mappingLookup.ResolveMember(obfClassName, methodNameStr);
            if (!friendly.empty()) {
                displayMethodName = friendly;
                methodIsDeobfuscated = true;
            }
        }

        std::string sigKey = displayMethodName;
        if (isGenericMethod) sigKey += "`" + std::to_string(genericParamNames.size());
        sigKey += "(";
        for (uint32_t i = 0; i < paramCount; ++i) {
            if (i > 0) sigKey += ",";
            sigKey += paramTypeNames[i];
        }
        sigKey += ")";
        if (!emittedMethodSigs.insert(sigKey).second) continue;  // skip duplicate

        if (!hasMethods) {
            ss << "\n        // Methods\n";
            hasMethods = true;
        }

        // Signature
        if (methodIsDeobfuscated) {
            ss << "        /// <summary>Deobfuscated method. IL2CPP name: '" << methodNameStr << "'</summary>\n";
        }
        ss << "        " << vis;
        if (isStatic) ss << " static";
        ss << " " << returnTypeName << " " << displayMethodName;
        // Append generic type parameters for generic method definitions
        if (isGenericMethod) {
            ss << "<";
            for (size_t gi = 0; gi < genericParamNames.size(); ++gi) {
                if (gi > 0) ss << ", ";
                ss << genericParamNames[gi];
            }
            ss << ">";
        }
        ss << "(";
        for (uint32_t i = 0; i < paramCount; ++i) {
            if (i > 0) ss << ", ";
            ss << paramRefKind[i] << paramTypeNames[i] << " " << paramNames[i];
        }
        ss << ")";
        // Emit generic constraints — use 'class' so Call<T> marshaling works for reference types
        if (isGenericMethod) {
            for (size_t gi = 0; gi < genericParamNames.size(); ++gi) {
                ss << "\n            where " << genericParamNames[gi] << " : class";
            }
        }
        ss << "\n";
        ss << "        {\n";

        // Emit default assignments for 'out' parameters (CS0269/CS0177)
        for (uint32_t i = 0; i < paramCount; ++i) {
            if (paramRefKind[i] == "out ") {
                ss << "            " << paramNames[i] << " = default;\n";
            }
        }

        // Build Type[] expression for parameter types
        std::string typeArrayExpr;
        if (paramCount == 0) {
            typeArrayExpr = "global::System.Type.EmptyTypes";
        } else {
            typeArrayExpr = "new global::System.Type[] { ";
            for (uint32_t i = 0; i < paramCount; ++i) {
                if (i > 0) typeArrayExpr += ", ";
                typeArrayExpr += "typeof(" + paramTypeNames[i] + ")";
            }
            typeArrayExpr += " }";
        }

        // Build generic args expression for generic methods
        std::string genericArgsExpr;
        if (isGenericMethod) {
            genericArgsExpr = "new global::System.Type[] { ";
            for (size_t gi = 0; gi < genericParamNames.size(); ++gi) {
                if (gi > 0) genericArgsExpr += ", ";
                genericArgsExpr += "typeof(" + genericParamNames[gi] + ")";
            }
            genericArgsExpr += " }";
        }

        // Method body — use CallGeneric/InvokeGenericVoid for generic methods
        if (isGenericMethod) {
            // Generic method invocation (requires inflation)
            if (isStatic) {
                if (isVoid) {
                    ss << "            Il2CppRuntime.InvokeStaticGenericVoid(\"" << staticNs << "\", \""
                       << className << "\", \"" << methodNameStr << "\", " << genericArgsExpr << ", " << typeArrayExpr;
                } else {
                    ss << "            return Il2CppRuntime.CallStaticGeneric<" << returnTypeName << ">(\""
                       << staticNs << "\", \"" << className << "\", \"" << methodNameStr << "\", " << genericArgsExpr << ", " << typeArrayExpr;
                }
            } else {
                if (isVoid) {
                    ss << "            Il2CppRuntime.InvokeGenericVoid(this, \"" << methodNameStr << "\", " << genericArgsExpr << ", " << typeArrayExpr;
                } else {
                    ss << "            return Il2CppRuntime.CallGeneric<" << returnTypeName << ">(this, \""
                       << methodNameStr << "\", " << genericArgsExpr << ", " << typeArrayExpr;
                }
            }
        } else {
            // Non-generic method invocation (original path)
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

    // Determine display name (info.name may already be friendly after Phase 1.6)
    std::string displayName = info.name;
    bool isDeobfuscated = (info.name != SanitizeTypeName(info.rawName));

    if (isDeobfuscated) {
        ss << "    /// <summary>Deobfuscated class. IL2CPP name: '" << info.rawName << "'</summary>\n";
    }

    ss << "    " << info.visibility << " partial class " << displayName << " : " << info.base_class << "\n";
    ss << "    {\n";

    // IL2CPP metadata constants (always emit for runtime reflection)
    ss << "        private const string _il2cppClassName = \"" << info.rawName << "\";\n";
    ss << "        private const string _il2cppNamespace = \"" << info.rawNs << "\";\n\n";

    ss << "        public " << displayName << "(IntPtr nativePtr) : base(nativePtr) { }\n";

    // Fields as properties (skip for static classes)
    if (!info.is_static) {
        ss << GenerateClassFields(info.klass, currentNamespace, info.rawName);
    }

    // Properties
    ss << GenerateClassProperties(info.klass, currentNamespace, info.is_static, info.rawName);

    // Methods
    ss << GenerateClassMethods(info.klass, currentNamespace, info.is_static, info.rawName);

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
    DumpResult result = { false, "", "", "", 0, 0, {}, 0, 0, 0, 0 };

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

    // ---- BeeByte Fake Method Detection ----
    MDB::Obfuscation::DetectorConfig obfConfig;
    obfConfig.pointer_sharing_threshold = 10;
    obfConfig.whitelist_vtable_methods = true;
    obfConfig.check_stub_patterns = true;
    MDB::Obfuscation::Detector obfuscation_detector(obfConfig);
    obfuscation_detector.Analyze(assemblies, size);
    g_obfuscation_detector = &obfuscation_detector;

    result.fake_methods_detected = obfuscation_detector.GetTotalFakeMethods();
    result.fake_classes_detected = obfuscation_detector.GetTotalFakeClasses();

    // Write BeeByte report to MDB/Dump/fake_methods.txt
    {
        char exePath[MAX_PATH];
        GetModuleFileNameA(nullptr, exePath, MAX_PATH);
        std::string exeDir(exePath);
        size_t lastSlash = exeDir.find_last_of("\\/");
        if (lastSlash != std::string::npos) exeDir = exeDir.substr(0, lastSlash);
        std::string dumpDir = exeDir + "\\MDB\\Dump";
        std::filesystem::create_directories(dumpDir);
        std::string fakeReportPath = dumpDir + "\\fake_methods.txt";
        obfuscation_detector.WriteFakeReport(fakeReportPath);
        result.fake_report_path = fakeReportPath;
    }

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

            // Skip system/internal namespaces (check raw namespace first)
            if (ShouldSkipNamespace(nsStr)) continue;

            // Skip non-public types
            int flags = api::il2cpp_class_get_flags(klass);
            auto vis = flags & TYPE_ATTRIBUTE_VISIBILITY_MASK;
            if (vis != TYPE_ATTRIBUTE_PUBLIC && vis != TYPE_ATTRIBUTE_NESTED_PUBLIC) continue;

            // Obfuscation filter: skip entirely-fake classes
            if (g_obfuscation_detector && g_obfuscation_detector->IsEntirelyFakeClass(klass)) continue;

            // Resolve effective namespace: nested types inherit their declaring type's namespace
            std::string resolvedNs = nsStr.empty() ? ResolveEffectiveNamespace(klass) : nsStr;

            // Re-check the resolved namespace — nested types from System/Mono/etc. must also be skipped
            if (resolvedNs != nsStr && ShouldSkipNamespace(resolvedNs)) continue;

            std::string bucketNs = resolvedNs.empty() ? "Global" : resolvedNs;

            ClassInfo info = ClassifyType(klass, dllName, bucketNs);
            typesByNamespace[bucketNs].push_back(info);
        }
    }
    result.total_classes = totalClasses;

    // ---- Phase 1.5: Build known types registry ----
    g_knownTypes.clear();
    for (const auto& [regNs, regTypes] : typesByNamespace) {
        for (const auto& regInfo : regTypes) {
            // Use the effective namespace (which includes resolved declaring type namespace)
            std::string effectiveNs = regInfo.ns == "Global" ? "" : regInfo.ns;
            std::string fqn = effectiveNs.empty() ? regInfo.name : (effectiveNs + "." + regInfo.name);
            g_knownTypes.insert(fqn);
        }
    }

    // ---- Phase 1.6: Load deobfuscation mappings & apply friendly names ----
    {
        char exePath2[MAX_PATH];
        GetModuleFileNameA(nullptr, exePath2, MAX_PATH);
        std::string exeDir2(exePath2);
        size_t ls2 = exeDir2.find_last_of("\\/");
        if (ls2 != std::string::npos) exeDir2 = exeDir2.substr(0, ls2);
        std::string mappingsPath = exeDir2 + "\\MDB\\Dump\\mappings.json";

        if (g_mappingLookup.Load(mappingsPath)) {
            result.mappings_loaded = g_mappingLookup.TotalCount();

            // Update ClassInfo display names with friendly names from mappings
            for (auto& [mapNs, mapTypes] : typesByNamespace) {
                for (auto& mapInfo : mapTypes) {
                    std::string friendly = g_mappingLookup.ResolveType(mapInfo.rawName);
                    if (!friendly.empty()) {
                        mapInfo.name = friendly;
                    }
                }
            }
        }
    }

    // Re-validate base classes against known types registry
    // (Now that g_mappingLookup is loaded, GetFullyQualifiedClassName will
    //  apply friendly name remapping to base class references too)
    for (auto& [valNs, valTypes] : typesByNamespace) {
        for (auto& valInfo : valTypes) {
            if (valInfo.kind == TypeKind::Class && !valInfo.base_class.empty() && valInfo.base_class != "Il2CppObject") {
                auto* parent = api::il2cpp_class_get_parent(valInfo.klass);
                if (parent) {
                    valInfo.base_class = GetFullyQualifiedClassName(parent, valInfo.ns);
                    if (valInfo.base_class.empty() || valInfo.base_class == "object") {
                        valInfo.base_class = "Il2CppObject";
                    }
                    // Detect circular base type (e.g., FancyScrollView<T,U> extends FancyScrollView<T>)
                    std::string baseTail = valInfo.base_class;
                    auto lastDot = baseTail.rfind('.');
                    if (lastDot != std::string::npos) baseTail = baseTail.substr(lastDot + 1);
                    if (baseTail == valInfo.name) {
                        valInfo.base_class = "Il2CppObject";
                    }
                }
            }
        }
    }

    // ---- Phase 2: Generate .cs files per namespace ----
    std::filesystem::create_directories(output_directory);

    for (auto& [ns, types] : typesByNamespace) {
        if (types.empty()) continue;

        std::stringstream file;

        // File header
        file << "// Auto-generated Il2Cpp wrapper classes\n";
        file << "// Namespace: " << ns << "\n";
        file << "// Do not edit manually\n\n";
        file << "#pragma warning disable 0108, 0114, 0162, 0168, 0219\n\n";

        // Using statements
        file << BuildUsingStatements(ns) << "\n";

        // Namespace declaration
        file << "namespace " << ns << "\n";
        file << "{\n";

        // Sort types: delegates → enums → interfaces → structs → classes
        std::stable_sort(types.begin(), types.end(), [](const ClassInfo& a, const ClassInfo& b) {
            return static_cast<int>(a.kind) < static_cast<int>(b.kind);
        });

        // Track emitted type names to avoid CS0101 duplicate definitions
        std::set<std::string> emittedTypes;

        for (const auto& info : types) {
            // Skip duplicate type names within the same namespace
            if (!emittedTypes.insert(info.name).second) continue;

            switch (info.kind) {
            case TypeKind::Delegate:
                file << GenerateDelegate(info.klass, ns, info.rawName) << "\n";
                break;
            case TypeKind::Enum:
                file << GenerateEnum(info.klass, info.rawName) << "\n";
                break;
            case TypeKind::Interface:
                file << GenerateInterface(info.klass, info.rawName) << "\n";
                break;
            case TypeKind::Struct:
                file << GenerateStruct(info.klass, ns, info.rawName) << "\n";
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

    // Clean up global detector pointer (stack-allocated, about to go out of scope)
    g_obfuscation_detector = nullptr;

    // Note: g_mappingLookup persists for potential future use but is harmless

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
