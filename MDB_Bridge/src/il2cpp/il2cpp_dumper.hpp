#pragma once
#include <string>
#include <vector>

// ============================================================================
// IL2CPP Runtime Dumper & Wrapper Generator for MDB Bridge
// ============================================================================
// This module dumps IL2CPP metadata at runtime and directly generates
// buildable C# wrapper files that make calls through the MDB Bridge.
// All generated types use fully-qualified names to avoid ambiguity.
// Includes BeeByte fake method detection — fake methods, properties,
// and entirely-fake classes are excluded from generated code.

namespace MDB {
namespace Dumper {

struct DumpResult {
    bool success;
    std::string dump_path;                         // Path to raw dump.cs (diagnostic)
    std::string fake_report_path;                  // Path to fake_methods.txt (BeeByte report)
    std::string error_message;
    size_t total_classes;
    size_t total_assemblies;
    std::vector<std::string> generated_files;      // Paths to generated .cs wrapper files
    size_t total_wrappers_generated;
    // BeeByte detection stats
    size_t fake_methods_detected;
    size_t fake_classes_detected;
};

// Main dumper function - dumps IL2CPP metadata and generates buildable C# wrappers
DumpResult DumpIL2CppRuntime(const std::string& output_directory);

// Check if dump already exists and is fresh (not stale)
bool IsDumpFresh(const std::string& dump_path);

// Check if generated wrappers exist and are fresh
bool AreWrappersFresh(const std::string& output_directory);

} // namespace Dumper
} // namespace MDB
