#pragma once
#include <Windows.h>
#include <Psapi.h>
#include <vector>
#include <string>
#include <unordered_map>
#include <cstdint>

#pragma comment(lib, "psapi.lib")

// ============================================================================
// SignatureScanner - Pattern scanning and function resolution for IL2CPP
// 
// Supports multiple resolution strategies:
// 1. Standard exports (GetProcAddress)
// 2. Obfuscated/renamed exports (suffix matching)
// 3. Pattern scanning (byte signatures)
// 4. String reference scanning (find functions via string usage)
// 5. Thunk resolution (follow JMP chains)
// ============================================================================

class SignatureScanner {
public:
    struct ModuleInfo {
        uintptr_t base;
        size_t size;
        uintptr_t textStart;
        size_t textSize;
        uintptr_t rdataStart;
        size_t rdataSize;
    };

    // Initialize with a module handle - parse PE sections and build export map
    static bool Initialize(HMODULE hModule) {
        if (!hModule) return false;
        
        MODULEINFO modInfo;
        if (!GetModuleInformation(GetCurrentProcess(), hModule, &modInfo, sizeof(modInfo))) {
            return false;
        }
        
        s_hModule = hModule;
        s_module.base = reinterpret_cast<uintptr_t>(hModule);
        s_module.size = modInfo.SizeOfImage;
        
        // Parse PE sections to find .text and .rdata
        auto dosHeader = reinterpret_cast<PIMAGE_DOS_HEADER>(hModule);
        if (dosHeader->e_magic != IMAGE_DOS_SIGNATURE) return false;
        
        auto ntHeaders = reinterpret_cast<PIMAGE_NT_HEADERS>(
            reinterpret_cast<uint8_t*>(hModule) + dosHeader->e_lfanew);
        if (ntHeaders->Signature != IMAGE_NT_SIGNATURE) return false;
        
        auto sectionHeader = IMAGE_FIRST_SECTION(ntHeaders);
        for (WORD i = 0; i < ntHeaders->FileHeader.NumberOfSections; i++) {
            std::string sectionName(reinterpret_cast<char*>(sectionHeader[i].Name), 8);
            sectionName = sectionName.c_str(); // Trim at null
            
            uintptr_t sectionStart = s_module.base + sectionHeader[i].VirtualAddress;
            size_t sectionSize = sectionHeader[i].Misc.VirtualSize;
            
            if (sectionName == ".text") {
                s_module.textStart = sectionStart;
                s_module.textSize = sectionSize;
            }
            else if (sectionName == ".rdata") {
                s_module.rdataStart = sectionStart;
                s_module.rdataSize = sectionSize;
            }
        }
        
        // Build export map for suffix matching and thunk analysis
        BuildExportMap(hModule);
        
        s_initialized = true;
        return true;
    }

    static bool IsInitialized() { return s_initialized; }
    static HMODULE GetModule() { return s_hModule; }
    static const ModuleInfo& GetModuleInfo() { return s_module; }

    // ========================================================================
    // Pattern Scanning
    // ========================================================================
    
    // Pattern scan in .text section only (faster, for code patterns)
    // Pattern uses \x00 for wildcards when mask char is '?'
    static uintptr_t FindPattern(const char* pattern, const char* mask) {
        if (!s_initialized || !s_module.textStart) return 0;
        return FindPatternInternal(s_module.textStart, s_module.textSize, pattern, mask);
    }

    // Pattern scan in entire module (slower, for data patterns)
    static uintptr_t FindPatternInModule(const char* pattern, const char* mask) {
        if (!s_initialized) return 0;
        return FindPatternInternal(s_module.base, s_module.size, pattern, mask);
    }

    // ========================================================================
    // String Reference Scanning
    // ========================================================================
    
