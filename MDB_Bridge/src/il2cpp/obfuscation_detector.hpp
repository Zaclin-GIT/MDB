#pragma once
// ============================================================================
// Obfuscation Fake Method Detector for MDB Bridge
// ============================================================================
// Detects and filters fake (dummy) methods injected by obfuscation tools
// (e.g. BeeByte, etc.) that flood assemblies with useless stub methods.
//
// Detection strategy (ordered by reliability):
//   1. methodPointer deduplication — stubs reused by 10+ methods are fake
//   2. VTable / interface slot membership — always real (whitelist)
//   3. Native stub pattern matching — tiny ret/xor+ret bodies (tiebreaker)
//
// Usage:
//   Obfuscation::Detector detector;
//   detector.Analyze(assemblies, assemblyCount);
//   if (detector.IsFakeMethod(method)) { ... }
//   detector.WriteFakeReport(outputPath);

#include "il2cpp_resolver.hpp"
#include <string>
#include <vector>
#include <unordered_set>
#include <unordered_map>
#include <set>

namespace MDB {
namespace Obfuscation {

// ============================================================================
// Configuration
// ============================================================================

struct DetectorConfig {
    // Minimum number of methods sharing one methodPointer to be considered fake.
    // Obfuscation stubs are typically shared by hundreds/thousands.
    // Real ICF (Identical COMDAT Folding) almost never exceeds ~5.
    size_t pointer_sharing_threshold = 10;

    // Maximum native function body size (bytes) to consider as a potential stub.
    // Only used as tiebreaker, never as primary signal.
    size_t max_stub_body_size = 16;

    // Whether to check for known x86-64 stub byte patterns
    bool check_stub_patterns = true;

    // Whether to whitelist vtable/interface methods (always real)
    bool whitelist_vtable_methods = true;

    // Assembly name prefixes to whitelist (never flagged as fake).
    // Engine, framework, and well-known third-party assemblies contain
    // legitimately tiny methods (bitcasts, getters, etc.) that share code
    // via ICF. Excluding them prevents false positives AND stops them from
    // inflating shared-pointer counts for real obfuscation stubs.
    std::vector<std::string> assembly_prefixes_whitelist = {
        "UnityEngine",      // UnityEngine.dll, UnityEngine.CoreModule.dll, etc.
        "Unity.",           // Unity.Mathematics, Unity.TextMeshPro, Unity.Burst, etc.
        "System",           // System.dll, System.Core.dll, System.Runtime.dll, etc.
        "mscorlib",         // Core .NET
        "Mono.",            // Mono.Security, etc.
        "netstandard",      // .NET Standard
        "Newtonsoft",       // Newtonsoft.Json
    };
};

// ============================================================================
// Per-method detection result
// ============================================================================

enum class FakeReason {
    NotFake = 0,
    SharedMethodPointer,    // methodPointer shared by >= threshold methods
    NullMethodPointer,      // methodPointer is nullptr
    StubPattern,            // Native body matches known stub pattern (only as confirmation)
};

struct FakeMethodInfo {
    const il2cpp::_internal::unity_structs::il2cppMethodInfo* method;
    std::string class_name;         // "Namespace.ClassName"
    std::string method_name;
    std::string full_signature;     // "ReturnType ClassName::MethodName(Params)"
    uintptr_t method_pointer;
    FakeReason reason;
    size_t shared_count;            // How many methods share this pointer
};

// ============================================================================
// Per-class detection result
// ============================================================================

struct ClassAnalysis {
    il2cpp::_internal::unity_structs::il2cppClass* klass;
    std::string full_name;          // "Namespace.ClassName"
    size_t total_methods;
    size_t fake_methods;
    size_t real_methods;
    bool is_entirely_fake;          // All non-system methods are fake
};

// ============================================================================
// Detector
// ============================================================================

class Detector {
public:
    explicit Detector(const DetectorConfig& config = DetectorConfig{})
        : m_config(config) {}

    /// Run analysis across all assemblies. Call once after IL2CPP is initialized.
    void Analyze(il2cpp::_internal::unity_structs::il2cppAssembly** assemblies, size_t count);

