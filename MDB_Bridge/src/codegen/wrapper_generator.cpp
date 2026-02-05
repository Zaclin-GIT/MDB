#include "wrapper_generator.hpp"
#include <fstream>
#include <sstream>
#include <filesystem>
#include <set>
#include <regex>
#include <algorithm>

namespace MDB {
namespace WrapperGen {

// Universal namespaces that should always be skipped
static const std::set<std::string> SKIP_NAMESPACES = {
    "System", "System.Collections", "System.Collections.Generic", "System.IO", "System.Text",
    "System.Threading", "System.Threading.Tasks", "System.Linq", "System.Reflection",
    "System.Runtime", "System.Runtime.CompilerServices", "System.Runtime.InteropServices",
    "System.Diagnostics", "System.Globalization", "System.Security", "System.ComponentModel",
    "System.Net", "System.Xml", "Mono", "mscorlib", "Internal", "Microsoft",
    "UnityEngine.Internal", "UnityEngineInternal"
};

// Skip namespace prefixes
static bool ShouldSkipNamespace(const std::string& ns) {
    if (ns.empty()) return true;
    if (SKIP_NAMESPACES.find(ns) != SKIP_NAMESPACES.end()) return true;
    if (ns.find("System.") == 0) return true;
    if (ns.find("Mono.") == 0) return true;
    if (ns.find("Internal.") == 0) return true;
    if (ns.find("Microsoft.") == 0) return true;
    return false;
}

struct TypeInfo {
    std::string dll;
    std::string ns;
    std::string name;
    std::string kind;  // class, interface, enum, struct
    std::string visibility;
    std::string base_type;
    bool is_sealed = false;
    std::vector<std::string> fields;
    std::vector<std::string> properties;
    std::vector<std::string> methods;
};

// Simple parser to extract type information from dump.cs
static std::vector<TypeInfo> ParseDumpFile(const std::string& dump_file_path, std::string& error) {
    std::vector<TypeInfo> types;
    std::ifstream file(dump_file_path);
    
    if (!file.is_open()) {
        error = "Failed to open dump file: " + dump_file_path;
        return types;
    }
    
    std::string line;
    std::string current_dll;
    std::string current_ns;
    TypeInfo* current_type = nullptr;
    bool in_type = false;
    int brace_depth = 0;
    
    std::regex dll_regex(R"(^//\s*Dll\s*:\s*(.+)$)");
    std::regex ns_regex(R"(^//\s*Namespace:\s*(.*)$)");
    std::regex class_regex(R"(^(public|internal|private)\s+((?:sealed\s+|abstract\s+|static\s+)*)(class|interface|enum|struct)\s+(\S+)(?:\s*:\s*(\S+))?)");
    
    while (std::getline(file, line)) {
        std::smatch match;
        
        // Check for DLL marker
        if (std::regex_search(line, match, dll_regex)) {
            current_dll = match[1].str();
            continue;
        }
        
        // Check for namespace marker
        if (std::regex_search(line, match, ns_regex)) {
            current_ns = match[1].str();
            continue;
        }
        
        // Check for class/interface/enum/struct declaration
        if (!in_type && std::regex_search(line, match, class_regex)) {
            TypeInfo type;
            type.dll = current_dll;
            type.ns = current_ns;
            type.visibility = match[1].str();
            std::string modifiers = match[2].str();
            type.is_sealed = (modifiers.find("sealed") != std::string::npos);
            type.kind = match[3].str();
            type.name = match[4].str();
            if (match.size() > 5 && match[5].matched) {
                type.base_type = match[5].str();
            }
            
            types.push_back(type);
            current_type = &types.back();
            in_type = true;
            brace_depth = 0;
            continue;
        }
        
        // Track braces
        if (in_type) {
            for (char c : line) {
                if (c == '{') brace_depth++;
                else if (c == '}') brace_depth--;
            }
            
            if (brace_depth < 0) {
                in_type = false;
                current_type = nullptr;
            }
        }
    }
    
    return types;
}

// Generate wrapper class code
static std::string GenerateWrapperClass(const TypeInfo& type, const std::string& namespace_prefix) {
    std::stringstream ss;
    
    // Skip enums - they don't need wrappers
    if (type.kind == "enum") {
        return "";
    }
    
    // Determine base class for wrapper
    std::string base_class = "Il2CppObject";
    if (type.kind == "struct") {
        base_class = "Il2CppStruct";
    }
    
    ss << "    " << type.visibility << " class " << type.name << " : " << base_class << "\n";
    ss << "    {\n";
    
    // Constructor
    ss << "        public " << type.name << "(IntPtr ptr) : base(ptr) { }\n";
    ss << "\n";
    
    // Static constructor helper
    ss << "        public static " << type.name << " Wrap(IntPtr ptr)\n";
    ss << "        {\n";
    ss << "            return ptr != IntPtr.Zero ? new " << type.name << "(ptr) : null;\n";
    ss << "        }\n";
    
    ss << "    }\n";
    
    return ss.str();
}

GeneratorResult GenerateWrappers(
    const std::string& dump_file_path,
    const std::string& output_directory,
    const std::string& namespace_prefix)
{
    GeneratorResult result = { false, "", {}, 0 };
    
    // Create output directory
    std::filesystem::create_directories(output_directory);
    
    // Parse dump file
    std::string parse_error;
    auto types = ParseDumpFile(dump_file_path, parse_error);
    
    if (!parse_error.empty()) {
        result.error_message = parse_error;
        return result;
    }
    
    // Group types by namespace
    std::map<std::string, std::vector<TypeInfo>> types_by_namespace;
    
    for (const auto& type : types) {
        // Skip system/internal namespaces
        if (ShouldSkipNamespace(type.ns)) {
            continue;
        }
        
        // Skip non-public types
        if (type.visibility != "public") {
            continue;
        }
        
        types_by_namespace[type.ns].push_back(type);
    }
    
    // Generate wrapper files per namespace
    for (const auto& [ns, ns_types] : types_by_namespace) {
        if (ns_types.empty()) continue;
        
        std::stringstream file_content;
        
        // File header
        file_content << "// Auto-generated Il2Cpp wrapper classes\n";
        file_content << "// Namespace: " << ns << "\n";
        file_content << "// Do not edit manually\n\n";
        
        // Using statements
        file_content << "using System;\n";
        file_content << "using System.Runtime.InteropServices;\n";
        file_content << "using GameSDK.Core;\n\n";
        
        // Namespace declaration
        std::string wrapper_ns = namespace_prefix;
        if (!ns.empty()) {
            wrapper_ns += "." + ns;
        }
        
        file_content << "namespace " << wrapper_ns << "\n";
        file_content << "{\n";
        
        // Generate wrapper classes
        for (const auto& type : ns_types) {
            std::string wrapper_code = GenerateWrapperClass(type, namespace_prefix);
            if (!wrapper_code.empty()) {
                file_content << wrapper_code << "\n";
                result.total_classes_generated++;
            }
        }
        
        file_content << "}\n";
        
        // Write to file
        std::string safe_ns = ns;
        std::replace(safe_ns.begin(), safe_ns.end(), '.', '_');
        if (safe_ns.empty()) safe_ns = "Global";
        
        std::filesystem::path filename = std::filesystem::path(output_directory) / 
                                        (namespace_prefix + "." + safe_ns + ".cs");
        std::ofstream out_file(filename);
        
        if (!out_file.is_open()) {
            result.error_message = "Failed to write file: " + filename.string();
            return result;
        }
        
        out_file << file_content.str();
        out_file.close();
        
        result.generated_files.push_back(filename.string());
    }
    
    result.success = true;
    return result;
}

bool AreWrappersFresh(const std::string& output_directory) {
    // Check if directory exists and has files
    if (!std::filesystem::exists(output_directory)) {
        return false;
    }
    
    // Check if there are any generated wrapper files
    bool has_files = false;
    for (const auto& entry : std::filesystem::directory_iterator(output_directory)) {
        if (entry.path().extension() == ".cs") {
            has_files = true;
            break;
        }
    }
    
    if (!has_files) {
        return false;
    }
    
    // Check if dump.cs exists (the source for generation)
    std::filesystem::path output_path(output_directory);
    std::filesystem::path dump_path = output_path.parent_path().parent_path() / "MDB" / "Dump" / "dump.cs";
    
    if (!std::filesystem::exists(dump_path)) {
        return false;
    }
    
    try {
        // Find the oldest generated wrapper file
        auto dump_time = std::filesystem::last_write_time(dump_path);
        std::filesystem::file_time_type oldest_wrapper_time = std::filesystem::file_time_type::max();
        
        for (const auto& entry : std::filesystem::directory_iterator(output_directory)) {
            if (entry.path().extension() == ".cs") {
                auto wrapper_time = std::filesystem::last_write_time(entry.path());
                if (wrapper_time < oldest_wrapper_time) {
                    oldest_wrapper_time = wrapper_time;
                }
            }
        }
        
        // If wrappers are newer than dump, they're fresh
        return oldest_wrapper_time > dump_time;
    } catch (...) {
        return false;
    }
}

} // namespace WrapperGen
} // namespace MDB
