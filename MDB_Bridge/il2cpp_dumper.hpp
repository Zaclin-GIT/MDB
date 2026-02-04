#pragma once
#include <string>
#include <vector>

// ============================================================================
// IL2CPP Runtime Dumper for MDB Bridge
// ============================================================================
// This module dumps IL2CPP metadata at runtime to generate dump.cs files
// that can be processed by the wrapper generator.

namespace MDB {
namespace Dumper {

struct DumpResult {
    bool success;
    std::string dump_path;
    std::string error_message;
    size_t total_classes;
    size_t total_assemblies;
};

// Main dumper function - dumps IL2CPP metadata to a dump.cs file
DumpResult DumpIL2CppRuntime(const std::string& output_directory);

// Check if dump already exists and is fresh (not stale)
bool IsDumpFresh(const std::string& dump_path);

} // namespace Dumper
} // namespace MDB