    /// Check if a specific method is fake.
    bool IsFakeMethod(const il2cpp::_internal::unity_structs::il2cppMethodInfo* method) const;

    /// Check if a class is entirely composed of fake methods (likely a fake class).
    bool IsEntirelyFakeClass(il2cpp::_internal::unity_structs::il2cppClass* klass) const;

    /// Get all detected fake methods.
    const std::vector<FakeMethodInfo>& GetFakeMethodList() const { return m_fake_methods; }

    /// Get class analysis results.
    const std::vector<ClassAnalysis>& GetClassAnalysis() const { return m_class_analysis; }

    /// Get detection statistics.
    size_t GetTotalMethodsAnalyzed() const { return m_total_methods; }
    size_t GetTotalFakeMethods() const { return m_fake_methods.size(); }
    size_t GetTotalFakeClasses() const { return m_fake_class_count; }
    size_t GetUniqueStubPointers() const { return m_stub_pointers.size(); }
    size_t GetWhitelistedMethods() const { return m_whitelisted_methods; }
    size_t GetGenericSkipped() const { return m_generic_skipped; }

    /// Write the fake method report to a file (MDB/Dump/fake_methods.txt).
    bool WriteFakeReport(const std::string& output_path) const;

    /// Get the set of fake method pointers (raw MethodInfo* addresses) for fast lookup.
    const std::unordered_set<const void*>& GetFakeMethodSet() const { return m_fake_method_set; }

    /// Get the set of entirely-fake class pointers for fast lookup.
    const std::unordered_set<const void*>& GetFakeClassSet() const { return m_fake_class_set; }

private:
    // Phase 1: Collect all method pointers and count sharing
    void CollectMethodPointers(il2cpp::_internal::unity_structs::il2cppAssembly** assemblies, size_t count);

    // Phase 2: Build vtable whitelist
    void BuildVTableWhitelist(il2cpp::_internal::unity_structs::il2cppAssembly** assemblies, size_t count);

    // Phase 3: Classify methods as real or fake
    void ClassifyMethods(il2cpp::_internal::unity_structs::il2cppAssembly** assemblies, size_t count);

    // Check if a native function body matches known stub patterns
    bool IsStubPattern(uintptr_t address) const;

    // Check if an image (assembly) name matches any whitelist prefix
    bool IsWhitelistedImage(const char* imageName) const;

    // Check if a method is generic or belongs to a generic class (IL2CPP generic sharing)
    bool IsGenericShared(const il2cpp::_internal::unity_structs::il2cppMethodInfo* method) const;

    // Check if a class name indicates a generic class (contains backtick, e.g. "List`1")
    static bool IsGenericClassName(const char* className);

    // Check if a method name matches the obfuscation pattern (11-char all-caps)
    static bool IsObfuscatedName(const char* name);

    // Build a human-readable method signature
    std::string BuildMethodSignature(const il2cpp::_internal::unity_structs::il2cppMethodInfo* method,
                                     const std::string& className) const;

    DetectorConfig m_config;

    // methodPointer -> list of MethodInfo* that share it
    std::unordered_map<uintptr_t, std::vector<const il2cpp::_internal::unity_structs::il2cppMethodInfo*>> m_pointer_map;

    // Set of methodPointer addresses identified as stubs
    std::unordered_set<uintptr_t> m_stub_pointers;

    // Set of MethodInfo* addresses in vtable slots (whitelist)
    std::unordered_set<const void*> m_vtable_methods;

    // Results
    std::vector<FakeMethodInfo> m_fake_methods;
    std::vector<ClassAnalysis> m_class_analysis;
    std::unordered_set<const void*> m_fake_method_set;   // Fast lookup by MethodInfo*
    std::unordered_set<const void*> m_fake_class_set;    // Fast lookup by il2cppClass*

    size_t m_total_methods = 0;
    size_t m_whitelisted_methods = 0;
    size_t m_generic_skipped = 0;
    size_t m_fake_class_count = 0;
};

} // namespace Obfuscation
} // namespace MDB