    // Find a string in .rdata and return its address
    static uintptr_t FindString(const char* str) {
        if (!s_initialized || !s_module.rdataStart) return 0;
        
        size_t len = strlen(str);
        for (size_t i = 0; i < s_module.rdataSize - len; i++) {
            if (memcmp(reinterpret_cast<void*>(s_module.rdataStart + i), str, len) == 0) {
                // Verify null terminator
                if (reinterpret_cast<uint8_t*>(s_module.rdataStart + i)[len] == 0) {
                    return s_module.rdataStart + i;
                }
            }
        }
        return 0;
    }

    // Find code that references a string via LEA instruction
    // Returns address of the LEA instruction
    static uintptr_t FindStringReference(const char* str) {
        uintptr_t strAddr = FindString(str);
        if (!strAddr) return 0;
        
        // Search for LEA instructions referencing this string
        // 48 8D 0D xx xx xx xx = LEA RCX, [rip+offset]
        // 48 8D 15 xx xx xx xx = LEA RDX, [rip+offset]
        // 48 8D 05 xx xx xx xx = LEA RAX, [rip+offset]
        // 4C 8D 05 xx xx xx xx = LEA R8, [rip+offset]
        // 4C 8D 0D xx xx xx xx = LEA R9, [rip+offset]
        // 4C 8D 15 xx xx xx xx = LEA R10, [rip+offset]
        
        for (size_t i = 0; i < s_module.textSize - 7; i++) {
            uintptr_t addr = s_module.textStart + i;
            uint8_t* bytes = reinterpret_cast<uint8_t*>(addr);
            
            bool isLea = false;
            // REX.W LEA with RIP-relative addressing
            if (bytes[0] == 0x48 && bytes[1] == 0x8D) {
                // Check ModR/M byte for RIP-relative (mod=00, r/m=101)
                uint8_t modrm = bytes[2];
                if ((modrm & 0xC7) == 0x05) { // mod=00, r/m=101
                    isLea = true;
                }
            }
            // REX.WR LEA (R8-R15 destination)
            else if (bytes[0] == 0x4C && bytes[1] == 0x8D) {
                uint8_t modrm = bytes[2];
                if ((modrm & 0xC7) == 0x05) {
                    isLea = true;
                }
            }
            
            if (isLea) {
                int32_t offset = *reinterpret_cast<int32_t*>(addr + 3);
                uintptr_t targetAddr = addr + 7 + offset;
                if (targetAddr == strAddr) {
                    return addr;
                }
            }
        }
        return 0;
    }

    // ========================================================================
    // Thunk Detection and Resolution
    // ========================================================================
    
    // Check if an address is a thunk (just a JMP to somewhere else)
    static bool IsThunk(uintptr_t address, uintptr_t* realTarget = nullptr) {
        if (!address) return false;
        
        uint8_t* bytes = reinterpret_cast<uint8_t*>(address);
        
        // E9 xx xx xx xx = JMP rel32
        if (bytes[0] == 0xE9) {
            if (realTarget) {
                int32_t offset = *reinterpret_cast<int32_t*>(address + 1);
                *realTarget = address + 5 + offset;
            }
            return true;
        }
        
        // FF 25 xx xx xx xx = JMP [rip+rel32] (indirect jump via pointer)
        if (bytes[0] == 0xFF && bytes[1] == 0x25) {
            if (realTarget) {
                int32_t offset = *reinterpret_cast<int32_t*>(address + 2);
                uintptr_t ptrAddr = address + 6 + offset;
                *realTarget = *reinterpret_cast<uintptr_t*>(ptrAddr);
            }
            return true;
        }
        
        return false;
    }

    // Follow thunk chain to get the real function address
    static uintptr_t FollowThunk(uintptr_t address) {
        uintptr_t current = address;
        uintptr_t target;
        int maxDepth = 10; // Prevent infinite loops
        
        while (maxDepth-- > 0 && IsThunk(current, &target)) {
            current = target;
        }
        return current;
    }

    // ========================================================================
    // Export Suffix Matching (for obfuscated exports)
    // ========================================================================
    
    // Known obfuscation suffixes that replace IL2CPP function name suffixes
    static inline const char* OBFUSCATION_SUFFIXES[] = {
        "_wasting_your_time",
        "_wasting_your_life",
        "_stop_reversing",
        "_go_outside",
        // Add more patterns as discovered
        nullptr
    };

