#pragma once
#include <vector>
#include <string>

// ============================================================================
// IL2CPP Function Signatures for Pattern Scanning - Shared Implementation
// 
// When exports are obfuscated, we fall back to pattern scanning.
// Each function has:
// - Original export name
// - Byte patterns (multiple variants for different Unity versions)
// - Known obfuscation suffixes
// 
// This is used by both the dumper and bridge to resolve IL2CPP functions
// when standard GetProcAddress fails.
// ============================================================================

struct SignaturePattern {
    const char* pattern;
    const char* mask;
    const char* description; // Unity version or other identifying info
};

struct FunctionSignature {
    const char* name;                           // Original IL2CPP export name
    std::vector<SignaturePattern> patterns;     // Byte patterns to try
    std::vector<const char*> suffixes;          // Known obfuscation suffixes
};

// ============================================================================
// Critical Function Signatures
// These are the entry points needed to bootstrap IL2CPP interaction
// ============================================================================

namespace Il2CppSignatures {

    // il2cpp_domain_get - returns pointer to IL2CPP domain
    // Very simple function: just loads and returns a global pointer
    // mov rax, [rip+offset]; ret
    inline const FunctionSignature DOMAIN_GET = {
        "il2cpp_domain_get",
        {
            { "\x48\x8B\x05\x00\x00\x00\x00\xC3", "xxx????x", "Generic - mov rax,[rip+x]; ret" },
            { "\x48\x8B\x05\x00\x00\x00\x00\x48\x85\xC0", "xxx????xxx", "Generic - mov rax,[rip+x]; test rax,rax" },
        },
        {} // No known suffixes - usually not obfuscated
    };

    // il2cpp_domain_get_assemblies - returns array of assemblies
    // Takes domain pointer and size_t* for count
    inline const FunctionSignature DOMAIN_GET_ASSEMBLIES = {
        "il2cpp_domain_get_assemblies",
        {
            // Typical prologue + parameter handling
            { "\x48\x89\x5C\x24\x00\x48\x89\x74\x24\x00\x57\x48\x83\xEC", "xxxx?xxxx?xxxx", "Generic prologue" },
            { "\x40\x53\x48\x83\xEC\x00\x48\x8B\xDA", "xxxxx?xxx", "push rbx; sub rsp,x; mov rbx,rdx" },
        },
        {
            "_wasting_your_time",
            "_wasting_your_life",
        }
    };

    // il2cpp_assembly_get_image - returns image from assembly
    // Simple accessor, usually just a pointer dereference
    inline const FunctionSignature ASSEMBLY_GET_IMAGE = {
        "il2cpp_assembly_get_image",
        {
            { "\x48\x8B\x41\x00\xC3", "xxx?x", "mov rax,[rcx+x]; ret" },
            { "\x48\x8B\x81\x00\x00\x00\x00\xC3", "xxx????x", "mov rax,[rcx+x]; ret (large offset)" },
        },
        {}
    };

    // il2cpp_image_get_class_count - returns number of classes in image
    inline const FunctionSignature IMAGE_GET_CLASS_COUNT = {
        "il2cpp_image_get_class_count",
        {
            { "\x8B\x41\x00\xC3", "xx?x", "mov eax,[rcx+x]; ret" },
            { "\x48\x8B\x41\x00\xC3", "xxx?x", "mov rax,[rcx+x]; ret" },
            { "\x8B\x81\x00\x00\x00\x00\xC3", "xx????x", "mov eax,[rcx+x]; ret (large offset)" },
        },
        {}
    };

    // il2cpp_class_get_name - returns class name string
    // Critical for dumping - we use it to verify other functions work
    inline const FunctionSignature CLASS_GET_NAME = {
        "il2cpp_class_get_name",
        {
            { "\x48\x8B\x41\x00\xC3", "xxx?x", "mov rax,[rcx+x]; ret" },
            { "\x48\x8B\x81\x00\x00\x00\x00\xC3", "xxx????x", "mov rax,[rcx+x]; ret (large offset)" },
        },
        {}
    };

    // ========================================================================
    // All signatures in order for iteration
    // ========================================================================
    inline const FunctionSignature* CRITICAL_SIGNATURES[] = {
        &DOMAIN_GET,
        &DOMAIN_GET_ASSEMBLIES,
        &ASSEMBLY_GET_IMAGE,
        &IMAGE_GET_CLASS_COUNT,
        &CLASS_GET_NAME,
        nullptr
    };

    // ========================================================================
    // Known obfuscation suffix mappings
    // Maps obfuscation suffix -> original IL2CPP function suffix
    // ========================================================================
    inline const struct {
        const char* obfuscatedSuffix;
        const char* originalSuffix;
    } SUFFIX_MAPPINGS[] = {
        { "_wasting_your_time", "_domain_get_assemblies" },
        { "_wasting_your_life", "_domain_get_assemblies" },
        { "_stop_reversing", "_domain_get_assemblies" },
        { "_go_outside", "_domain_get_assemblies" },
        { nullptr, nullptr }
    };

} // namespace Il2CppSignatures
