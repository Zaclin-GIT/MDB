#include "obfuscation_detector.hpp"
#include <Il2CppTableDefine.hpp>
#include <Il2CppTypes.hpp>

#define NOMINMAX
#include <Windows.h>
#include <fstream>
#include <sstream>
#include <algorithm>
#include <chrono>
#include <iomanip>
#include <ctime>
#include <filesystem>
#include <map>

namespace api = il2cpp::_internal;
using namespace il2cpp::_internal::unity_structs;

namespace MDB {
namespace Obfuscation {

// ============================================================================
// Safe Memory Read (SEH-compatible, no C++ objects)
// ============================================================================

// Standalone function for SEH — cannot coexist with C++ destructors
static bool SafeMemRead(uintptr_t address, void* dest, size_t size) {
    __try {
        memcpy(dest, reinterpret_cast<const void*>(address), size);
        return true;
    } __except (EXCEPTION_EXECUTE_HANDLER) {
        return false;
    }
}

// ============================================================================
// Stub Pattern Detection (x86-64)
// ============================================================================

bool Detector::IsStubPattern(uintptr_t address) const {
    if (!address) return true;

    // Safely read up to 16 bytes at the method pointer
    uint8_t buf[16] = {};
    if (!SafeMemRead(address, buf, sizeof(buf))) {
        // Can't read memory at this address — treat as suspicious
        return true;
    }

    // Pattern 1: Single RET (0xC3) — empty void stub
    if (buf[0] == 0xC3) return true;

    // Pattern 2: INT3 + RET (0xCC 0xC3) — padding + ret
    if (buf[0] == 0xCC && buf[1] == 0xC3) return true;

    // Pattern 3: XOR EAX, EAX; RET (0x33 0xC0 0xC3) — returns 0/null/false
    if (buf[0] == 0x33 && buf[1] == 0xC0 && buf[2] == 0xC3) return true;

    // Pattern 4: XOR EAX, EAX; RET with NOP prefix
    // 0x90 0x33 0xC0 0xC3
    if (buf[0] == 0x90 && buf[1] == 0x33 && buf[2] == 0xC0 && buf[3] == 0xC3) return true;

    // Pattern 5: MOV EAX, 0; RET (0xB8 0x00 0x00 0x00 0x00 0xC3) — returns 0
    if (buf[0] == 0xB8 && buf[1] == 0x00 && buf[2] == 0x00 && buf[3] == 0x00 &&
        buf[4] == 0x00 && buf[5] == 0xC3) return true;

    // Pattern 6: MOV EAX, 1; RET (0xB8 0x01 0x00 0x00 0x00 0xC3) — returns true/1
    if (buf[0] == 0xB8 && buf[1] == 0x01 && buf[2] == 0x00 && buf[3] == 0x00 &&
        buf[4] == 0x00 && buf[5] == 0xC3) return true;

    // Pattern 7: PUSH RBP; MOV RBP, RSP; POP RBP; RET — empty function with frame
    // 0x55 0x48 0x89 0xE5 0x5D 0xC3
    if (buf[0] == 0x55 && buf[1] == 0x48 && buf[2] == 0x89 && buf[3] == 0xE5 &&
        buf[4] == 0x5D && buf[5] == 0xC3) return true;

    // Pattern 8: SUB RSP, XX; ADD RSP, XX; RET — empty function with stack frame
    // Check if RET appears within first 8 bytes without any meaningful instructions
    if (buf[0] == 0x48 && buf[1] == 0x83 && buf[2] == 0xEC) {
        // SUB RSP, imm8 — check if followed quickly by ADD RSP + RET
        uint8_t frame_size = buf[3];
        if (buf[4] == 0x48 && buf[5] == 0x83 && buf[6] == 0xC4 && buf[7] == frame_size) {
            // ADD RSP, same_size
            if (buf[8] == 0xC3) return true;
        }
    }

    return false;
}

// ============================================================================
// Assembly Whitelist Check
// ============================================================================

bool Detector::IsWhitelistedImage(const char* imageName) const {
    if (!imageName || !imageName[0]) return false;

    std::string name(imageName);
    for (const auto& prefix : m_config.assembly_prefixes_whitelist) {
        if (name.compare(0, prefix.size(), prefix) == 0) {
            return true;
        }
    }
    return false;
}

// ============================================================================
// Generic Method Detection
// ============================================================================

bool Detector::IsGenericShared(const il2cppMethodInfo* method) const {
    // Not used directly anymore — see IsGenericClassName below.
    // Kept for interface compatibility.
    if (!method || !method->m_pClass) return false;
    const char* className = api::il2cpp_class_get_name(method->m_pClass);
    return IsGenericClassName(className);
}

bool Detector::IsGenericClassName(const char* className) {
    if (!className) return false;
    // Standard .NET naming: generic classes have backtick + arity
    // e.g. "List`1", "Dictionary`2", "Action`3"
    for (const char* p = className; *p; ++p) {
        if (*p == '`') return true;
    }
    return false;
}

// ============================================================================
// Obfuscated Name Detection
// ============================================================================

bool Detector::IsObfuscatedName(const char* name) {
    if (!name || !name[0]) return false;

    // BeeByte obfuscation pattern: exactly 11 uppercase ASCII characters
    // e.g. AJLPLCGICMF, FPGHODFCFKC, KLFGNILMCJN
    size_t len = 0;
    for (const char* p = name; *p; ++p) {
        if (*p < 'A' || *p > 'Z') return false;
        len++;
        if (len > 11) return false; // too long
    }
    return len == 11;
}

// ============================================================================
// Method Signature Builder
// ============================================================================

std::string Detector::BuildMethodSignature(const il2cppMethodInfo* method,
                                            const std::string& className) const {
    std::stringstream ss;

    // Return type
    if (api::il2cpp_method_get_return_type && method) {
        auto retType = api::il2cpp_method_get_return_type(method);
        if (retType) {
            auto* retClass = api::il2cpp_class_from_type ? api::il2cpp_class_from_type(retType) : nullptr;
            if (retClass && api::il2cpp_class_get_name) {
                ss << api::il2cpp_class_get_name(retClass);
            } else {
                ss << "?";
            }
        } else {
            ss << "void";
        }
    } else {
        ss << "?";
    }

    ss << " " << className << "::";

    // Method name
    const char* name = api::il2cpp_method_get_name ? api::il2cpp_method_get_name(method) : nullptr;
    ss << (name ? name : "???");

    // Parameters
    ss << "(";
    if (api::il2cpp_method_get_param_count && api::il2cpp_method_get_param) {
        auto paramCount = api::il2cpp_method_get_param_count(method);
        for (uint32_t i = 0; i < paramCount; ++i) {
            if (i > 0) ss << ", ";
            auto param = api::il2cpp_method_get_param(method, i);
            if (param && api::il2cpp_class_from_type) {
                auto* pClass = api::il2cpp_class_from_type(param);
                if (pClass && api::il2cpp_class_get_name) {
                    ss << api::il2cpp_class_get_name(pClass);
                } else {
                    ss << "?";
                }
            } else {
                ss << "?";
            }

            if (api::il2cpp_method_get_param_name) {
                const char* pName = api::il2cpp_method_get_param_name(method, i);
                if (pName && pName[0]) {
                    ss << " " << pName;
                }
            }
        }
    }
    ss << ")";

    return ss.str();
}

// ============================================================================
// Phase 1: Collect All Method Pointers
// ============================================================================

void Detector::CollectMethodPointers(il2cppAssembly** assemblies, size_t count) {
    m_pointer_map.clear();
    m_total_methods = 0;
    m_whitelisted_methods = 0;
    m_generic_skipped = 0;

    for (size_t i = 0; i < count; ++i) {
        auto image = api::il2cpp_assembly_get_image(assemblies[i]);
        if (!image) continue;

        // Check if this entire image (assembly) is whitelisted
        const char* imgName = api::il2cpp_image_get_name ? api::il2cpp_image_get_name(image) : nullptr;
        bool whitelisted = IsWhitelistedImage(imgName);

        auto classCount = api::il2cpp_image_get_class_count(image);
        for (size_t j = 0; j < classCount; ++j) {
            auto klass = api::il2cpp_image_get_class(image, j);
            if (!klass) continue;

            // Check once per class if this is a generic class (name has backtick)
            const char* klassName = api::il2cpp_class_get_name(klass);
            bool isGenericClass = IsGenericClassName(klassName);

            void* iter = nullptr;
            while (auto method = api::il2cpp_class_get_methods(klass, &iter)) {
                m_total_methods++;

                // Whitelisted assemblies don't contribute to shared-pointer counts.
                // This prevents legitimate ICF (e.g., Unity.Mathematics bitcasts)
                // from inflating stub pointer detection.
                if (whitelisted) {
                    m_whitelisted_methods++;
                    continue;
                }

                // Generic classes use IL2CPP's "generic sharing" mechanism which
                // legitimately compiles many methods to a single entry point.
                // Exclude them so they don't inflate shared-pointer counts.
                if (isGenericClass) {
                    m_generic_skipped++;
                    continue;
                }

                auto ptr = reinterpret_cast<uintptr_t>(method->m_pMethodPointer);
                m_pointer_map[ptr].push_back(method);
            }
        }
    }

    // Identify stub pointers: any pointer shared by >= threshold methods
    for (const auto& [ptr, methods] : m_pointer_map) {
        if (methods.size() >= m_config.pointer_sharing_threshold) {
            m_stub_pointers.insert(ptr);
        }
    }
}

// ============================================================================
// Phase 2: Build VTable Whitelist
// ============================================================================

void Detector::BuildVTableWhitelist(il2cppAssembly** assemblies, size_t count) {
    m_vtable_methods.clear();

    if (!m_config.whitelist_vtable_methods) return;

    // il2cpp doesn't expose a direct vtable iteration API, but we can check
    // METHOD_ATTRIBUTE_VIRTUAL flag — virtual methods that are in the vtable
    // are always real because obfuscators can't inject into vtable slots without
    // breaking polymorphism.
    //
    // Additionally, methods implementing interface slots are real.
    // We approximate this by whitelisting any method with VIRTUAL flag.

    for (size_t i = 0; i < count; ++i) {
        auto image = api::il2cpp_assembly_get_image(assemblies[i]);
        if (!image) continue;

        auto classCount = api::il2cpp_image_get_class_count(image);
        for (size_t j = 0; j < classCount; ++j) {
            auto klass = api::il2cpp_image_get_class(image, j);
            if (!klass) continue;

            void* iter = nullptr;
            while (auto method = api::il2cpp_class_get_methods(klass, &iter)) {
                uint32_t iflags = 0;
                auto flags = api::il2cpp_method_get_flags(method, &iflags);

                // Virtual methods are in the vtable — always real
                if (flags & METHOD_ATTRIBUTE_VIRTUAL) {
                    m_vtable_methods.insert(method);
                }

                // Abstract methods have no body but are part of the type contract — real
                if (flags & METHOD_ATTRIBUTE_ABSTRACT) {
                    m_vtable_methods.insert(method);
                }
            }
        }
    }
}

// ============================================================================
// Phase 3: Classify Methods
// ============================================================================

void Detector::ClassifyMethods(il2cppAssembly** assemblies, size_t count) {
    m_fake_methods.clear();
    m_fake_method_set.clear();
    m_fake_class_set.clear();
    m_class_analysis.clear();
    m_fake_class_count = 0;

    for (size_t i = 0; i < count; ++i) {
        auto image = api::il2cpp_assembly_get_image(assemblies[i]);
        if (!image) continue;

        // Skip whitelisted assemblies entirely — all methods are real
        const char* imgName = api::il2cpp_image_get_name ? api::il2cpp_image_get_name(image) : nullptr;
        if (IsWhitelistedImage(imgName)) continue;

        auto classCount = api::il2cpp_image_get_class_count(image);
        for (size_t j = 0; j < classCount; ++j) {
            auto klass = api::il2cpp_image_get_class(image, j);
            if (!klass) continue;

            const char* ns = api::il2cpp_class_get_namespace(klass);
            const char* name = api::il2cpp_class_get_name(klass);
            if (!name) continue;

            std::string nsStr(ns ? ns : "");
            std::string nameStr(name);
            std::string fullName = nsStr.empty() ? nameStr : (nsStr + "." + nameStr);
            bool isGenericClass = IsGenericClassName(name);

            ClassAnalysis classResult{};
            classResult.klass = klass;
            classResult.full_name = fullName;
            classResult.total_methods = 0;
            classResult.fake_methods = 0;
            classResult.real_methods = 0;
            classResult.is_entirely_fake = false;

            void* iter = nullptr;
            while (auto method = api::il2cpp_class_get_methods(klass, &iter)) {
                classResult.total_methods++;

                const char* methodName = api::il2cpp_method_get_name(method);
                auto ptr = reinterpret_cast<uintptr_t>(method->m_pMethodPointer);

                // Step 1: VTable whitelist — always real
                if (m_vtable_methods.count(method)) {
                    classResult.real_methods++;
                    continue;
                }

                // Step 2: Constructors and finalizers are always real
                if (methodName) {
                    std::string mName(methodName);
                    if (mName == ".ctor" || mName == ".cctor" || mName == "Finalize") {
                        classResult.real_methods++;
                        continue;
                    }
                }

                // Step 3: Generic classes — always real
                // Generic definitions have null pointers by design, and generic
                // shared implementations legitimately reuse code addresses.
                if (isGenericClass) {
                    classResult.real_methods++;
                    continue;
                }

                // Step 4: Null method pointer — fake if name is obfuscated
                // Generic method definitions in non-generic classes (e.g.
                // ByteBuffer::ToArray<T>) legitimately have null pointers.
                // Only flag as fake if the method name is obfuscated.
                if (ptr == 0) {
                    if (IsObfuscatedName(methodName)) {
                        FakeMethodInfo info{};
                        info.method = method;
                        info.class_name = fullName;
                        info.method_name = methodName ? methodName : "???";
                        info.full_signature = BuildMethodSignature(method, fullName);
                        info.method_pointer = 0;
                        info.reason = FakeReason::NullMethodPointer;
                        info.shared_count = 0;
                        m_fake_methods.push_back(info);
                        m_fake_method_set.insert(method);
                        classResult.fake_methods++;
                    } else {
                        classResult.real_methods++;
                    }
                    continue;
                }

                // Step 5: Shared method pointer — primary detection signal
                // Only flag if the method name looks obfuscated. Real methods
                // (e.g. get_Position, Update) can share code via MSVC ICF with
                // BeeByte stubs but are not themselves fake.
                if (m_stub_pointers.count(ptr)) {
                    bool nameIsObfuscated = IsObfuscatedName(methodName);

                    if (nameIsObfuscated) {
                        FakeMethodInfo info{};
                        info.method = method;
                        info.class_name = fullName;
                        info.method_name = methodName ? methodName : "???";
                        info.full_signature = BuildMethodSignature(method, fullName);
                        info.method_pointer = ptr;
                        info.reason = FakeReason::SharedMethodPointer;
                        info.shared_count = m_pointer_map[ptr].size();
                        m_fake_methods.push_back(info);
                        m_fake_method_set.insert(method);
                        classResult.fake_methods++;
                        continue;
                    }
                    // Non-obfuscated name on a shared pointer → likely ICF collateral.
                    // Treat as real.
                    classResult.real_methods++;
                    continue;
                }

                // If it passed all checks, it's real
                classResult.real_methods++;
            }

            // Determine if class is entirely fake:
            // A class where ALL non-special methods (.ctor, .cctor, Finalize) are fake
            // and it has at least some methods
            if (classResult.total_methods > 0 && classResult.real_methods == 0) {
                classResult.is_entirely_fake = true;
            }
            // More nuanced: if >90% of methods are fake with at least 5+ fakes
            else if (classResult.fake_methods >= 5 &&
                     classResult.total_methods > 0 &&
                     (classResult.fake_methods * 100 / classResult.total_methods) >= 90) {
                classResult.is_entirely_fake = true;
            }

            if (classResult.is_entirely_fake) {
                m_fake_class_set.insert(klass);
                m_fake_class_count++;
            }

            // Only store analysis for classes that have at least one fake method
            if (classResult.fake_methods > 0) {
                m_class_analysis.push_back(classResult);
            }
        }
    }
}

// ============================================================================
// Main Analysis Entry Point
// ============================================================================

void Detector::Analyze(il2cppAssembly** assemblies, size_t count) {
    if (!assemblies || count == 0) return;
    if (!api::il2cpp_class_get_methods || !api::il2cpp_method_get_flags) return;

    // Phase 1: Collect all method pointers across entire runtime
    CollectMethodPointers(assemblies, count);

    // Phase 2: Build vtable whitelist
    BuildVTableWhitelist(assemblies, count);

    // Phase 3: Classify each method
    ClassifyMethods(assemblies, count);
}

// ============================================================================
// Query Methods
// ============================================================================

bool Detector::IsFakeMethod(const il2cppMethodInfo* method) const {
    return m_fake_method_set.count(method) > 0;
}

bool Detector::IsEntirelyFakeClass(il2cppClass* klass) const {
    return m_fake_class_set.count(klass) > 0;
}

// ============================================================================
// Report Writer
// ============================================================================

bool Detector::WriteFakeReport(const std::string& output_path) const {
    // Ensure parent directory exists
    std::filesystem::path filePath(output_path);
    std::filesystem::create_directories(filePath.parent_path());

    std::ofstream file(output_path);
    if (!file.is_open()) return false;

    // Get current timestamp
    auto now = std::chrono::system_clock::now();
    auto time_t_now = std::chrono::system_clock::to_time_t(now);
    std::tm tm_now{};
    localtime_s(&tm_now, &time_t_now);

    // Get GameAssembly base for RVA calculation
    uintptr_t gaBase = reinterpret_cast<uintptr_t>(GetModuleHandleW(L"GameAssembly.dll"));

    file << "// ============================================================================\n";
    file << "// Obfuscation Fake Method Detection Report\n";
    file << "// Generated: " << std::put_time(&tm_now, "%Y-%m-%d %H:%M:%S") << "\n";
    file << "// ============================================================================\n";
    file << "//\n";
    file << "// Detection Configuration:\n";
    file << "//   Pointer sharing threshold: " << m_config.pointer_sharing_threshold << "\n";
    file << "//   VTable whitelist enabled:  " << (m_config.whitelist_vtable_methods ? "yes" : "no") << "\n";
    file << "//   Stub pattern check:        " << (m_config.check_stub_patterns ? "yes" : "no") << "\n";
    file << "//   Assembly whitelist:         ";
    for (size_t i = 0; i < m_config.assembly_prefixes_whitelist.size(); ++i) {
        if (i > 0) file << ", ";
        file << m_config.assembly_prefixes_whitelist[i] << "*";
    }
    file << "\n";
    file << "//\n";
    file << "// Summary:\n";
    file << "//   Total methods analyzed:    " << m_total_methods << "\n";
    file << "//   Whitelisted (skipped):     " << m_whitelisted_methods << "\n";
    file << "//   Generic shared (skipped):  " << m_generic_skipped << "\n";
    file << "//   Fake methods detected:     " << m_fake_methods.size() << "\n";
    file << "//   Fake classes detected:     " << m_fake_class_count << "\n";
    file << "//   Unique stub pointers:      " << m_stub_pointers.size() << "\n";
    file << "//   VTable methods (whitelist): " << m_vtable_methods.size() << "\n";
    file << "// ============================================================================\n\n";

    // Section 1: Stub pointer summary
    file << "// ============================================================================\n";
    file << "// STUB POINTERS (shared by " << m_config.pointer_sharing_threshold << "+ methods)\n";
    file << "// ============================================================================\n\n";

    // Sort stub pointers by usage count
    std::vector<std::pair<uintptr_t, size_t>> sorted_stubs;
    for (auto ptr : m_stub_pointers) {
        sorted_stubs.push_back({ ptr, m_pointer_map.at(ptr).size() });
    }
    std::sort(sorted_stubs.begin(), sorted_stubs.end(),
              [](const auto& a, const auto& b) { return a.second > b.second; });

    for (const auto& [ptr, count] : sorted_stubs) {
        uintptr_t rva = gaBase ? (ptr - gaBase) : ptr;
        file << "// Pointer 0x" << std::hex << ptr << " (RVA: 0x" << rva << std::dec
             << ") — shared by " << count << " methods";

        // Show stub bytes if available
        uint8_t buf[8] = {};
        if (SafeMemRead(ptr, buf, sizeof(buf))) {
            file << " — bytes: ";
            for (int b = 0; b < 8; ++b) {
                file << std::hex << std::setw(2) << std::setfill('0') << (int)buf[b] << " ";
            }
            file << std::dec;
        } else {
            file << " — [unreadable]";
        }

        file << "\n";
    }

    // Section 2: Entirely fake classes
    file << "\n// ============================================================================\n";
    file << "// ENTIRELY FAKE CLASSES (" << m_fake_class_count << " detected)\n";
    file << "// ============================================================================\n\n";

    for (const auto& ca : m_class_analysis) {
        if (!ca.is_entirely_fake) continue;
        file << "// [FAKE CLASS] " << ca.full_name
             << " — " << ca.fake_methods << "/" << ca.total_methods << " methods are fake\n";
    }

    // Section 3: All fake methods grouped by class
    file << "\n// ============================================================================\n";
    file << "// ALL FAKE METHODS (" << m_fake_methods.size() << " detected)\n";
    file << "// ============================================================================\n\n";

    // Group by class
    std::map<std::string, std::vector<const FakeMethodInfo*>> by_class;
    for (const auto& fm : m_fake_methods) {
        by_class[fm.class_name].push_back(&fm);
    }

    for (const auto& [className, methods] : by_class) {
        // Check if this is an entirely fake class
        bool isFakeClass = false;
        for (const auto& ca : m_class_analysis) {
            if (ca.full_name == className && ca.is_entirely_fake) {
                isFakeClass = true;
                break;
            }
        }

        file << "// --- " << className;
        if (isFakeClass) file << " [ENTIRE CLASS IS FAKE]";
        file << " ---\n";

        for (const auto* fm : methods) {
            uintptr_t rva = (gaBase && fm->method_pointer) ? (fm->method_pointer - gaBase) : fm->method_pointer;
            file << "//   ";

            switch (fm->reason) {
            case FakeReason::SharedMethodPointer:
                file << "[SHARED x" << fm->shared_count << "] ";
                break;
            case FakeReason::NullMethodPointer:
                file << "[NULL PTR] ";
                break;
            case FakeReason::StubPattern:
                file << "[STUB] ";
                break;
            default:
                break;
            }

            file << fm->full_signature;
            if (fm->method_pointer) {
                file << " // RVA: 0x" << std::hex << rva << std::dec;
            }
            file << "\n";
        }
        file << "\n";
    }

    // Section 4: Classes with partial fakes (mixed real + fake)
    file << "// ============================================================================\n";
    file << "// PARTIALLY AFFECTED CLASSES (mix of real + fake methods)\n";
    file << "// ============================================================================\n\n";

    for (const auto& ca : m_class_analysis) {
        if (ca.is_entirely_fake) continue;
        file << "// " << ca.full_name
             << " — " << ca.fake_methods << " fake / " << ca.real_methods << " real / "
             << ca.total_methods << " total\n";
    }

    file << "\n// === End of Report ===\n";
    file.close();
    return true;
}

} // namespace Obfuscation
} // namespace MDB