    // Find an export that ends with a known obfuscation suffix
    // Returns the real function address (thunks followed)
    static uintptr_t FindExportBySuffix(const char* suffix) {
        if (!s_initialized) return 0;
        
        for (const auto& [name, addr] : s_exports) {
            size_t nameLen = name.length();
            size_t suffixLen = strlen(suffix);
            
            if (nameLen > suffixLen) {
                if (name.compare(nameLen - suffixLen, suffixLen, suffix) == 0) {
                    return FollowThunk(addr);
                }
            }
        }
        return 0;
    }

    // Try to find an export matching any known obfuscation suffix for a given original suffix
    // e.g., originalSuffix = "_domain_get_assemblies" might match "xyz_wasting_your_time"
    static uintptr_t FindObfuscatedExport(const char* originalName) {
        if (!s_initialized) return 0;
        
        // First try direct lookup
        auto it = s_exports.find(originalName);
        if (it != s_exports.end()) {
            return FollowThunk(it->second);
        }
        
        // Try each known obfuscation suffix
        for (int i = 0; OBFUSCATION_SUFFIXES[i] != nullptr; i++) {
            uintptr_t addr = FindExportBySuffix(OBFUSCATION_SUFFIXES[i]);
            if (addr) {
                // For now, return first match - could be improved with heuristics
                return addr;
            }
        }
        
        return 0;
    }

    // Get all exports (for debugging/analysis)
    static const std::unordered_map<std::string, uintptr_t>& GetExports() {
        return s_exports;
    }

private:
    static inline bool s_initialized = false;
    static inline HMODULE s_hModule = nullptr;
    static inline ModuleInfo s_module = {};
    static inline std::unordered_map<std::string, uintptr_t> s_exports;

    static uintptr_t FindPatternInternal(uintptr_t start, size_t size, const char* pattern, const char* mask) {
        size_t maskLen = strlen(mask);
        
        for (size_t i = 0; i < size - maskLen; i++) {
            bool found = true;
            for (size_t j = 0; j < maskLen; j++) {
                if (mask[j] == 'x') {
                    if (reinterpret_cast<uint8_t*>(start + i)[j] != static_cast<uint8_t>(pattern[j])) {
                        found = false;
                        break;
                    }
                }
                // '?' in mask = wildcard, skip comparison
            }
            if (found) {
                return start + i;
            }
        }
        return 0;
    }

    static void BuildExportMap(HMODULE hModule) {
        s_exports.clear();
        
        auto dosHeader = reinterpret_cast<PIMAGE_DOS_HEADER>(hModule);
        auto ntHeaders = reinterpret_cast<PIMAGE_NT_HEADERS>(
            reinterpret_cast<uint8_t*>(hModule) + dosHeader->e_lfanew);
        
        auto& exportDir = ntHeaders->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_EXPORT];
        if (!exportDir.VirtualAddress) return;
        
        auto exports = reinterpret_cast<PIMAGE_EXPORT_DIRECTORY>(
            reinterpret_cast<uint8_t*>(hModule) + exportDir.VirtualAddress);
        
        auto names = reinterpret_cast<uint32_t*>(
            reinterpret_cast<uint8_t*>(hModule) + exports->AddressOfNames);
        auto ordinals = reinterpret_cast<uint16_t*>(
            reinterpret_cast<uint8_t*>(hModule) + exports->AddressOfNameOrdinals);
        auto functions = reinterpret_cast<uint32_t*>(
            reinterpret_cast<uint8_t*>(hModule) + exports->AddressOfFunctions);
        
        for (DWORD i = 0; i < exports->NumberOfNames; i++) {
            const char* name = reinterpret_cast<const char*>(
                reinterpret_cast<uint8_t*>(hModule) + names[i]);
            uintptr_t funcAddr = reinterpret_cast<uintptr_t>(hModule) + functions[ordinals[i]];
            s_exports[name] = funcAddr;
        }
    }
};
